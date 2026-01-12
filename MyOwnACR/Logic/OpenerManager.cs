// Archivo: Logic/OpenerManager.cs
// Descripción: Gestor de secuencias de apertura con LOGS VOMITIVOS (Verbose).
// VERSION: v28.3 - Fix Race Condition & Log Format Adjustment.

using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData;
using MyOwnACR.JobConfigs;
using MyOwnACR.Models;
using Newtonsoft.Json;
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

        private readonly List<OpenerProfile> availableOpeners = new();
        private OpenerProfile? activeProfile = null;

        private DateTime stepStartTime;
        private DateTime lastRequestTime = DateTime.MinValue;
        private uint lastRequestedId = 0;

        private DateTime throttleTime = DateTime.MinValue;

        private const double ACTION_CONFIRM_WINDOW = 0.6;
        private const double TIMEOUT_SECONDS = 3.5;

        private int lastLoggedStepIndex = -1;

        public static OpenerManager Instance { get; } = new OpenerManager();
        private OpenerManager() { }

        // Helper para "vomitar" logs
        private void LogVerbose(string message)
        {
            Plugin.Log.Info($"[OPNR-DBG] {message}");
            Plugin.Instance.SendLog($"[OPNR] {message}");
        }

        // --- GESTIÓN DE ARCHIVOS ---
        public void LoadOpeners(string pluginConfigDir)
        {
            try
            {
                var openersDir = Path.Combine(pluginConfigDir, "Openers");
                if (!Directory.Exists(openersDir)) Directory.CreateDirectory(openersDir);

                availableOpeners.Clear();
                var files = Directory.GetFiles(openersDir, "*.json");

                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    var profile = JsonConvert.DeserializeObject<OpenerProfile>(content);
                    if (profile != null && profile.Steps.Count > 0)
                    {
                        availableOpeners.Add(profile);
                    }
                }
                LogVerbose($"Openers cargados: {availableOpeners.Count}");
            }
            catch (Exception ex)
            {
                LogVerbose($"ERROR FATAL CARGANDO OPENERS: {ex.Message}");
            }
        }

        public List<string> GetOpenerNames() => availableOpeners.Select(o => o.Name).ToList();

        public void SelectOpener(string name)
        {
            // FIX: Eliminado RunOnTick para evitar que Start() se ejecute antes de que esto termine.
            // Asumimos que SelectOpener se llama desde el hilo principal.
            try
            {
                LogVerbose($"--- SELECCIONANDO OPENER: '{name}' ---");
                activeProfile = availableOpeners.FirstOrDefault(o => o.Name == name);
                CurrentOpenerName = activeProfile?.Name ?? "Ninguno";

                if (activeProfile != null)
                {
                    ResolveActionIds(activeProfile);
                    LogVerbose($"Opener ACTIVO: {activeProfile.Name} (Pasos: {activeProfile.Steps.Count})");
                }
                else
                {
                    LogVerbose($"ERROR: No se encontró el opener con nombre '{name}'.");
                }

                Reset();
            }
            catch (Exception ex)
            {
                LogVerbose($"EXCEPCIÓN EN SELECTOPENER: {ex.Message}");
            }
        }

        // --- REFLECTION MULTI-CLASE ---
        private void ResolveActionIds(OpenerProfile profile)
        {
            List<Type> searchTypes = new List<Type>();

#pragma warning disable CS0618
            var player = Plugin.ClientState.LocalPlayer;
#pragma warning restore CS0618

            if (player == null)
            {
                LogVerbose("ERROR CRÍTICO: LocalPlayer es NULL al resolver IDs.");
                return;
            }

            // Debug de Job
            LogVerbose($"Jugador detectado: JobID {player.ClassJob.RowId}. Buscando clases de IDs...");

            // 1. IDs del Job
            if (player.ClassJob.RowId == JobDefinitions.MNK) { searchTypes.Add(typeof(MNK_IDs)); LogVerbose("-> Agregado MNK_IDs"); }
            else if (player.ClassJob.RowId == JobDefinitions.SAM) { searchTypes.Add(typeof(SAM_IDs)); LogVerbose("-> Agregado SAM_IDs"); }
            else if (player.ClassJob.RowId == JobDefinitions.BRD) { searchTypes.Add(typeof(BRD_IDs)); LogVerbose("-> Agregado BRD_IDs"); }

            // 2. IDs Globales
            searchTypes.Add(typeof(Melee_IDs));
            searchTypes.Add(typeof(All_IDs));

            int resolvedCount = 0;
            foreach (var step in profile.Steps)
            {
                if (step.Type == "Potion") continue;
                // Reseteamos ID a 0 si es un nuevo run de carga para asegurar que se busque bien
                if (!string.IsNullOrEmpty(step.KeyName)) step.ActionId = 0;

                bool found = false;
                foreach (var type in searchTypes)
                {
                    try
                    {
                        var field = type.GetField(step.KeyName, BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            var val = field.GetValue(null);
                            if (val != null)
                            {
                                step.ActionId = (uint)val;
                                found = true;
                                resolvedCount++;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (!found) LogVerbose($"[WARN] No se encontró ID para: {step.KeyName}");
            }
            LogVerbose($"Resolución finalizada. IDs mapeados: {resolvedCount}/{profile.Steps.Count}.");
        }

        public void Start()
        {
            if (activeProfile == null)
            {
                LogVerbose("INTENTO DE INICIO FALLIDO: No hay perfil seleccionado.");
                return;
            }

            LogVerbose(">>> INICIANDO SECUENCIA DE APERTURA <<<");

            IsRunning = true;
            CurrentStepIndex = 0;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;
            throttleTime = DateTime.MinValue;
            lastLoggedStepIndex = -1;
        }

        public void Stop()
        {
            if (IsRunning)
            {
                LogVerbose($"=== OPENER FINALIZADO/DETENIDO (Paso {CurrentStepIndex}/{activeProfile?.Steps.Count ?? 0}) ===");
            }
            IsRunning = false;
            CurrentStepIndex = 0;
        }

        public void Reset() => Stop();

        public unsafe (uint actionId, KeyBind? bind) GetNextAction(ActionManager* am, IPlayerCharacter player, object config)
        {
            if (!IsRunning || activeProfile == null) return (0, null);

            // 0. THROTTLE CHECK
            if (DateTime.Now < throttleTime) return (0, null);

            // 1. CHEQUEO DE FINALIZACIÓN
            if (CurrentStepIndex >= activeProfile.Steps.Count)
            {
                LogVerbose("Opener COMPLETADO exitosamente.");
                Stop();
                return (0, null);
            }

            var currentStep = activeProfile.Steps[CurrentStepIndex];

            // --- MANEJO DE PREPULL ---
            if (currentStep.Prepull > 0)
            {
                LogVerbose($"[Paso {CurrentStepIndex + 1}/{activeProfile.Steps.Count}] Prepull: {currentStep.Name} (-{currentStep.Prepull}s). Saltando...");
                AdvanceStep(am, 0);
                return GetNextAction(am, player, config);
            }

            uint actionId = currentStep.ActionId;
            string keyName = currentStep.KeyName;

            // ==========================================================
            // LOG PRINCIPAL DEL PASO (Formato solicitado)
            // ==========================================================
            if (lastLoggedStepIndex != CurrentStepIndex)
            {
                // AQUÍ ESTÁ EL FORMATO QUE PEDISTE: "Paso 1/20 NombreAccion"
                LogVerbose($"Paso {CurrentStepIndex + 1}/{activeProfile.Steps.Count} {currentStep.Name} (ID: {actionId})");
                lastLoggedStepIndex = CurrentStepIndex;
            }

            // Check crítico: Si ID es 0, no podemos hacer nada (excepto pociones)
            if (actionId == 0 && currentStep.Type != "Potion")
            {
                LogVerbose($"ERROR CRÍTICO: ID es 0 para {currentStep.Name}. El mapeo falló. Saltando paso.");
                AdvanceStep(am, 0);
                return GetNextAction(am, player, config);
            }

            // ==========================================================
            // MNK CHECK
            // ==========================================================
#pragma warning disable CS0618
            if (player.ClassJob.RowId == JobDefinitions.MNK)
#pragma warning restore CS0618
            {
                if (ActionLibrary.IsGCD(actionId))
                {
                    var gauge = Plugin.JobGauges.Get<MNKGauge>();
                    if (gauge.Chakra >= 5)
                    {
                        if (IsActionReady(am, MNK_IDs.TheForbiddenChakra))
                        {
                            LogVerbose("[AUTO] Inyectando The Forbidden Chakra (Overflow).");
                            lastRequestTime = DateTime.Now;
                            lastRequestedId = MNK_IDs.TheForbiddenChakra;
                            var chakraBind = MapKeyBind("ForbiddenChakra", config);
                            throttleTime = DateTime.Now.AddMilliseconds(600);
                            return (MNK_IDs.TheForbiddenChakra, chakraBind);
                        }
                    }
                }
            }

            // --- BARD PROCS ---
            if (actionId == BRD_IDs.BurstShot)
            {
                bool hasRefulgentProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                                        HasStatus(player, BRD_IDs.Status_HawksEye);
                if (hasRefulgentProc)
                {
                    LogVerbose("[AUTO] Proc detectado -> Refulgent Arrow.");
                    actionId = BRD_IDs.RefulgentArrow;
                    keyName = "RefulgentArrow";
                }
            }

            // --- POCIONES ---
            if (currentStep.Name == "Potion" || currentStep.Type == "Potion")
            {
                var ops = Plugin.Instance.Config.Operation;
                if (!ops.UsePotion || ops.SelectedPotionId == 0)
                {
                    LogVerbose($"[SKIP] Pociones OFF o ID 0.");
                    AdvanceStep(am, 0);
                    return GetNextAction(am, player, config);
                }

                var potionId = ops.SelectedPotionId;
                if (!InventoryManager.IsPotionReady(am, potionId))
                {
                    LogVerbose($"[SKIP] Poción no lista (CD/Cant).");
                    AdvanceStep(am, 0);
                    return GetNextAction(am, player, config);
                }

                if (IsPotionRecentlyUsed(am, potionId))
                {
                    LogVerbose($"[OK] Poción USADA. Avanzando.");
                    AdvanceStep(am, 1100);
                    return GetNextAction(am, player, config);
                }

                if ((DateTime.Now - lastRequestTime).TotalSeconds > ACTION_CONFIRM_WINDOW)
                {
                    var requestSent = InventoryManager.UseSpecificPotion(am, potionId);
                    if (requestSent)
                    {
                        LogVerbose($"[EXECUTE] Usando Poción ID {potionId}...");
                        lastRequestTime = DateTime.Now;
                        return (0, null);
                    }
                    else
                    {
                        LogVerbose("[ERROR] Fallo request Poción.");
                        AdvanceStep(am, 0);
                        return GetNextAction(am, player, config);
                    }
                }
                return (0, null);
            }

            // --- VERIFICACIÓN DE ÉXITO ---
            if (IsBuffApplied(player, actionId))
            {
                LogVerbose($"[OK] Buff detectado. Paso completado.");
                AdvanceStep(am, actionId);
                return GetNextAction(am, player, config);
            }

            if (IsActionRecentlyUsed(am, actionId))
            {
                LogVerbose($"[OK] Recast/Uso detectado. Paso completado.");
                AdvanceStep(am, actionId);
                return GetNextAction(am, player, config);
            }

            // Timeout
            if ((DateTime.Now - stepStartTime).TotalSeconds > TIMEOUT_SECONDS)
            {
                LogVerbose($"[ABORT] Timeout en {currentStep.Name}. Deteniendo Opener.");
                Stop();
                return (0, null);
            }

            // Evitar spam
            if (actionId == lastRequestedId && (DateTime.Now - lastRequestTime).TotalSeconds < ACTION_CONFIRM_WINDOW)
            {
                return (0, null);
            }

            // --- EJECUCIÓN ---
            lastRequestTime = DateTime.Now;
            lastRequestedId = actionId;

            var bind = MapKeyBind(keyName, config);
            // Log final de intento
            // LogVerbose($"[EXECUTE] {currentStep.Name}"); // Comentado para no spamear doble, ya está el log principal arriba

            return (actionId, bind);
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private unsafe bool IsActionReady(ActionManager* am, uint actionId)
        {
            return am->GetActionStatus(ActionType.Action, actionId) == 0;
        }

        private unsafe bool IsPotionRecentlyUsed(ActionManager* am, uint potionId)
        {
            var elapsed = am->GetRecastTimeElapsed(ActionType.Item, potionId);
            var total = am->GetRecastTime(ActionType.Item, potionId);
            return total > 0 && elapsed > 0 && elapsed < 5.0f;
        }

        private bool IsBuffApplied(IPlayerCharacter player, uint actionId)
        {
            if (player == null) return false;
            uint expectedBuff = 0;
            switch (actionId)
            {
                case 3559: expectedBuff = BRD_IDs.Status_WanderersMinuet; break;
                case 101: expectedBuff = BRD_IDs.Status_RagingStrikes; break;
                case 118: expectedBuff = BRD_IDs.Status_BattleVoice; break;
                case 25785: expectedBuff = BRD_IDs.Status_RadiantFinale; break;
                case 107: expectedBuff = BRD_IDs.Status_Barrage; break;
                case MNK_IDs.RiddleOfFire: expectedBuff = MNK_IDs.Status_RiddleOfFire; break;
                case MNK_IDs.Brotherhood: expectedBuff = MNK_IDs.Status_Brotherhood; break;
                case MNK_IDs.RiddleOfWind: expectedBuff = MNK_IDs.Status_RiddleOfWind; break;
                case MNK_IDs.RiddleOfEarth: expectedBuff = MNK_IDs.Status_RiddleOfEarth; break;
                case MNK_IDs.PerfectBalance: expectedBuff = MNK_IDs.Status_PerfectBalance; break;
                case MNK_IDs.Mantra: expectedBuff = MNK_IDs.Status_Mantra; break;
                case MNK_IDs.FormShift: expectedBuff = MNK_IDs.Status_FormlessFist; break;
                case SAM_IDs.MeikyoShisui: expectedBuff = SAM_IDs.Status_MeikyoShisui; break;
                case SAM_IDs.Ikishoten: expectedBuff = SAM_IDs.Status_OgiNamikiriReady; break;
                case SAM_IDs.Meditate: expectedBuff = SAM_IDs.Status_Meditate; break;
                case SAM_IDs.ThirdEye: expectedBuff = SAM_IDs.Status_ThirdEye; break;
                case SAM_IDs.Tengentsu: expectedBuff = SAM_IDs.Status_Tengentsu; break;
            }
            if (expectedBuff == 0) return false;
            foreach (var status in player.StatusList) if (status.StatusId == expectedBuff) return true;
            return false;
        }

        private unsafe bool IsActionRecentlyUsed(ActionManager* am, uint actionId)
        {
            if (actionId == MNK_IDs.ElixirBurst || actionId == MNK_IDs.RisingPhoenix || actionId == MNK_IDs.PhantomRush)
                if (IsActionRecentlyUsed(am, MNK_IDs.MasterfulBlitz)) return true;

            if (actionId == SAM_IDs.Higanbana || actionId == SAM_IDs.MidareSetsugekka || actionId == SAM_IDs.TenkaGoken || actionId == SAM_IDs.TendoGoken || actionId == SAM_IDs.TendoSetsugekka)
                if (IsActionRecentlyUsed(am, SAM_IDs.Iaijutsu)) return true;

            if (actionId == SAM_IDs.KaeshiGoken || actionId == SAM_IDs.KaeshiSetsugekka || actionId == SAM_IDs.TendoKaeshiGoken || actionId == SAM_IDs.TendoKaeshiSetsugekka)
                if (IsActionRecentlyUsed(am, SAM_IDs.TsubameGaeshi)) return true;

            if (actionId == SAM_IDs.KaeshiNamikiri)
                if (IsActionRecentlyUsed(am, SAM_IDs.OgiNamikiri)) return true;

            var elapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
            var totalRecast = am->GetRecastTime(ActionType.Action, actionId);

            if (totalRecast <= 0) return false;

            bool isGCD = ActionLibrary.IsGCD(actionId);
            if (isGCD) { if (elapsed < 0.8f) return true; }
            else { if (elapsed < 2.2f) return true; }

            return false;
        }

        private unsafe void AdvanceStep(ActionManager* am, uint completedActionId)
        {
            float throttleMs = 450;
            if (completedActionId != 0 && ActionLibrary.IsGCD(completedActionId))
            {
                var currentGCD = am->GetRecastTime(ActionType.Action, 11);
                if (currentGCD > 0) throttleMs = (currentGCD * 1000) - 1000;
                else throttleMs = 1200;
            }
            if (throttleMs < 0) throttleMs = 0;

            // Log pequeño de avance
            // LogVerbose($"-> Avanzando paso. Espera: {throttleMs:F0}ms");

            CurrentStepIndex++;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;
            if (throttleMs > 0) throttleTime = DateTime.Now.AddMilliseconds(throttleMs);
        }

        private unsafe void AdvanceStep(ActionManager* am, float manualDelayMs)
        {
            CurrentStepIndex++;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;
            if (manualDelayMs > 0) throttleTime = DateTime.Now.AddMilliseconds(manualDelayMs);
            else throttleTime = DateTime.MinValue;
        }

        private KeyBind? MapKeyBind(string keyName, object config)
        {
            if (string.IsNullOrEmpty(keyName) || config == null || keyName == "Pocion") return null;
            try
            {
                var type = config.GetType();
                var field = type.GetField(keyName);
                if (field != null) return field.GetValue(config) as KeyBind;
                var prop = type.GetProperty(keyName);
                if (prop != null) return prop.GetValue(config) as KeyBind;
            }
            catch { }
            return null;
        }

        private bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return false;
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return true;
            return false;
        }
    }
}
