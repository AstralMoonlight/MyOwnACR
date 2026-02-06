// Archivo: Logic/Jobs/Bard/Skills/ResonantLogic.cs
// DESCRIPCIÓN: Gestión del GCD Resonant Arrow (Nvl 96+).
// Se activa tras usar Barrage.

using MyOwnACR.Logic.Jobs.Bard;
using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.GameData.Jobs.Bard;
namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class ResonantLogic
    {
        /// <summary>
        /// Verifica si tenemos el buff para lanzar Resonant Arrow.
        /// </summary>
        public static uint GetGcd(IPlayerCharacter player)
        {
            if (player.Level < BRD_Levels.ResonantArrow) return 0;

            // Verificamos si tenemos el buff "Resonant Arrow Ready"
            // Nota: Asegúrate de tener este ID en tu archivo de constantes (aprox ID 3818 en Dawntrail)
            if (Helpers.HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
            {
                // Es un GCD de alta prioridad.
                return BRD_IDs.ResonantArrow;
            }

            return 0;
        }
    }
}
