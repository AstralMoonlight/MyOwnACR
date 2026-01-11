// Archivo: Logic/BRD_Logic.cs
// Descripción: Lógica de combate para Bardo (Dawntrail 7.x).
// VERSION: v19.0 - Fix Songs (Targetless/Self-cast) + Key Listener.

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.Keys;
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

        private bool wasInCombat = false;

        // --- VARIABLES DE INYECCIÓN (KEY LISTENER) ---
        private uint queuedPriorityAction = 0;
        private DateTime queueExpirationTime = DateTime.MinValue;
        private bool isManualInputActive = false;

        // IMPLEMENTACIÓN DE INTERFAZ
        public void QueueManualAction(string actionName) { }
        public string GetQueuedAction() => queuedPriorityAction != 0 ? $"INJECT: {queuedPriorityAction}" : (OpenerManager.Instance.IsRunning ? "OPENER" : "AUTO");
        public void PrintDebugInfo(IChatGui chat) { }

        public void QueueActionId(uint actionId)
        {
            queuedPriorityAction = actionId;
            queueExpirationTime = DateTime.Now.AddSeconds(2.0);
            Plugin.Instance.SendLog($"[INJECTION] Externo: {actionId}");
        }

        // =========================================================================
        // EJECUCIÓN PRINCIPAL
        // =========================================================================
        public unsafe void Execute(ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            if (am == null || player == null) return;

            // -----------------------------------------------------------------
            // 0. LISTENER DE TECLAS (PRIORIDAD MÁXIMA)
            // -----------------------------------------------------------------
            CheckManualInput(config.Bard);

            // -----------------------------------------------------------------
            // 1. EJECUCIÓN DE INYECCIÓN (CARRIL EXPRESO)
            // -----------------------------------------------------------------
            if (queuedPriorityAction != 0)
            {
                if (DateTime.Now > queueExpirationTime)
                {
                    queuedPriorityAction = 0; // Timeout
                }
                else
                {
                    // Sprint (General Action ID 3)
                    if (queuedPriorityAction == 3)
                    {
                        if (am->GetActionStatus(ActionType.GeneralAction, 3) == 0)
                        {
                            Plugin.Instance.SendLog($"[INJECTION] Executing Sprint");
                            ExecuteAction(am, 3, player.GameObjectId, config.Operation.UseMemoryInput);
                            queuedPriorityAction = 0;
                            return;
                        }
                    }
                    // Acciones de Job
                    else if (CanUse(am, queuedPriorityAction))
                    {
                        ulong targetId = player.TargetObject?.GameObjectId ?? player.GameObjectId;
                        Plugin.Instance.SendLog($"[INJECTION] Executing ID: {queuedPriorityAction}");
                        ExecuteAction(am, queuedPriorityAction, targetId, config.Operation.UseMemoryInput);
                        queuedPriorityAction = 0;
                        return;
                    }
                    return; // Bloquea el resto
                }
            }

            // ... (Resto de la lógica normal)

            bool inCombat = Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            var op = config.Operation;
            var brdConfig = config.Bard;

            if (inCombat && !wasInCombat)
            {
                if (op.UseOpener && !string.IsNullOrEmpty(op.SelectedOpener) && op.SelectedOpener != "Ninguno")
                {
                    Plugin.Instance.SendLog($"[DEBUG] Auto-Start Opener: {op.SelectedOpener}");
                    OpenerManager.Instance.SelectOpener(op.SelectedOpener);
                    OpenerManager.Instance.Start();
                }
            }
            wasInCombat = inCombat;

            if (OpenerManager.Instance.IsRunning)
            {
                var (opActionId, opBind) = OpenerManager.Instance.GetNextAction(am, player, brdConfig);
                if (opActionId != 0)
                {
                    ulong opTargetId = player.GameObjectId;
                    bool isSelfBuff = BRD_IDs.IsSong(opActionId) ||
                                      opActionId == BRD_IDs.RagingStrikes ||
                                      opActionId == BRD_IDs.BattleVoice ||
                                      opActionId == BRD_IDs.RadiantFinale ||
                                      opActionId == BRD_IDs.Barrage ||
                                      opActionId == BRD_IDs.Troubadour;

                    if (!isSelfBuff && player.TargetObject != null) opTargetId = player.TargetObject.GameObjectId;

                    ExecuteAction(am, opActionId, opTargetId, op.UseMemoryInput);
                    return;
                }
                if (OpenerManager.Instance.IsRunning) return;
            }

            if (!inCombat) return;

            var now = DateTime.Now;
            isDebugFrame = (now - lastDebugTime).TotalSeconds > 2.0;
            if (isDebugFrame) lastDebugTime = now;

            var gauge = Plugin.JobGauges.Get<BRDGauge>();

            // 2. GESTIÓN DE CANCIONES (CORREGIDO: TARGETLESS / SELF)
            if (brdConfig.AutoSong && !op.SaveCD)
            {
                if ((now - lastSongTime).TotalSeconds > 2.0)
                {
                    var nextSongId = CheckSongRotationSmart(am, gauge, brdConfig);
                    if (nextSongId != 0)
                    {
                        bool safeToSing = true;
                        // Late-Weave Minuet si estamos atacando (para no clippear GCD)
                        if (nextSongId == BRD_IDs.WanderersMinuet)
                        {
                            float songGcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
                            if (songGcdElapsed < 1.1f) safeToSing = false;
                        }

                        if (safeToSing && CanUse(am, nextSongId))
                        {
                            Plugin.Instance.SendLog($"[SONG] Activando -> {nextSongId}");
                            // FIX: Usamos player.GameObjectId (Self) porque las canciones son AoE alrededor del Bardo.
                            // No requieren target enemigo explícito.
                            ExecuteAction(am, nextSongId, player.GameObjectId, op.UseMemoryInput);
                            lastSongTime = now;
                            return;
                        }
                    }
                }
            }

            if (player.TargetObject == null) return;
            ulong mainTargetId = player.TargetObject.GameObjectId;

            if ((now - lastActionTime).TotalMilliseconds < 200) return;

            float gcdTotal = am->GetRecastTime(ActionType.Action, BRD_IDs.BurstShot);
            float gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
            float gcdRem = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            if (gcdRem <= 0.3f)
            {
                var (hasStorm, hasCaustic, stormTime, causticTime) = GetTargetDotStatus(player);
                bool ironJawsSafe = (now - lastIronJawsTime).TotalSeconds > 2.0;

                if (HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                {
                    ExecuteAction(am, BRD_IDs.BlastArrow, mainTargetId, op.UseMemoryInput);
                    return;
                }

                if (hasStorm && hasCaustic)
                {
                    float rsTime = GetStatusTime(player, BRD_IDs.Status_RagingStrikes);
                    bool isSnapshotWindow = rsTime > 0 && rsTime < 3.5f;
                    bool isFallingOff = stormTime < 6.0f || causticTime < 6.0f;

                    if ((isSnapshotWindow || isFallingOff) && ironJawsSafe)
                    {
                        Plugin.Instance.SendLog($"[GCD] Iron Jaws");
                        ExecuteAction(am, BRD_IDs.IronJaws, mainTargetId, op.UseMemoryInput);
                        UpdateState(BRD_IDs.IronJaws);
                        return;
                    }
                }
                else if (ironJawsSafe)
                {
                    if (!hasStorm) { ExecuteAction(am, BRD_IDs.Stormbite, mainTargetId, op.UseMemoryInput); return; }
                    if (!hasCaustic) { ExecuteAction(am, BRD_IDs.CausticBite, mainTargetId, op.UseMemoryInput); return; }
                }

                if (HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
                {
                    ExecuteAction(am, BRD_IDs.ResonantArrow, mainTargetId, op.UseMemoryInput);
                    return;
                }

                if (HasStatus(player, BRD_IDs.Status_RadiantEncoreReady))
                {
                    float rsCD = GetRecastTime(am, BRD_IDs.RagingStrikes);
                    float bvCD = GetRecastTime(am, BRD_IDs.BattleVoice);
                    float rfCD = GetRecastTime(am, BRD_IDs.RadiantFinale);
                    bool fullBurst = rsCD > 60 && bvCD > 60 && rfCD > 60;
                    float procTime = GetStatusTime(player, BRD_IDs.Status_RadiantEncoreReady);

                    if (fullBurst || procTime < 5.0f)
                    {
                        ExecuteAction(am, BRD_IDs.RadiantEncore, mainTargetId, op.UseMemoryInput);
                        return;
                    }
                }

                bool ragingActive = HasStatus(player, BRD_IDs.Status_RagingStrikes);
                bool useApex = (ragingActive && gauge.SoulVoice >= 80) || (gauge.SoulVoice == 100);
                if (useApex)
                {
                    ExecuteAction(am, BRD_IDs.ApexArrow, mainTargetId, op.UseMemoryInput);
                    return;
                }

                bool hasProc = HasStatus(player, BRD_IDs.Status_StraightShotReady) || HasStatus(player, BRD_IDs.Status_HawksEye);
                if (hasProc) ExecuteAction(am, BRD_IDs.RefulgentArrow, mainTargetId, op.UseMemoryInput);
                else ExecuteAction(am, BRD_IDs.BurstShot, mainTargetId, op.UseMemoryInput);
            }
            else
            {
                TryFireOgcds(am, player, mainTargetId, config);
            }
        }

        private unsafe void TryFireOgcds(ActionManager* am, IPlayerCharacter player, ulong targetId, Configuration config)
        {
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            var op = config.Operation;
            bool inMinuet = gauge.Song == Song.Wanderer;
            float songTimer = gauge.SongTimer / 1000f;

            if (inMinuet && gauge.Repertoire == 3 && CanUse(am, BRD_IDs.PitchPerfect))
            {
                ExecuteAction(am, BRD_IDs.PitchPerfect, targetId, op.UseMemoryInput);
                return;
            }

            if (op.SaveCD)
            {
                if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3 && CanUse(am, BRD_IDs.HeartbreakShot))
                    ExecuteAction(am, BRD_IDs.HeartbreakShot, targetId, op.UseMemoryInput);
                return;
            }

            if (inMinuet && songTimer < 3.5f && gauge.Repertoire > 0 && CanUse(am, BRD_IDs.PitchPerfect))
            {
                ExecuteAction(am, BRD_IDs.PitchPerfect, targetId, op.UseMemoryInput);
                return;
            }

            float rsCD = GetRecastTime(am, BRD_IDs.RagingStrikes);
            float bvCD = GetRecastTime(am, BRD_IDs.BattleVoice);
            float rfCD = GetRecastTime(am, BRD_IDs.RadiantFinale);

            bool rsStarted = rsCD > 100.0f || HasStatus(player, BRD_IDs.Status_RagingStrikes);
            bool rfFinished = rfCD > 100.0f;
            bool burstLock = inMinuet && rsStarted && !rfFinished;

            if (inMinuet)
            {
                if (op.UsePotion && op.SelectedPotionId != 0 && am->GetActionStatus(ActionType.Action, BRD_IDs.RagingStrikes) == 0)
                {
                    if (InventoryManager.IsPotionReady(am, op.SelectedPotionId))
                    {
                        Plugin.Instance.SendLog("[ITEM] Using Potion");
                        InventoryManager.UseSpecificPotion(am, op.SelectedPotionId);
                        UpdateState(0);
                        return;
                    }
                }

                if (CanUse(am, BRD_IDs.RagingStrikes))
                {
                    float burstGcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);
                    if (burstGcdElapsed < 1.0f) return;
                    ExecuteAction(am, BRD_IDs.RagingStrikes, player.GameObjectId, op.UseMemoryInput);
                    return;
                }

                if (rsStarted && CanUse(am, BRD_IDs.BattleVoice))
                {
                    ExecuteAction(am, BRD_IDs.BattleVoice, player.GameObjectId, op.UseMemoryInput);
                    return;
                }

                if (bvCD > 100.0f && CanUse(am, BRD_IDs.RadiantFinale))
                {
                    ExecuteAction(am, BRD_IDs.RadiantFinale, player.GameObjectId, op.UseMemoryInput);
                    return;
                }

                if (!burstLock && rsStarted && !HasStatus(player, BRD_IDs.Status_ResonantArrowReady) && CanUse(am, BRD_IDs.Barrage))
                {
                    ExecuteAction(am, BRD_IDs.Barrage, player.GameObjectId, op.UseMemoryInput);
                    return;
                }
            }

            if (burstLock) return;

            if (GetCharges(am, BRD_IDs.HeartbreakShot) == 3 && CanUse(am, BRD_IDs.HeartbreakShot))
            {
                ExecuteAction(am, BRD_IDs.HeartbreakShot, targetId, op.UseMemoryInput);
                return;
            }

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

            bool inPaeon = gauge.Song == Song.Army;
            bool shouldSpend = !inPaeon || inBurstMode;

            if (inPaeon && !inBurstMode)
            {
                shouldSpend = false;
                if (GetCharges(am, BRD_IDs.HeartbreakShot) == 2)
                {
                    float nextCharge = GetRecastTimeRemaining(am, BRD_IDs.HeartbreakShot);
                    float timeToMinuet = songTimer - config.Bard.SongCutoff_Paeon;
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
        // MANUAL INPUT CHECKER (LISTENER)
        // =========================================================================
        private void CheckManualInput(JobConfig_BRD config)
        {
            uint actionToQueue = 0;

            if (IsKeyPressed((VirtualKey)config.Troubadour.Key)) actionToQueue = BRD_IDs.Troubadour;
            else if (IsKeyPressed((VirtualKey)config.NaturesMinne.Key)) actionToQueue = BRD_IDs.NaturesMinne;
            else if (IsKeyPressed((VirtualKey)config.WardensPaean.Key)) actionToQueue = BRD_IDs.WardensPaean;
            else if (IsKeyPressed((VirtualKey)config.RepellingShot.Key)) actionToQueue = BRD_IDs.RepellingShot;
            else if (IsKeyPressed((VirtualKey)config.HeadGraze.Key)) actionToQueue = 7554;
            //else if (IsKeyPressed((VirtualKey)config.ArmsLength.Key)) actionToQueue = 7548;
            else if (IsKeyPressed((VirtualKey)config.Sprint.Key)) actionToQueue = 3;

            if (actionToQueue != 0)
            {
                if (!isManualInputActive)
                {
                    QueueActionId(actionToQueue);
                    isManualInputActive = true;
                }
            }
            else
            {
                isManualInputActive = false;
            }
        }

        private bool IsKeyPressed(VirtualKey key)
        {
            if ((int)key == 0) return false;
            return Plugin.KeyState[key];
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
            if (actionId == 3) am->UseAction(ActionType.GeneralAction, 3, targetId);
            else am->UseAction(ActionType.Action, actionId, targetId);
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
