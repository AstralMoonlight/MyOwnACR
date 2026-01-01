// Archivo: Logic/RotationManager.cs
// Descripción: Orquestador central que detecta el Job actual y delega la ejecución a la lógica específica.

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData;
using System.Collections.Generic;

namespace MyOwnACR.Logic
{
    public class RotationManager
    {
        // Singleton Instance
        public static RotationManager Instance { get; } = new RotationManager();

        // Almacén de todas las lógicas disponibles (Job ID -> Implementación)
        private readonly Dictionary<uint, IJobLogic> jobLogics = new();

        // Lógica actualmente activa (puede ser null si el Job no está soportado)
        public IJobLogic? CurrentLogic { get; private set; }

        private uint lastJobId = 0;

        private RotationManager()
        {
            // === REGISTRO DE CLASES ===
            // Aquí es donde añadiremos las futuras clases (DRG, VPR, etc.)
            RegisterLogic(MNK_Logic.Instance);

            // Ejemplo futuro:
            // RegisterLogic(DRG_Logic.Instance);
            // RegisterLogic(VPR_Logic.Instance);
        }

        private void RegisterLogic(IJobLogic logic)
        {
            if (!jobLogics.ContainsKey(logic.JobId))
            {
                jobLogics.Add(logic.JobId, logic);
            }
        }

        /// <summary>
        /// Método principal llamado por el Plugin en cada frame.
        /// </summary>
        public unsafe void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            IObjectTable objectTable,
            Configuration config)
        {
            if (player == null) return;

            // 1. Detección de Cambio de Job
            uint currentJobId = player.ClassJob.RowId;
            if (currentJobId != lastJobId)
            {
                UpdateCurrentLogic(currentJobId);
            }

            // 2. Ejecución Delegada
            // Si tenemos lógica para este job, la ejecutamos.
            if (CurrentLogic != null)
            {
                CurrentLogic.Execute(am, player, objectTable, config);
            }
        }

        /// <summary>
        /// Cambia la lógica activa cuando el usuario cambia de arma/clase.
        /// </summary>
        private void UpdateCurrentLogic(uint newJobId)
        {
            lastJobId = newJobId;

            if (jobLogics.TryGetValue(newJobId, out var logic))
            {
                CurrentLogic = logic;
                // Opcional: Podríamos loguear el cambio
                // Plugin.Instance.SendLog($"[System] Cambio de clase detectado: {JobDefinitions.GetAbbr(newJobId)}");
            }
            else
            {
                CurrentLogic = null; // Clase no soportada (ej. Fisher o clase base sin implementar)
            }
        }

        // =========================================================================
        // MÉTODOS PROXY (Pasan la llamada a la lógica activa)
        // =========================================================================

        public void QueueManualAction(string actionName)
        {
            if (CurrentLogic != null)
            {
                CurrentLogic.QueueManualAction(actionName);
            }
        }

        public string GetQueuedAction()
        {
            return CurrentLogic != null ? CurrentLogic.GetQueuedAction() : "";
        }

        public uint GetLastProposedAction()
        {
            return CurrentLogic != null ? CurrentLogic.LastProposedAction : 0;
        }

        public void PrintDebugInfo(IChatGui chat)
        {
            if (CurrentLogic != null)
            {
                CurrentLogic.PrintDebugInfo(chat);
            }
            else
            {
                chat.Print("[ACR] No hay lógica activa para el Job actual.");
            }
        }
    }
}
