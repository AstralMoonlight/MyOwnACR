using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models;
using MyOwnInventoryManager = MyOwnACR.Logic.Core.InventoryManager;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public unsafe static class PotionLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, ActionManager* am, uint potionId)
        {
            // 1. VALIDACIONES BÁSICAS
            if (potionId == 0) return null; // No hay poción configurada

            // Usamos nuestra clase custom (gracias al alias de arriba)
            if (!MyOwnInventoryManager.IsPotionReady(am, potionId)) return null;

            // 2. LÓGICA DE ALINEACIÓN (IKISHOTEN)
            float timeToIkishoten = ctx.IkishotenCD;

            // CONDICIÓN A: BURST INMINENTE (< 4.5s)
            bool burstComingSoon = timeToIkishoten < 4.5f;

            // CONDICIÓN B: PROTECCIÓN POST-BURST (> 110s)
            bool justUsedBurst = timeToIkishoten > 110.0f;

            // CONDICIÓN C: OPENER (Combate < 15s y con Nieve)
            // Queremos: Gyofu -> Yukikaze -> POCIÓN -> Meikyo/Ikishoten.
            bool isOpenerCondition = ctx.CombatTime < 15.0f && ctx.HasSetsu;

            // 3. DECISIÓN FINAL
            if ((burstComingSoon || isOpenerCondition) && !justUsedBurst)
            {
                // Devolvemos el Plan con Prioridad ALTA.
                return new OgcdPlan(potionId, WeavePriority.High);
            }

            return null;
        }
    }
}
