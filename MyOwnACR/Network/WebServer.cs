using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Dalamud.Plugin;

namespace MyOwnACR.Network
{
    public class WebServer : IDisposable
    {
        private HttpListener? _listener;
        private WebSocket? _activeSocket;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _socketLock = new(1, 1);
        private readonly CommandProcessor _processor;
        private readonly Plugin _plugin;

        public WebServer(Plugin plugin)
        {
            _plugin = plugin;
            _processor = new CommandProcessor(plugin);
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
            catch (Exception ex) { Plugin.Log.Debug($"[WS Send Error] {ex.Message}"); }
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

            try
            {
                _listener.Start();
                Plugin.Log.Info("Servidor Web iniciado en puerto 5055");
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
                            catch (Exception ex) { Plugin.Log.Debug($"Error en cliente WS: {ex.Message}"); }
                        }, token);
                    }
                    else
                    {
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
                    var result = await s.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var jsonMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _ = Task.Run(() => _processor.HandleCommand(jsonMsg), token);
                }
                catch { break; }
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
