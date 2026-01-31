using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Websocket.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACR_Dashboard.Core
{
    public static class NetworkManager
    {
        // FIX CS8618: Marcamos como nullable porque se inicializa en Initialize()
        private static WebsocketClient? Client;
        private static readonly Uri Url = new Uri("ws://127.0.0.1:5055");

        // FIX CS8618: Eventos nullables
        public static event Action<bool>? OnConnectionChanged;
        public static event Action<JObject>? OnDataReceived;

        public static void Initialize()
        {
            Client = new WebsocketClient(Url);

            Client.ReconnectTimeout = TimeSpan.FromSeconds(2);
            Client.ErrorReconnectTimeout = TimeSpan.FromSeconds(2);

            Client.ReconnectionHappened.Subscribe(info =>
            {
                Debug.WriteLine($"[WS] Conectado: {info.Type}");
                OnConnectionChanged?.Invoke(true);
            });

            Client.DisconnectionHappened.Subscribe(info =>
            {
                Debug.WriteLine("[WS] Desconectado");
                OnConnectionChanged?.Invoke(false);
            });

            Client.MessageReceived.Subscribe(msg =>
            {
                try
                {
                    // FIX CS8604: Validamos que el texto no sea nulo ni vacío
                    if (!string.IsNullOrEmpty(msg.Text))
                    {
                        var json = JObject.Parse(msg.Text);
                        OnDataReceived?.Invoke(json);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WS] Error parseando datos: {ex.Message}");
                }
            });

            Task.Run(() => Client.Start());
        }

        // FIX CS8625: Aceptamos explícitamente 'object?' (nullable)
        public static void Send(string cmd, object? data = null)
        {
            if (Client == null || !Client.IsRunning) return;

            var payload = new { cmd = cmd, data = data };
            var json = JsonConvert.SerializeObject(payload);
            Client.Send(json);
        }
    }
}
