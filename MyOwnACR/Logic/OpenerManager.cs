// Archivo: Logic/OpenerManager.cs
// VERSION: Logic Update (Dynamic Burst Shot -> Refulgent).

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Models;
using MyOwnACR.GameData; // Necesario para BRD_IDs
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

            // Variables locales para manipulación dinámica
            uint actionId = currentStep.ActionId;
            string keyName = currentStep.KeyName;

            // ==========================================================
            // LÓGICA DINÁMICA: Sustitución de Procs (Bard)
            // ==========================================================
            if (actionId == BRD_IDs.BurstShot)
            {
                // Chequear Status: Straight Shot Ready (122) o Hawk's Eye (3861)
                bool hasRefulgentProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                                        HasStatus(player, BRD_IDs.Status_HawksEye);

                if (hasRefulgentProc)
                {
                    // Cambiamos la acción a Refulgent Arrow dinámicamente
                    actionId = BRD_IDs.RefulgentArrow;
                    keyName = "RefulgentArrow"; // Importante: Cambiar KeyName para buscar el bind correcto
                    // Plugin.Instance.SendLog("[OPENER] Proc detectado: Burst Shot -> Refulgent Arrow");
                }
            }
            // ==========================================================

            // LÓGICA DE POCIONES
            if (currentStep.Name == "Potion" || currentStep.Type == "Potion")
            {
                var ops = Plugin.Instance.Config.Operation;

                if (!ops.UsePotion || ops.SelectedPotionId == 0)
                {
                    Plugin.Instance.SendLog("[OPENER] Pociones desactivadas o ninguna seleccionada. Saltando paso.");
                    AdvanceStep();
                    return GetNextAction(am, player, config);
                }

                var potionId = ops.SelectedPotionId;

                if (!InventoryManager.IsPotionReady(am, potionId))
                {
                    Plugin.Instance.SendLog($"[OPENER] Poción (ID {potionId}) en CD o no disponible. Saltando paso.");
                    AdvanceStep();
                    return GetNextAction(am, player, config);
                }

                if (IsPotionRecentlyUsed(am, potionId))
                {
                    AdvanceStep();
                    return GetNextAction(am, player, config);
                }

                if ((DateTime.Now - lastRequestTime).TotalSeconds > ACTION_CONFIRM_WINDOW)
                {
                    var requestSent = InventoryManager.UseSpecificPotion(am, potionId);

                    if (requestSent)
                    {
                        Plugin.Instance.SendLog($"[OPENER] Usando Poción ID {potionId}...");
                        lastRequestTime = DateTime.Now;
                        return (0, null);
                    }
                    else
                    {
                        Plugin.Instance.SendLog("[OPENER] Fallo al usar poción (¿Sin stock?). Saltando paso.");
                        AdvanceStep();
                        return GetNextAction(am, player, config);
                    }
                }

                return (0, null);
            }

            // 2. VERIFICACIÓN DE ÉXITO (Usando el actionId dinámico)
            if (IsActionRecentlyUsed(am, actionId) || IsBuffApplied(player, actionId))
            {
                AdvanceStep();
                return GetNextAction(am, player, config);
            }

            // 3. FAIL-SAFE
            if ((DateTime.Now - stepStartTime).TotalSeconds > TIMEOUT_SECONDS)
            {
                Plugin.Instance.SendLog($"[OPENER] ABORTADO - Timeout en paso {CurrentStepIndex + 1} ({currentStep.Name})");
                Stop();
                return (0, null);
            }

            // 4. ANTI-DOUBLE CAST
            if (actionId == lastRequestedId && (DateTime.Now - lastRequestTime).TotalSeconds < ACTION_CONFIRM_WINDOW)
            {
                return (0, null);
            }

            lastRequestTime = DateTime.Now;
            lastRequestedId = actionId;

            var bind = MapKeyBind(keyName, config);
            return (actionId, bind);
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
            }
            if (expectedBuff == 0) return false;
            foreach (var status in player.StatusList)
            {
                if (status.StatusId == expectedBuff) return true;
            }
            return false;
        }

        private unsafe bool IsActionRecentlyUsed(ActionManager* am, uint actionId)
        {
            var elapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
            var totalRecast = am->GetRecastTime(ActionType.Action, actionId);
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
                var type = config.GetType();
                var field = type.GetField(keyName);
                if (field != null) return field.GetValue(config) as KeyBind;
                var prop = type.GetProperty(keyName);
                if (prop != null) return prop.GetValue(config) as KeyBind;
            }
            catch { }
            return null;
        }

        // Helper para chequear estatus (Procs)
        private bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return false;
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return true;
            return false;
        }
    }
}
