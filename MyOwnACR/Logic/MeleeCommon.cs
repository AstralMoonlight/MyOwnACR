using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.GameData; // Usamos los nuevos IDs
using System;

namespace MyOwnACR.Logic
{
    public static class MeleeCommon
    {
        // Timer de protección interno
        private static DateTime LastTNTime = DateTime.MinValue;

        public static unsafe bool HandleTrueNorth(
            ActionManager* am,
            IPlayerCharacter player,
            OperationalSettings operation,
            KeyBind trueNorthKeybind,
            Position requiredPosition,
            ref uint lastProposedAction,
            ref DateTime lastAnyActionTime
            )
        {
            if (!operation.TrueNorth_Auto) return false;

            // USAMOS Melee_IDs AHORA
            if (HasStatus(player, Melee_IDs.Status_TrueNorth)) return false;

            var now = DateTime.Now;
            if ((now - LastTNTime).TotalSeconds < 2.5) return false;

            if (requiredPosition == Position.Unknown) return false;

            var myPos = CombatHelpers.GetPosition(player);
            if (myPos == requiredPosition) return false;

            // USAMOS Melee_IDs AHORA
            bool hasCharges = am->GetCurrentCharges(Melee_IDs.TrueNorth) > 0;
            bool canPress = am->GetActionStatus(ActionType.Action, Melee_IDs.TrueNorth) == 0;

            if (hasCharges && canPress)
            {
                lastProposedAction = Melee_IDs.TrueNorth;
                InputSender.Send(trueNorthKeybind.Key, trueNorthKeybind.Bar);

                LastTNTime = now;
                lastAnyActionTime = now;

                return true;
            }

            return false;
        }

        private static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return false;
            foreach (var s in player.StatusList)
                if (s.StatusId == statusId) return true;
            return false;
        }
    }
}
