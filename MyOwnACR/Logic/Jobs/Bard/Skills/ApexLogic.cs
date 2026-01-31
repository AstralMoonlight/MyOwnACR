// Archivo: Logic/Jobs/Bard/Skills/ApexLogic.cs
// Descripción: Gestión de Apex Arrow y Blast Arrow (Soul Voice Gauge).
// Prioridad: Blast Arrow > Apex Arrow (High Gauge) > Burst Shot.

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core; // Solo por consistencia, no usa OgcdPlan
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class ApexLogic
    {
        /// <summary>
        /// Determina si se debe usar un GCD de la barra de Soul Voice.
        /// </summary>
        /// <param name="ctx">Contexto (SoulVoice, Buffs).</param>
        /// <param name="level">Nivel del jugador.</param>
        /// <param name="hasBlastProc">Si tenemos el proc 'Blast Arrow Ready'.</param>
        /// <returns>ID de la acción GCD o 0.</returns>
        public static uint GetAction(BardContext ctx, int level, bool hasBlastProc)
        {
            // 1. BLAST ARROW (Prioridad Absoluta)
            // Si tenemos el proc (dura 10s), hay que usarlo antes de que expire.
            // Proviene de haber usado Apex Arrow con >= 80 de barra.
            if (level >= BRD_Levels.BlastArrow && hasBlastProc)
            {
                return BRD_IDs.BlastArrow;
            }

            // 2. APEX ARROW
            if (level >= BRD_Levels.ApexArrow)
            {
                // Caso A: Barra llena (100). Usar para no desperdiciar generación.
                if (ctx.SoulVoice == 100)
                {
                    return BRD_IDs.ApexArrow;
                }

                // Caso B: Burst (Raging Strikes).
                // Si tenemos >= 80 de barra y estamos bajo buffs, quemamos la barra
                // para potenciar el daño y generar un Blast Arrow potenciado.
                if (ctx.SoulVoice >= 80 && ctx.IsRagingStrikesActive)
                {
                    return BRD_IDs.ApexArrow;
                }

                // Caso C: Minuto impar (Mage's Ballad)
                // A veces conviene gastar a 80+ para limpiar la barra antes del burst de 2 min.
                // (Opcional, pero recomendado).
                if (ctx.SoulVoice >= 80 && ctx.CurrentSong == Song.Mage)
                {
                    return BRD_IDs.ApexArrow;
                }
            }

            return 0; // No usar nada de Soul Voice
        }
    }
}
