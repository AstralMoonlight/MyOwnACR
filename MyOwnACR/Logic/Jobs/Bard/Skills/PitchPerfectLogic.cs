using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class PitchPerfectLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx)
        {
            // Solo activo en Minuet
            if (ctx.CurrentSong != Song.Wanderer) return null;

            // Si no hay stacks, no podemos disparar nada
            if (ctx.Repertoire == 0) return null;

            // 1. PRIORIDAD MÁXIMA: 3 Stacks
            // Evitar Overcap (perder el siguiente proc).
            if (ctx.Repertoire == 3)
            {
                return new OgcdPlan(BRD_IDs.PitchPerfect, WeavePriority.High, WeaveSlot.Any);
            }

            // 2. DUMP FINAL (La canción se muere)
            // Quedan menos de 3 segundos (menos de 1 GCD y medio aprox).
            // Si tenemos 1 o 2 stacks, hay que gastarlos YA o se perderán al cambiar de canción.
            // CAMBIO: Prioridad elevada a 'High' para asegurar que salga antes que cualquier filler.
            if (ctx.SongTimerMS < 3000)
            {
                return new OgcdPlan(BRD_IDs.PitchPerfect, WeavePriority.High, WeaveSlot.Any);
            }

            // 3. HOLD (Esperar a 3 stacks)
            // Si tenemos 1 o 2 stacks y todavía queda tiempo de canción, esperamos.
            return null;
        }
    }
}
