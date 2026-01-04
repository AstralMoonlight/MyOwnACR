// Archivo: Logic/BRD_Logic.cs
// Descripción: Lógica de combate para Bardo (Dawntrail 7.x).
// VERSION: v12.0 - BURST LOCK (Block HB/PP until RF is out).

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
        private DateTime lastIronJawsTime = DateTime.MinValue;
        private DateTime lastDebugTime = DateTime.MinValue;
        private bool isDebugFrame = false;

        public void QueueManualAction(string actionName) { }
        public string GetQueuedAction() => "v12.0 LOCKED";
        public void PrintDebugInfo(IChatGui chat) { }

        // =========================================================================
        // EJECUCIÓN PRINCIPAL
        // =========================================================================
        public unsafe void Execute(ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            if (am == null || player == null) return;

            bool inCombat = Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            if (!inCombat) return;

            if (player.TargetObject == null) return;
            ulong targetId = player.TargetObject.GameObjectId;

            var now = DateTime.Now;
            isDebugFrame = (now - lastDebugTime).TotalSeconds > 2.0;
            if (isDebugFrame) lastDebugTime = now;

            // Anti-Spam (200ms)
            if ((now - lastActionTime).TotalMilliseconds < 200) return;

            // 2. CHEQUEO DE GCD
            float gcdTotal = am->GetRecastTime(ActionType.Action, BRD_IDs.BurstShot);
            float gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
            float gcdRem = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            if (gcdRem <= 0.3f)
            {
                // --- GCD LOGIC ---
                var (hasStorm, hasCaustic, stormTime, causticTime) = GetTargetDotStatus(player);
                bool ironJawsSafe = (now - lastIronJawsTime).TotalSeconds > 4.0;

                float rsRem = GetStatusTime(player, BRD_IDs.Status_RagingStrikes);
                float bvRem = GetStatusTime(player, BRD_IDs.Status_BattleVoice);
                string buffInfo = $"RS:{rsRem:F0} BV:{bvRem:F0}";

                // A. BLAST ARROW
                if (HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                {
                    Plugin.Instance.SendLog($"[GCD] Blast Arrow | {buffInfo}");
                    am->UseAction(ActionType.Action, BRD_IDs.BlastArrow, targetId);
                    UpdateState(BRD_IDs.BlastArrow);
                    return;
                }

                // B. DOTS (Snapshot + Refresh)
                if (hasStorm && hasCaustic && ironJawsSafe)
                {
                    bool isSnapshotWindow = rsRem > 0 && rsRem < 4.0f;
                    bool isFallingOff = stormTime < 6.0f || causticTime < 6.0f;

                    if (isSnapshotWindow || isFallingOff)
                    {
                        string reason = isSnapshotWindow ? "Snapshot" : "Refresh";
                        Plugin.Instance.SendLog($"[GCD] Iron Jaws [{reason}] | {buffInfo}");
                        am->UseAction(ActionType.Action, BRD_IDs.IronJaws, targetId);
                        UpdateState(BRD_IDs.IronJaws);
                        return;
                    }
                }
                else if ((!hasStorm || !hasCaustic) && ironJawsSafe)
                {
                    if (!hasStorm)
                    {
                        am->UseAction(ActionType.Action, BRD_IDs.Stormbite, targetId);
                        UpdateState(BRD_IDs.Stormbite);
                        return;
                    }
                    if (!hasCaustic)
                    {
                        am->UseAction(ActionType.Action, BRD_IDs.CausticBite, targetId);
                        UpdateState(BRD_IDs.CausticBite);
                        return;
                    }
                }

                // C. RESONANT ARROW
                if (HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
                {
                    Plugin.Instance.SendLog($"[GCD] Resonant Arrow | {buffInfo}");
                    am->UseAction(ActionType.Action, BRD_IDs.ResonantArrow, targetId);
                    UpdateState(BRD_IDs.ResonantArrow);
                    return;
                }

                // D. RADIANT ENCORE
                if (HasStatus(player, BRD_IDs.Status_RadiantEncoreReady))
                {
                    float rsCD = GetRecastTime(am, BRD_IDs.RagingStrikes);
                    float bvCD = GetRecastTime(am, BRD_IDs.BattleVoice);
                    float rfCD = GetRecastTime(am, BRD_IDs.RadiantFinale);
                    bool fullBurst = rsCD > 60 && bvCD > 60 && rfCD > 60;
                    float procTime = GetStatusTime(player, BRD_IDs.Status_RadiantEncoreReady);

                    if (fullBurst || procTime < 5.0f)
                    {
                        Plugin.Instance.SendLog($"[GCD] Radiant Encore | {buffInfo}");
                        am->UseAction(ActionType.Action, BRD_IDs.RadiantEncore, targetId);
                        UpdateState(BRD_IDs.RadiantEncore);
                        return;
                    }
                }

                // E. APEX ARROW
                var gauge = Plugin.JobGauges.Get<BRDGauge>();
                bool ragingActive = HasStatus(player, BRD_IDs.Status_RagingStrikes);
                bool useApex = (ragingActive && gauge.SoulVoice >= 80) || (gauge.SoulVoice == 100);

                if (useApex)
                {
                    Plugin.Instance.SendLog($"[GCD] Apex Arrow (SV:{gauge.SoulVoice})");
                    am->UseAction(ActionType.Action, BRD_IDs.ApexArrow, targetId);
                    UpdateState(BRD_IDs.ApexArrow);
                    return;
                }

                // F. REFULGENT / BURST
                bool hasProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                               HasStatus(player, BRD_IDs.Status_HawksEye);

                if (hasProc)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.RefulgentArrow, targetId);
                    UpdateState(BRD_IDs.RefulgentArrow);
                }
                else
                {
                    am->UseAction(ActionType.Action, BRD_IDs.BurstShot, targetId);
                    UpdateState(BRD_IDs.BurstShot);
                }
            }
            else
            {
                // oGCD
                TryFireOgcds(am, player, targetId, config.Bard);
            }
        }

        private unsafe void TryFireOgcds(ActionManager* am, IPlayerCharacter player, ulong targetId, JobConfig_BRD config)
        {
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            bool inMinuet = gauge.Song == Song.Wanderer;
            float songTimer = gauge.SongTimer / 1000f;
            float gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);

            // ===================================================================
            // 0. PITCH PERFECT DUMP (ANTES DE CAMBIAR CANCIÓN)
            // ===================================================================
            if (inMinuet && songTimer < 3.5f && gauge.Repertoire > 0)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.PitchPerfect) == 0)
                {
                    Plugin.Instance.SendLog($"[oGCD] Pitch Perfect Dump");
                    am->UseAction(ActionType.Action, BRD_IDs.PitchPerfect, targetId);
                    UpdateState(BRD_IDs.PitchPerfect);
                    return;
                }
            }

            // ===================================================================
            // 1. ROTACIÓN DE CANCIONES (Late Weave Minuet)
            // ===================================================================
            var nextSongId = CheckSongRotation(gauge, config);
            if (nextSongId != 0)
            {
                if (nextSongId == BRD_IDs.WanderersMinuet && gcdElapsed < 1.0f) return;

                if (am->GetActionStatus(ActionType.Action, nextSongId) == 0)
                {
                    Plugin.Instance.SendLog($"[SONG] Rotando Canción -> {nextSongId}");
                    am->UseAction(ActionType.Action, nextSongId, player.GameObjectId);
                    UpdateState(nextSongId);
                    return;
                }
            }

            // ===================================================================
            // 2. VENTANA DE BURST (LATE WEAVE RAGING -> BV -> RF)
            // ===================================================================

            // --- ESTADO DEL BURST ---
            // Detectar si el burst está en progreso para bloquear recursos
            float rsCD = GetRecastTime(am, BRD_IDs.RagingStrikes);
            float bvCD = GetRecastTime(am, BRD_IDs.BattleVoice);
            float rfCD = GetRecastTime(am, BRD_IDs.RadiantFinale);

            bool rsStarted = rsCD > 100.0f || HasStatus(player, BRD_IDs.Status_RagingStrikes);
            bool rfFinished = rfCD > 100.0f; // Asumimos que si CD > 100, ya salió

            // SEMÁFORO ROJO: Estamos en burst (RS activo/usado) pero RF aún no sale.
            bool burstLock = inMinuet && rsStarted && !rfFinished;

            if (inMinuet)
            {
                // A. RAGING STRIKES
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.RagingStrikes) == 0)
                {
                    if (gcdElapsed < 1.0f) return; // Late Weave
                    Plugin.Instance.SendLog("[BUFF] Raging Strikes (Late)");
                    am->UseAction(ActionType.Action, BRD_IDs.RagingStrikes, player.GameObjectId);
                    UpdateState(BRD_IDs.RagingStrikes);
                    return;
                }

                // B. BATTLE VOICE (Si RS ya salió)
                if (rsStarted)
                {
                    if (am->GetActionStatus(ActionType.Action, BRD_IDs.BattleVoice) == 0)
                    {
                        Plugin.Instance.SendLog("[BUFF] Battle Voice");
                        am->UseAction(ActionType.Action, BRD_IDs.BattleVoice, player.GameObjectId);
                        UpdateState(BRD_IDs.BattleVoice);
                        return;
                    }
                }

                // C. RADIANT FINALE (Si BV ya salió - CD > 100)
                if (bvCD > 100.0f)
                {
                    if (am->GetActionStatus(ActionType.Action, BRD_IDs.RadiantFinale) == 0)
                    {
                        Plugin.Instance.SendLog("[BUFF] Radiant Finale");
                        am->UseAction(ActionType.Action, BRD_IDs.RadiantFinale, player.GameObjectId);
                        UpdateState(BRD_IDs.RadiantFinale);
                        return;
                    }
                }

                // D. BARRAGE (Si RF ya salió o no está disponible)
                // Usamos Barrage solo cuando el burst completo de buffs esté arriba
                if (burstLock == false && rsStarted)
                {
                    if (!HasStatus(player, BRD_IDs.Status_ResonantArrowReady) &&
                        am->GetActionStatus(ActionType.Action, BRD_IDs.Barrage) == 0)
                    {
                        Plugin.Instance.SendLog("[BUFF] Barrage");
                        am->UseAction(ActionType.Action, BRD_IDs.Barrage, player.GameObjectId);
                        UpdateState(BRD_IDs.Barrage);
                        return;
                    }
                }
            }

            // ===================================================================
            // CHECKPOINT: BLOQUEO DE RECURSOS
            // ===================================================================
            // Si "burstLock" es true, significa que estamos construyendo los buffs.
            // NO se permite pasar de aquí. Retornamos para esperar el siguiente frame/slot.
            if (burstLock) return;

            // ===================================================================
            // 3. RECURSOS (Solo si NO hay bloqueo)
            // ===================================================================

            if (inMinuet && gauge.Repertoire == 3)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.PitchPerfect) == 0)
                {
                    Plugin.Instance.SendLog("[oGCD] Pitch Perfect (3)");
                    am->UseAction(ActionType.Action, BRD_IDs.PitchPerfect, targetId);
                    UpdateState(BRD_IDs.PitchPerfect);
                    return;
                }
            }

            if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.HeartbreakShot) == 0)
                {
                    Plugin.Instance.SendLog("[oGCD] Heartbreak (Cap)");
                    am->UseAction(ActionType.Action, BRD_IDs.HeartbreakShot, targetId);
                    UpdateState(BRD_IDs.HeartbreakShot);
                    return;
                }
            }

            // ===================================================================
            // 4. RESTO
            // ===================================================================

            // Si RS va a volver pronto (< 5s), STOP oGCDs
            if (rsCD > 0 && rsCD < 5.0f) return;

            if (am->GetActionStatus(ActionType.Action, BRD_IDs.EmpyrealArrow) == 0)
            {
                am->UseAction(ActionType.Action, BRD_IDs.EmpyrealArrow, targetId);
                UpdateState(BRD_IDs.EmpyrealArrow);
                return;
            }

            bool inBurstMode = rsStarted;
            bool holdSidewinder = rsCD > 0 && rsCD < 20.0f;

            if (!holdSidewinder && am->GetActionStatus(ActionType.Action, BRD_IDs.Sidewinder) == 0)
            {
                am->UseAction(ActionType.Action, BRD_IDs.Sidewinder, targetId);
                UpdateState(BRD_IDs.Sidewinder);
                return;
            }

            if (inMinuet)
            {
                if (songTimer < 3.0f && gauge.Repertoire > 0)
                {
                    if (am->GetActionStatus(ActionType.Action, BRD_IDs.PitchPerfect) == 0)
                    {
                        am->UseAction(ActionType.Action, BRD_IDs.PitchPerfect, targetId);
                        UpdateState(BRD_IDs.PitchPerfect);
                        return;
                    }
                }
            }

            bool inBallad = gauge.Song == Song.Mage;
            bool inPaeon = gauge.Song == Song.Army;
            bool shouldSpend = !inPaeon || inBurstMode;

            if (inPaeon && !inBurstMode)
            {
                shouldSpend = false;
                if (GetCharges(am, BRD_IDs.HeartbreakShot) == 2)
                {
                    float nextCharge = GetRecastTimeRemaining(am, BRD_IDs.HeartbreakShot);
                    float timeToMinuet = songTimer - config.SongCutoff_Paeon;
                    if (nextCharge < timeToMinuet) shouldSpend = true;
                }
            }

            if (shouldSpend && GetCharges(am, BRD_IDs.HeartbreakShot) > 0)
            {
                if (am->GetActionStatus(ActionType.Action, BRD_IDs.HeartbreakShot) == 0)
                {
                    am->UseAction(ActionType.Action, BRD_IDs.HeartbreakShot, targetId);
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
            if (actionId == BRD_IDs.IronJaws) lastIronJawsTime = DateTime.Now;
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

        private static unsafe float GetRecastTimeRemaining(ActionManager* am, uint id)
        {
            var total = am->GetRecastTime(ActionType.Action, id);
            var elapsed = am->GetRecastTimeElapsed(ActionType.Action, id);
            return Math.Max(0, total - elapsed);
        }

        private static unsafe int GetCharges(ActionManager* am, uint id) => (int)am->GetCurrentCharges(id);
    }
}
