using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // Necesario para parsear el mensaje entrante
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

        // [CRÍTICO] El evento que Plugin.cs está buscando
        public event Action<string, string>? OnMessage;

        public WebServer(Plugin plugin)
        {
            _plugin = plugin;
            // Iniciamos el servidor en un hilo separado
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

                // Envolvemos los datos en el formato { type: "...", data: ... }
                var wrapper = new { type, data = content };
                var json = JsonConvert.SerializeObject(wrapper);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                // Logueamos solo en debug para no spammear si se desconecta
                // Plugin.Log.Debug($"[WS Send Error] {ex.Message}"); 
            }
            finally
            {
                if (lockTaken) try { _socketLock.Release(); } catch { }
            }
        }

        private async Task StartServer(CancellationToken token)
        {
            try { _listener?.Close(); } catch { }
            _listener = new HttpListener();

            // Puerto 5055 (Asegúrate que coincida con el JS)
            _listener.Prefixes.Add("http://127.0.0.1:5055/");

            try
            {
                _listener.Start();
                Plugin.Log.Info("Servidor Web iniciado en puerto 5055 (Native HttpListener)");
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

                    if (context.Request.IsWebSocketRequest)
                    {
                        // Aceptamos la conexión WebSocket
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var wsContext = await context.AcceptWebSocketAsync(null);

                                // Guardamos el socket activo de forma segura
                                await _socketLock.WaitAsync(token);
                                try
                                {
                                    if (_activeSocket != null) try { _activeSocket.Abort(); _activeSocket.Dispose(); } catch { }
                                    _activeSocket = wsContext.WebSocket;
                                }
                                finally { _socketLock.Release(); }

                                Plugin.Log.Info("Cliente WebSocket conectado al Dashboard");

                                // Entramos al bucle de recepción
                                await ReceiveLoop(_activeSocket, token);
                            }
                            catch (Exception ex) { Plugin.Log.Error($"Error en cliente WS: {ex.Message}"); }
                        }, token);
                    }
                    else
                    {
                        // Si no es WebSocket, devolvemos 400 o servimos archivos estáticos si quisieras
                        context.Response.StatusCode = 400;
                        context.Response.Close();
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
                    // Recibimos datos
                    var result = await s.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    // Convertimos bytes a string JSON
                    var jsonMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // [NUEVO] Procesamos el JSON para disparar el evento OnMessage
                    try
                    {
                        var obj = JObject.Parse(jsonMsg);
                        var cmd = obj["cmd"]?.ToString();

                        // 'data' puede ser un objeto complejo, lo pasamos como string para que Plugin.cs lo decida
                        var dataToken = obj["data"];
                        var dataStr = dataToken != null ? dataToken.ToString(Formatting.None) : "";

                        if (!string.IsNullOrEmpty(cmd))
                        {
                            // Disparamos el evento hacia Plugin.cs
                            OnMessage?.Invoke(cmd, dataStr);
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Plugin.Log.Error($"Error parseando mensaje WS: {jsonEx.Message}");
                    }
                }
                catch
                {
                    break;
                }
            }
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
