// Archivo: Logic/BRD_Logic.cs
// Descripción: Lógica de combate para Bardo (Dawntrail 7.x).
// VERSION: v17.0 - OPENER DEBUG + TARGET FIXES + SAVE CD.

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
        private DateTime lastSongTime = DateTime.MinValue;
        private DateTime lastDebugTime = DateTime.MinValue;
        private bool isDebugFrame = false;

        // Estado de combate previo
        private bool wasInCombat = false;

        public void QueueManualAction(string actionName) { }
        public string GetQueuedAction() => OpenerManager.Instance.IsRunning ? "OPENER RUNNING" : "AUTO";
        public void PrintDebugInfo(IChatGui chat) { }

        // =========================================================================
        // EJECUCIÓN PRINCIPAL
        // =========================================================================
        public unsafe void Execute(ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            if (am == null || player == null) return;

            bool inCombat = Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            var op = config.Operation;
            var brdConfig = config.Bard;

            // =================================================================================
            // 0. AUTO-START OPENER (Con Logs de Debug)
            // =================================================================================
            if (inCombat && !wasInCombat)
            {
                Plugin.Instance.SendLog($"[DEBUG] Entrada en Combate detectada.");

                if (op.UseOpener)
                {
                    Plugin.Instance.SendLog($"[DEBUG] Intentando iniciar Opener: '{op.SelectedOpener}'");
                    if (!string.IsNullOrEmpty(op.SelectedOpener) && op.SelectedOpener != "Ninguno")
                    {
                        OpenerManager.Instance.SelectOpener(op.SelectedOpener);
                        OpenerManager.Instance.Start();
                    }
                    else
                    {
                        Plugin.Instance.SendLog("[DEBUG] Error: Opener seleccionado es nulo o 'Ninguno'.");
                    }
                }
            }
            wasInCombat = inCombat;

            // =================================================================================
            // 1. EJECUCIÓN DE OPENER
            // =================================================================================
            if (OpenerManager.Instance.IsRunning)
            {
                var (opActionId, opBind) = OpenerManager.Instance.GetNextAction(am, player, brdConfig);

                if (opActionId != 0)
                {
                    // Determinar target correcto:
                    // Si es Canción o Buff -> Self. Si es ataque -> Target.
                    ulong opTargetId = player.GameObjectId; // Default Self

                    bool isSelfBuff = BRD_IDs.IsSong(opActionId) ||
                                      opActionId == BRD_IDs.RagingStrikes ||
                                      opActionId == BRD_IDs.BattleVoice ||
                                      opActionId == BRD_IDs.RadiantFinale ||
                                      opActionId == BRD_IDs.Barrage;

                    if (!isSelfBuff && player.TargetObject != null)
                    {
                        opTargetId = player.TargetObject.GameObjectId;
                    }

                    ExecuteAction(am, opActionId, opTargetId, op.UseMemoryInput);
                    return;
                }

                if (OpenerManager.Instance.IsRunning) return; // Esperando tiempo
            }

            // =================================================================================
            // ROTACIÓN NORMAL
            // =================================================================================

            if (!inCombat) return;

            var now = DateTime.Now;
            isDebugFrame = (now - lastDebugTime).TotalSeconds > 2.0;
            if (isDebugFrame) lastDebugTime = now;

            var gauge = Plugin.JobGauges.Get<BRDGauge>();

            // 2. GESTIÓN DE CANCIONES (PRIORIDAD GLOBAL & TARGETLESS)
            if (brdConfig.AutoSong && !op.SaveCD)
            {
                if ((now - lastSongTime).TotalSeconds > 2.0)
                {
                    var nextSongId = CheckSongRotationSmart(am, gauge, brdConfig);

                    if (nextSongId != 0)
                    {
                        bool safeToSing = true;
                        if (nextSongId == BRD_IDs.WanderersMinuet && player.TargetObject != null)
                        {
                            float songGcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
                            if (songGcdElapsed < 1.1f) safeToSing = false;
                        }

                        if (safeToSing && CanUse(am, nextSongId))
                        {
                            Plugin.Instance.SendLog($"[SONG] Activando -> {nextSongId}");
                            ExecuteAction(am, nextSongId, player.GameObjectId, op.UseMemoryInput);
                            lastSongTime = now;
                            return;
                        }
                    }
                }
            }

            // 3. VALIDACIÓN DE TARGET
            if (player.TargetObject == null) return;
            ulong targetId = player.TargetObject.GameObjectId;

            // Anti-Spam (200ms)
            if ((now - lastActionTime).TotalMilliseconds < 200) return;

            // 4. CHEQUEO DE GCD
            float gcdTotal = am->GetRecastTime(ActionType.Action, BRD_IDs.BurstShot);
            float gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
            float gcdRem = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            if (gcdRem <= 0.3f)
            {
                // --- GCD LOGIC ---
                var (hasStorm, hasCaustic, stormTime, causticTime) = GetTargetDotStatus(player);
                bool ironJawsSafe = (now - lastIronJawsTime).TotalSeconds > 2.0;

                // A. BLAST ARROW
                if (HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                {
                    Plugin.Instance.SendLog("[GCD] Blast Arrow");
                    ExecuteAction(am, BRD_IDs.BlastArrow, targetId, op.UseMemoryInput);
                    return;
                }

                // B. DOTS (Snapshot + Refresh)
                if (hasStorm && hasCaustic)
                {
                    float rsTime = GetStatusTime(player, BRD_IDs.Status_RagingStrikes);
                    bool isSnapshotWindow = rsTime > 0 && rsTime < 3.5f;
                    bool isFallingOff = stormTime < 6.0f || causticTime < 6.0f;

                    if ((isSnapshotWindow || isFallingOff) && ironJawsSafe)
                    {
                        string reason = isSnapshotWindow ? "Snapshot" : "Refresh";
                        Plugin.Instance.SendLog($"[GCD] Iron Jaws [{reason}]");
                        ExecuteAction(am, BRD_IDs.IronJaws, targetId, op.UseMemoryInput);
                        UpdateState(BRD_IDs.IronJaws);
                        return;
                    }
                }
                else if (ironJawsSafe)
                {
                    if (!hasStorm)
                    {
                        Plugin.Instance.SendLog("[GCD] Stormbite (Missing)");
                        ExecuteAction(am, BRD_IDs.Stormbite, targetId, op.UseMemoryInput);
                        return;
                    }
                    if (!hasCaustic)
                    {
                        Plugin.Instance.SendLog("[GCD] Caustic Bite (Missing)");
                        ExecuteAction(am, BRD_IDs.CausticBite, targetId, op.UseMemoryInput);
                        return;
                    }
                }

                // C. RESONANT ARROW
                if (HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
                {
                    Plugin.Instance.SendLog("[GCD] Resonant Arrow");
                    ExecuteAction(am, BRD_IDs.ResonantArrow, targetId, op.UseMemoryInput);
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
                        Plugin.Instance.SendLog("[GCD] Radiant Encore");
                        ExecuteAction(am, BRD_IDs.RadiantEncore, targetId, op.UseMemoryInput);
                        return;
                    }
                }

                // E. APEX ARROW
                bool ragingActive = HasStatus(player, BRD_IDs.Status_RagingStrikes);
                bool useApex = (ragingActive && gauge.SoulVoice >= 80) || (gauge.SoulVoice == 100);

                if (useApex)
                {
                    Plugin.Instance.SendLog($"[GCD] Apex Arrow (SV:{gauge.SoulVoice})");
                    ExecuteAction(am, BRD_IDs.ApexArrow, targetId, op.UseMemoryInput);
                    return;
                }

                // F. REFULGENT / BURST
                bool hasProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                               HasStatus(player, BRD_IDs.Status_HawksEye);

                if (hasProc)
                {
                    ExecuteAction(am, BRD_IDs.RefulgentArrow, targetId, op.UseMemoryInput);
                }
                else
                {
                    ExecuteAction(am, BRD_IDs.BurstShot, targetId, op.UseMemoryInput);
                }
            }
            else
            {
                // 5. CHEQUEO DE oGCD
                TryFireOgcds(am, player, targetId, config);
            }
        }

        private unsafe void TryFireOgcds(ActionManager* am, IPlayerCharacter player, ulong targetId, Configuration config)
        {
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            var brdConfig = config.Bard;
            var op = config.Operation;

            bool inMinuet = gauge.Song == Song.Wanderer;
            float songTimer = gauge.SongTimer / 1000f;

            // -1. PITCH PERFECT (3 STACKS - PRIO)
            if (inMinuet && gauge.Repertoire == 3)
            {
                if (CanUse(am, BRD_IDs.PitchPerfect))
                {
                    Plugin.Instance.SendLog("[oGCD] Pitch Perfect (3 Stacks)");
                    ExecuteAction(am, BRD_IDs.PitchPerfect, targetId, op.UseMemoryInput);
                    return;
                }
            }

            // 0. MODO SAVE CD
            if (op.SaveCD)
            {
                if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3 && CanUse(am, BRD_IDs.HeartbreakShot))
                {
                    Plugin.Instance.SendLog("[oGCD] Heartbreak (SaveCD Cap)");
                    ExecuteAction(am, BRD_IDs.HeartbreakShot, targetId, op.UseMemoryInput);
                }
                return;
            }

            // 1. PITCH PERFECT DUMP
            if (inMinuet && songTimer < 3.5f && gauge.Repertoire > 0)
            {
                if (CanUse(am, BRD_IDs.PitchPerfect))
                {
                    Plugin.Instance.SendLog($"[oGCD] Pitch Perfect Dump");
                    ExecuteAction(am, BRD_IDs.PitchPerfect, targetId, op.UseMemoryInput);
                    return;
                }
            }

            // 2. VENTANA DE BURST (RS -> BV -> RF) + POCIONES
            float rsCD = GetRecastTime(am, BRD_IDs.RagingStrikes);
            float bvCD = GetRecastTime(am, BRD_IDs.BattleVoice);
            float rfCD = GetRecastTime(am, BRD_IDs.RadiantFinale);

            bool rsStarted = rsCD > 100.0f || HasStatus(player, BRD_IDs.Status_RagingStrikes);
            bool rfFinished = rfCD > 100.0f;
            bool burstLock = inMinuet && rsStarted && !rfFinished;

            if (inMinuet)
            {
                // A. POCIÓN
                if (op.UsePotion && op.SelectedPotionId != 0)
                {
                    if (am->GetActionStatus(ActionType.Action, BRD_IDs.RagingStrikes) == 0)
                    {
                        if (InventoryManager.IsPotionReady(am, op.SelectedPotionId))
                        {
                            Plugin.Instance.SendLog("[ITEM] Using Potion");
                            InventoryManager.UseSpecificPotion(am, op.SelectedPotionId);
                            UpdateState(0);
                            return;
                        }
                    }
                }

                // B. RAGING STRIKES
                if (CanUse(am, BRD_IDs.RagingStrikes))
                {
                    float burstGcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
                    if (burstGcdElapsed < 1.0f) return;

                    Plugin.Instance.SendLog("[BUFF] Raging Strikes");
                    ExecuteAction(am, BRD_IDs.RagingStrikes, player.GameObjectId, op.UseMemoryInput);
                    return;
                }

                // C. BATTLE VOICE
                if (rsStarted && CanUse(am, BRD_IDs.BattleVoice))
                {
                    Plugin.Instance.SendLog("[BUFF] Battle Voice");
                    ExecuteAction(am, BRD_IDs.BattleVoice, player.GameObjectId, op.UseMemoryInput);
                    return;
                }

                // D. RADIANT FINALE
                if (bvCD > 100.0f && CanUse(am, BRD_IDs.RadiantFinale))
                {
                    Plugin.Instance.SendLog("[BUFF] Radiant Finale");
                    ExecuteAction(am, BRD_IDs.RadiantFinale, player.GameObjectId, op.UseMemoryInput);
                    return;
                }

                // E. BARRAGE
                if (burstLock == false && rsStarted)
                {
                    if (!HasStatus(player, BRD_IDs.Status_ResonantArrowReady) && CanUse(am, BRD_IDs.Barrage))
                    {
                        Plugin.Instance.SendLog("[BUFF] Barrage");
                        ExecuteAction(am, BRD_IDs.Barrage, player.GameObjectId, op.UseMemoryInput);
                        return;
                    }
                }
            }

            if (burstLock) return;

            // 3. RECURSOS
            if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3 && CanUse(am, BRD_IDs.HeartbreakShot))
            {
                Plugin.Instance.SendLog("[oGCD] Heartbreak (Cap)");
                ExecuteAction(am, BRD_IDs.HeartbreakShot, targetId, op.UseMemoryInput);
                return;
            }

            // 4. RESTO
            if (rsCD > 0 && rsCD < 5.0f) return;

            if (CanUse(am, BRD_IDs.EmpyrealArrow))
            {
                ExecuteAction(am, BRD_IDs.EmpyrealArrow, targetId, op.UseMemoryInput);
                return;
            }

            bool inBurstMode = rsStarted;
            bool holdSidewinder = rsCD > 0 && rsCD < 20.0f;

            if (!holdSidewinder && CanUse(am, BRD_IDs.Sidewinder))
            {
                ExecuteAction(am, BRD_IDs.Sidewinder, targetId, op.UseMemoryInput);
                return;
            }

            if (inMinuet && songTimer < 3.0f && gauge.Repertoire > 0 && CanUse(am, BRD_IDs.PitchPerfect))
            {
                ExecuteAction(am, BRD_IDs.PitchPerfect, targetId, op.UseMemoryInput);
                return;
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
                    float timeToMinuet = songTimer - brdConfig.SongCutoff_Paeon;
                    if (nextCharge < timeToMinuet) shouldSpend = true;
                }
            }

            if (shouldSpend && GetCharges(am, BRD_IDs.HeartbreakShot) > 0 && CanUse(am, BRD_IDs.HeartbreakShot))
            {
                ExecuteAction(am, BRD_IDs.HeartbreakShot, targetId, op.UseMemoryInput);
                return;
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private unsafe uint CheckSongRotationSmart(ActionManager* am, BRDGauge gauge, JobConfig_BRD config)
        {
            float timerSec = gauge.SongTimer / 1000f;
            if (gauge.Song == Song.None)
            {
                if (CanUse(am, BRD_IDs.WanderersMinuet)) return BRD_IDs.WanderersMinuet;
                if (CanUse(am, BRD_IDs.MagesBallad)) return BRD_IDs.MagesBallad;
                if (CanUse(am, BRD_IDs.ArmysPaeon)) return BRD_IDs.ArmysPaeon;
                return 0;
            }
            if (gauge.Song == Song.Wanderer && timerSec <= config.SongCutoff_Minuet) return BRD_IDs.MagesBallad;
            if (gauge.Song == Song.Mage && timerSec <= config.SongCutoff_Ballad) return BRD_IDs.ArmysPaeon;
            if (gauge.Song == Song.Army && timerSec <= config.SongCutoff_Paeon) return BRD_IDs.WanderersMinuet;
            return 0;
        }

        private unsafe void ExecuteAction(ActionManager* am, uint actionId, ulong targetId, bool useMem)
        {
            am->UseAction(ActionType.Action, actionId, targetId);
            UpdateState(actionId);
        }

        private unsafe bool CanUse(ActionManager* am, uint id)
        {
            return am->GetActionStatus(ActionType.Action, id) == 0;
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
