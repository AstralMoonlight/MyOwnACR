using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.JobGauge.Types;
using Lumina.Excel.Sheets;

namespace MyOwnACR
{
    // Estructura de mensajes Web
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

        public Configuration Config { get; set; }

        private HttpListener? httpListener;
        private WebSocket? activeSocket;
        private CancellationTokenSource cts;

        private bool isRunning = false;
        private DateTime lastSentTime = DateTime.MinValue;
        private bool isSending = false;

        public Plugin()
        {
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            // --- COMANDOS ---
            CommandManager.AddHandler("/acr", new CommandInfo(OnCommand) { HelpMessage = "Activar/Pausar Bot" });

            CommandManager.AddHandler("/acrstatus", new CommandInfo(OnCommandStatus)
            {
                HelpMessage = "Muestra los estados (buffs) actuales del jugador."
            });

            // Comando de Debugging
            CommandManager.AddHandler("/acrdebug", new CommandInfo(OnCommandDebug)
            {
                HelpMessage = "Muestra información interna de la lógica (Nadis, Chakras, Intención)."
            });

            cts = new CancellationTokenSource();
            Task.Run(() => StartWebServer(cts.Token));

            Framework.Update += OnGameUpdate;
            Chat.Print("[MyOwnACR] Cargado. Configurable vía Web.");
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

        private void OnCommand(string command, string args)
        {
            isRunning = !isRunning;
            Chat.Print(isRunning ? ">> ACR ACTIVADO <<" : ">> ACR PAUSADO <<");
        }

        private void OnCommandStatus(string command, string args)
        {
            var player = ObjectTable.LocalPlayer;
            if (player == null)
            {
                Chat.Print("No se encontró al jugador.");
                return;
            }

            // --- CORRECCIÓN AQUÍ: Especificamos el namespace completo para evitar ambigüedad ---
            var statusSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();

            Chat.Print("====== BUFFS ACTIVOS ======");

            foreach (var status in player.StatusList)
            {
                if (status.StatusId == 0) continue;

                string name = "Desconocido";
                if (statusSheet != null && statusSheet.TryGetRow(status.StatusId, out var row))
                {
                    name = row.Name.ToString();
                }

                Chat.Print($"[{status.StatusId}] {name} ({status.RemainingTime:F1}s)");
            }
            Chat.Print("===========================");
        }

        // Handler para /acrdebug
        private void OnCommandDebug(string command, string args)
        {
            // Llama a la función estática de MNK_Logic
            Logic.MNK_Logic.PrintDebugInfo(Chat);
        }

        private unsafe void OnGameUpdate(IFramework framework)
        {
            try
            {
                if (!isRunning)
                    return;

                var player = ObjectTable.LocalPlayer;
                if (player == null) return;

                if (player.CurrentHp <= 0 || player.TargetObject == null)
                    return;

                ActionManager* am = ActionManager.Instance();
                if (am == null) return;

                var jobId = player.ClassJob.RowId;

                // --- LÓGICA DE COMBATE ---
                if (jobId == 20 || jobId == 2) // Monk / Pugilist
                {
                    bool survivalActionUsed = Logic.Survival.Execute(
                        am,
                        player,
                        Config.Survival,
                        Config.Monk.SecondWind,
                        Config.Monk.Bloodbath
                    );

                    if (!survivalActionUsed)
                    {
                        Logic.MNK_Logic.Execute(am, player, Config.Monk, ObjectTable, Config.Operation);
                    }
                }

                // --- ENVÍO DE ESTADO AL PANEL WEB ---
                var now = DateTime.Now;
                if ((now - lastSentTime).TotalMilliseconds >= 100)
                {
                    lastSentTime = now;

                    var playerName = player.Name.TextValue;
                    var targetName = player.TargetObject?.Name?.TextValue ?? "--";
                    var combo = am->Combo.Action;

                    uint targetHp = 0;
                    uint targetMaxHp = 0;

                    if (player.TargetObject is Dalamud.Game.ClientState.Objects.Types.ICharacter tChar)
                    {
                        targetHp = tChar.CurrentHp;
                        targetMaxHp = tChar.MaxHp;
                    }

                    bool inCombat = Condition[ConditionFlag.InCombat];
                    var status = inCombat ? "COMBATIENDO" : "IDLE";

                    _ = SendJsonAsync("status", new
                    {
                        hp = (int)player.CurrentHp,
                        max_hp = (int)player.MaxHp,
                        target = targetName,
                        job = jobId,
                        combo = combo,

                        // Enviamos la intención futura para el panel
                        next_action = Logic.MNK_Logic.LastProposedAction,

                        status = status,
                        player_name = playerName,
                        target_hp = (int)targetHp,
                        target_max_hp = (int)targetMaxHp
                    });
                }
            }
            catch
            {
                // Chat.PrintError("[MyOwnACR] Error en OnGameUpdate");
            }
        }

        private async Task SendJsonAsync(string type, object content)
        {
            if (activeSocket == null || activeSocket.State != WebSocketState.Open || isSending)
                return;

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

        // --- SERVIDOR WEB ---
        private async Task StartWebServer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (httpListener != null)
                    {
                        try { httpListener.Close(); } catch { }
                    }

                    httpListener = new HttpListener();
                    httpListener.Prefixes.Add("http://127.0.0.1:5055/");
                    httpListener.Start();

                    while (httpListener.IsListening && !token.IsCancellationRequested)
                    {
                        var contextTask = httpListener.GetContextAsync();
                        var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, token));
                        if (completedTask != contextTask)
                            return;

                        var context = await contextTask;
                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);
                            activeSocket = wsContext.WebSocket;
                            await ReceiveCommands(activeSocket, token);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
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
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

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
                    Chat.Print(">> START <<");
                }

                if (type == "STOP")
                {
                    isRunning = false;
                    Chat.Print(">> STOP <<");
                }

                if (type == "get_config")
                {
                    var payload = new
                    {
                        Monk = Config.Monk,
                        Survival = Config.Survival,
                        Operation = Config.Operation
                    };
                    _ = SendJsonAsync("config_data", payload);
                }

                if (type == "save_config")
                {
                    var newMonkConfig = cmd["data"]?.ToObject<JobConfigs.JobConfig_MNK>();
                    if (newMonkConfig != null)
                    {
                        Config.Monk = newMonkConfig;
                        Config.Save();
                        Chat.Print("[ACR] Configuración de Monk actualizada.");
                    }
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
                }
            }
            catch (Exception ex)
            {
                Chat.PrintError("Error recibiendo comando web: " + ex.Message);
            }
        }
    }
}
