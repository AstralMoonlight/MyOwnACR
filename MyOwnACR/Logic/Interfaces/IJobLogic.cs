// Archivo: Logic/Interfaces/IJobLogic.cs
// Descripción: Contrato actualizado para integrar ActionScheduler.

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace MyOwnACR.Logic.Interfaces
{
    public unsafe interface IJobLogic
    {
        /// <summary>
        /// ID del Job al que pertenece esta lógica (ej. 23 para BRD).
        /// </summary>
        uint JobId { get; }

        /// <summary>
        /// Última acción propuesta por la lógica (para mostrar en el Dashboard/Overlay).
        /// </summary>
        uint LastProposedAction { get; }

        /// <summary>
        /// Método principal de ejecución (llamado en cada frame).
        /// </summary>
        /// <param name="scheduler">El motor de tiempo y weaving (NUEVO).</param>
        /// <param name="am">Puntero al ActionManager del juego.</param>
        /// <param name="player">El jugador local.</param>
        /// <param name="objectTable">Tabla de objetos para buscar enemigos.</param>
        /// <param name="config">Configuración global.</param>
        void Execute(
            ActionScheduler scheduler,
            ActionManager* am,
            IPlayerCharacter player,
            IObjectTable objectTable,
            Configuration config);

        /// <summary>
        /// Recibe una orden manual por ID (Más rápido y preciso).
        /// </summary>
        void QueueManualAction(uint actionId);

        /// <summary>
        /// Recibe una orden manual por nombre (Legacy/UI).
        /// </summary>
        void QueueManualAction(string actionName);

        /// <summary>
        /// Obtiene el nombre/estado de la acción manual en cola.
        /// </summary>
        string GetQueuedAction();

        /// <summary>
        /// Imprime información de depuración en el chat.
        /// </summary>
        void PrintDebugInfo(IChatGui chat);
    }
}
