using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Dalamud.Game.ClientState.Objects.Enums;
// Usamos el namespace donde vive AgentCountDownSettingDialog
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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

            int index = sm->GetStatusIndex(statusId);

            // Validamos rango (0-59)
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
                // [FIX] Acceso manual a la memoria para leer los stacks (Param)
                // El offset del array de status suele ser 0x8
                var basePtr = (byte*)sm;
                var statusArray = (Status*)(basePtr + 0x8);

                // Leemos el campo Param, que contiene los stacks
                return statusArray[index].Param;
            }
            return 0;
        }

        // =========================================================================
        // LECTURA DE DEBUFFS EN ENEMIGO
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

            // Iteramos los 60 slots manualmente
            for (int i = 0; i < 60; i++)
            {
                var status = statusArray[i];

                if (status.StatusId == statusId)
                {
                    // [FIX] SourceId ahora se llama SourceObject y es un struct.
                    // Lo casteamos a uint para comparar con el ID del jugador.
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

        // =========================================================================
        // [NUEVO] OBLIGATORIO PARA OPENER PRE-PULL
        // Usamos AgentCountDownSettingDialog como descubriste
        // =========================================================================
        public static float GetCountdownRemaining()
        {
            try
            {
                // Usamos el struct que encontraste
                var agent = AgentCountDownSettingDialog.Instance();

                if (agent == null) return 0;

                // Verificamos si está activo y devolvemos el tiempo
                // FieldOffset 56 = Active, FieldOffset 40 = TimeRemaining
                if (agent->Active && agent->TimeRemaining > 0)
                {
                    return agent->TimeRemaining;
                }
            }
            catch
            {
                // Silenciamos errores por si la estructura cambia en el futuro
            }

            return 0f;
        }
    }
}
