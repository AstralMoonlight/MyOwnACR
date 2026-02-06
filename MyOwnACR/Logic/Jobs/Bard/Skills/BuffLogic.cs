// Archivo: Logic/Jobs/Bard/Skills/BuffLogic.cs
// VERSIÓN: V2.1 - PREDICTIVE BURST
// DESCRIPCIÓN: Gestión de Buffs con predicción de estado para evitar drift.
//              Permite Double Weave (ej. RS + BV) simulando la activación de RS.

using System.Collections.Generic;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class BuffLogic
    {
        // =========================================================================
        // LÓGICA GCD (Radiant Encore)
        // =========================================================================
        /// <summary>
        /// Decide si usar Radiant Encore.
        /// Prioridad: Burst > Expiración inminente > Hold.
        /// </summary>
        public static uint GetEncoreGcd(BardContext ctx, int level)
        {
            if (level < BRD_Levels.RadiantEncore) return 0;
            if (ctx.RadiantEncoreTimeLeft <= 0) return 0; // Sin proc

            // 1. BURST MODE (Raging Strikes Activo)
            if (ctx.IsRagingStrikesActive)
            {
                return BRD_IDs.RadiantEncore;
            }

            // 2. PANIC MODE (A punto de expirar)
            // Si quedan < 5s y no estamos en burst, úsalo para no perderlo.
            if (ctx.RadiantEncoreTimeLeft < 5.0f)
            {
                return BRD_IDs.RadiantEncore;
            }

            // 3. HOLD (Esperar al burst)
            return 0;
        }

        // =========================================================================
        // LÓGICA oGCD (Buffs en Cascada Predictiva)
        // =========================================================================
        /// <summary>
        /// Genera una lista de buffs a usar en este ciclo.
        /// Usa simulación para permitir encadenar buffs dependientes de RS.
        /// </summary>
        public static List<OgcdPlan> GetPlans(BardContext ctx, int level)
        {
            var plans = new List<OgcdPlan>();
            float buffer = 0.6f;
            var priority = WeavePriority.High; // Buffs = Prioridad Alta/Forzada

            // VARIABLE PREDICTIVA:
            // Empieza siendo el estado real del juego.
            // Si decidimos lanzar RS, la cambiamos a 'true' para engañar a los siguientes chequeos.
            bool simulatedRsActive = ctx.IsRagingStrikesActive;

            // ---------------------------------------------------------------------
            // PASO 1: RAGING STRIKES (El Iniciador)
            // ---------------------------------------------------------------------
            if (level >= BRD_Levels.RagingStrikes && ctx.RagingStrikesCD < buffer)
            {
                // Solo iniciar si estamos en Minuet o Inicio (evitar activar en Paeon tardío)
                if (ctx.CurrentSong == Song.Wanderer || ctx.CurrentSong == Song.None)
                {
                    plans.Add(new OgcdPlan(BRD_IDs.RagingStrikes, priority, WeaveSlot.Any));

                    // PREDICCIÓN: Asumimos que RS está activo para el resto de esta función.
                    simulatedRsActive = true;
                }
            }

            // GATEKEEPER:
            // Si RS no está activo (real) Y no lo acabamos de planear (simulado), 
            // no tiene sentido seguir con BV o RF. Abortamos.
            if (!simulatedRsActive) return plans;

            // ---------------------------------------------------------------------
            // PASO 2: BATTLE VOICE (Party Buff)
            // ---------------------------------------------------------------------
            // Verificamos contra 'simulatedRsActive'.
            if (level >= BRD_Levels.BattleVoice && ctx.BattleVoiceCD < buffer)
            {
                // Como simulatedRsActive es true, esto entrará en la lista.
                plans.Add(new OgcdPlan(BRD_IDs.BattleVoice, priority, WeaveSlot.Any));
            }

            // ---------------------------------------------------------------------
            // PASO 3: RADIANT FINALE (Party Buff + Self Buff)
            // ---------------------------------------------------------------------
            if (level >= BRD_Levels.RadiantFinale && ctx.RadiantFinaleCD < buffer)
            {
                // CRÍTICO: Validar Coda >= 1.
                if (ctx.CodaCount >= 1)
                {
                    plans.Add(new OgcdPlan(BRD_IDs.RadiantFinale, priority, WeaveSlot.Any));
                }
            }

            return plans;
        }
    }
}
