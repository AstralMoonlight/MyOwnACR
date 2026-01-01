// Archivo: MyOwnACR/Plugin.cs
// Descripción: Clase principal del Plugin.
// VERSION: Final Refactor (RotationManager Integrated).

using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
using MyOwnACR.GameData;
using System.Linq;
using MyOwnACR.Logic;
using MyOwnACR.Models;

namespace MyOwnACR
{
    public class WebMessage
    {
        public string type { get; set; } = "info";
        public object? data { get; set; }
    }

    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "MyOwnACR Pro";
        public static Plugin Instance { get; private set; } = null!;

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
        [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        public Configuration Config { get; set; }

        private HttpListener? httpListener;
        private WebSocket? activeSocket;
        private readonly CancellationTokenSource cts = new();
        private readonly SemaphoreSlim socketLock = new(1, 1);

        private volatile bool isRunning = false;
        private volatile bool isHotkeyDown = false;
        private volatile bool isSaveCdKeyDown = false;

        private DateTime lastSentTime = DateTime.MinValue;
        private DateTime lastErrorLogTime = DateTime.MinValue;

        // Suppress LibraryImport recommendation for now
#pragma warning disable SYSLIB1054 
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
#pragma warning restore SYSLIB1054

        public Plugin()
        {
            Instance = this;
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            var assemblyDir = PluginInterface.AssemblyLocation.DirectoryName!;
            OpenerManager.Instance.LoadOpeners(assemblyDir);

            try
            {
                MNK_ActionData.Initialize();
                InputSender.Initialize();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error crítico inicializando subsistemas");
            }

            CommandManager.AddHandler("/acr", new CommandInfo(OnCommand) { HelpMessage = "Activar/Pausar Bot" });
            CommandManager.AddHandler("/acrstatus", new CommandInfo(OnCommandStatus) { HelpMessage = "Ver Buffs" });
            CommandManager.AddHandler("/acrdebug", new CommandInfo(OnCommandDebug) { HelpMessage = "Debug Logic" });

            Task.Run(() => StartWebServer(cts.Token));
            Framework.Update += OnGameUpdate;
        }

        public void Dispose()
        {
            Framework.Update -= OnGameUpdate;
            try { cts.Cancel(); } catch { }

            CommandManager.RemoveHandler("/acr");
            CommandManager.RemoveHandler("/acrstatus");
            CommandManager.RemoveHandler("/acrdebug");

            InputSender.Dispose();

            try
            {
                if (activeSocket != null)
                {
                    activeSocket.Abort();
                    activeSocket.Dispose();
                    activeSocket = null;
                }
            }
            catch (Exception ex) { Log.Warning($"Error matando WebSocket: {ex.Message}"); }

            try
            {
                if (httpListener != null)
                {
                    if (httpListener.IsListening) httpListener.Stop();
                    httpListener.Close();
                    httpListener = null;
                }
            }
            catch (Exception ex) { Log.Warning($"Error matando HttpListener: {ex.Message}"); }

            try { socketLock.Dispose(); cts.Dispose(); } catch { }
        }

        public void SendLog(string message) => _ = SendJsonAsync("log", message);

        private void FocusGame()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero) SetForegroundWindow(hWnd);
            }
            catch (Exception ex) { Log.Error(ex, "Error enfocando ventana"); }
        }

        private unsafe bool IsSafeToAct(out string failReason)
        {
            failReason = "";
            var gameHandle = Process.GetCurrentProcess().MainWindowHandle;
            var activeHandle = GetForegroundWindow();

            if (activeHandle != IntPtr.Zero && gameHandle != activeHandle)
            {
                failReason = $"Ventana Inactiva (Juego: {gameHandle}, Foco: {activeHandle})";
                return false;
            }
            return true;
        }

        private void OnCommand(string command, string args) => ToggleRunning();

        private void ToggleRunning()
        {
            isRunning = !isRunning;
            var status = isRunning ? "ACTIVADO" : "PAUSADO";
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
                var name = "Desconocido";
                if (statusSheet != null && statusSheet.TryGetRow(status.StatusId, out var row))
                    name = row.Name.ToString();

                var msg = $"[{status.StatusId}] {name} ({status.RemainingTime:F1}s)";
                Chat.Print(msg);
                SendLog(msg);
            }
        }

        private void OnCommandDebug(string command, string args)
        {
            // CAMBIO: Delegar al Manager
            RotationManager.Instance.PrintDebugInfo(Chat);
            SendLog("Debug Info impresa en chat del juego.");
        }

        private unsafe void OnGameUpdate(IFramework framework)
        {
            if (cts.IsCancellationRequested) return;

            try
            {
                var currentState = KeyState[Config.ToggleHotkey];
                if (currentState && !isHotkeyDown)
                {
                    isHotkeyDown = true;
                    ToggleRunning();
                }
                else if (!currentState)
                {
                    isHotkeyDown = false;
                }

                var currentSaveCdState = KeyState[VirtualKey.SUBTRACT];
                if (currentSaveCdState && !isSaveCdKeyDown)
                {
                    isSaveCdKeyDown = true;
                    Config.Operation.SaveCD = !Config.Operation.SaveCD;
                    Config.Save();

                    var status = Config.Operation.SaveCD ? "ACTIVADO" : "DESACTIVADO";
                    Chat.Print($"[ACR] Save Cooldowns: {status}");
                    SendLog($"Save Cooldowns (Manual): {status}");

                    var payload = new
                    {
                        Monk = Config.Monk,
                        Survival = Config.Survival,
                        Operation = Config.Operation,
                        Global = new { ToggleKey = Config.ToggleHotkey.ToString() }
                    };
                    _ = SendJsonAsync("config_data", payload);
                }
                else if (!currentSaveCdState)
                {
                    isSaveCdKeyDown = false;
                }

                if (isRunning)
                {
                    string blockReason;
                    if (IsSafeToAct(out blockReason))
                    {
                        var player = ObjectTable.LocalPlayer;
                        if (player != null && player.CurrentHp > 0)
                        {
                            var am = ActionManager.Instance();
                            if (am != null)
                            {
                                var jobId = player.ClassJob.RowId;
                                // Nota: Quitamos el check manual de "if jobId == 20" porque el RotationManager ya se encarga de eso.
                                // Si no hay lógica para el job actual, RotationManager simplemente no hace nada.

                                // Ejecutar supervivencia (General para todos)
                                var survivalActionUsed = Logic.Survival.Execute(
                                    am, player, Config.Survival, Config.Monk.SecondWind, Config.Monk.Bloodbath
                                );

                                if (!survivalActionUsed)
                                {
                                    // CAMBIO: Llamada centralizada al RotationManager
                                    RotationManager.Instance.Execute(am, player, ObjectTable, Config);
                                }
                            }
                        }
                    }
                    else
                    {
                        if ((DateTime.Now - lastErrorLogTime).TotalSeconds > 1)
                        {
                            lastErrorLogTime = DateTime.Now;
                            var msg = $"Bot detenido por seguridad: {blockReason}";
                            SendLog(msg);
                        }
                    }
                }

                var now = DateTime.Now;
                if ((now - lastSentTime).TotalMilliseconds >= 100)
                {
                    lastSentTime = now;
                    var player = ObjectTable.LocalPlayer;

                    var targetName = "--";
                    var playerName = "--";
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

                        var am = ActionManager.Instance();
                        if (am != null) combo = am->Combo.Action;
                    }

                    var inCombat = Condition[ConditionFlag.InCombat];
                    var statusText = isRunning ? (inCombat ? "COMBATIENDO" : "ESPERANDO") : "PAUSADO";

                    // CAMBIO: Obtener datos de acción desde el Manager
                    _ = SendJsonAsync("status", new
                    {
                        is_running = isRunning,
                        save_cd = Config.Operation.SaveCD,
                        status = statusText,
                        hp = (player != null) ? (int)player.CurrentHp : 0,
                        max_hp = (player != null) ? (int)player.MaxHp : 1,
                        target = targetName,
                        job = jobId,
                        combo,
                        next_action = RotationManager.Instance.GetLastProposedAction(),
                        queued_action = RotationManager.Instance.GetQueuedAction(),
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

            var lockTaken = false;
            try
            {
                await socketLock.WaitAsync(cts.Token);
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
                if (lockTaken) try { socketLock.Release(); } catch { }
            }
        }

        private async Task StartWebServer(CancellationToken token)
        {
            try { httpListener?.Close(); } catch { }
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://127.0.0.1:5055/");

            try
            {
                httpListener.Start();
                Log.Info("Servidor Web iniciado en puerto 5055");
            }
            catch (Exception ex)
            {
                Log.Error($"No se pudo iniciar HttpListener: {ex.Message}");
                return;
            }

            while (!token.IsCancellationRequested && httpListener.IsListening)
            {
                try
                {
                    var context = await httpListener.GetContextAsync();

                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var wsContext = await context.AcceptWebSocketAsync(null);
                                await socketLock.WaitAsync(token);
                                try
                                {
                                    if (activeSocket != null)
                                    {
                                        try { activeSocket.Abort(); activeSocket.Dispose(); } catch { }
                                    }
                                    activeSocket = wsContext.WebSocket;
                                }
                                finally { socketLock.Release(); }

                                Log.Info("Cliente WebSocket conectado (Dashboard)");
                                await ReceiveCommands(activeSocket, token);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug($"Error en manejo de cliente WS: {ex.Message}");
                            }
                        }, token);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) break;
                    Log.Error(ex, "Error en bucle HttpListener. Reintentando...");
                    await Task.Delay(1000, token);
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

                    var jsonMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
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
                var cmd = JObject.Parse(json);
                var type = cmd["cmd"]?.ToString() ?? "";

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
                    var actionName = cmd["data"]?.ToString() ?? "";
                    // CAMBIO: Delegar al Manager
                    RotationManager.Instance.QueueManualAction(actionName);
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
                    var keyStr = cmd["data"]?["ToggleKey"]?.ToString() ?? "F8";
                    if (Enum.TryParse(keyStr, out VirtualKey k))
                    {
                        Config.ToggleHotkey = k;
                        Config.Save();
                        SendLog($"Hotkey global actualizada a: {k}");
                    }
                }
                else if (type == "save_config")
                {
                    var newMonk = cmd["data"]?.ToObject<JobConfigs.JobConfig_MNK>();
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
                else if (type == "get_openers")
                {
                    var list = OpenerManager.Instance.GetOpenerNames();
                    _ = SendJsonAsync("opener_list", list);
                }
                else if (type == "get_potions")
                {
                    Framework.RunOnTick(() =>
                    {
                        try
                        {
                            var player = ObjectTable.LocalPlayer;
                            if (player != null)
                            {
                                var jobId = player.ClassJob.RowId;
                                var mainStat = JobPotionMapping.GetMainStat(jobId);
                                var potionsDict = Potion_IDs.GetListForStat(mainStat);

                                var list = potionsDict
                                    .Select(kv => new { Name = kv.Key, Id = kv.Value })
                                    .ToList();

                                _ = SendJsonAsync("potion_list", list);
                            }
                            else
                            {
                                _ = SendJsonAsync("potion_list", Array.Empty<object>());
                            }
                        }
                        catch (Exception ex) { Log.Error(ex, "Error getting potions on main thread"); }
                    });
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
