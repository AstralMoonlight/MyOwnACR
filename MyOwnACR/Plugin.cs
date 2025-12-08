// Archivo: Plugin.cs - Clase principal del Plugin. Gestiona el ciclo de vida, servidor Web, WebSockets y bucle de juego.
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.JobGauge.Types;
using Lumina.Excel.Sheets;

namespace MyOwnACR
{
    /// <summary>
    /// Clase contenedora para estandarizar los mensajes JSON enviados al cliente Web.
    /// </summary>
    public class WebMessage
    {
        public string type { get; set; } = "info";
        public object? data { get; set; }
    }

    /// <summary>
    /// Clase principal del Plugin que implementa IDalamudPlugin.
    /// </summary>
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "MyOwnACR Pro";

        // Instancia estática para acceder a SendLog desde otras clases (ej. MNK_Logic)
        public static Plugin Instance { get; private set; } = null!;

        // --- Inyección de dependencias de Dalamud ---
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IChatGui Chat { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static ICondition Condition { get; private set; } = null!;
        [PluginService] internal static IJobGauges JobGauges { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IKeyState KeyState { get; private set; } = null!;

        // Configuración persistente
        public Configuration Config { get; set; }

        // Variables para el servidor Web y WebSockets
        private HttpListener? httpListener;
        private WebSocket? activeSocket;
        private CancellationTokenSource cts;

        // Variables de estado del Bot
        private bool isRunning = false;
        private DateTime lastSentTime = DateTime.MinValue; // Control de tasa de refresco del Dashboard
        private bool isSending = false; // Semáforo simple para evitar colisiones en envío async

        // Control de estado de teclas (para evitar rebotes/spam del toggle)
        private bool isHotkeyDown = false;

        // Importación de DLL para traer la ventana del juego al frente
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Constructor: Inicializa configuración, comandos, servidor web y hooks del framework.
        /// </summary>
        public Plugin()
        {
            Instance = this; // Asignar instancia estática

            // Carga o crea la configuración
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            // Registro de comandos de chat
            CommandManager.AddHandler("/acr", new CommandInfo(OnCommand) { HelpMessage = "Activar/Pausar Bot" });
            CommandManager.AddHandler("/acrstatus", new CommandInfo(OnCommandStatus) { HelpMessage = "Ver Buffs" });
            CommandManager.AddHandler("/acrdebug", new CommandInfo(OnCommandDebug) { HelpMessage = "Debug Logic" });





            // Inicio del servidor Web en un hilo separado
            cts = new CancellationTokenSource();
            Task.Run(() => StartWebServer(cts.Token));

            // Suscripción al bucle de actualización del juego (cada frame)
            Framework.Update += OnGameUpdate;
        }

        /// <summary>
        /// Limpieza de recursos al descargar el plugin.
        /// </summary>
        public void Dispose()
        {
            Framework.Update -= OnGameUpdate;
            CommandManager.RemoveHandler("/acr");
            CommandManager.RemoveHandler("/acrstatus");
            CommandManager.RemoveHandler("/acrdebug");

            // Cancelación y limpieza del servidor web y sockets
            cts.Cancel();
            try { httpListener?.Abort(); } catch { }
            try { activeSocket?.Dispose(); } catch { }
            cts.Dispose();
        }

        /// <summary>
        /// Envía un mensaje de LOG a la consola de depuración web.
        /// Puede ser llamado desde cualquier parte con: Plugin.Instance.SendLog("mensaje");
        /// </summary>
        /// <param name="message">El texto a mostrar en la consola web.</param>
        public void SendLog(string message)
        {
            // Reutiliza el método asíncrono existente. El "_" descarta la Task devuelta (fire and forget).
            _ = SendJsonAsync("log", message);
        }

        /// <summary>
        /// Trae la ventana del juego al primer plano (útil cuando se controla desde el navegador).
        /// </summary>
        private void FocusGame()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                IntPtr hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero) SetForegroundWindow(hWnd);
            }
            catch { }
        }

        /// <summary>
        /// Handler para el comando /acr. Alterna el estado de ejecución.
        /// </summary>
        private void OnCommand(string command, string args)
        {
            ToggleRunning();
        }

        /// <summary>
        /// Lógica interna para alternar entre Activado/Pausado.
        /// </summary>
        private void ToggleRunning()
        {
            isRunning = !isRunning;
            SendLog(isRunning ? "Bot ACTIVADO manualmente" : "Bot PAUSADO manualmente");
        }

        /// <summary>
        /// Handler para /acrstatus. Muestra los buffs actuales en el chat (útil para debug de IDs).
        /// </summary>
        private void OnCommandStatus(string command, string args)
        {
            var player = ObjectTable.LocalPlayer;
            if (player == null) return;

            var statusSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            Chat.Print("====== BUFFS ACTIVOS ======");
            SendLog("--- Solicitud de Status ---");
            foreach (var status in player.StatusList)
            {
                if (status.StatusId == 0) continue;
                string name = "Desconocido";
                // Intenta obtener el nombre real del buff desde la hoja de Excel del juego
                if (statusSheet != null && statusSheet.TryGetRow(status.StatusId, out var row))
                    name = row.Name.ToString();

                string msg = $"[{status.StatusId}] {name} ({status.RemainingTime:F1}s)";
                Chat.Print(msg);
                SendLog(msg); // También enviar a la consola web
            }
        }

        /// <summary>
        /// Handler para /acrdebug. Invoca la función de debug de la lógica específica de MNK.
        /// </summary>
        private void OnCommandDebug(string command, string args)
        {
            Logic.MNK_Logic.PrintDebugInfo(Chat);
            SendLog("Debug Info impresa en chat del juego.");
        }

        /// <summary>
        /// Bucle principal del juego. Se ejecuta en cada frame.
        /// </summary>
        private unsafe void OnGameUpdate(IFramework framework)
        {
            try
            {
                // 1. GESTIÓN DE HOTKEY FÍSICA
                // Detecta si la tecla configurada (por defecto F8) está presionada
                bool currentState = KeyState[Config.ToggleHotkey];
                if (currentState && !isHotkeyDown)
                {
                    isHotkeyDown = true;
                    ToggleRunning(); // Activa/Desactiva solo en el flanco de subida (press)
                }
                else if (!currentState)
                {
                    isHotkeyDown = false; // Resetea el flag cuando se suelta la tecla
                }

                // 2. EJECUCIÓN DE LÓGICA DE COMBATE
                if (isRunning)
                {
                    var player = ObjectTable.LocalPlayer;
                    // Validaciones básicas: Jugador existe, está vivo y tiene un objetivo
                    if (player != null && player.CurrentHp > 0 && player.TargetObject != null)
                    {
                        ActionManager* am = ActionManager.Instance();
                        if (am != null)
                        {
                            var jobId = player.ClassJob.RowId;
                            // ID 20 = Monk, ID 2 = Pugilist
                            if (jobId == 20 || jobId == 2)
                            {
                                // Primero intenta ejecutar lógica de supervivencia (curas)
                                bool survivalActionUsed = Logic.Survival.Execute(
                                    am, player, Config.Survival, Config.Monk.SecondWind, Config.Monk.Bloodbath
                                );

                                // Si no se usó una cura, intenta ejecutar la rotación de daño
                                if (!survivalActionUsed)
                                {
                                    Logic.MNK_Logic.Execute(am, player, Config.Monk, ObjectTable, Config.Operation);
                                }
                            }
                        }
                    }
                }

                // 3. ACTUALIZACIÓN DEL DASHBOARD WEB (Limitado a 10Hz / 100ms)
                var now = DateTime.Now;
                if ((now - lastSentTime).TotalMilliseconds >= 100)
                {
                    lastSentTime = now;
                    var player = ObjectTable.LocalPlayer;

                    // Valores por defecto para el JSON
                    string targetName = "--";
                    string playerName = "--";
                    uint jobId = 0;
                    uint combo = 0;
                    uint tHp = 0; uint tMax = 0;

                    if (player != null)
                    {
                        playerName = player.Name.TextValue;
                        jobId = player.ClassJob.RowId;

                        // Información del Target
                        if (player.TargetObject != null)
                        {
                            targetName = player.TargetObject.Name.TextValue;
                            if (player.TargetObject is Dalamud.Game.ClientState.Objects.Types.ICharacter tChar)
                            {
                                tHp = tChar.CurrentHp;
                                tMax = tChar.MaxHp;
                            }
                        }

                        // Información del Combo actual
                        ActionManager* am = ActionManager.Instance();
                        if (am != null) combo = am->Combo.Action;
                    }

                    // Determina estado textual para la UI
                    bool inCombat = Condition[ConditionFlag.InCombat];
                    var statusText = isRunning ? (inCombat ? "COMBATIENDO" : "ESPERANDO") : "PAUSADO";

                    // Envía el paquete JSON al WebSocket de forma asíncrona
                    _ = SendJsonAsync("status", new
                    {
                        is_running = isRunning,
                        status = statusText,
                        hp = (player != null) ? (int)player.CurrentHp : 0,
                        max_hp = (player != null) ? (int)player.MaxHp : 1,
                        target = targetName,
                        job = jobId,
                        combo = combo,
                        next_action = Logic.MNK_Logic.LastProposedAction, // Acción calculada por la lógica
                        queued_action = Logic.MNK_Logic.GetQueuedAction(), // Acción manual en cola
                        player_name = playerName,
                        target_hp = (int)tHp,
                        target_max_hp = (int)tMax
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Envía un mensaje JSON al cliente WebSocket conectado.
        /// </summary>
        private async Task SendJsonAsync(string type, object content)
        {
            if (activeSocket == null || activeSocket.State != WebSocketState.Open || isSending) return;
            isSending = true;
            try
            {
                var wrapper = new WebMessage { type = type, data = content };
                var json = JsonConvert.SerializeObject(wrapper);
                var bytes = Encoding.UTF8.GetBytes(json);
                // Envío final del buffer
                await activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
            finally { isSending = false; }
        }

        /// <summary>
        /// Inicializa el servidor HTTP local para servir el WebSocket.
        /// </summary>
        private async Task StartWebServer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (httpListener != null) try { httpListener.Close(); } catch { }
                    httpListener = new HttpListener();
                    httpListener.Prefixes.Add("http://127.0.0.1:5055/"); // Puerto local 5055
                    httpListener.Start();

                    while (httpListener.IsListening && !token.IsCancellationRequested)
                    {
                        // Espera conexión entrante
                        var context = await httpListener.GetContextAsync();
                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);
                            activeSocket = wsContext.WebSocket;
                            await ReceiveCommands(activeSocket, token); // Entra al bucle de escucha del socket
                        }
                        else
                        {
                            // Rechaza peticiones HTTP normales que no sean WS
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                }
                catch { await Task.Delay(2000, token); } // Reintento en caso de error
            }
        }

        /// <summary>
        /// Escucha mensajes entrantes desde el cliente WebSocket (Dashboard).
        /// </summary>
        private async Task ReceiveCommands(WebSocket s, CancellationToken token)
        {
            var buffer = new byte[4096];
            while (s.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    var result = await s.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    string jsonMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleJsonCommand(jsonMsg); // Procesa el comando JSON recibido
                }
                catch { break; }
            }
        }

        /// <summary>
        /// Procesa los comandos JSON recibidos desde la Web (START, STOP, Configuración).
        /// </summary>
        private void HandleJsonCommand(string json)
        {
            try
            {
                JObject cmd = JObject.Parse(json);
                string type = cmd["cmd"]?.ToString() ?? "";

                // Comandos básicos de control
                if (type == "START")
                {
                    isRunning = true;
                    FocusGame(); // Trae el juego al frente al iniciar
                    SendLog("Recibido comando START desde Web");
                }

                if (type == "STOP")
                {
                    isRunning = false;
                    SendLog("Recibido comando STOP desde Web");
                }

                // Inyección de acción manual desde la UI
                if (type == "force_action")
                {
                    string actionName = cmd["data"]?.ToString() ?? "";
                    Logic.MNK_Logic.QueueManualAction(actionName);
                    FocusGame();
                    SendLog($"Acción forzada recibida: {actionName}");
                }

                // Solicitud de configuración actual
                if (type == "get_config")
                {
                    var payload = new
                    {
                        Monk = Config.Monk,
                        Survival = Config.Survival,
                        Operation = Config.Operation,
                        Global = new { ToggleKey = Config.ToggleHotkey.ToString() }
                    };
                    _ = SendJsonAsync("config_data", payload);
                }

                // Guardado de Hotkey Global
                if (type == "save_global")
                {
                    string keyStr = cmd["data"]?["ToggleKey"]?.ToString() ?? "F8";
                    if (Enum.TryParse(keyStr, out VirtualKey k))
                    {
                        Config.ToggleHotkey = k;
                        Config.Save();
                        SendLog($"Hotkey global actualizada a: {k}");
                    }
                }

                // Guardado de configuraciones específicas
                if (type == "save_config")
                {
                    var newMonk = cmd["data"]?.ToObject<MyOwnACR.JobConfigs.JobConfig_MNK>();
                    if (newMonk != null) { Config.Monk = newMonk; Config.Save(); }
                }
                if (type == "save_survival")
                {
                    var newSurv = cmd["data"]?.ToObject<SurvivalConfig>();
                    if (newSurv != null) { Config.Survival = newSurv; Config.Save(); }
                }
                if (type == "save_operation")
                {
                    var newOp = cmd["data"]?.ToObject<OperationalSettings>();
                    if (newOp != null) { Config.Operation = newOp; Config.Save(); }
                    FocusGame(); // Al cambiar opciones operativas, suele ser útil volver al juego
                }
            }
            catch { }
        }
    }
}


// ==================================================================================
// RECORDATORIO: CÓMO ENVIAR LOGS A LA CONSOLA WEB
// ==================================================================================
// Desde cualquier parte del código (Logic, Eventos, etc.), usa:
// Plugin.Instance.SendLog("Tu mensaje aquí");

// Ejemplos prácticos:
// Plugin.Instance.SendLog($"Detectado Boss con HP: {hp}%");
// Plugin.Instance.SendLog("Iniciando fase de Burst (Riddle of Fire)");
