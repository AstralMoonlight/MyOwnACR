using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using MyOwnACR.Logic.Openers;
using MyOwnACR.Logic.Openers.Jobs;

namespace MyOwnACR.Logic.Core
{
    public class OpenerManager
    {
        public static OpenerManager Instance { get; } = new OpenerManager();
        private OpenerManager() { }

        public bool IsRunning => _isRunning;
        public int CurrentStepIndex => _stepIndex;
        public List<string> AvailableOpeners { get; private set; } = new();

        private Dictionary<string, OpenerProfile> _profiles = new();
        private OpenerProfile? _currentProfile;
        private int _stepIndex = 0;
        private bool _isRunning = false;
        private DateTime _startTime;

        public void LoadOpeners(string unusedPath = "")
        {
            _profiles.Clear();
            AvailableOpeners.Clear();
            RegisterOpener(SAM_Openers.Standard_Lv100);
            Plugin.Log.Info($"[OpenerManager] Cargados {_profiles.Count} openers.");
        }

        private void RegisterOpener(OpenerProfile profile)
        {
            if (profile == null) return;
            _profiles[profile.Name] = profile;
            AvailableOpeners.Add(profile.Name);
        }

        public void SelectOpener(string name)
        {
            if (_profiles.ContainsKey(name))
            {
                _currentProfile = _profiles[name];
                Plugin.Instance.SendLog($"[Opener] Seleccionado: {name}");
            }
            else
            {
                _currentProfile = null;
            }
            Reset();
        }

        public void Reset()
        {
            if (_isRunning) Plugin.Instance.SendLog("[Opener] Reseteado/Detenido.");
            _stepIndex = 0;
            _isRunning = false;
        }

        // [MODIFICADO] Ahora acepta el tiempo restante de la cuenta atrás
        public uint GetNextAction(IGameObject? target, float countdownRemaining = 0)
        {
            if (_currentProfile == null) return 0;

            // Si terminamos
            if (_stepIndex >= _currentProfile.Steps.Count)
            {
                if (_isRunning)
                {
                    Plugin.Instance.SendLog("[Opener] FINALIZADO EXITOSAMENTE.");
                    _isRunning = false;
                }
                return 0;
            }

            // Si empezamos ahora
            if (!_isRunning)
            {
                _isRunning = true;
                _startTime = DateTime.Now;
                Plugin.Instance.SendLog($"[Opener] INICIANDO SECUENCIA: {_currentProfile.Name}");
            }

            var currentStep = _currentProfile.Steps[_stepIndex];

            // =================================================================
            // LÓGICA DE ESPERA (PRE-PULL)
            // =================================================================
            // Si hay una cuenta atrás activa y el paso tiene un tiempo definido (ej: 14s)
            if (countdownRemaining > 0 && currentStep.PrepullSeconds > 0)
            {
                // Si faltan 15s y el paso es a los 14s...
                // Esperamos hasta que el contador baje a 14.5s (damos 0.5s de margen)
                if (countdownRemaining > (currentStep.PrepullSeconds + 0.5f))
                {
                    // Retornamos 0 (Esperar), pero mantenemos _isRunning en TRUE
                    return 0;
                }
                // Si llegamos aquí, es hora de ejecutar
            }

            // Si hay cuenta atrás pero el paso NO es prepull (es de combate), 
            // esperamos a que el contador llegue casi a 0.
            if (countdownRemaining > 0.5f && currentStep.PrepullSeconds <= 0)
            {
                return 0;
            }

            // Lógica Poción
            if (currentStep.Type == OpenerStepType.Potion)
            {
                var selectedPotionId = Plugin.Instance.Config.Operation.SelectedPotionId;
                if (selectedPotionId == 0)
                {
                    Plugin.Instance.SendLog($"[Opener] Saltando Poción (No configurada).");
                    AdvanceStep();
                    return GetNextAction(target, countdownRemaining);
                }
                return selectedPotionId;
            }

            return currentStep.ActionId;
        }

        public void AdvanceStep()
        {
            if (_currentProfile != null && _stepIndex < _currentProfile.Steps.Count)
            {
                var justFinished = _currentProfile.Steps[_stepIndex].Name;
                _stepIndex++;
                Plugin.Instance.SendLog($"[Opener] COMPLETADO: {justFinished} -> Siguiente: {_stepIndex + 1}");

                if (_stepIndex >= _currentProfile.Steps.Count)
                {
                    _isRunning = false;
                    Plugin.Instance.SendLog("[Opener] Secuencia Terminada.");
                }
            }
        }
    }
}
