// Archivo: Logic/Jobs/Bard/Skills/SongLogic.cs
// CORRECCIÓN: Uso de nombres de Enum correctos (Minuet, Mage, Army).

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class SongLogic
    {
        private const float CUTOFF_MINUET = 3.0f;
        private const float CUTOFF_BALLAD = 6.0f;
        private const float CUTOFF_PAEON = 9.0f;

        private const int LVL_MINUET = 52;
        private const int LVL_BALLAD = 30;
        private const int LVL_PAEON = 40;

        public static OgcdPlan? GetPlan(BardContext ctx, int level)
        {
            if (level < LVL_MINUET) return GetLowLevelPlan(ctx, level);

            var priority = WeavePriority.Forced;
            var slot = WeaveSlot.Any;

            // 1. APERTURA
            if (ctx.CurrentSong == Song.None)
            {
                return new OgcdPlan(BRD_IDs.WanderersMinuet, priority, slot);
            }

            float timerSec = ctx.SongTimerMS / 1000f;

            // 2. FASE 1: THE WANDERER'S MINUET (Nombre correcto: Song.Wanderer)
            if (ctx.CurrentSong == Song.Wanderer)
            {
                if (level >= LVL_BALLAD && timerSec <= CUTOFF_MINUET)
                {
                    return new OgcdPlan(BRD_IDs.MagesBallad, priority, slot);
                }
            }

            // 3. FASE 2: MAGE'S BALLAD (Nombre correcto: Song.Mage)
            if (ctx.CurrentSong == Song.Mage)
            {
                if (level >= LVL_PAEON && timerSec <= CUTOFF_BALLAD)
                {
                    return new OgcdPlan(BRD_IDs.ArmysPaeon, priority, slot);
                }
            }

            // 4. FASE 3: ARMY'S PAEON (Nombre correcto: Song.Army)
            if (ctx.CurrentSong == Song.Army)
            {
                bool minuetReady = ctx.MinuetCD < 0.6f;
                if ((timerSec <= CUTOFF_PAEON && minuetReady) || minuetReady)
                {
                    return new OgcdPlan(BRD_IDs.WanderersMinuet, priority, slot);
                }
            }

            return null;
        }

        private static OgcdPlan? GetLowLevelPlan(BardContext ctx, int level)
        {
            if (ctx.CurrentSong == Song.None)
            {
                if (level >= LVL_BALLAD && ctx.SongTimerMS <= 0) return new OgcdPlan(BRD_IDs.MagesBallad);
            }
            return null;
        }
    }
}
