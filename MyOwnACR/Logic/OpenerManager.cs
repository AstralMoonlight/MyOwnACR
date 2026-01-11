// Archivo: Logic/OpenerManager.cs
// Descripción: Gestor de secuencias de apertura (Openers) con control de flujo inteligente.
// VERSION: v22.1 - Fix CS0214 (Unsafe context added to helper).

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Models;
using MyOwnACR.GameData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MyOwnACR.Logic
{
    /// <summary>
    /// Gestiona la ejecución paso a paso de secuencias de combate predefinidas (Openers).
    /// Incluye lógica de detección de éxito, manejo de pociones y control de tiempos (throttling).
    /// </summary>
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

        // Freno temporal para respetar animaciones y GCDs compartidos.
        private DateTime throttleTime = DateTime.MinValue;

        private const double ACTION_CONFIRM_WINDOW = 0.6;
        private const double TIMEOUT_SECONDS = 3.5;

        public static OpenerManager Instance { get; } = new OpenerManager();
        private OpenerManager() { }

        // =========================================================================
        // GESTIÓN DE ARCHIVOS Y PERFILES
        // =========================================================================

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
                    if (profile != null && profile.Steps.Count > 0) availableOpeners.Add(profile);
                }
                Plugin.Log.Info($"[OpenerManager] Cargados {availableOpeners.Count} openers.");
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

        // =========================================================================
        // CONTROL DE ESTADO
        // =========================================================================

        public void Start()
        {
            if (activeProfile == null) return;
            IsRunning = true;
            CurrentStepIndex = 0;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;
            throttleTime = DateTime.MinValue;
            Plugin.Instance.SendLog($"[OPENER START] {activeProfile.Name}");
        }

        public void Stop()
        {
            IsRunning = false;
            CurrentStepIndex = 0;
        }

        public void Reset() => Stop();

        // =========================================================================
        // NÚCLEO DE EJECUCIÓN
        // =========================================================================

        public unsafe (uint actionId, KeyBind? bind) GetNextAction(ActionManager* am, IPlayerCharacter player, object config)
        {
            if (!IsRunning || activeProfile == null) return (0, null);

            // 0. THROTTLE CHECK
            if (DateTime.Now < throttleTime) return (0, null);

            // 1. CHEQUEO DE FINALIZACIÓN
            if (CurrentStepIndex >= activeProfile.Steps.Count)
            {
                Plugin.Instance.SendLog("[OPENER FINISH] Secuencia completada.");
                Stop();
                return (0, null);
            }

            var currentStep = activeProfile.Steps[CurrentStepIndex];
            uint actionId = currentStep.ActionId;
            string keyName = currentStep.KeyName;

            // --- LÓGICA DINÁMICA (BARD PROCS) ---
            if (actionId == BRD_IDs.BurstShot)
            {
                bool hasRefulgentProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                                        HasStatus(player, BRD_IDs.Status_HawksEye);
                if (hasRefulgentProc)
                {
                    actionId = BRD_IDs.RefulgentArrow;
                    keyName = "RefulgentArrow";
                }
            }

            // --- LÓGICA DE POCIONES ---
            if (currentStep.Name == "Potion" || currentStep.Type == "Potion")
            {
                var ops = Plugin.Instance.Config.Operation;

                // Si pociones están desactivadas, saltar paso
                if (!ops.UsePotion || ops.SelectedPotionId == 0)
                {
                    AdvanceStep(am, 0);
                    return GetNextAction(am, player, config);
                }

                var potionId = ops.SelectedPotionId;

                // Si no está lista o en CD largo, saltar paso
                if (!InventoryManager.IsPotionReady(am, potionId))
                {
                    AdvanceStep(am, 0);
                    return GetNextAction(am, player, config);
                }

                // Si se usó recientemente, éxito
                if (IsPotionRecentlyUsed(am, potionId))
                {
                    AdvanceStep(am, 1100); // Delay por animación de poción
                    return GetNextAction(am, player, config);
                }

                // Intentar usar poción
                if ((DateTime.Now - lastRequestTime).TotalSeconds > ACTION_CONFIRM_WINDOW)
                {
                    var requestSent = InventoryManager.UseSpecificPotion(am, potionId);
                    if (requestSent)
                    {
                        lastRequestTime = DateTime.Now;
                        return (0, null); // Esperando confirmación
                    }
                    else
                    {
                        // Fallo al enviar (probablemente sin stock), saltar paso
                        AdvanceStep(am, 0);
                        return GetNextAction(am, player, config);
                    }
                }
                return (0, null);
            }

            // --- VERIFICACIÓN DE ÉXITO DE ACCIÓN ---

            // 1. Buff Activo (Prioridad alta para oGCDs que aplican buffs)
            if (IsBuffApplied(player, actionId))
            {
                AdvanceStep(am, actionId);
                return GetNextAction(am, player, config);
            }

            // 2. Cooldown Detectado (Usando ActionLibrary para manejar GCDs compartidos)
            if (IsActionRecentlyUsed(am, actionId))
            {
                AdvanceStep(am, actionId);
                return GetNextAction(am, player, config);
            }

            // 3. FAIL-SAFE (Timeout)
            if ((DateTime.Now - stepStartTime).TotalSeconds > TIMEOUT_SECONDS)
            {
                Plugin.Instance.SendLog($"[OPENER ABORT] Timeout en paso {CurrentStepIndex + 1} ({currentStep.Name}).");
                Stop();
                return (0, null);
            }

            // 4. ANTI-DOUBLE CAST
            if (actionId == lastRequestedId && (DateTime.Now - lastRequestTime).TotalSeconds < ACTION_CONFIRM_WINDOW)
            {
                return (0, null);
            }

            // --- ENVIAR ACCIÓN ---
            lastRequestTime = DateTime.Now;
            lastRequestedId = actionId;

            var bind = MapKeyBind(keyName, config);
            return (actionId, bind);
        }

        // =========================================================================
        // HELPERS DE DETECCIÓN
        // =========================================================================

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
                // BARDO
                case 3559: expectedBuff = BRD_IDs.Status_WanderersMinuet; break;
                case 101: expectedBuff = BRD_IDs.Status_RagingStrikes; break;
                case 118: expectedBuff = BRD_IDs.Status_BattleVoice; break;
                case 25785: expectedBuff = BRD_IDs.Status_RadiantFinale; break;
                case 107: expectedBuff = BRD_IDs.Status_Barrage; break;

                // MONJE
                case MNK_IDs.RiddleOfFire: expectedBuff = MNK_IDs.Status_RiddleOfFire; break;
                case MNK_IDs.Brotherhood: expectedBuff = MNK_IDs.Status_Brotherhood; break;
                case MNK_IDs.RiddleOfWind: expectedBuff = MNK_IDs.Status_RiddleOfWind; break;
                case MNK_IDs.RiddleOfEarth: expectedBuff = MNK_IDs.Status_RiddleOfEarth; break;
                case MNK_IDs.PerfectBalance: expectedBuff = MNK_IDs.Status_PerfectBalance; break;
                case MNK_IDs.Mantra: expectedBuff = MNK_IDs.Status_Mantra; break;
                case MNK_IDs.FormShift: expectedBuff = MNK_IDs.Status_FormlessFist; break;
            }
            if (expectedBuff == 0) return false;
            foreach (var status in player.StatusList) if (status.StatusId == expectedBuff) return true;
            return false;
        }

        private unsafe bool IsActionRecentlyUsed(ActionManager* am, uint actionId)
        {
            // Blitz Mapping: El juego reporta Masterful Blitz aunque usemos un ID específico
            if (actionId == MNK_IDs.ElixirBurst || actionId == MNK_IDs.RisingPhoenix || actionId == MNK_IDs.PhantomRush)
            {
                if (IsActionRecentlyUsed(am, MNK_IDs.MasterfulBlitz)) return true;
            }

            var elapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
            var totalRecast = am->GetRecastTime(ActionType.Action, actionId);

            if (totalRecast <= 0) return false;

            // Usar ActionLibrary para diferenciar GCDs de oGCDs
            bool isGCD = ActionLibrary.IsGCD(actionId);

            if (isGCD)
            {
                // GCD: Solo éxito si el CD acaba de empezar (< 0.8s).
                // Si es mayor, es un cooldown compartido residual.
                if (elapsed < 0.8f) return true;
            }
            else
            {
                // oGCD: Ventana estándar de 2.2s
                if (elapsed < 2.2f) return true;
            }

            return false;
        }

        // =========================================================================
        // AVANCE DE PASOS
        // =========================================================================

        private unsafe void AdvanceStep(ActionManager* am, uint completedActionId)
        {
            CurrentStepIndex++;
            stepStartTime = DateTime.Now;
            lastRequestTime = DateTime.MinValue;
            lastRequestedId = 0;

            // Lógica de Freno Inteligente (Throttle)
            float throttleMs = 500; // Base para oGCDs

            if (completedActionId != 0)
            {
                if (ActionLibrary.IsGCD(completedActionId))
                {
                    // Si completamos un GCD, esperamos casi todo el GCD global antes de continuar.
                    var currentGCD = am->GetRecastTime(ActionType.Action, 11); // ID 11 como referencia
                    if (currentGCD > 0)
                        throttleMs = (currentGCD * 1000) - 850; // Despertar 0.85s antes del siguiente GCD (Relaxed)
                    else
                        throttleMs = 1200;
                }
            }

            if (throttleMs > 0) throttleTime = DateTime.Now.AddMilliseconds(throttleMs);
        }

        // Sobrecarga para skips simples (sin freno) - FIX: MARKED UNSAFE
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
