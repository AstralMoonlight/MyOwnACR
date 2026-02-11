using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class ShohaLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, int level)
        {
            // Requisito: Nivel 80+
            if (level < SAM_Levels.Shoha) return null;

            // Lógica: Solo usar si tenemos 3 Stacks de Meditación
            if (ctx.MeditationStacks == 3)
            {
                // Prioridad ALTA: Para no perder el siguiente proc
                return new OgcdPlan(SAM_IDs.Shoha, WeavePriority.High);
            }

            return null;
        }
    }
}
