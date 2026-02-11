using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.Logic.Core; // Aquí vive InventoryManager original
using MyOwnACR.Models;
// Alias para evitar conflicto de nombres con el InventoryManager del juego
using MyOwnInventoryManager = MyOwnACR.Logic.Core.InventoryManager;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public unsafe static class PotionLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx, ActionManager* am, uint potionId)
        {
            // 1. VALIDACIONES BÁSICAS
            // Si no hay ID configurado o el usuario desactivó pociones, retornamos null.
            if (potionId == 0) return null;

            // Verificamos si la poción está lista (Cooldown y Stock) usando tu manager.
            if (!MyOwnInventoryManager.IsPotionReady(am, potionId)) return null;

            // 2. LÓGICA DE ALINEACIÓN (RAGING STRIKES)
            // Raging Strikes es el buff de daño de 2 min (120s CD).
            // La poción (30s) debe cubrir los 20s de Raging Strikes.

            float rsCD = ctx.RagingStrikesCD;

            // CONDICIÓN A: BURST INMINENTE (Ventana de 2 min)
            // Si Raging Strikes está listo (0) o vuelve en menos de 4.5 segundos.
            // Esto asegura que usemos la poción 1 o 2 GCDs antes de pulsar RS.
            bool burstComingSoon = rsCD < 4.5f;

            // CONDICIÓN B: PROTECCIÓN POST-BURST
            // Si RS tiene un CD > 110s, significa que ACABAMOS de usarlo.
            // Es demasiado tarde para la poción. Abortar.
            bool justUsedBurst = rsCD > 110.0f;

            // CONDICIÓN C: OPENER (Inicio del combate)
            // En el opener del Bardo, generalmente hacemos:
            // Stormbite -> Caustic Bite -> (Poción) -> Raging Strikes.
            // Así que si llevamos poco tiempo en combate (<15s) y RS está listo, ¡Fuego!
            bool isOpenerCondition = ctx.CombatTime < 15.0f && rsCD < 1.0f;

            // 3. DECISIÓN FINAL
            // Si viene el burst (o estamos empezando) Y no lo hemos gastado ya...
            if ((burstComingSoon || isOpenerCondition) && !justUsedBurst)
            {
                // Prioridad HIGH para forzar el bloqueo de weaving en BardRotation
                return new OgcdPlan(potionId, WeavePriority.High);
            }

            return null;
        }
    }
}
