// Archivo: Logic/Jobs/Bard/Skills/DotLogic.cs
// VERSIÓN: V3.0 - PROC PROTECTION & SNAPSHOT
// DESCRIPCIÓN: Prioriza mantener DoTs pero respeta los procs de Refulgent Arrow.

using System;
using MyOwnACR.GameData;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class DotLogic
    {
        // Umbrales de Tiempo
        private const float REFRESH_THRESHOLD = 5.0f; // Renovar si falta < 5s (Estándar)
        private const float CRITICAL_THRESHOLD = 2.5f; // Renovar SIEMPRE si falta < 2.5s (Pánico)

        public static uint GetAction(
            bool hasStorm, bool hasCaustic,
            float stormTime, float causticTime,
            float ragingStrikesTimeLeft,
            int level,
            bool hasRefulgentProc) // NUEVO PARÁMETRO
        {
            uint stormAction = (level >= BRD_Levels.Stormbite) ? BRD_IDs.Stormbite : BRD_IDs.Windbite;
            uint causticAction = (level >= BRD_Levels.CausticBite) ? BRD_IDs.CausticBite : BRD_IDs.VenomousBite;

            // 1. APLICACIÓN INICIAL (Si no existen)
            if (!hasStorm && level >= BRD_Levels.Windbite) return stormAction;
            if (!hasCaustic && level >= BRD_Levels.VenomousBite) return causticAction;

            // Pre-Iron Jaws (Nivel < 56)
            if (level < BRD_Levels.IronJaws)
            {
                if (stormTime < 6.0f) return stormAction;
                if (causticTime < 6.0f) return causticAction;
                return 0;
            }

            // 2. LÓGICA IRON JAWS (Nivel 56+)
            float minTimer = Math.Min(stormTime, causticTime);

            // A. MODO PÁNICO (Prioridad Absoluta)
            // Si los DoTs se van a caer en el próximo GCD, renovar SÍ O SÍ.
            if (minTimer < CRITICAL_THRESHOLD)
            {
                return BRD_IDs.IronJaws;
            }

            // B. PROTECCIÓN DE PROCS (Hawk's Eye / Straight Shot Ready)
            // Si tenemos el proc listo, y los DoTs están "seguros" (> 2.5s),
            // NO renovamos todavía. Dejamos que FillerLogic use Refulgent Arrow.
            if (hasRefulgentProc)
            {
                return 0;
            }

            // C. SNAPSHOTTING DE BUFFS (Raging Strikes)
            // Si RS está activo y le queda poco (< 3s), renovamos para guardar el buff.
            // Solo si los DoTs no están recién puestos (< 40s).
            if (ragingStrikesTimeLeft > 0 && ragingStrikesTimeLeft < 3.0f)
            {
                if (minTimer < 40.0f)
                {
                    return BRD_IDs.IronJaws;
                }
            }

            // D. RENOVACIÓN ESTÁNDAR
            // Si no hay proc y queda poco tiempo (< 3s), renovar.
            if (minTimer < REFRESH_THRESHOLD)
            {
                return BRD_IDs.IronJaws;
            }

            return 0;
        }
    }
}
