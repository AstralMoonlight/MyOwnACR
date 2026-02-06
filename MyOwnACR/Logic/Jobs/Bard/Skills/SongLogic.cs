// Archivo: Logic/Jobs/Bard/Skills/SongLogic.cs
// VERSIÓN: V3.1 - FIX OPENER PRIORITY
// DESCRIPCIÓN: Estrategia 3-6-9 con corrección para evitar Ballad en el Opener.

using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class SongLogic
    {
        // ESTRATEGIA DE CORTE (3 - 6 - 9)
        private const float CUTOFF_MINUET = 3.0f;
        private const float CUTOFF_BALLAD = 6.0f;
        private const float CUTOFF_PAEON = 9.0f;

        public static OgcdPlan? GetPlan(BardContext ctx, int level)
        {
            // Chequeo de nivel bajo (Pre-Minuet)
            if (level < BRD_Levels.TheWanderersMinuet) return GetLowLevelPlan(ctx, level);

            var priority = WeavePriority.Forced;

            // -----------------------------------------------------------------
            // 1. RECUPERACIÓN / APERTURA (Sin Canción Activa)
            // -----------------------------------------------------------------
            if (ctx.CurrentSong == Song.None)
            {
                // --- REGLA DE ORO: OPENER ---
                // Si Minuet está listo (CD < 1s), es el Opener o el inicio de ciclo.
                // Usamos Minuet obligatoriamente. Esto protege contra lecturas erróneas de RS.
                if (ctx.MinuetCD < 1.0f)
                {
                    return new OgcdPlan(BRD_IDs.WanderersMinuet, priority);
                }

                // --- LÓGICA DE RECUPERACIÓN (Si Minuet NO está listo) ---
                // Si morimos y revivimos, o si se cortó la rotación, usamos RS para ubicarnos.
                float rsCD = ctx.RagingStrikesCD;

                // CASO A: RS casi listo (< 15s).
                // (Ya cubierto por la Regla de Oro si Minuet estuviera listo, 
                // pero si Minuet falta poco y RS está listo, esperamos o forzamos Minuet).
                if (rsCD < 15.0f)
                {
                    return new OgcdPlan(BRD_IDs.WanderersMinuet, priority);
                }

                // CASO B: Mitad de ciclo (Faltan 15s a 60s para RS). -> PAEON
                if (rsCD >= 15.0f && rsCD < 60.0f)
                {
                    if (level >= BRD_Levels.ArmysPaeon)
                        return new OgcdPlan(BRD_IDs.ArmysPaeon, priority);
                }

                // CASO C: Inicio de ciclo lejano (Faltan > 60s para RS). -> BALLAD
                if (rsCD >= 60.0f)
                {
                    if (level >= BRD_Levels.MagesBallad)
                        return new OgcdPlan(BRD_IDs.MagesBallad, priority);
                }

                // Fallback
                return new OgcdPlan(BRD_IDs.WanderersMinuet, priority);
            }

            // -----------------------------------------------------------------
            // 2. CICLO NORMAL (Canción Activa)
            // -----------------------------------------------------------------
            float timerSec = ctx.SongTimerMS / 1000f;

            // FASE 1: MINUET (Cortar a los 3s)
            if (ctx.CurrentSong == Song.Wanderer)
            {
                if (timerSec <= CUTOFF_MINUET)
                {
                    return new OgcdPlan(BRD_IDs.MagesBallad, priority);
                }
            }

            // FASE 2: BALLAD (Cortar a los 6s)
            if (ctx.CurrentSong == Song.Mage)
            {
                if (timerSec <= CUTOFF_BALLAD)
                {
                    return new OgcdPlan(BRD_IDs.ArmysPaeon, priority);
                }
            }

            // FASE 3: PAEON (Cortar a los 9s O cuando Minuet esté listo)
            if (ctx.CurrentSong == Song.Army)
            {
                // Anti-Drift: Si Minuet volvió, cortar Paeon YA.
                if (ctx.MinuetCD < 0.5f)
                {
                    return new OgcdPlan(BRD_IDs.WanderersMinuet, priority);
                }

                // Corte estándar 9s (Solo si Minuet está cerca)
                if (timerSec <= CUTOFF_PAEON && ctx.MinuetCD < 3.0f)
                {
                    return new OgcdPlan(BRD_IDs.WanderersMinuet, priority);
                }
            }

            return null;
        }

        private static OgcdPlan? GetLowLevelPlan(BardContext ctx, int level)
        {
            if (ctx.CurrentSong == Song.None)
            {
                // Prioridad simple para niveles bajos: Ballad > Paeon
                if (level >= BRD_Levels.MagesBallad) return new OgcdPlan(BRD_IDs.MagesBallad);
                if (level >= BRD_Levels.ArmysPaeon) return new OgcdPlan(BRD_IDs.ArmysPaeon);
            }
            return null;
        }
    }
}
