// Archivo: Logic/Core/OpenerManager.cs
// LÓGICA: Gestor PASIVO. No gestiona tiempos.
// Solo mantiene el índice y entrega el siguiente paso cuando se le pide.

using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.Logic.Common;
using MyOwnACR.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyOwnACR.Logic.Core
{
    public class OpenerManager
    {
        public static OpenerManager Instance { get; } = new OpenerManager();
        private OpenerManager() { }

        public bool IsRunning { get; private set; } = false;
        public int CurrentStepIndex { get; private set; } = 0;
        public string CurrentOpenerName { get; private set; } = "Ninguno";

        private List<OpenerProfile> availableOpeners = new();
        private OpenerProfile? activeProfile = null;

        public void LoadOpeners(string pluginConfigDir)
        {
            try
            {
                var openersDir = Path.Combine(pluginConfigDir, "Openers");
                if (!Directory.Exists(openersDir)) Directory.CreateDirectory(openersDir);
                availableOpeners.Clear();
                foreach (var file in Directory.GetFiles(openersDir, "*.json"))
                {
                    var profile = JsonConvert.DeserializeObject<OpenerProfile>(File.ReadAllText(file));
                    if (profile != null && profile.Steps.Count > 0)
                    {
                        ResolveActionIds(profile);
                        availableOpeners.Add(profile);
                    }
                }
            }
            catch (Exception ex) { Plugin.Log.Error(ex, "Error cargando openers"); }
        }

        private void ResolveActionIds(OpenerProfile profile)
        {
            foreach (var step in profile.Steps)
            {
                if (step.Type == "Potion") continue;
                if (step.ActionId == 0 && !string.IsNullOrEmpty(step.KeyName))
                    step.ActionId = ActionLibrary.GetIdByName(step.KeyName);
            }
        }

        public List<string> GetOpenerNames() => availableOpeners.Select(o => o.Name).ToList();

        public void SelectOpener(string name)
        {
            activeProfile = availableOpeners.FirstOrDefault(o => o.Name == name);
            CurrentOpenerName = activeProfile?.Name ?? "Ninguno";
            IsRunning = false;
        }

        public void Start()
        {
            if (activeProfile == null) return;
            IsRunning = true;
            CurrentStepIndex = 0;
            Plugin.Instance.SendLog($"[OPNR] INICIANDO: {activeProfile.Name}");
        }

        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                Plugin.Instance.SendLog($"[OPNR] Finalizado.");
            }
        }

        // --- MÉTODOS DE CONSULTA (SIN LÓGICA DE TIEMPO) ---

        public OpenerStep? GetCurrentStep()
        {
            if (!IsRunning || activeProfile == null) return null;
            if (CurrentStepIndex >= activeProfile.Steps.Count)
            {
                Stop();
                return null;
            }
            return activeProfile.Steps[CurrentStepIndex];
        }

        public OpenerStep? PeekNextStep(int offset = 1)
        {
            if (!IsRunning || activeProfile == null) return null;
            int idx = CurrentStepIndex + offset;
            if (idx >= activeProfile.Steps.Count) return null;
            return activeProfile.Steps[idx];
        }

        // --- MÉTODO DE AVANCE (CONTROLADO EXTERNAMENTE) ---
        public void Advance()
        {
            if (!IsRunning || activeProfile == null) return;

            var step = activeProfile.Steps[CurrentStepIndex];
            Plugin.Instance.SendLog($"[OPNR] OK: {step.Name}"); // Log simple

            CurrentStepIndex++;

            if (CurrentStepIndex >= activeProfile.Steps.Count)
            {
                Plugin.Instance.SendLog("[OPNR] Opener COMPLETADO.");
                Stop();
            }
        }
    }
}
