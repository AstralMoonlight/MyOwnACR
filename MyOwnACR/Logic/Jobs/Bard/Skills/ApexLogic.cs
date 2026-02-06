// Archivo: Logic/Jobs/Bard/Skills/ApexLogic.cs
// VERSIÓN: V3.1 - BUFF SYNCHRONIZATION
// DESCRIPCIÓN: Optimización de recursos.
//              - Blast/Apex esperan a que TODOS los buffs (RS + BV + RF) estén activos.
//              - Paeon: Gasta al inicio, Guarda (Hold) al final.

using Dalamud.Game.ClientState.JobGauge.Enums;
using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class ApexLogic
    {
        public static uint GetAction(BardContext ctx, int level, bool hasBlastProc)
        {
            // -----------------------------------------------------------------
            // 1. BLAST ARROW (Prioridad Absoluta)
            // -----------------------------------------------------------------
            if (level >= BRD_Levels.BlastArrow && hasBlastProc)
            {
                // [NUEVO] SYNC CHECK PARA BLAST ARROW
                // Si Raging Strikes está activo, verificamos si faltan buffs por salir.
                // Blast Arrow dura 10s, así que tenemos margen para esperar 1 o 2 GCDs
                // a que Battle Voice y Radiant Finale estén activos.
                if (ctx.IsRagingStrikesActive)
                {
                    if (ShouldWaitForBuffs(ctx, level)) return 0; // Hold (Usa filler mientras)
                }

                return BRD_IDs.BlastArrow;
            }

            // -----------------------------------------------------------------
            // 2. APEX ARROW (Gestión de Soul Voice)
            // -----------------------------------------------------------------
            if (level >= BRD_Levels.ApexArrow && ctx.SoulVoice >= 80)
            {
                // A. VÁLVULA DE SEGURIDAD (Overcap Inminente)
                // Si llegamos a 100, gastamos SIEMPRE para no perder generación.
                if (ctx.SoulVoice == 100)
                {
                    return BRD_IDs.ApexArrow;
                }

                // B. BURST WINDOW (Raging Strikes Activo)
                if (ctx.IsRagingStrikesActive)
                {
                    // [NUEVO] SYNC CHECK
                    // Si RS está activo, pero Battle Voice o Radiant Finale están "Listos" (CD < 2s),
                    // significa que el bot los va a tirar pronto (oGCD).
                    // Retornamos 0 para dejar pasar un GCD filler (Burst Shot) mientras salen los buffs.
                    if (ShouldWaitForBuffs(ctx, level))
                    {
                        return 0;
                    }

                    return BRD_IDs.ApexArrow;
                }

                // C. ESTRATEGIA DE PAEON (El Puente)
                if (ctx.CurrentSong == Song.Army)
                {
                    // Inicio de Paeon (>20s para RS): GASTAR.
                    if (ctx.RagingStrikesCD > 20.0f)
                    {
                        return BRD_IDs.ApexArrow;
                    }
                    // Final de Paeon (<20s para RS): HOLD.
                    // Guardamos la barra para el Burst.
                    return 0;
                }

                // D. MAGE'S BALLAD & OTROS
                // Si estamos en Ballad y no estamos en 100, guardamos.
                return 0;
            }

            return 0;
        }

        // =========================================================================
        // HELPER: Detecta si faltan buffs por salir en la ventana de Burst
        // =========================================================================
        private static bool ShouldWaitForBuffs(BardContext ctx, int level)
        {
            // Si no tenemos nivel para Battle Voice, no esperamos nada.
            if (level < BRD_Levels.BattleVoice) return false;

            // Chequeo de Battle Voice:
            // Si el CD es menor a 2.0s, significa que está listo y debería ser usado inminentemente.
            // Si ya lo usamos, el CD será ~120s, por lo que esta condición será falsa.
            bool waitingForBV = (ctx.BattleVoiceCD < 2.0f);

            // Chequeo de Radiant Finale:
            bool waitingForRF = false;
            if (level >= BRD_Levels.RadiantFinale)
            {
                waitingForRF = (ctx.RadiantFinaleCD < 2.0f);
            }

            // Si alguno de los dos está pendiente de salir, decimos que SÍ hay que esperar.
            return waitingForBV || waitingForRF;
        }
    }
}
