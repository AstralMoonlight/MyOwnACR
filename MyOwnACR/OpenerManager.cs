using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types; // NECESARIO PARA IPlayerCharacter
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Openers;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MyOwnACR.Logic
{
    public class OpenerManager
    {
        public bool IsRunning { get; private set; } = false;
        public int CurrentStepIndex { get; private set; } = 0;
        public string CurrentOpenerName { get; private set; } = "Ninguno";

        private List<OpenerProfile> availableOpeners = new();
        private OpenerProfile? activeProfile = null;
        private DateTime stepStartTime;

        // --- CONTROL DE TRÁFICO ---
        private DateTime lastRequestTime = DateTime.MinValue;
        private uint lastRequestedId = 0;
        private const double ACTION_CONFIRM_WINDOW = 0.6;
        private const double TIMEOUT_SECONDS = 3.0;

        public static OpenerManager Instance { get; } = new OpenerManager();

        private OpenerManager() { }

        // --- GESTIÓN DE ARCHIVOS ---
        public void LoadOpeners(string pluginConfigDir)
        {
            try
            {
                string openersDir = Path.Combine(pluginConfigDir, "Openers");
                if (!Directory.Exists(openersDir)) Directory.CreateDirectory(openersDir);

                availableOpeners.Clear();
                var files = Directory.GetFiles(openersDir, "*.json");

                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    var profile = JsonConvert.DeserializeObject<OpenerProfile>(content);
                    if (profile != null && profile.Steps.Count > 0) availableOpeners.Add(profile);
                }
                Plugin.Log.Info($"Cargados {availableOpeners.Count} openers.");
            }
            catch (Exception ex) { Plugin.Log.Error(ex, "Error cargando openers."); }
        }

        public List<string> GetOpenerNames() => availableOpeners.Select(o => o.Name).ToList();

        public void SelectOpener(string name)
        {
            activeProfile = availableOpeners.FirstOrDefault(o => o.Name == name);
            CurrentOpenerName = activeProfile?.Name ?? "Ninguno";
            Reset();
        }

        public void Start()
        {
            if (activeProfile == null) return;
            IsRunning = true;
            CurrentStepIndex = 0;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;
            Plugin.Instance.SendLog($"[OPENER] Iniciando: {activeProfile.Name}");
        }

        public void Stop()
        {
            IsRunning = false;
            CurrentStepIndex = 0;
        }

        public void Reset() => Stop();

        // CAMBIO: Aceptamos 'object' en config para hacerlo genérico
        public unsafe (uint actionId, KeyBind? bind) GetNextAction(ActionManager* am, IPlayerCharacter player, object config)
        {
            if (!IsRunning || activeProfile == null) return (0, null);

            // 1. CHEQUEO DE FINALIZACIÓN
            if (CurrentStepIndex >= activeProfile.Steps.Count)
            {
                Plugin.Instance.SendLog("[OPENER] Finalizado con éxito.");
                Stop();
                return (0, null);
            }

            var currentStep = activeProfile.Steps[CurrentStepIndex];

            // -----------------------------------------------------------------------
            // LÓGICA DE POCIONES (INTEGRACIÓN COMPLETA)
            // -----------------------------------------------------------------------
            if (currentStep.Name == "Potion" || currentStep.Type == "Potion")
            {
                // A. Verificar configuración del usuario (Dashboard)
                // Accedemos a la configuración operativa global a través de la instancia del Plugin
                var ops = Plugin.Instance.Config.Operation;

                if (!ops.UsePotion || ops.SelectedPotionId == 0)
                {
                    Plugin.Instance.SendLog("[OPENER] Pociones desactivadas o ninguna seleccionada. Saltando paso.");
                    AdvanceStep();
                    // Llamada recursiva para procesar inmediatamente el siguiente paso real
                    return GetNextAction(am, player, config);
                }

                uint potionId = ops.SelectedPotionId;

                // B. Chequeo de Cooldown (¿Está disponible en el juego?)
                // Si retorna false, significa que está en CD o no tenemos el ítem.
                if (!InventoryManager.IsPotionReady(am, potionId))
                {
                    Plugin.Instance.SendLog($"[OPENER] Poción (ID {potionId}) en CD o no disponible. Saltando paso.");
                    AdvanceStep();
                    return GetNextAction(am, player, config);
                }

                // C. Chequeo de Éxito (¿Se activó el CD recientemente?)
                // Si acabamos de intentar usarla y el juego activó el cooldown, consideramos éxito.
                if (IsPotionRecentlyUsed(am, potionId))
                {
                    AdvanceStep();
                    return GetNextAction(am, player, config);
                }

                // D. Intentar usar la poción (Con Control de Tráfico)
                // Respetamos el ACTION_CONFIRM_WINDOW para no spamear la llamada al servidor.
                if ((DateTime.Now - lastRequestTime).TotalSeconds > ACTION_CONFIRM_WINDOW)
                {
                    // InventoryManager se encarga de la inyección en memoria
                    bool requestSent = InventoryManager.UseSpecificPotion(am, potionId);

                    if (requestSent)
                    {
                        Plugin.Instance.SendLog($"[OPENER] Usando Poción ID {potionId}...");
                        lastRequestTime = DateTime.Now;
                        // Retornamos (0, null) para pausar la lógica del bot mientras el ítem se usa.
                        // En el siguiente ciclo, IsPotionRecentlyUsed debería dar true.
                        return (0, null);
                    }
                    else
                    {
                        Plugin.Instance.SendLog("[OPENER] Fallo al usar poción (¿Sin stock?). Saltando paso.");
                        AdvanceStep();
                        return GetNextAction(am, player, config);
                    }
                }

                // Si estamos esperando confirmación, no hacemos nada.
                return (0, null);
            }
            // -----------------------------------------------------------------------

            // 2. VERIFICACIÓN DE ÉXITO PARA HABILIDADES (Doble Check: Cooldown O Buff)
            // Si el CD se activó O si tenemos el Buff correspondiente (para skills con cargas como PB)
            if (IsActionRecentlyUsed(am, currentStep.ActionId) || IsBuffApplied(player, currentStep.ActionId))
            {
                AdvanceStep();
                return GetNextAction(am, player, config);
            }

            // 3. FAIL-SAFE (TIMEOUT)
            // Si pasamos demasiado tiempo intentando un paso sin éxito, abortamos para no quedarnos pegados.
            if ((DateTime.Now - stepStartTime).TotalSeconds > TIMEOUT_SECONDS)
            {
                Plugin.Instance.SendLog($"[OPENER] ABORTADO - Timeout en paso {CurrentStepIndex + 1} ({currentStep.Name})");
                Stop();
                return (0, null);
            }

            // 4. RETORNAR ACCIÓN (CON ANTI-DOUBLE CAST)
            // Si ya pedimos esta acción hace poco, esperamos.
            if (currentStep.ActionId == lastRequestedId && (DateTime.Now - lastRequestTime).TotalSeconds < ACTION_CONFIRM_WINDOW)
            {
                return (0, null);
            }

            lastRequestTime = DateTime.Now;
            lastRequestedId = currentStep.ActionId;

            KeyBind? bind = MapKeyBind(currentStep.KeyName, config);
            return (currentStep.ActionId, bind);
        }

        /// <summary>
        /// Verifica si una poción específica acaba de entrar en cooldown.
        /// </summary>
        private unsafe bool IsPotionRecentlyUsed(ActionManager* am, uint potionId)
        {
            float elapsed = am->GetRecastTimeElapsed(ActionType.Item, potionId);
            float total = am->GetRecastTime(ActionType.Item, potionId);

            // Las pociones tienen CD largo (4m 30s).
            // Si el elapsed es pequeño (< 5s) y hay un total activo, es que acabamos de usarla con éxito.
            return total > 0 && elapsed > 0 && elapsed < 5.0f;
        }

        // Detecta si una habilidad con cargas (como PB) aplicó su efecto
        private bool IsBuffApplied(IPlayerCharacter player, uint actionId)
        {
            if (player == null) return false;

            // Mapeo manual de Acción -> Buff ID (Se puede mover a un diccionario después)
            uint expectedBuff = 0;

            switch (actionId)
            {
                case 69: expectedBuff = 110; break;   // Perfect Balance -> Buff 110
                case 7396: expectedBuff = 1185; break; // Brotherhood -> Buff 1185
                case 7395: expectedBuff = 1181; break; // Riddle of Fire -> Buff 1181
                case 25766: expectedBuff = 2687; break; // Riddle of Wind -> Buff 2687
            }

            if (expectedBuff == 0) return false;

            // Buscamos si el buff está activo
            foreach (var status in player.StatusList)
            {
                if (status.StatusId == expectedBuff) return true;
            }
            return false;
        }

        private unsafe bool IsActionRecentlyUsed(ActionManager* am, uint actionId)
        {
            float elapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
            float totalRecast = am->GetRecastTime(ActionType.Action, actionId);
            // Si el CD está activo y lleva menos de 2.5s
            if (totalRecast > 0 && elapsed > 0 && elapsed < 2.5f) return true;
            return false;
        }

        private void AdvanceStep()
        {
            CurrentStepIndex++;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;
        }

        private KeyBind? MapKeyBind(string keyName, object config)
        {
            if (string.IsNullOrEmpty(keyName) || config == null || keyName == "Pocion") return null;
            try
            {
                Type type = config.GetType();
                FieldInfo? field = type.GetField(keyName);
                if (field != null) return field.GetValue(config) as KeyBind;
                PropertyInfo? prop = type.GetProperty(keyName);
                if (prop != null) return prop.GetValue(config) as KeyBind;
            }
            catch { }
            return null;
        }
    }
}
