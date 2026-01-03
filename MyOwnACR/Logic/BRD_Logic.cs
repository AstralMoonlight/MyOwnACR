// Archivo: Logic/BRD_Logic.cs
// Descripción: Lógica de combate para Bardo (Dawntrail 7.x).
// VERSION: v7.1 - Iron Jaws Safety + Burst Sync (BV -> RF).

using System;
using System.Linq;
using System.Numerics;
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
        private DateTime lastIronJawsTime = DateTime.MinValue; // FIX DOBLE CAST
        private DateTime lastDebugTime = DateTime.MinValue;
        private bool isDebugFrame = false;

        public void QueueManualAction(string actionName) { }
        public string GetQueuedAction() => "v7.1 MODE";
        public void PrintDebugInfo(IChatGui chat) { }

        // =========================================================================
        // EJECUCIÓN PRINCIPAL
        // =========================================================================
        public unsafe void Execute(ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            if (am == null || player == null) return;

            // 1. CHECK DE COMBATE
            bool inCombat = Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            if (!inCombat) return;

            if (player.TargetObject == null) return;

            var now = DateTime.Now;
            isDebugFrame = (now - lastDebugTime).TotalSeconds > 2.0;
            if (isDebugFrame) lastDebugTime = now;

            // Anti-Spam (0.5s)
            if ((now - lastActionTime).TotalMilliseconds < 250) return;

            // 2. CHEQUEO DE GCD
            float gcdTotal = am->GetRecastTime(ActionType.Action, BRD_IDs.BurstShot);
            float gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
            float gcdRem = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            if (gcdRem <= 0.3f)
            {
                // --- GCD LOGIC ---
                var (hasStorm, hasCaustic, stormTime, causticTime) = GetTargetDotStatus(player);

                // A. BLAST ARROW (Top Priority)
                if (HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                {
                    Plugin.Instance.SendLog("[GCD] Blast Arrow");
                    am->UseAction(ActionType.Action, BRD_IDs.BlastArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.BlastArrow);
                    return;
                }

                // B. DOTS (Snapshot + Refresh)
                // Primero verificamos si acabamos de usar Iron Jaws hace poco (Safety)
                bool ironJawsSafe = (now - lastIronJawsTime).TotalSeconds > 4.0;

                if (hasStorm && hasCaustic && ironJawsSafe)
                {
                    // Lógica Snapshot: Raging Strikes activo y < 4s restantes
                    float rsTime = GetStatusTime(player, BRD_IDs.Status_RagingStrikes);
                    bool isSnapshotWindow = rsTime > 0 && rsTime < 4.0f;

                    // Lógica Refresh Normal
                    bool isFallingOff = stormTime < 5.0f || causticTime < 5.0f;

                    if (isSnapshotWindow || isFallingOff)
                    {
                        string reason = isSnapshotWindow ? $"Snapshot (RS: {rsTime:F1}s)" : "Refresh";
                        Plugin.Instance.SendLog($"[GCD] Iron Jaws - {reason}");
                        am->UseAction(ActionType.Action, BRD_IDs.IronJaws, player.TargetObject.GameObjectId);
                        UpdateState(BRD_IDs.IronJaws);
                        return;
                    }
                }
                else if ((!hasStorm || !hasCaustic) && ironJawsSafe)
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

                // C. RESONANT ARROW
                if (HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
                {
                    Plugin.Instance.SendLog("[GCD] Resonant Arrow");
                    am->UseAction(ActionType.Action, BRD_IDs.ResonantArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.ResonantArrow);
                    return;
                }

                // D. RADIANT ENCORE
                // Solo si Full Burst (BV+RF+RS) o a punto de expirar
                if (HasStatus(player, BRD_IDs.Status_RadiantEncoreReady))
                {
                    bool hasBV = HasStatus(player, BRD_IDs.Status_BattleVoice);
                    bool hasRF = HasStatus(player, BRD_IDs.Status_RadiantFinale);
                    bool hasRS = HasStatus(player, BRD_IDs.Status_RagingStrikes);
                    float procTime = GetStatusTime(player, BRD_IDs.Status_RadiantEncoreReady);

                    if ((hasBV && hasRF && hasRS) || procTime < 5.0f)
                    {
                        Plugin.Instance.SendLog("[GCD] Radiant Encore");
                        am->UseAction(ActionType.Action, BRD_IDs.RadiantEncore, player.TargetObject.GameObjectId);
                        UpdateState(BRD_IDs.RadiantEncore);
                        return;
                    }
                }

                // E. APEX ARROW
                var gauge = Plugin.JobGauges.Get<BRDGauge>();
                if (gauge.SoulVoice >= 80)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.ApexArrow, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.ApexArrow);
                    return;
                }

                // F. REFULGENT / BURST
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
                // Si el GCD está ocupado, intentamos oGCDs
                TryFireOgcds(am, player, config.Bard);
            }
        }

        private unsafe void TryFireOgcds(ActionManager* am, IPlayerCharacter player, JobConfig_BRD config)
        {
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            bool inMinuet = gauge.Song == Song.Wanderer;
            float songTimer = gauge.SongTimer / 1000f;

            // ===================================================================
            // 0. PITCH PERFECT DUMP (ANTES DE CAMBIAR CANCIÓN)
            // ===================================================================
            if (inMinuet && songTimer < 3.5f && gauge.Repertoire > 0)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.PitchPerfect) == 0)
                {
                    Plugin.Instance.SendLog($"[oGCD] Pitch Perfect Dump (Timer: {songTimer:F1}s)");
                    am->UseAction(ActionType.Action, BRD_IDs.PitchPerfect, player.TargetObject.GameObjectId);
                    UpdateState(BRD_IDs.PitchPerfect);
                    return;
                }
            }

            // ===================================================================
            // 1. ROTACIÓN DE CANCIONES (3-6-9)
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
            // 2. VENTANA DE BURST (BUFFS) - ORDEN ESTRICTO
            // ===================================================================
            if (inMinuet)
            {
                // A. BATTLE VOICE (Primero)
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.BattleVoice) == 0)
                {
                    Plugin.Instance.SendLog("[BUFF] >>> BATTLE VOICE <<<");
                    am->UseAction(ActionType.Action, BRD_IDs.BattleVoice, player.GameObjectId);
                    UpdateState(BRD_IDs.BattleVoice);
                    return;
                }

                // B. RADIANT FINALE (Segundo - SOLO DESPUÉS DE BV)
                // Lógica: Está listo Y (BV está en CD recién usado O tenemos el buff de BV)
                float bvRecast = GetRecastTime(am, BRD_IDs.BattleVoice);
                bool bvJustUsed = bvRecast > 110.0f; // Si el CD es > 110s, acabamos de usarlo
                bool hasBvBuff = HasStatus(player, BRD_IDs.Status_BattleVoice);

                if (am->GetActionStatus(ActionType.Action, BRD_IDs.RadiantFinale) == 0)
                {
                    // Si BV está listo (GetActionStatus == 0), no usamos RF todavía, dejamos que el ciclo anterior use BV
                    if (am->GetActionStatus(ActionType.Action, BRD_IDs.BattleVoice) != 0)
                    {
                        // BV no está listo (ya se usó). Verificamos que sea reciente para encadenar
                        if (bvJustUsed || hasBvBuff)
                        {
                            Plugin.Instance.SendLog("[BUFF] >>> RADIANT FINALE (Sync) <<<");
                            am->UseAction(ActionType.Action, BRD_IDs.RadiantFinale, player.GameObjectId);
                            UpdateState(BRD_IDs.RadiantFinale);
                            return;
                        }
                    }
                }

                // C. RAGING STRIKES
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.RagingStrikes) == 0)
                {
                    Plugin.Instance.SendLog("[BUFF] >>> RAGING STRIKES <<<");
                    am->UseAction(ActionType.Action, BRD_IDs.RagingStrikes, player.GameObjectId);
                    UpdateState(BRD_IDs.RagingStrikes);
                    return;
                }

                // D. BARRAGE
                if (HasStatus(player, BRD_IDs.Status_RagingStrikes))
                {
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
            // 3. RECURSOS PRIORITARIOS
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

            // Heartbreak Shot @ 3 Cargas (Anti-Cap)
            if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.HeartbreakShot) == 0)
                {
                    Plugin.Instance.SendLog("[oGCD] Heartbreak (Anti-Cap)");
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

            // Sidewinder
            // Bloqueo: No usar si faltan < 20s para burst y no estamos en burst
            float rsRecast = GetRecastTime(am, BRD_IDs.RagingStrikes);
            bool inBurst = HasStatus(player, BRD_IDs.Status_RagingStrikes);
            bool holdSidewinder = !inBurst && rsRecast < 20.0f;

            if (!holdSidewinder && am->GetActionStatus(ActionType.Action, BRD_IDs.Sidewinder) == 0)
            {
                am->UseAction(ActionType.Action, BRD_IDs.Sidewinder, player.TargetObject.GameObjectId);
                UpdateState(BRD_IDs.Sidewinder);
                return;
            }

            // Pitch Perfect Dump (< 3)
            if (inMinuet)
            {
                float timer = gauge.SongTimer / 1000f;
                // Si la canción está por terminar (< 3s) y hay stacks, úsalos
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

            if (actionId == BRD_IDs.IronJaws)
                lastIronJawsTime = DateTime.Now;
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
