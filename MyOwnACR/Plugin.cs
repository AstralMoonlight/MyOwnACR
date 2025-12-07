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
    public class WebMessage
    {
        public string type { get; set; } = "info";
        public object? data { get; set; }
    }

    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "MyOwnACR Pro";

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

        public Configuration Config { get; set; }

        private HttpListener? httpListener;
        private WebSocket? activeSocket;
        private CancellationTokenSource cts;

        private bool isRunning = false;
        private DateTime lastSentTime = DateTime.MinValue;
        private bool isSending = false;

        private bool isHotkeyDown = false;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public Plugin()
        {
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            CommandManager.AddHandler("/acr", new CommandInfo(OnCommand) { HelpMessage = "Activar/Pausar Bot" });
            CommandManager.AddHandler("/acrstatus", new CommandInfo(OnCommandStatus) { HelpMessage = "Ver Buffs" });
            CommandManager.AddHandler("/acrdebug", new CommandInfo(OnCommandDebug) { HelpMessage = "Debug Logic" });

            cts = new CancellationTokenSource();
            Task.Run(() => StartWebServer(cts.Token));

            Framework.Update += OnGameUpdate;

            // SILENCIADO: Mensaje de carga
            // Chat.Print("[MyOwnACR] Cargado. Pulsa " + Config.ToggleHotkey + " para activar.");
        }

        public void Dispose()
        {
            Framework.Update -= OnGameUpdate;
            CommandManager.RemoveHandler("/acr");
            CommandManager.RemoveHandler("/acrstatus");
            CommandManager.RemoveHandler("/acrdebug");

            cts.Cancel();
            try { httpListener?.Abort(); } catch { }
            try { activeSocket?.Dispose(); } catch { }
            cts.Dispose();
        }

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

        private void OnCommand(string command, string args)
        {
            ToggleRunning();
        }

        private void ToggleRunning()
        {
            isRunning = !isRunning;

            // SILENCIADO: Mensaje de activación/desactivación por chat
            /*
            var color = isRunning ? 45 : 538; 
            var msg = isRunning ? "ACTIVADO" : "PAUSADO";
            var sb = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder()
                .AddUiForeground((ushort)color)
                .AddText($">> ACR {msg} <<")
                .Build();
            Chat.Print(sb);
            */
        }

        private void OnCommandStatus(string command, string args)
        {
            // ESTE SE MANTIENE porque es una solicitud explícita de información
            var player = ObjectTable.LocalPlayer;
            if (player == null) return;

            var statusSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            Chat.Print("====== BUFFS ACTIVOS ======");
            foreach (var status in player.StatusList)
            {
                if (status.StatusId == 0) continue;
                string name = "Desconocido";
                if (statusSheet != null && statusSheet.TryGetRow(status.StatusId, out var row))
                    name = row.Name.ToString();
                Chat.Print($"[{status.StatusId}] {name} ({status.RemainingTime:F1}s)");
            }
        }

        private void OnCommandDebug(string command, string args)
        {
            // ESTE SE MANTIENE para debugging
            Logic.MNK_Logic.PrintDebugInfo(Chat);
        }

        private unsafe void OnGameUpdate(IFramework framework)
        {
            try
            {
                // 1. HOTKEY
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

                // 2. LÓGICA
                if (isRunning)
                {
                    var player = ObjectTable.LocalPlayer;
                    if (player != null && player.CurrentHp > 0 && player.TargetObject != null)
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

                // 3. DASHBOARD UPDATE
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
            catch { }
        }

        private async Task SendJsonAsync(string type, object content)
        {
            if (activeSocket == null || activeSocket.State != WebSocketState.Open || isSending) return;
            isSending = true;
            try
            {
                var wrapper = new WebMessage { type = type, data = content };
                var json = JsonConvert.SerializeObject(wrapper);
                var bytes = Encoding.UTF8.GetBytes(json);
                await activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
            finally { isSending = false; }
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

                    while (httpListener.IsListening && !token.IsCancellationRequested)
                    {
                        var context = await httpListener.GetContextAsync();
                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);
                            activeSocket = wsContext.WebSocket;
                            await ReceiveCommands(activeSocket, token);
                        }
                        else { context.Response.StatusCode = 400; context.Response.Close(); }
                    }
                }
                catch { await Task.Delay(2000, token); }
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
                    HandleJsonCommand(jsonMsg);
                }
                catch { break; }
            }
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
                    // SILENCIADO: Chat.Print(">> START (Web) <<");
                    FocusGame();
                }

                if (type == "STOP")
                {
                    isRunning = false;
                    // SILENCIADO: Chat.Print(">> STOP (Web) <<");
                }

                if (type == "force_action")
                {
                    string actionName = cmd["data"]?.ToString() ?? "";
                    Logic.MNK_Logic.QueueManualAction(actionName);
                    FocusGame();
                }

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

                if (type == "save_global")
                {
                    string keyStr = cmd["data"]?["ToggleKey"]?.ToString() ?? "F8";
                    if (Enum.TryParse(keyStr, out VirtualKey k))
                    {
                        Config.ToggleHotkey = k;
                        Config.Save();
                        // SILENCIADO: Chat.Print("[ACR] Hotkey cambiada a: " + k);
                    }
                }

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
                    FocusGame();
                }
            }
            catch { }
        }
    }
}
