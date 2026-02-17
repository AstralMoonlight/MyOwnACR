using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;                // [NUEVO] Para leer archivos
using System.Collections.Generic; // [NUEVO] Para diccionarios
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Dalamud.Plugin;

namespace MyOwnACR.Network
{
    public class WebServer : IDisposable
    {
        private HttpListener? _listener;
        private WebSocket? _activeSocket;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _socketLock = new(1, 1);
        private readonly Plugin _plugin;

        public event Action<string, string>? OnMessage;

        public WebServer(Plugin plugin)
        {
            _plugin = plugin;
            Task.Run(() => StartServer(_cts.Token));
        }

        public async void SendJson(string type, object content)
        {
            if (_activeSocket == null || _activeSocket.State != WebSocketState.Open || _cts.IsCancellationRequested) return;

            var lockTaken = false;
            try
            {
                await _socketLock.WaitAsync(_cts.Token);
                lockTaken = true;

                if (_activeSocket == null || _activeSocket.State != WebSocketState.Open) return;

                var wrapper = new { type, data = content };
                var json = JsonConvert.SerializeObject(wrapper);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception) { /* Ignorar errores de envío */ }
            finally
            {
                if (lockTaken) try { _socketLock.Release(); } catch { }
            }
        }

        private async Task StartServer(CancellationToken token)
        {
            try { _listener?.Close(); } catch { }
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:5055/");

            // =======================================================================
            // [CONFIGURACIÓN] RUTA DE TU DASHBOARD (MODULAR)
            // Cambia esto a la ruta donde crearás la carpeta en el Paso 2
            // =======================================================================
            string dashboardRoot = @"C:\Proyectos\MyOwnACR\ACR_Dashboard";

            try
            {
                _listener.Start();
                Plugin.Log.Info($"Servidor Web iniciado en puerto 5055. Sirviendo archivos desde: {dashboardRoot}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"No se pudo iniciar HttpListener: {ex.Message}");
                return;
            }

            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    // -----------------------------------------------------------
                    // CASO A: PETICIÓN WEBSOCKET (Comunicación Real-Time)
                    // -----------------------------------------------------------
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var wsContext = await context.AcceptWebSocketAsync(null);
                                await _socketLock.WaitAsync(token);
                                try
                                {
                                    if (_activeSocket != null) try { _activeSocket.Abort(); _activeSocket.Dispose(); } catch { }
                                    _activeSocket = wsContext.WebSocket;
                                }
                                finally { _socketLock.Release(); }

                                Plugin.Log.Info("Cliente WebSocket conectado");
                                await ReceiveLoop(_activeSocket, token);
                            }
                            catch (Exception ex) { Plugin.Log.Error($"Error en cliente WS: {ex.Message}"); }
                        }, token);
                    }
                    // -----------------------------------------------------------
                    // CASO B: PETICIÓN HTTP (Servir Archivos HTML/CSS/JS)
                    // -----------------------------------------------------------
                    else
                    {
                        _ = Task.Run(async () =>
                        {
                            var response = context.Response;
                            try
                            {
                                string filename = context.Request.Url?.AbsolutePath ?? "/";

                                // Si piden la raíz, servimos index.html
                                if (filename == "/") filename = "/index.html";

                                // Limpieza de ruta para evitar hackeos (Directory Traversal)
                                filename = filename.TrimStart('/').Replace("..", "");

                                string filePath = Path.Combine(dashboardRoot, filename);

                                if (File.Exists(filePath))
                                {
                                    byte[] buffer = await File.ReadAllBytesAsync(filePath, token);

                                    response.ContentLength64 = buffer.Length;
                                    response.ContentType = GetContentType(Path.GetExtension(filePath));
                                    response.StatusCode = 200;
                                    // CORS headers para evitar problemas
                                    response.AddHeader("Access-Control-Allow-Origin", "*");

                                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, token);
                                }
                                else
                                {
                                    response.StatusCode = 404; // No encontrado
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.Error($"Error sirviendo archivo: {ex.Message}");
                                response.StatusCode = 500;
                            }
                            finally
                            {
                                response.Close();
                            }
                        }, token);
                    }
                }
                catch { break; }
            }
        }

        private async Task ReceiveLoop(WebSocket s, CancellationToken token)
        {
            var buffer = new byte[4096];
            while (s.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    var result = await s.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    var jsonMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        var obj = JObject.Parse(jsonMsg);
                        var cmd = obj["cmd"]?.ToString();
                        var dataToken = obj["data"];
                        var dataStr = dataToken != null ? dataToken.ToString(Formatting.None) : "";

                        if (!string.IsNullOrEmpty(cmd))
                        {
                            OnMessage?.Invoke(cmd, dataStr);
                        }
                    }
                    catch (Exception jsonEx) { Plugin.Log.Error($"Error parseando WS: {jsonEx.Message}"); }
                }
                catch { break; }
            }
        }

        // Helper para tipos MIME correctos
        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream",
            };
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener?.Stop(); _listener?.Close(); } catch { }
            try { _activeSocket?.Abort(); _activeSocket?.Dispose(); } catch { }
            try { _socketLock.Dispose(); _cts.Dispose(); } catch { }
        }
    }
}