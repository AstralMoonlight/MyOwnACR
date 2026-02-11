using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
// Quitamos Control porque usaremos punteros directos para mayor compatibilidad
// using FFXIVClientStructs.FFXIV.Client.Game.Control; 
using MyOwnACR.GameData;
using MyOwnACR.Logic.Interfaces;
using MyOwnACR.Logic.Jobs.Bard;
using MyOwnACR.Logic.Jobs.Monk;
using MyOwnACR.Logic.Jobs.Samurai;
using MyOwnACR.Test;
using System.Collections.Generic;

namespace MyOwnACR.Logic.Core
{
    public class RotationManager
    {
        public static RotationManager Instance { get; } = new RotationManager();

        private readonly ActionScheduler _scheduler;
        private readonly EnemyLogger enemyLogger;
        private readonly Dictionary<uint, IJobLogic> jobLogics = new();
        public IJobLogic? CurrentLogic { get; private set; }
        private uint lastJobId = 0;

        private RotationManager()
        {
            _scheduler = new ActionScheduler(Plugin.PluginInterface, Plugin.Chat, Plugin.DataManager);
            enemyLogger = new EnemyLogger();

            RegisterLogic(MonkRotation.Instance);
            RegisterLogic(SamuraiRotation.Instance);
            RegisterLogic(BardRotation.Instance);
        }

        private void RegisterLogic(IJobLogic logic)
        {
            if (!jobLogics.ContainsKey(logic.JobId))
                jobLogics.Add(logic.JobId, logic);
        }

        public unsafe void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            IObjectTable objectTable,
            Configuration config,
            IDataManager dataManager,
            IClientState clientState)
        {
            if (player == null) return;

            // 0. RESET
            _scheduler.ResetCycle();

            // 1. Análisis
            enemyLogger.AnalyzeCombatArea(objectTable, dataManager, clientState);

            // 2. Cambio de Job
            uint currentJobId = player.ClassJob.RowId;
            if (currentJobId != lastJobId) UpdateCurrentLogic(currentJobId);

            // 3. Ejecutar Lógica
            if (CurrentLogic != null)
            {
                CurrentLogic.Execute(_scheduler, am, player, objectTable, config);
            }

            // =========================================================================
            // 4. GESTIÓN DE MOVIMIENTO (CORRECCIÓN DE OFFSETS)
            // =========================================================================
            if (_scheduler.StopRequested)
            {
                DisableMovementInput();
            }

            // 5. Scheduler
            _scheduler.Update(am, player);
        }

        /// <summary>
        /// Sobrescribe la memoria del Joystick Virtual saltando la VTable.
        /// </summary>
        private unsafe void DisableMovementInput()
        {
            try
            {
                // Obtenemos la instancia del InputManager
                var inputManager = FFXIVClientStructs.FFXIV.Client.Game.Control.InputManager.Instance();
                if (inputManager == null) return;

                // TRUCO DE MEMORIA AVANZADO:
                // Convertimos el puntero del struct a un puntero de floats.
                float* rawData = (float*)inputManager;

                // Offset 0 y 1 (Bytes 0-7) suelen ser la VTable (Punteros de función).
                // NO TOCAR ESOS.

                // Los datos de movimiento (Analog Sticks) suelen empezar en el float índice 2 o superior.
                // En versiones recientes, el Left Stick (Movimiento) suele estar alrededor de los offsets 0x20 - 0x30.

                // BARRIDO DE SEGURIDAD:
                // Ponemos a 0 varios floats que suelen corresponder a inputs de ejes.
                // Esto "apaga" el stick virtual.

                // Probamos un rango seguro donde suelen vivir X, Y, Turn, Camera.
                // Indices 2 a 16 (Bytes 8 a 64).
                for (int i = 2; i < 20; i++)
                {
                    // Solo sobrescribimos si el valor parece un input de eje (entre -1.0 y 1.0)
                    // Esto evita corromper punteros u otros datos grandes.
                    float val = rawData[i];
                    if (val >= -1.0f && val <= 1.0f && val != 0.0f)
                    {
                        rawData[i] = 0.0f;
                    }
                }

                // REFUERZO: AgentMap (Navegación)
                var agentMap = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
                if (agentMap != null)
                {
                    // Desactivar flag de "Jugador Moviéndose"
                    agentMap->IsPlayerMoving = false;

                    // Si estuvieras en Auto-Run, esto suele cortarlo
                    // (Depende de la versión, pero no hace daño intentarlo)
                    // agentMap->IsControlKeyPressed = false; 
                }
            }
            catch
            {
                // Silencioso
            }
        }

        private void UpdateCurrentLogic(uint newJobId)
        {
            lastJobId = newJobId;
            if (jobLogics.TryGetValue(newJobId, out var logic))
            {
                CurrentLogic = logic;
                _scheduler.ResetCycle();
            }
            else CurrentLogic = null;
        }

        // Métodos Proxy
        public void QueueManualAction(string actionName) => CurrentLogic?.QueueManualAction(actionName);
        public void QueueManualAction(uint actionId) => CurrentLogic?.QueueManualAction(actionId);
        public string GetQueuedAction() => CurrentLogic?.GetQueuedAction() ?? "";
        public uint GetLastProposedAction() => CurrentLogic?.LastProposedAction ?? 0;
        public void PrintDebugInfo(IChatGui chat)
        {
            if (CurrentLogic != null) CurrentLogic.PrintDebugInfo(chat);
            else chat.Print("[ACR] No hay lógica activa.");
        }
    }
}
