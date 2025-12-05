using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR;

namespace MyOwnACR.Logic
{
    public static class Survival
    {
        // IDs de Role Actions (Compartidas por todos los Melee)
        private const uint ID_SecondWind = 7541;
        private const uint ID_Bloodbath = 7542;

        public unsafe static bool Execute(ActionManager* am, IPlayerCharacter player, SurvivalConfig settings, KeyBind keySecondWind, KeyBind keyBloodbath)
        {
            // 1. Chequeo Maestro: ¿Está activado el módulo?
            if (!settings.Enabled) return false;

            // Calculamos el % de vida actual (0 a 100)
            float hpPercent = ((float)player.CurrentHp / player.MaxHp) * 100;

            // 2. Lógica de Second Wind
            if (hpPercent < settings.MinHp_SecondWind)
            {
                if (CanUse(am, ID_SecondWind))
                {
                    InputSender.Send(keySecondWind.Key, keySecondWind.Bar);
                    return true; // Accion realizada
                }
            }

            // 3. Lógica de Bloodbath
            if (hpPercent < settings.MinHp_Bloodbath)
            {
                if (CanUse(am, ID_Bloodbath))
                {
                    InputSender.Send(keyBloodbath.Key, keyBloodbath.Bar);
                    return true;
                }
            }

            return false; // No se hizo nada de supervivencia
        }

        private unsafe static bool CanUse(ActionManager* am, uint id)
        {
            return am->GetRecastTime(ActionType.Action, id) == 0;
        }
    }
}
