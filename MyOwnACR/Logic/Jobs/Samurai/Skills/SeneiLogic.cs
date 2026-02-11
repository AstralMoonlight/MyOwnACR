using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class SeneiLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, int level)
        {
            // Requisito: Nivel 72, CD listo
            if (level < SAM_Levels.HissatsuSenei || ctx.SeneiCD > 0.6f) return null;

            // Lógica: Coste 25 Kenki
            if (ctx.Kenki >= 25)
            {
                // Prioridad ALTA: Es un CD de 2 minutos, debe alinearse con buffs
                return new OgcdPlan(SAM_IDs.HissatsuSenei, WeavePriority.High);
            }

            return null;
        }
    }
}
