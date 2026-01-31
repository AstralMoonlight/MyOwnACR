// Archivo: Logic/Core/RotationManager.cs
// Descripción: Orquestador central actualizado.
// CORRECCIÓN: Pasa las dependencias al ActionScheduler y asegura la llamada correcta a Update.

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData;
using MyOwnACR.Logic.Interfaces;
using MyOwnACR.Logic.Jobs.Bard;
using MyOwnACR.Logic.Jobs.Monk;
using MyOwnACR.Logic.Jobs.Samurai;
using MyOwnACR.Test; // Para EnemyLogger
using System.Collections.Generic;

namespace MyOwnACR.Logic.Core
{
    public class RotationManager
    {
        // =========================================================================
        // SINGLETON & ESTADO
        // =========================================================================
        public static RotationManager Instance { get; } = new RotationManager();

        // Motor de tiempo (NUEVO)
        private readonly ActionScheduler _scheduler;

        // Logger de enemigos (Preservado de tu versión)
        private readonly EnemyLogger enemyLogger;

        // Almacén de lógicas
        private readonly Dictionary<uint, IJobLogic> jobLogics = new();

        // Lógica activa
        public IJobLogic? CurrentLogic { get; private set; }
        private uint lastJobId = 0;

        private RotationManager()
        {
            // 1. Inicializar Scheduler con dependencias del Plugin
            // SOLUCIÓN ERROR CS7036: Pasamos las interfaces necesarias.
            // Asumimos que Plugin.PluginInterface, Plugin.Chat y Plugin.DataManager son estáticos accesibles.
            _scheduler = new ActionScheduler(Plugin.PluginInterface, Plugin.Chat, Plugin.DataManager);

            enemyLogger = new EnemyLogger();

            // 2. Registrar Clases
            RegisterLogic(MonkRotation.Instance);
            RegisterLogic(SamuraiRotation.Instance);
            RegisterLogic(BardRotation.Instance);
        }

        private void RegisterLogic(IJobLogic logic)
        {
            if (!jobLogics.ContainsKey(logic.JobId))
            {
                jobLogics.Add(logic.JobId, logic);
            }
        }

        // =========================================================================
        // EXECUTE (Loop Principal llamado por Plugin.cs)
        // =========================================================================
        public unsafe void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            IObjectTable objectTable,
            Configuration config,
            IDataManager dataManager,
            IClientState clientState)
        {
            if (player == null) return;

            // 1. Enemy Logger (Tu lógica existente)
            enemyLogger.AnalyzeCombatArea(objectTable, dataManager, clientState);

            // 2. Detección de Cambio de Job
            uint currentJobId = player.ClassJob.RowId;
            if (currentJobId != lastJobId)
            {
                UpdateCurrentLogic(currentJobId);
            }

            // 3. Ejecución de Lógica de Job (DECISIÓN)
            if (CurrentLogic != null)
            {
                // Pasamos '_scheduler' como primer argumento para cumplir IJobLogic.
                CurrentLogic.Execute(_scheduler, am, player, objectTable, config);
            }

            // 4. Ejecución del Scheduler (ACCIÓN)
            // SOLUCIÓN ERROR CS1061: Asegúrate de que ActionScheduler.cs tenga el método "public unsafe void Update(...)"
            _scheduler.Update(am, player);
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
                // Al cambiar de job, limpiamos la cola del scheduler para evitar que un skill de Bardo se intente usar en Monje
                // (Si tu ActionScheduler tiene método Reset/Clear, úsalo aquí)
                // _scheduler.ClearQueue(); 
            }
            else
            {
                CurrentLogic = null;
            }
        }

        // =========================================================================
        // MÉTODOS PROXY (Pasan la llamada a la lógica activa)
        // =========================================================================

        public void QueueManualAction(string actionName)
        {
            CurrentLogic?.QueueManualAction(actionName);
        }

        // Nuevo método overload para IDs (usado por Bard)
        public void QueueManualAction(uint actionId)
        {
            CurrentLogic?.QueueManualAction(actionId);
        }

        public string GetQueuedAction()
        {
            return CurrentLogic?.GetQueuedAction() ?? "";
        }

        public uint GetLastProposedAction()
        {
            return CurrentLogic?.LastProposedAction ?? 0;
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
