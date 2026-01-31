// Archivo: Logic/Jobs/Bard/Skills/BuffLogic.cs
// Descripción: Gestión integral de Buffs (Cascada oGCD) y Remates (Encore GCD).

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class BuffLogic
    {
        // =========================================================================
        // LÓGICA GCD (Radiant Encore)
        // =========================================================================
        /// <summary>
        /// Decide si es el momento óptimo para usar Radiant Encore.
        /// </summary>
        public static uint GetEncoreGcd(BardContext ctx, int level)
        {
            // Validaciones básicas
            if (level < BRD_Levels.RadiantEncore) return 0;
            if (ctx.RadiantEncoreTimeLeft <= 0) return 0; // No tenemos el proc

            // 1. BURST MODE:
            // Si Raging Strikes está activo, queremos meter el Encore dentro de la ventana de daño.
            if (ctx.IsRagingStrikesActive)
            {
                return BRD_IDs.RadiantEncore;
            }

            // 2. PANIC MODE:
            // Si no estamos en burst, pero el buff está a punto de caducar (< 5s), 
            // lo usamos para no perder el daño.
            if (ctx.RadiantEncoreTimeLeft < 5.0f)
            {
                return BRD_IDs.RadiantEncore;
            }

            // 3. HOLD:
            // Si tenemos el proc, no hay burst activo, y sobra tiempo... esperamos.
            return 0;
        }

        // =========================================================================
        // LÓGICA oGCD (Buffs en Cascada)
        // =========================================================================
        public static OgcdPlan? GetPlan(BardContext ctx, int level)
        {
            float buffer = 0.6f;

            // 1. SIDEWINDER (Independiente)
            // Se usa a CD, idealmente alineado con 60s/120s, pero su prioridad es no driftear.

            /*if (level >= BRD_Levels.Sidewinder && ctx.SidewinderCD < buffer)
            {
                return new OgcdPlan(BRD_IDs.Sidewinder, WeavePriority.Normal, WeaveSlot.Any);
            }*/

            // ---------------------------------------------------------------------
            // LÓGICA DE BURST EN CASCADA
            // Orden Deseado: Raging Strikes -> Battle Voice -> Radiant Finale
            // ---------------------------------------------------------------------

            // A. Detectar Ventana
            bool alignedBurst = ctx.CurrentSong == Song.Wanderer || ctx.CurrentSong == Song.None;
            bool recoveryBurst = ctx.RagingStrikesCD < buffer;

            if (!alignedBurst && !recoveryBurst) return null;

            var priority = WeavePriority.High;

            // PASO 1: RAGING STRIKES (El Iniciador)
            if (level >= BRD_Levels.RagingStrikes && ctx.RagingStrikesCD < buffer)
            {
                return new OgcdPlan(BRD_IDs.RagingStrikes, priority, WeaveSlot.Any);
            }

            // PASO 2: BATTLE VOICE (Depende de RS)
            if (level >= BRD_Levels.BattleVoice && ctx.BattleVoiceCD < buffer)
            {
                if (ctx.IsRagingStrikesActive)
                {
                    return new OgcdPlan(BRD_IDs.BattleVoice, priority, WeaveSlot.Any);
                }
            }

            // PASO 3: RADIANT FINALE (Depende de RS y cierra la triada)
            if (level >= BRD_Levels.RadiantFinale && ctx.RadiantFinaleCD < buffer)
            {
                if (ctx.IsRagingStrikesActive)
                {
                    return new OgcdPlan(BRD_IDs.RadiantFinale, priority, WeaveSlot.Any);
                }
            }

            // PASO 4: BARRAGE (Depende de RS)
            if (level >= BRD_Levels.Barrage && ctx.BarrageCD < buffer && ctx.IsRagingStrikesActive)
            {
                return new OgcdPlan(BRD_IDs.Barrage, priority, WeaveSlot.SlotA);
            }

            return null;
        }
    }
}
