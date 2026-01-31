using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.Logic.Common;

#pragma warning disable IDE1006

namespace MyOwnACR.Logic.Core
{
    public enum WeavePriority { Normal, High, Forced }
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
        // --- CONSTANTES NOCLIPPY AGRESIVAS ---
        private const float DANGER_ZONE = 0.45f;
        private const float GCD_SPAM_WINDOW = 0.5f;
        private const float ANIM_LOCK_THRESHOLD = 0.05f;
        private const double SPAM_INTERVAL = 0.03;

        public CombatCycle CurrentCycle { get; private set; } = new CombatCycle();
        private OgcdPlan? injectedOgcd = null;
        private DateTime _lastRequestTime = DateTime.MinValue;

        private readonly IChatGui _chat;
        private readonly IDataManager _dataManager;

        public ActionScheduler(IDalamudPluginInterface pluginInterface, IChatGui chat, IDataManager dataManager)
        {
            _chat = chat;
            _dataManager = dataManager;
        }

        public void Update(ActionManager* am, IPlayerCharacter player)
        {
            if (am == null || player == null) return;
            ulong targetId = player.TargetObject?.GameObjectId ?? player.GameObjectId;

            float totalGcd = am->GetRecastTime(ActionType.Action, 11);
            float elapsedGcd = am->GetRecastTimeElapsed(ActionType.Action, 11);
            float remainingGcd = (totalGcd > 0) ? Math.Max(0, totalGcd - elapsedGcd) : 0;
            float animLock = am->AnimationLock;

            if ((DateTime.Now - _lastRequestTime).TotalSeconds < SPAM_INTERVAL) return;

            // 1. INYECCIÓN (Prioridad Absoluta)
            if (injectedOgcd.HasValue && injectedOgcd.Value.Priority == WeavePriority.Forced)
            {
                if (animLock <= ANIM_LOCK_THRESHOLD)
                {
                    if (UseAction(am, injectedOgcd.Value.ActionId, targetId)) { injectedOgcd = null; return; }
                }
            }

            // 2. GCD (Frame Perfect)
            if (remainingGcd <= GCD_SPAM_WINDOW)
            {
                if (CurrentCycle.GcdActionId != 0 && animLock <= ANIM_LOCK_THRESHOLD)
                {
                    UseAction(am, CurrentCycle.GcdActionId, targetId);
                }
                return; // Prohibido weavear en ventana de GCD
            }

            // 3. WEAVING
            if (animLock > ANIM_LOCK_THRESHOLD) return;

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
                actionToUse = ResolveOgcd(am, CurrentCycle.Ogcd2.Value, remainingGcd);
                if (actionToUse != 0) CurrentCycle.Ogcd2 = null;
            }

            if (actionToUse != 0)
            {
                if (UseAction(am, actionToUse, targetId))
                {
                    if (consumedInjected) injectedOgcd = null;
                    if (consumedSlot1)
                    {
                        CurrentCycle.Ogcd1 = null;
                        if (CurrentCycle.Ogcd2.HasValue) { CurrentCycle.Ogcd1 = CurrentCycle.Ogcd2; CurrentCycle.Ogcd2 = null; }
                    }
                }
            }
        }

        private bool UseAction(ActionManager* am, uint actionId, ulong targetId)
        {
            if (InputSender.CastAction(actionId))
            {
                _lastRequestTime = DateTime.Now;
                return true;
            }
            return false;
        }

        private uint ResolveOgcd(ActionManager* am, OgcdPlan plan, float remainingGcd)
        {
            if (am->GetActionStatus(ActionType.Action, plan.ActionId) != 0) return 0;

            // FORCED ignora Danger Zone
            if (plan.Priority == WeavePriority.Forced) return plan.ActionId;

            // Danger Zone check
            if (remainingGcd < DANGER_ZONE && plan.Priority != WeavePriority.High) return 0;

            return plan.ActionId;
        }

        public void SetNextCycle(uint gcd, OgcdPlan? ogcd1 = null, OgcdPlan? ogcd2 = null)
        {
            // Evitar re-alloc si es lo mismo
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
