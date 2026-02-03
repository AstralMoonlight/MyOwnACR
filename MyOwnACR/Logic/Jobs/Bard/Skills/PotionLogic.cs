// Archivo: Logic/Jobs/Bard/Skills/PotionLogic.cs
// VERSIÓN: V24.0 - MONK STYLE
// DESCRIPCIÓN: Lógica de poción corregida usando métodos existentes.

using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.Logic.Core; // Aquí está InventoryManager
using MyOwnACR.GameData;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static unsafe class PotionLogic
    {
        // Recibimos primitivos para evitar errores de referencia de clases de Config
        public static OgcdPlan? GetPlan(BardContext ctx, bool usePotion, uint potionId, bool saveCD, ActionManager* am)
        {
            // 1. Validaciones Básicas
            if (saveCD) return null;
            if (!usePotion) return null;
            if (potionId == 0) return null;

            // 2. Validación de Inventario (Estilo Monk)
            // Usamos IsPotionReady que valida CD, Animación y Cantidad en inventario.
            if (!MyOwnACR.Logic.Core.InventoryManager.IsPotionReady(am, potionId)) return null;

            // -----------------------------------------------------------------
            // ESTRATEGIA DE USO
            // -----------------------------------------------------------------
            float rsCD = ctx.RagingStrikesCD;

            // CASO A: Pre-Burst (Anticipación)
            // Raging Strikes viene en camino (1.5s a 4.5s).
            if (rsCD > 1.5f && rsCD < 4.5f)
            {
                return new OgcdPlan(potionId, WeavePriority.High, WeaveSlot.Any);
            }

            // CASO B: Recuperación (Dentro del Burst)
            if (ctx.IsRagingStrikesActive)
            {
                // FAILSAFE (Anti-Atasco):
                // Si ya pasaron 3 segundos de burst (quedan < 17s), ABORTAMOS.
                // Esto es crucial para que Barrage y Sidewinder no se queden esperando la poción eternamente.
                if (ctx.RagingStrikesTimeLeft < 17.0f) return null;

                // Si estamos al inicio del burst (BV y RF activos), insistimos con la poción.
                if (ctx.IsBattleVoiceActive && ctx.IsRadiantFinaleActive)
                {
                    return new OgcdPlan(potionId, WeavePriority.High, WeaveSlot.Any);
                }
            }

            return null;
        }
    }
}
