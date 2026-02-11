using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models; // Asumo que aquí está OgcdPlan

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class IkishotenLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, int level)
        {
            // Requisito: Nivel, CD listo
            if (level < SAM_Levels.Ikishoten || ctx.IkishotenCD > 0.6f) return null;

            // Lógica: No usar si vamos a desperdiciar Kenki (Max 100, Ikishoten da 50)
            if (ctx.Kenki > 50) return null;

            // Prioridad ALTA: Es vital para habilitar Ogi Namikiri y generar recursos
            return new OgcdPlan(SAM_IDs.Ikishoten, WeavePriority.High);
        }
    }
}
