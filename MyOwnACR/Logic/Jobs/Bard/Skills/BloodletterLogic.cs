// Archivo: Logic/Jobs/Bard/Skills/BloodletterLogic.cs
// VERSIÓN: V3.0 - BUFF SYNCHRONIZATION
// DESCRIPCIÓN: Gestión de cargas de Heartbreak Shot / Rain of Death.
//              - Burst: Gasta todo, pero ESPERA a que salgan todos los buffs.
//              - Ballad: Gasta agresivo.
//              - Paeon: Guarda (Pool) para el burst.

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class BloodletterLogic
    {
        private const float OVERCAP_SAFETY_THRESHOLD = 3.0f;

        public static OgcdPlan? GetPlan(BardContext ctx, int playerLevel, bool useAoE)
        {
            // 0. SIN CARGAS -> SALIR
            if (ctx.BloodletterCharges == 0) return null;

            // 1. SELECCIÓN DE ID
            uint actionId;
            if (useAoE)
                actionId = (playerLevel >= BRD_Levels.RainOfDeath) ? BRD_IDs.RainOfDeath : BRD_IDs.Bloodletter;
            else
                actionId = (playerLevel >= BRD_Levels.HeartbreakShot) ? BRD_IDs.HeartbreakShot : BRD_IDs.Bloodletter;

            int maxCharges = (playerLevel >= 84) ? 3 : 2;

            // -------------------------------------------------------------------------
            // ESCENARIO 1: BURST (Raging Strikes Activo) -> GASTAR TODO
            // -------------------------------------------------------------------------
            if (ctx.IsRagingStrikesActive)
            {
                // [NUEVO] SYNC CHECK
                // Si RS está activo, pero Battle Voice o Radiant Finale están "Listos" (CD < 2s),
                // significa que el bot los va a tirar pronto.
                // Es mejor esperar 1 o 2 GCDs para meter los Heartbreak Shot con TODOS los buffs.

                // EXCEPCIÓN: Si estamos a punto de hacer Overcap (3 cargas), ignoramos la espera
                // y disparamos igual, porque perder una carga es peor que perder un buff.
                if (ctx.BloodletterCharges < maxCharges) // Solo si tenemos espacio para esperar
                {
                    if (ShouldWaitForBuffs(ctx, playerLevel))
                    {
                        return null; // Hold (Espera a BV/RF)
                    }
                }

                // Si ya salieron los buffs (o estamos llenos), FUEGO.
                return new OgcdPlan(actionId, WeavePriority.High, WeaveSlot.Any);
            }

            // -------------------------------------------------------------------------
            // ESCENARIO 2: MAGE'S BALLAD (Reducción de CD) -> NO POOLING
            // -------------------------------------------------------------------------
            if (ctx.CurrentSong == Song.Mage)
            {
                return new OgcdPlan(actionId, WeavePriority.Normal, WeaveSlot.Any);
            }

            // -------------------------------------------------------------------------
            // ESCENARIO 3: POOLING (Army's Paeon / Minuet sin Burst / Sin Canción)
            // -------------------------------------------------------------------------

            // A. ESTAMOS LLENOS (Overcap Inminente) -> GASTAR
            if (ctx.BloodletterCharges >= maxCharges)
            {
                return new OgcdPlan(actionId, WeavePriority.High, WeaveSlot.Any);
            }

            // B. ESTAMOS "CASI" LLENOS (Zona de Riesgo)
            if (ctx.BloodletterCharges == maxCharges - 1)
            {
                if (ctx.BloodletterCD < OVERCAP_SAFETY_THRESHOLD)
                {
                    return new OgcdPlan(actionId, WeavePriority.Normal, WeaveSlot.Any);
                }
                return null; // Hold
            }

            // C. POCAS CARGAS -> HOLD
            return null;
        }

        // =========================================================================
        // HELPER: Detecta si faltan buffs por salir (Mismo que en ApexLogic)
        // =========================================================================
        private static bool ShouldWaitForBuffs(BardContext ctx, int level)
        {
            if (level < BRD_Levels.BattleVoice) return false;

            // Battle Voice listo? (CD < 2s)
            bool waitingForBV = (ctx.BattleVoiceCD < 2.0f);

            // Radiant Finale listo?
            bool waitingForRF = false;
            if (level >= BRD_Levels.RadiantFinale)
            {
                waitingForRF = (ctx.RadiantFinaleCD < 2.0f);
            }

            return waitingForBV || waitingForRF;
        }
    }
}
