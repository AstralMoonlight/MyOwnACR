// Archivo: Logic/BRD_Logic.cs
// Descripción: Lógica de combate para Bardo (Dawntrail 7.x).
// VERSION: STEP 6.0 - OPTIMIZED HARDCORE (Snapshot + Pooling + Protections).

using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.GameData;
using MyOwnACR;

namespace MyOwnACR.Logic
{
    public class BRD_Logic : IJobLogic
    {
        public static BRD_Logic Instance { get; } = new BRD_Logic();
        private BRD_Logic() { }

        public uint JobId => JobDefinitions.BRD;
        public uint LastProposedAction { get; private set; } = 0;

        private DateTime lastActionTime = DateTime.MinValue;
        private DateTime lastDebugTime = DateTime.MinValue;

        public void QueueManualAction(string actionName) { }
        public string GetQueuedAction() => "OPTIMIZED MODE";
        public void PrintDebugInfo(IChatGui chat) { }

        // =========================================================================
        // EJECUCIÓN DIRECTA
        // =========================================================================
        public unsafe void Execute(ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            if (am == null || player == null) return;
            if (player.TargetObject == null) return;

            var now = DateTime.Now;
            if ((now - lastActionTime).TotalMilliseconds < 500) return;

            // 1. CHEQUEO DE GCD
            float gcdTotal = am->GetRecastTime(ActionType.Action, BRD_IDs.BurstShot);
            float gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
            float gcdRem = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            if (gcdRem <= 0.3f)
            {
                // ===============================================================
                // LÓGICA DE GCD (OPTIMIZED)
                // ===============================================================
                var (hasStorm, hasCaustic, stormTime, causticTime) = GetTargetDotStatus(player);

                // 1. PROCS DE ALTA PRIORIDAD (Blast / Resonant / Encore)
                if (HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                {
                    am->UseAction(ActionType.Action, BRD_IDs.BlastArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.BlastArrow);
                    return;
                }
                if (HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
                {
                    Plugin.Instance.SendLog("[GCD] Resonant Arrow");
                    am->UseAction(ActionType.Action, BRD_IDs.ResonantArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.ResonantArrow);
                    return;
                }
                if (HasStatus(player, BRD_IDs.Status_RadiantEncoreReady))
                {
                    Plugin.Instance.SendLog("[GCD] Radiant Encore");
                    am->UseAction(ActionType.Action, BRD_IDs.RadiantEncore, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.RadiantEncore);
                    return;
                }

                // 2. DOTS INTELIGENTES (Mantenimiento + Snapshot)
                bool ragingActive = HasStatus(player, BRD_IDs.Status_RagingStrikes);
                float ragingTime = GetStatusTime(player, BRD_IDs.Status_RagingStrikes);

                // SNAPSHOT: Si Raging se acaba en < 3s, refrescar DoTs para guardar el buff
                bool snapshotWindow = ragingActive && ragingTime < 3.0f;
                bool dotsFalling = stormTime < 5.0f || causticTime < 5.0f;

                if (hasStorm && hasCaustic)
                {
                    if (dotsFalling || snapshotWindow)
                    {
                        string reason = snapshotWindow ? "Snapshot" : "Refresh";
                        Plugin.Instance.SendLog($"[GCD] Iron Jaws ({reason})");
                        am->UseAction(ActionType.Action, BRD_IDs.IronJaws, player.TargetObject.GameObjectId);
                        UpdateState(BRD_IDs.IronJaws);
                        return;
                    }
                }
                else
                {
                    // Aplicación inicial
                    if (!hasStorm)
                    {
                        am->UseAction(ActionType.Action, BRD_IDs.Stormbite, player.TargetObject.GameObjectId);
                        UpdateState(BRD_IDs.Stormbite);
                        return;
                    }
                    if (!hasCaustic)
                    {
                        am->UseAction(ActionType.Action, BRD_IDs.CausticBite, player.TargetObject.GameObjectId);
                        UpdateState(BRD_IDs.CausticBite);
                        return;
                    }
                }

                // 3. APEX ARROW INTELIGENTE (Pooling)
                var gauge = Plugin.JobGauges.Get<BRDGauge>();
                bool useApex = false;

                if (ragingActive)
                {
                    // En Burst: Usar si tenemos >= 80 para generar Blast Arrow
                    if (gauge.SoulVoice >= 80) useApex = true;
                }
                else
                {
                    // Fuera de Burst: Solo usar si estamos llenos (100) para no desperdiciar
                    // pero guardar barra para cuando llegue Raging
                    if (gauge.SoulVoice == 100) useApex = true;
                }

                if (useApex)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.ApexArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.ApexArrow);
                    return;
                }

                // 4. REFULGENT / BURST
                bool hasProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                               HasStatus(player, BRD_IDs.Status_HawksEye);

                if (hasProc)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.RefulgentArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.RefulgentArrow);
                }
                else
                {
                    am->UseAction(ActionType.Action, BRD_IDs.BurstShot, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.BurstShot);
                }
            }
            else
            {
                // oGCD Logic
                TryFireOgcds(am, player, config.Bard);
            }
        }

        private unsafe void TryFireOgcds(ActionManager* am, IPlayerCharacter player, JobConfig_BRD config)
        {
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            bool inMinuet = gauge.Song == Song.Wanderer;

            // ===================================================================
            // 1. ROTACIÓN DE CANCIONES
            // ===================================================================
            var nextSongId = CheckSongRotation(gauge, config);
            if (nextSongId != 0)
            {
                if (am->GetActionStatus(ActionType.Action, nextSongId) == 0)
                {
                    Plugin.Instance.SendLog($"[SONG] Rotando Canción -> {nextSongId}");
                    am->UseAction(ActionType.Action, nextSongId, player.GameObjectId);
                    UpdateState(nextSongId);
                    return;
                }
            }

            // ===================================================================
            // 2. VENTANA DE BURST (BUFFS)
            // ===================================================================
            if (inMinuet)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.RadiantFinale) == 0)
                {
                    Plugin.Instance.SendLog("[BUFF] >>> RADIANT FINALE <<<");
                    am->UseAction(ActionType.Action, BRD_IDs.RadiantFinale, player.GameObjectId);
                    UpdateState(BRD_IDs.RadiantFinale);
                    return;
                }

                if (am->GetActionStatus(ActionType.Action, BRD_IDs.BattleVoice) == 0)
                {
                    Plugin.Instance.SendLog("[BUFF] >>> BATTLE VOICE <<<");
                    am->UseAction(ActionType.Action, BRD_IDs.BattleVoice, player.GameObjectId);
                    UpdateState(BRD_IDs.BattleVoice);
                    return;
                }

                if (am->GetActionStatus(ActionType.Action, BRD_IDs.RagingStrikes) == 0)
                {
                    Plugin.Instance.SendLog("[BUFF] >>> RAGING STRIKES <<<");
                    am->UseAction(ActionType.Action, BRD_IDs.RagingStrikes, player.GameObjectId);
                    UpdateState(BRD_IDs.RagingStrikes);
                    return;
                }

                // Barrage Protection: Solo si NO hay proc de Refulgent ya activo
                bool hasRefulgentProc = HasStatus(player, BRD_IDs.Status_StraightShotReady);

                if (HasStatus(player, BRD_IDs.Status_RagingStrikes) && !hasRefulgentProc)
                {
                    // Tampoco si tenemos Resonant Ready (ya es un GCD fuerte asegurado)
                    if (!HasStatus(player, BRD_IDs.Status_ResonantArrowReady) &&
                        am->GetActionStatus(ActionType.Action, BRD_IDs.Barrage) == 0)
                    {
                        Plugin.Instance.SendLog("[BUFF] >>> BARRAGE <<<");
                        am->UseAction(ActionType.Action, BRD_IDs.Barrage, player.GameObjectId);
                        UpdateState(BRD_IDs.Barrage);
                        return;
                    }
                }
            }

            // ===================================================================
            // 3. RECURSOS AL LÍMITE (Evitar Overcap)
            // ===================================================================

            // Pitch Perfect @ 3 Stacks
            if (inMinuet && gauge.Repertoire == 3)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.PitchPerfect) == 0)
                {
                    Plugin.Instance.SendLog("[oGCD] Pitch Perfect (3 Stacks)!");
                    am->UseAction(ActionType.Action, BRD_IDs.PitchPerfect, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.PitchPerfect);
                    return;
                }
            }

            // Heartbreak Shot @ 3 Cargas
            if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.HeartbreakShot) == 0)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.HeartbreakShot, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.HeartbreakShot);
                    return;
                }
            }

            // ===================================================================
            // 4. RESTO DE OGCDs
            // ===================================================================

            // Empyreal Arrow
            if (am->GetActionStatus(ActionType.Action, BRD_IDs.EmpyrealArrow) == 0)
            {
                am->UseAction(ActionType.Action, BRD_IDs.EmpyrealArrow, player.TargetObject.GameObjectId);
                UpdateState(BRD_IDs.EmpyrealArrow);
                return;
            }

            // Sidewinder Inteligente
            // Solo usar si Raging Strikes está activo O falta mucho para que vuelva (>15s)
            float ragingCD = GetRecastTime(am, BRD_IDs.RagingStrikes);
            bool inBurst = HasStatus(player, BRD_IDs.Status_RagingStrikes);
            bool safeToUseSidewinder = inBurst || ragingCD > 15.0f;

            if (safeToUseSidewinder)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.Sidewinder) == 0)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.Sidewinder, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.Sidewinder);
                    return;
                }
            }

            // Pitch Perfect Dump (< 3)
            if (inMinuet)
            {
                float timer = gauge.SongTimer / 1000f;
                if (timer < 3.0f && gauge.Repertoire > 0)
                {
                    if (am->GetActionStatus(ActionType.Action, BRD_IDs.PitchPerfect) == 0)
                    {
                        am->UseAction(ActionType.Action, BRD_IDs.PitchPerfect, player.TargetObject.GameObjectId);
                        UpdateState(BRD_IDs.PitchPerfect);
                        return;
                    }
                }
            }

            // Heartbreak Shot (Gestión Inteligente)
            bool inBallad = gauge.Song == Song.Mage;
            bool inPaeon = gauge.Song == Song.Army;
            bool shouldSpendHeartbreak = inBurst || inBallad || inMinuet;
            if (inPaeon && !inBurst) shouldSpendHeartbreak = false;

            if (shouldSpendHeartbreak && GetCharges(am, BRD_IDs.HeartbreakShot) > 0)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.HeartbreakShot) == 0)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.HeartbreakShot, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.HeartbreakShot);
                    return;
                }
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private uint CheckSongRotation(BRDGauge gauge, JobConfig_BRD config)
        {
            float timerSec = gauge.SongTimer / 1000f;
            if (gauge.Song == Song.None) return BRD_IDs.WanderersMinuet;
            if (gauge.Song == Song.Wanderer && timerSec <= config.SongCutoff_Minuet) return BRD_IDs.MagesBallad;
            if (gauge.Song == Song.Mage && timerSec <= config.SongCutoff_Ballad) return BRD_IDs.ArmysPaeon;
            if (gauge.Song == Song.Army && timerSec <= config.SongCutoff_Paeon) return BRD_IDs.WanderersMinuet;
            return 0;
        }

        private void UpdateState(uint actionId)
        {
            lastActionTime = DateTime.Now;
            LastProposedAction = actionId;
        }

        private static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return true;
            return false;
        }

        private static float GetStatusTime(IPlayerCharacter player, ushort statusId)
        {
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return s.RemainingTime;
            return 0;
        }

        private (bool hasStorm, bool hasCaustic, float stormTime, float causticTime) GetTargetDotStatus(IPlayerCharacter player)
        {
            if (player.TargetObject is IBattleChara targetEnemy)
            {
                bool hasStorm = false;
                bool hasCaustic = false;
                float stormTime = 0;
                float causticTime = 0;

                foreach (var status in targetEnemy.StatusList)
                {
                    if (status.SourceId != player.GameObjectId) continue;
                    if (status.StatusId == BRD_IDs.Debuff_Stormbite) { hasStorm = true; stormTime = status.RemainingTime; }
                    if (status.StatusId == BRD_IDs.Debuff_CausticBite) { hasCaustic = true; causticTime = status.RemainingTime; }
                }
                return (hasStorm, hasCaustic, stormTime, causticTime);
            }
            return (false, false, 0, 0);
        }

        private static unsafe float GetRecastTime(ActionManager* am, uint id)
        {
            var total = am->GetRecastTime(ActionType.Action, id);
            var elapsed = am->GetRecastTimeElapsed(ActionType.Action, id);
            return (total > 0) ? Math.Max(0, total - elapsed) : 0;
        }

        private static unsafe int GetCharges(ActionManager* am, uint id) => (int)am->GetCurrentCharges(id);
    }
}
