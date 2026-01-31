// Archivo: Logic/Common/Survival.cs
// Descripción: Lógica de supervivencia automática (Second Wind, Bloodbath).
// ACTUALIZADO: Usa InputSender.CastAction para unificar lógica de disparo.

using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Common
{
    public static class Survival
    {
        private static DateTime LastHealTime = DateTime.MinValue;

        public unsafe static bool Execute(
            ActionManager* am,
            IPlayerCharacter player,
            SurvivalConfig config,
            KeyBind secondWindBind,
            KeyBind bloodbathBind)
        {
            if (am == null || player == null || !config.Enabled) return false;

            // Evitar spamear curas demasiado rápido (1.5s delay global)
            if ((DateTime.Now - LastHealTime).TotalSeconds < 1.5) return false;

            float hpPercent = (float)player.CurrentHp / player.MaxHp * 100f;

            // 1. SECOND WIND
            if (hpPercent <= config.MinHp_SecondWind)
            {
                // ID Second Wind: 7541
                if (CanUse(am, 7541))
                {
                    ExecuteHeal(7541);
                    return true;
                }
            }

            // 2. BLOODBATH
            if (hpPercent <= config.MinHp_Bloodbath)
            {
                // ID Bloodbath: 7542
                if (CanUse(am, 7542))
                {
                    ExecuteHeal(7542);
                    return true;
                }
            }

            return false;
        }

        private static void ExecuteHeal(uint actionId)
        {
            // InputSender ya sabe si usar memoria o teclado según la config global
            InputSender.CastAction(actionId);

            LastHealTime = DateTime.Now;
            // Plugin.Instance.SendLog($"Survival Triggered: ID {actionId}");
        }

        private unsafe static bool CanUse(ActionManager* am, uint actionId)
        {
            return am->GetActionStatus(ActionType.Action, actionId) == 0;
        }
    }
}
