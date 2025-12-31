// Archivo: Logic/Survival.cs
// Descripción: Lógica de supervivencia automática (Second Wind, Bloodbath).
// ACTUALIZADO: Soporte para UseMemoryInput.

using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR;

namespace MyOwnACR.Logic
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

            // Obtenemos el ajuste operativo directamente desde la instancia del plugin
            // para saber si usamos memoria o teclado.
            bool useMemory = Plugin.Instance.Config.Operation.UseMemoryInput;

            float hpPercent = (float)player.CurrentHp / player.MaxHp * 100f;

            // 1. SECOND WIND
            if (hpPercent <= config.MinHp_SecondWind)
            {
                // ID Second Wind: 7541
                if (CanUse(am, 7541))
                {
                    ExecuteHeal(am, 7541, secondWindBind, player.GameObjectId, useMemory);
                    return true;
                }
            }

            // 2. BLOODBATH
            if (hpPercent <= config.MinHp_Bloodbath)
            {
                // ID Bloodbath: 7542
                if (CanUse(am, 7542))
                {
                    ExecuteHeal(am, 7542, bloodbathBind, player.GameObjectId, useMemory);
                    return true;
                }
            }

            return false;
        }

        private unsafe static void ExecuteHeal(
            ActionManager* am,
            uint actionId,
            KeyBind bind,
            ulong playerId,
            bool useMemory)
        {
            if (useMemory)
            {
                // Inyección Directa (Target = Player)
                am->UseAction(ActionType.Action, actionId, playerId);
            }
            else
            {
                // Simulación Teclado
                InputSender.Send(bind.Key, bind.Bar, false);
            }

            LastHealTime = DateTime.Now;
            // Opcional: Plugin.Instance.SendLog($"Survival Triggered: ID {actionId}");
        }

        private unsafe static bool CanUse(ActionManager* am, uint actionId)
        {
            return am->GetActionStatus(ActionType.Action, actionId) == 0;
        }
    }
}
