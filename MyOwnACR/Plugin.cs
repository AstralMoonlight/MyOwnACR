using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using MyOwnACR.GameData;
using MyOwnACR.Network;
using MyOwnACR.Services;
using MyOwnACR.Logic.Core;
using MyOwnACR.Logic.Common;
using MyOwnACR.JobConfigs;

namespace MyOwnACR
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "MyOwnACR Pro";
        public static Plugin Instance { get; private set; } = null!;

        // --- SERVICIOS DE DALAMUD ---
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IChatGui Chat { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] internal static ICondition Condition { get; private set; } = null!;
        [PluginService] internal static IJobGauges JobGauges { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] public static IKeyState KeyState { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        // --- COMPONENTES DEL PLUGIN ---
        public Configuration Config { get; set; }
        public GameService GameService { get; private set; }
        private WebServer? _webServer; // Puede ser null si falla el init

        // --- ESTADO ---
        public bool IsRunning { get; private set; } = false;
        private bool _isHotkeyDown = false;
        private DateTime _lastErrorLogTime = DateTime.MinValue;
        private DateTime _lastSentTime = DateTime.MinValue; // Timer para el dashboard

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            Instance = this;
            PluginInterface = pluginInterface;

            // 1. Cargar Configuración
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(PluginInterface);

            // 2. Inicializar Servicios Auxiliares
            GameService = new GameService(this);

            // 3. INICIALIZAR WEBSERVER (CRÍTICO: HACERLO ANTES DE CARGAR DATOS)
            // Esto evita el NullReferenceException cuando OpenerManager intenta loguear.
            try
            {
                _webServer = new WebServer(this);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fallo al iniciar el servidor web.");
            }

            // 4. Cargar Datos del Juego (IDs, Openers, etc.)
            // Ahora si OpenerManager hace SendLog, _webServer ya existe.
            InitializeGameData();

            // 5. Registrar Comandos y Eventos
            CommandManager.AddHandler("/acr", new CommandInfo((c, a) => ToggleRunning()) { HelpMessage = "Activar/Pausar Bot" });
            Framework.Update += OnGameUpdate;
        }

        private void InitializeGameData()
        {
            try
            {
                var assemblyDir = PluginInterface.AssemblyLocation.DirectoryName!;
                MyOwnACR.Logic.Common.ActionLibrary.Initialize(DataManager);


                // Inicialización de IDs
                MNK_ActionData.Initialize();
                SAM_ActionData.Initialize();
                BRD_ActionData.Initialize();

                // Carga de Openers
                OpenerManager.Instance.LoadOpeners(assemblyDir);

                InputSender.Initialize();
            }
            catch (Exception ex) { Log.Error(ex, "Error inicializando datos del juego"); }
        }

        public void Dispose()
        {
            Framework.Update -= OnGameUpdate;
            CommandManager.RemoveHandler("/acr");

            _webServer?.Dispose();
            InputSender.Dispose();
        }

        // --- MÉTODOS PÚBLICOS (API INTERNA) ---
        public void SetRunning(bool state) => IsRunning = state;

        public void ToggleRunning()
        {
            IsRunning = !IsRunning;
            SendLog($"Bot {(IsRunning ? "ACTIVADO" : "PAUSADO")}");
        }

        // PROTECCIÓN NULL (?): Si _webServer no está listo, no crashea.
        public void SendLog(string msg) => _webServer?.SendJson("log", msg);
        public void SendJson(string type, object data) => _webServer?.SendJson(type, data);

        public void FocusGame() => GameService.FocusGame();

        // --- BUCLE PRINCIPAL (UNSAFE) ---
        private unsafe void OnGameUpdate(IFramework framework)
        {
            // 1. Chequeo de Hotkey (F8 por defecto)
            var currentState = KeyState[Config.ToggleHotkey];
            if (currentState && !_isHotkeyDown) { _isHotkeyDown = true; ToggleRunning(); }
            else if (!currentState) { _isHotkeyDown = false; }

            // 2. Lógica del Bot
            if (IsRunning)
            {
                // Verificamos seguridad (Ventana activa, etc.)
                if (GameService.IsSafeToAct(out string blockReason))
                {
                    var player = ObjectTable.LocalPlayer;
                    if (player != null && player.CurrentHp > 0)
                    {
                        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
                        if (am != null)
                        {
                            // A. Supervivencia
                            bool survUsed = Survival.Execute(am, player, Config.Survival, Config.Monk.SecondWind, Config.Monk.Bloodbath);

                            // B. Rotación (Si no se usó supervivencia)
                            if (!survUsed)
                            {
                                RotationManager.Instance.Execute(am, player, ObjectTable, Config, DataManager, ClientState);

                            }
                        }
                    }
                }
                else
                {
                    // Throttle para no spammear el log de errores
                    if ((DateTime.Now - _lastErrorLogTime).TotalSeconds > 1)
                    {
                        _lastErrorLogTime = DateTime.Now;
                        if (!Config.Operation.UseMemoryInput) SendLog($"Bot detenido: {blockReason}");
                    }
                }
            }

            // 3. ENVÍO DE DATOS AL DASHBOARD (Cada 100ms)
            if ((DateTime.Now - _lastSentTime).TotalMilliseconds >= 100)
            {
                _lastSentTime = DateTime.Now;
                UpdateDashboard();
            }
        }

        // --- DASHBOARD UPDATER ---
        private unsafe void UpdateDashboard()
        {
            var player = ObjectTable.LocalPlayer;
            var targetName = "--";
            var playerName = "--";
            uint jobId = 0;
            uint combo = 0;
            uint tHp = 0; uint tMax = 1;

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

                var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
                if (am != null) combo = am->Combo.Action;
            }

            var inCombat = Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];
            var statusText = IsRunning ? (inCombat ? "COMBATIENDO" : "ESPERANDO") : "PAUSADO";

            // Enviamos el paquete de estado al WebServer
            SendJson("status", new
            {
                is_running = IsRunning,
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
}
