// Archivo: Logic/Jobs/Bard/Skills/SidewinderLogic.cs
// Descripción: Gestión de Sidewinder.
// Prioridad: Alta (High), se usa a CD para alinearse con ventanas de 60s/120s.

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class SidewinderLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx, int level)
        {
            // Nivel mínimo (60)
            if (level < BRD_Levels.Sidewinder) return null;

            // Chequeo de Cooldown (Buffer 0.6s)
            if (ctx.SidewinderCD > 0.6f) return null;

            // Prioridad High: Queremos que salga antes que Empyreal o Bloodletter
            // para que no se atrase (drift) y pierda alineación con los buffs de 2 minutos.
            return new OgcdPlan(BRD_IDs.Sidewinder, WeavePriority.High, WeaveSlot.Any);
        }
    }
}
