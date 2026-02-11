using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.Logic.Common; // Donde vive ActionLibrary
using MyOwnACR.Models;       // Donde viven los DTOs de OpenerProfile/Step
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyOwnACR.Logic.Core
{
    /// <summary>
    /// Gestor PASIVO de Secuencias de Apertura (Openers).
    /// No ejecuta acciones ni maneja tiempos. Solo sirve el siguiente paso cuando se le solicita.
    /// El avance (Advance) debe ser llamado explícitamente por el RotationManager cuando confirma la ejecución.
    /// </summary>
    public class OpenerManager
    {
        public static OpenerManager Instance { get; } = new OpenerManager();
        private OpenerManager() { }

        // Estado Público
        public bool IsRunning { get; private set; } = false;
        public int CurrentStepIndex { get; private set; } = 0;
        public string CurrentOpenerName { get; private set; } = "Ninguno";

        // Almacenamiento Interno
        private List<OpenerProfile> _loadedOpeners = new();
        private OpenerProfile? _activeProfile = null;

        // [SOLUCIÓN CS1061] Propiedad que busca Plugin.cs para el Dashboard
        public List<string> AvailableOpeners => _loadedOpeners.Select(o => o.Name).OrderBy(n => n).ToList();

        /// <summary>
        /// Carga todos los archivos .json de la carpeta "Openers".
        /// </summary>
        public void LoadOpeners(string pluginConfigDir)
        {
            try
            {
                var openersDir = Path.Combine(pluginConfigDir, "Openers");
                if (!Directory.Exists(openersDir)) Directory.CreateDirectory(openersDir);

                _loadedOpeners.Clear();

                foreach (var file in Directory.GetFiles(openersDir, "*.json"))
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        var profile = JsonConvert.DeserializeObject<OpenerProfile>(content);

                        if (profile != null && profile.Steps != null && profile.Steps.Count > 0)
                        {
                            ResolveActionIds(profile);
                            _loadedOpeners.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error($"Error leyendo opener {Path.GetFileName(file)}: {ex.Message}");
                    }
                }

                Plugin.Log.Info($"[OpenerManager] Cargados {_loadedOpeners.Count} openers.");
            }
            catch (Exception ex) { Plugin.Log.Error(ex, "Error fatal cargando openers"); }
        }

        private void ResolveActionIds(OpenerProfile profile)
        {
            foreach (var step in profile.Steps)
            {
                // Las pociones se manejan por lógica especial (ID = 0 o flag 'Potion')
                if (step.Type == "Potion") continue;

                // Si viene sin ID numérico pero con nombre (clave), buscamos el ID real
                if (step.ActionId == 0 && !string.IsNullOrEmpty(step.KeyName))
                {
                    step.ActionId = ActionLibrary.GetIdByName(step.KeyName);

                    if (step.ActionId == 0)
                        Plugin.Log.Warning($"[Opener] No se encontró ID para la acción: {step.KeyName}");
                }
            }
        }

        public void SelectOpener(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "Ninguno")
            {
                _activeProfile = null;
                CurrentOpenerName = "Ninguno";
                IsRunning = false;
                return;
            }

            _activeProfile = _loadedOpeners.FirstOrDefault(o => o.Name == name);
            if (_activeProfile != null)
            {
                CurrentOpenerName = _activeProfile.Name;
            }
            else
            {
                Plugin.Log.Error($"[OpenerManager] No se encontró el perfil: {name}");
                CurrentOpenerName = "Ninguno";
            }

            // Al seleccionar, reseteamos (no arrancamos automáticamente)
            IsRunning = false;
            CurrentStepIndex = 0;
        }

        public void Start()
        {
            if (_activeProfile == null) return;

            IsRunning = true;
            CurrentStepIndex = 0;
            Plugin.Instance.SendLog($"[OPNR] INICIANDO SECUENCIA: {_activeProfile.Name}");
        }

        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                Plugin.Instance.SendLog($"[OPNR] Secuencia Finalizada.");
            }
            // No reseteamos CurrentStepIndex aquí para permitir diagnósticos post-mortem si se desea
        }

        // --- MÉTODOS DE CONSULTA (SIN LÓGICA DE TIEMPO) ---

        public OpenerStep? GetCurrentStep()
        {
            if (!IsRunning || _activeProfile == null) return null;

            // Si nos pasamos del final, terminamos
            if (CurrentStepIndex >= _activeProfile.Steps.Count)
            {
                Stop();
                return null;
            }

            return _activeProfile.Steps[CurrentStepIndex];
        }

        public OpenerStep? PeekNextStep(int offset = 1)
        {
            if (!IsRunning || _activeProfile == null) return null;

            int idx = CurrentStepIndex + offset;
            if (idx >= _activeProfile.Steps.Count) return null;

            return _activeProfile.Steps[idx];
        }

        // --- MÉTODO DE AVANCE (CONTROLADO EXTERNAMENTE) ---
        // Este método debe ser llamado por RotationManager cuando detecta que la acción se ejecutó con éxito.
        public void Advance()
        {
            if (!IsRunning || _activeProfile == null) return;

            // Logueamos el paso completado
            if (CurrentStepIndex < _activeProfile.Steps.Count)
            {
                var step = _activeProfile.Steps[CurrentStepIndex];
                // Plugin.Instance.SendLog($"[OPNR] OK: {step.Name ?? step.KeyName}"); // Opcional para no spammear
            }

            CurrentStepIndex++;

            // Verificamos si terminamos
            if (CurrentStepIndex >= _activeProfile.Steps.Count)
            {
                Plugin.Instance.SendLog("[OPNR] Opener COMPLETADO EXITOSAMENTE.");
                Stop();
            }
        }
    }
}
