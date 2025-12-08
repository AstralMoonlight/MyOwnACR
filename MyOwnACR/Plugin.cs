// Archivo: MyOwnACR/Plugin.cs
// Descripción: Clase principal del Plugin.
// AJUSTES: Implementación de Thread Safety para WebSocket (SemaphoreSlim) y gestión robusta de hilos (Issue #2).

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
using MyOwnACR.GameData; // NECESARIO para inicializar MNK_ActionData

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

        // NUEVO: Servicio de Logging de Dalamud
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        // Configuración persistente
        public Configuration Config { get; set; }

        // --- CONCURRENCIA Y RED ---
        private HttpListener? httpListener;
        private WebSocket? activeSocket;
        private readonly CancellationTokenSource cts = new(); // Token global de cancelación para el plugin

        // Semáforo para proteger la escritura en el WebSocket (1 hilo a la vez)
        private readonly SemaphoreSlim _socketLock = new SemaphoreSlim(1, 1);

        // Variables de estado (Volatile para asegurar visibilidad entre hilos)
        private volatile bool isRunning = false;
        private volatile bool isHotkeyDown = false;

        private DateTime lastSentTime = DateTime.MinValue; // Control de tasa de refresco del Dashboard

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

            // INICIALIZACIÓN CENTRALIZADA DE DATOS DE JUEGO (GameData)
            try
            {
                MNK_ActionData.Initialize();
                InputSender.Initialize();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error crítico inicializando subsistemas");
            }

            // Registro de comandos de chat
            CommandManager.AddHandler("/acr", new CommandInfo(OnCommand) { HelpMessage = "Activar/Pausar Bot" });
            CommandManager.AddHandler("/acrstatus", new CommandInfo(OnCommandStatus) { HelpMessage = "Ver Buffs" });
            CommandManager.AddHandler("/acrdebug", new CommandInfo(OnCommandDebug) { HelpMessage = "Debug Logic" });

            // Inicio del servidor Web en un hilo separado
            Task.Run(() => StartWebServer(cts.Token));

            // Suscripción al bucle de actualización del juego (cada frame)
            Framework.Update += OnGameUpdate;
        }

        /// <summary>
        /// Limpieza de recursos al descargar el plugin.
        /// </summary>
        public void Dispose()
        {
            // 1. Detener lógica del juego primero
            Framework.Update -= OnGameUpdate;

            // 2. Cancelar todas las tareas asíncronas
            cts.Cancel();

            // 3. Limpiar comandos
            CommandManager.RemoveHandler("/acr");
            CommandManager.RemoveHandler("/acrstatus");
            CommandManager.RemoveHandler("/acrdebug");

            // 4. Detener sistemas externos
            InputSender.Dispose();

            // 5. Limpieza de Red
            try
            {
                httpListener?.Stop();
                httpListener?.Close();
            }
            catch (Exception ex) { Log.Warning($"Error cerrando HttpListener: {ex.Message}"); }

            try
            {
                if (activeSocket != null)
                {
                    if (activeSocket.State == WebSocketState.Open)
                    {
                        var closeTask = activeSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Plugin Disposing", CancellationToken.None);
                        closeTask.Wait(1000);
                    }
                    activeSocket.Dispose();
                }
            }
            catch (Exception ex) { Log.Warning($"Error cerrando WebSocket: {ex.Message}"); }

            _socketLock.Dispose();
            cts.Dispose();
        }

        public void SendLog(string message)
        {
            _ = SendJsonAsync("log", message);
        }

        private void FocusGame()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                IntPtr hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero) SetForegroundWindow(hWnd);
            }
            catch (Exception ex) { Log.Error(ex, "Error enfocando ventana"); }
        }

        private void OnCommand(string command, string args) => ToggleRunning();

        private void ToggleRunning()
        {
            isRunning = !isRunning;
            string status = isRunning ? "ACTIVADO" : "PAUSADO";
            SendLog($"Bot {status} manualmente");
            Log.Info($"Bot estado cambiado a: {status}");
        }

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
                if (statusSheet != null && statusSheet.TryGetRow(status.StatusId, out var row))
                    name = row.Name.ToString();

                string msg = $"[{status.StatusId}] {name} ({status.RemainingTime:F1}s)";
                Chat.Print(msg);
                SendLog(msg);
            }
        }

        private void OnCommandDebug(string command, string args)
        {
            Logic.MNK_Logic.PrintDebugInfo(Chat);
            SendLog("Debug Info impresa en chat del juego.");
        }

        private unsafe void OnGameUpdate(IFramework framework)
        {
            if (cts.IsCancellationRequested) return;

            try
            {
                bool currentState = KeyState[Config.ToggleHotkey];
                if (currentState && !isHotkeyDown)
                {
                    isHotkeyDown = true;
                    ToggleRunning();
                }
                else if (!currentState)
                {
                    isHotkeyDown = false;
                }

                if (isRunning)
                {
                    var player = ObjectTable.LocalPlayer;
                    if (player != null && player.CurrentHp > 0)
                    {
                        ActionManager* am = ActionManager.Instance();
                        if (am != null)
                        {
                            var jobId = player.ClassJob.RowId;
                            if (jobId == 20 || jobId == 2)
                            {
                                bool survivalActionUsed = Logic.Survival.Execute(
                                    am, player, Config.Survival, Config.Monk.SecondWind, Config.Monk.Bloodbath
                                );

                                if (!survivalActionUsed)
                                {
                                    Logic.MNK_Logic.Execute(am, player, Config.Monk, ObjectTable, Config.Operation);
                                }
                            }
                        }
                    }
                }

                // Dashboard Update
                var now = DateTime.Now;
                if ((now - lastSentTime).TotalMilliseconds >= 100)
                {
                    lastSentTime = now;
                    var player = ObjectTable.LocalPlayer;

                    string targetName = "--";
                    string playerName = "--";
                    uint jobId = 0;
                    uint combo = 0;
                    uint tHp = 0; uint tMax = 0;

                    if (player != null)
                    {
                        playerName = player.Name.TextValue;
                        jobId = player.ClassJob.RowId;

                        if (player.TargetObject != null)
                        {
                            targetName = player.TargetObject.Name.TextValue;
                            if (player.TargetObject is Dalamud.Game.ClientState.Objects.Types.ICharacter tChar)
                            {
                                tHp = tChar.CurrentHp;
                                tMax = tChar.MaxHp;
                            }
                        }

                        ActionManager* am = ActionManager.Instance();
                        if (am != null) combo = am->Combo.Action;
                    }

                    bool inCombat = Condition[ConditionFlag.InCombat];
                    var statusText = isRunning ? (inCombat ? "COMBATIENDO" : "ESPERANDO") : "PAUSADO";

                    _ = SendJsonAsync("status", new
                    {
                        is_running = isRunning,
                        status = statusText,
                        hp = (player != null) ? (int)player.CurrentHp : 0,
                        max_hp = (player != null) ? (int)player.MaxHp : 1,
                        target = targetName,
                        job = jobId,
                        combo = combo,
                        next_action = Logic.MNK_Logic.LastProposedAction,
                        queued_action = Logic.MNK_Logic.GetQueuedAction(),
                        player_name = playerName,
                        target_hp = (int)tHp,
                        target_max_hp = (int)tMax
                    });
                }
            }
            catch (Exception ex)
            {
                if ((DateTime.Now - lastSentTime).TotalSeconds > 1)
                    Log.Error(ex, "Error en OnGameUpdate");
            }
        }

        private async Task SendJsonAsync(string type, object content)
        {
            if (activeSocket == null || activeSocket.State != WebSocketState.Open || cts.IsCancellationRequested) return;

            bool lockTaken = false;
            try
            {
                await _socketLock.WaitAsync(cts.Token);
                lockTaken = true;

                if (activeSocket == null || activeSocket.State != WebSocketState.Open) return;

                var wrapper = new WebMessage { type = type, data = content };
                var json = JsonConvert.SerializeObject(wrapper);
                var bytes = Encoding.UTF8.GetBytes(json);

                await activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Warning($"Error WebSocket: {ex.Message}"); }
            finally
            {
                if (lockTaken) try { _socketLock.Release(); } catch { }
            }
        }

        private async Task StartWebServer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (httpListener != null) try { httpListener.Close(); } catch { }
                    httpListener = new HttpListener();
                    httpListener.Prefixes.Add("http://127.0.0.1:5055/");
                    httpListener.Start();
                    Log.Info("Servidor Web iniciado en puerto 5055");

                    while (httpListener.IsListening && !token.IsCancellationRequested)
                    {
                        var context = await httpListener.GetContextAsync();

                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);

                            await _socketLock.WaitAsync(token);
                            try { activeSocket = wsContext.WebSocket; }
                            finally { _socketLock.Release(); }

                            Log.Info("Cliente WebSocket conectado");
                            await ReceiveCommands(activeSocket, token);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error fatal en servidor web. Reiniciando...");
                    await Task.Delay(2000, token);
                }
            }
        }

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
                    _ = Task.Run(() => HandleJsonCommand(jsonMsg), token);
                }
                catch { break; }
            }
            Log.Info("Cliente WebSocket desconectado");
        }

        private void HandleJsonCommand(string json)
        {
            try
            {
                JObject cmd = JObject.Parse(json);
                string type = cmd["cmd"]?.ToString() ?? "";

                if (type == "START")
                {
                    isRunning = true;
                    FocusGame();
                    SendLog("Recibido comando START desde Web");
                }
                else if (type == "STOP")
                {
                    isRunning = false;
                    SendLog("Recibido comando STOP desde Web");
                }
                else if (type == "force_action")
                {
                    string actionName = cmd["data"]?.ToString() ?? "";
                    Logic.MNK_Logic.QueueManualAction(actionName);
                    FocusGame();
                    SendLog($"Acción forzada recibida: {actionName}");
                }
                else if (type == "get_config")
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
                else if (type == "save_global")
                {
                    string keyStr = cmd["data"]?["ToggleKey"]?.ToString() ?? "F8";
                    if (Enum.TryParse(keyStr, out VirtualKey k))
                    {
                        Config.ToggleHotkey = k;
                        Config.Save();
                        SendLog($"Hotkey global actualizada a: {k}");
                    }
                }
                else if (type == "save_config")
                {
                    var newMonk = cmd["data"]?.ToObject<MyOwnACR.JobConfigs.JobConfig_MNK>();
                    if (newMonk != null) { Config.Monk = newMonk; Config.Save(); }
                }
                else if (type == "save_survival")
                {
                    var newSurv = cmd["data"]?.ToObject<SurvivalConfig>();
                    if (newSurv != null) { Config.Survival = newSurv; Config.Save(); }
                }
                else if (type == "save_operation")
                {
                    var newOp = cmd["data"]?.ToObject<OperationalSettings>();
                    if (newOp != null) { Config.Operation = newOp; Config.Save(); }
                    FocusGame();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error procesando comando JSON");
                SendLog($"Error procesando comando: {ex.Message}");
            }
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
