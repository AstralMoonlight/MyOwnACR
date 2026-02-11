using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Dalamud.Game.ClientState.Objects.Enums;

namespace MyOwnACR.Logic.Jobs.Samurai
{
    internal static unsafe class Helpers
    {
        // =========================================================================
        // LECTURA DE BUFFS
        // =========================================================================

        public static bool HasStatus(IPlayerCharacter player, uint statusId)
        {
            if (player == null) return false;
            var chara = (BattleChara*)player.Address;
            return chara->GetStatusManager()->HasStatus(statusId);
        }

        public static float GetStatusTimeLeft(IPlayerCharacter player, uint statusId)
        {
            if (player == null) return 0f;
            var sm = ((BattleChara*)player.Address)->GetStatusManager();

            // Casteamos a (int) por si acaso tu versión de la librería lo pide
            int index = sm->GetStatusIndex(statusId);

            if (index >= 0 && index < 60)
            {
                return sm->GetRemainingTime(index);
            }
            return 0f;
        }

        public static int GetStatusStacks(IPlayerCharacter player, uint statusId)
        {
            if (player == null) return 0;
            var sm = ((BattleChara*)player.Address)->GetStatusManager();
            int index = sm->GetStatusIndex(statusId);

            if (index >= 0 && index < 60)
            {
                var basePtr = (byte*)sm;
                var statusArray = (Status*)(basePtr + 0x8);
                return statusArray[index].Param;
            }
            return 0;
        }

        // =========================================================================
        // [MODIFICADO] LECTURA DE DEBUFFS EN ENEMIGO
        // =========================================================================
        public static float GetDebuffTimeLeft(IBattleChara target, uint statusId, uint sourceId)
        {
            if (target == null) return 0f;
            var chara = (BattleChara*)target.Address;
            if (chara == null) return 0f;

            var sm = chara->GetStatusManager();

            // Punteros manuales al array de estados (Offset 0x8)
            var basePtr = (byte*)sm;
            var statusArray = (Status*)(basePtr + 0x8);

            // Iteramos los 60 slots
            for (int i = 0; i < 60; i++)
            {
                var status = statusArray[i];

                // Verificamos el ID del status
                if (status.StatusId == statusId)
                {
                    // AJUSTE CRÍTICO: 'SourceObject' es un struct GameObjectId.
                    // Necesitamos castearlo a (uint) para compararlo con sourceId.
                    // (La mayoría de structs GameObjectId permiten casting explícito).
                    if ((uint)status.SourceObject == sourceId)
                    {
                        return status.RemainingTime;
                    }
                }
            }
            return 0f;
        }

        // =========================================================================
        // UTILS
        // =========================================================================

        public static float GetCooldown(ActionManager* am, uint actionId)
        {
            if (am == null) return 0f;
            float total = am->GetRecastTime(ActionType.Action, actionId);
            float elapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
            return Math.Max(0, total - elapsed);
        }

        public static bool IsRealCombat(IPlayerCharacter player)
        {
            if (player == null) return false;
            if (player.StatusFlags.HasFlag(StatusFlags.InCombat)) return true;
            if (player.TargetObject is IBattleChara targetEnemy)
                return targetEnemy.StatusFlags.HasFlag(StatusFlags.InCombat);
            return false;
        }
    }
}
