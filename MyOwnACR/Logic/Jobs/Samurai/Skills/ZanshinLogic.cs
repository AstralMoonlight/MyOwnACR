using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class ZanshinLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, int level)
        {
            // Requisito: Nivel 96 (Dawntrail)
            if (level < SAM_Levels.Zanshin) return null;

            // Condición 1: Tener el Buff "Zanshin Ready" (Status 3855)
            // Condición 2: Tener 50 de Kenki (Coste de la habilidad)
            if (ctx.HasZanshinReady && ctx.Kenki >= 50)
            {
                // Prioridad ALTA:
                // Es parte del burst window y hace mucho daño. Queremos usarlo
                // antes de que el buff expire (30s) y antes de gastar Kenki en Shinten.
                return new OgcdPlan(SAM_IDs.Zanshin, WeavePriority.High);
            }

            return null;
        }
    }
}
