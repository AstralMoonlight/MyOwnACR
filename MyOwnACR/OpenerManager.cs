using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types; // NECESARIO PARA IPlayerCharacter
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Openers;
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

        // CAMBIO IMPORTANTE: AHORA RECIBIMOS 'player' PARA CHEQUEAR BUFFS
        public unsafe (uint actionId, KeyBind? bind) GetNextAction(ActionManager* am, IPlayerCharacter player, object config)
        {
            if (!IsRunning || activeProfile == null) return (0, null);

            if (CurrentStepIndex >= activeProfile.Steps.Count)
            {
                Plugin.Instance.SendLog("[OPENER] Finalizado con éxito.");
                Stop();
                return (0, null);
            }

            var currentStep = activeProfile.Steps[CurrentStepIndex];

            // 1. SALTAR POCIONES (FIX TEMPORAL)
            if (currentStep.Name == "Potion" || currentStep.Type == "Potion")
            {
                Plugin.Instance.SendLog($"[OPENER] Saltando paso {CurrentStepIndex + 1}: {currentStep.Name}");
                AdvanceStep();
                return GetNextAction(am, player, config);
            }

            // 2. VERIFICACIÓN DE ÉXITO (Doble Check: Cooldown O Buff)
            // Si el CD se activó O si tenemos el Buff correspondiente (para skills con cargas como PB)
            if (IsActionRecentlyUsed(am, currentStep.ActionId) || IsBuffApplied(player, currentStep.ActionId))
            {
                AdvanceStep();
                return GetNextAction(am, player, config);
            }

            // 3. FAIL-SAFE (TIMEOUT)
            if ((DateTime.Now - stepStartTime).TotalSeconds > TIMEOUT_SECONDS)
            {
                Plugin.Instance.SendLog($"[OPENER] ABORTADO - Timeout en paso {CurrentStepIndex + 1} ({currentStep.Name})");
                Stop();
                return (0, null);
            }

            // 4. RETORNAR ACCIÓN (CON ANTI-DOUBLE CAST)
            if (currentStep.ActionId == lastRequestedId && (DateTime.Now - lastRequestTime).TotalSeconds < ACTION_CONFIRM_WINDOW)
            {
                return (0, null);
            }

            lastRequestTime = DateTime.Now;
            lastRequestedId = currentStep.ActionId;

            KeyBind? bind = MapKeyBind(currentStep.KeyName, config);
            return (currentStep.ActionId, bind);
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
