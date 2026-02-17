// Archivo: Logic/Core/ActionScheduler.cs
// VERSIÓN: V3.5 - POTION SUPPORT + OPENER ADVANCE
// DESCRIPCIÓN: Estrategia "Fire & Forget". Mantiene WeavePriority/OgcdPlan originales.

using System;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.Logic.Common;

using MyInventoryManager = MyOwnACR.Logic.Core.InventoryManager;

#pragma warning disable IDE1006

namespace MyOwnACR.Logic.Core
{
    // MANTENEMOS TUS DEFINICIONES ORIGINALES
    public enum WeavePriority { Low, Normal, High, Forced }
    public enum WeaveSlot { Any, SlotA, SlotB }

    public struct OgcdPlan
    {
        public uint ActionId;
        public WeavePriority Priority;
        public WeaveSlot PreferredSlot;
        public OgcdPlan(uint id, WeavePriority prio = WeavePriority.Normal, WeaveSlot slot = WeaveSlot.Any)
        {
            ActionId = id; Priority = prio; PreferredSlot = slot;
        }
    }

    public class CombatCycle
    {
        public uint GcdActionId { get; set; } = 0;
        public OgcdPlan? Ogcd1 { get; set; } = null;
        public OgcdPlan? Ogcd2 { get; set; } = null;

        public void Set(uint gcd, OgcdPlan? o1 = null, OgcdPlan? o2 = null)
        {
            GcdActionId = gcd; Ogcd1 = o1; Ogcd2 = o2;
        }
    }

    public unsafe class ActionScheduler
    {
        private const float DANGER_ZONE = 0.85f;
        private const float GCD_QUEUE_WINDOW = 0.6f;
        private const long SPAM_MS_OGCD = 20;

        private readonly Stopwatch _spamTimer = new Stopwatch();

        public CombatCycle CurrentCycle { get; private set; } = new CombatCycle();
        private OgcdPlan? injectedOgcd = null;
        public bool StopRequested { get; private set; } = false;

        private readonly IChatGui _chat;
        private readonly IDataManager _dataManager;

        public ActionScheduler(IDalamudPluginInterface pluginInterface, IChatGui chat, IDataManager dataManager)
        {
            _chat = chat;
            _dataManager = dataManager;
            _spamTimer.Start();
        }

        public void ResetCycle() { StopRequested = false; }
        public void RequestStop() { StopRequested = true; }

        public void Update(ActionManager* am, IPlayerCharacter player)
        {
            if (am == null || player == null) return;
            ulong targetId = player.TargetObject?.GameObjectId ?? player.GameObjectId;

            float totalGcd = am->GetRecastTime(ActionType.Action, 11);
            float elapsedGcd = am->GetRecastTimeElapsed(ActionType.Action, 11);
            float remainingGcd = (totalGcd > 0) ? Math.Max(0, totalGcd - elapsedGcd) : 0;
            float animLock = am->AnimationLock;

            // 2. INYECCIÓN
            if (injectedOgcd.HasValue && injectedOgcd.Value.Priority == WeavePriority.Forced)
            {
                if (animLock <= 0.3f)
                {
                    if (UseAction(am, injectedOgcd.Value.ActionId, targetId)) { injectedOgcd = null; return; }
                }
            }

            // 3. GCD
            if (remainingGcd <= GCD_QUEUE_WINDOW)
            {
                if (CurrentCycle.GcdActionId != 0)
                {
                    bool criticalZone = remainingGcd < 0.2f;
                    if (criticalZone || _spamTimer.ElapsedMilliseconds >= 20)
                    {
                        if (UseAction(am, CurrentCycle.GcdActionId, targetId))
                        {
                            _spamTimer.Restart();

                            // [NUEVO] SI EL GCD SALIÓ EXITOSAMENTE, AVISAMOS AL OPENER
                            if (OpenerManager.Instance.IsRunning) OpenerManager.Instance.AdvanceStep();
                        }
                    }
                }
                return;
            }

            // 4. WEAVING
            if (animLock > 0.1f) return;
            if (_spamTimer.ElapsedMilliseconds < SPAM_MS_OGCD) return;

            uint actionToUse = 0;
            bool consumedSlot1 = false;
            bool consumedInjected = false;

            if (injectedOgcd.HasValue)
            {
                actionToUse = ResolveOgcd(am, injectedOgcd.Value, remainingGcd);
                if (actionToUse != 0) consumedInjected = true;
            }
            else if (CurrentCycle.Ogcd1.HasValue)
            {
                actionToUse = ResolveOgcd(am, CurrentCycle.Ogcd1.Value, remainingGcd);
                if (actionToUse != 0) consumedSlot1 = true;
            }
            else if (CurrentCycle.Ogcd1 == null && CurrentCycle.Ogcd2.HasValue)
            {
                if (remainingGcd > 1.25f)
                {
                    actionToUse = ResolveOgcd(am, CurrentCycle.Ogcd2.Value, remainingGcd);
                    if (actionToUse != 0) CurrentCycle.Ogcd2 = null;
                }
            }

            if (actionToUse != 0)
            {
                if (UseAction(am, actionToUse, targetId))
                {
                    _spamTimer.Restart();

                    // [NUEVO] SI EL OGCD SALIÓ EXITOSAMENTE, AVISAMOS AL OPENER
                    if (OpenerManager.Instance.IsRunning) OpenerManager.Instance.AdvanceStep();

                    if (consumedInjected) injectedOgcd = null;
                    if (consumedSlot1)
                    {
                        CurrentCycle.Ogcd1 = null;
                        if (CurrentCycle.Ogcd2.HasValue)
                        {
                            CurrentCycle.Ogcd1 = CurrentCycle.Ogcd2;
                            CurrentCycle.Ogcd2 = null;
                        }
                    }
                }
            }
        }

        private bool UseAction(ActionManager* am, uint actionId, ulong targetId)
        {
            if (actionId > 200000) return MyInventoryManager.UseSpecificPotion(am, actionId);
            return InputSender.CastAction(actionId);
        }

        private uint ResolveOgcd(ActionManager* am, OgcdPlan plan, float remainingGcd)
        {
            bool isReady;
            if (plan.ActionId > 200000) isReady = MyInventoryManager.IsPotionReady(am, plan.ActionId);
            else isReady = am->GetActionStatus(ActionType.Action, plan.ActionId) == 0;

            if (!isReady) return 0;

            if (plan.Priority == WeavePriority.Forced) return plan.ActionId;

            float threshold = DANGER_ZONE;
            if (plan.Priority == WeavePriority.High) threshold = 0.70f;

            if (remainingGcd < threshold) return 0;
            return plan.ActionId;
        }

        public void SetNextCycle(uint gcd, OgcdPlan? ogcd1 = null, OgcdPlan? ogcd2 = null)
        {
            if (CurrentCycle.GcdActionId == gcd && IsPlanEqual(CurrentCycle.Ogcd1, ogcd1) && IsPlanEqual(CurrentCycle.Ogcd2, ogcd2)) return;
            CurrentCycle.Set(gcd, ogcd1, ogcd2);
        }

        public void InjectOgcd(uint actionId, WeavePriority prio = WeavePriority.Forced)
        {
            injectedOgcd = new OgcdPlan(actionId, prio, WeaveSlot.Any);
        }

        private bool IsPlanEqual(OgcdPlan? a, OgcdPlan? b)
        {
            if (!a.HasValue && !b.HasValue) return true;
            if (!a.HasValue || !b.HasValue) return false;
            return a.Value.ActionId == b.Value.ActionId;
        }
    }
}
