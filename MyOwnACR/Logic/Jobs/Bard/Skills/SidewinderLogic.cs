// Archivo: Logic/Jobs/Bard/Skills/SidewinderLogic.cs
// VERSIÓN: V2.1 - DRIFT GUARD
// DESCRIPCIÓN: Gestión de Sidewinder. 
//              Prioriza el uso dentro de Raging Strikes y corrige drifts leves.

using MyOwnACR.GameData.Jobs.Bard;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class SidewinderLogic
    {
        // Si faltan menos de 10 segundos para Raging Strikes, aguantamos Sidewinder.
        // Aumentamos el umbral a 10s porque Sidewinder es débil fuera de buffs 
        // y vale la pena esperarlo para alinearlo de nuevo si se desfasó mucho.
        private const float HOLD_FOR_RS_THRESHOLD = 10.0f;

        public static OgcdPlan? GetPlan(BardContext ctx, int level)
        {
            // Validaciones básicas
            if (level < BRD_Levels.Sidewinder) return null;
            if (ctx.SidewinderCD > 0.6f) return null;

            // 1. ESCENARIO BURST (Raging Strikes Activo)
            // Si el buff está arriba, Sidewinder debe salir YA.
            // No importa si es el primer oGCD o el último del burst, 
            // lo importante es que salga MIENTRAS el buff dure.
            if (ctx.IsRagingStrikesActive)
            {
                // Usamos prioridad High. 
                // Nota: En el sistema de prioridades, los Buffs (Battle Voice) suelen ser Forced/High.
                // Así que naturalmente Sidewinder saldrá después de que los buffs estén puestos,
                // tal como en el opener.
                return new OgcdPlan(BRD_IDs.Sidewinder, WeavePriority.High, WeaveSlot.Any);
            }

            // 2. ESCENARIO PRE-BURST (Drift Correction)
            // Si RS no está activo, pero ya casi llega (faltan < 10s), NO DISPARES.
            // Es mejor perder 10s de CD en Sidewinder que usarlo sin buff.
            // Al esperar, lo realineamos forzosamente con el ciclo de 2 minutos.
            if (ctx.RagingStrikesCD < HOLD_FOR_RS_THRESHOLD)
            {
                return null; // Hold
            }

            // 3. VENTANA IMPAR (1 min, 3 min, 5 min...)
            // Si RS está lejos (ej. faltan 50s), estamos en la ventana impar.
            // Usarlo libremente a CD.
            return new OgcdPlan(BRD_IDs.Sidewinder, WeavePriority.High, WeaveSlot.Any);
        }
    }
}
