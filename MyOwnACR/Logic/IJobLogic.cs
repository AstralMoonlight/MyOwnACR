// Archivo: Logic/IJobLogic.cs
// Descripción: Contrato que todas las lógicas de Job deben cumplir.

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MyOwnACR.Logic
{
    public interface IJobLogic
    {
        /// <summary>
        /// ID del Job al que pertenece esta lógica (ej. 20 para MNK).
        /// </summary>
        uint JobId { get; }

        /// <summary>
        /// Última acción propuesta por la lógica (para mostrar en el Dashboard).
        /// </summary>
        uint LastProposedAction { get; }

        /// <summary>
        /// Método principal de ejecución (llamado en cada frame).
        /// </summary>
        /// <param name="am">Puntero al ActionManager del juego.</param>
        /// <param name="player">El jugador local.</param>
        /// <param name="objectTable">Tabla de objetos para buscar enemigos.</param>
        /// <param name="config">Configuración global (incluye settings del job y operaciones).</param>
        unsafe void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            IObjectTable objectTable,
            Configuration config);

        /// <summary>
        /// Recibe una orden manual desde el Dashboard.
        /// </summary>
        void QueueManualAction(string actionName);

        /// <summary>
        /// Obtiene el nombre de la acción manual en cola (para el Dashboard).
        /// </summary>
        string GetQueuedAction();

        /// <summary>
        /// Imprime información de depuración en el chat.
        /// </summary>
        void PrintDebugInfo(IChatGui chat);
    }
}
