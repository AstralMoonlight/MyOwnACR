// Archivo: Logic/Jobs/Bard/Helpers.cs
// Descripción: Funciones utilitarias estáticas exclusivas del Bardo.
// Namespace: MyOwnACR.Logic.Jobs.Bard (Accesible directamente desde BardRotation)

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums; // NECESARIO PARA StatusFlags
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard
{
    public static unsafe class Helpers
    {
        // =========================================================================
        // VALIDACIONES DE ESTADO (COMBATE)
        // =========================================================================

        // Verifica si el JUGADOR tiene la bandera de combate (espadas cruzadas)
        public static bool IsPlayerInCombat()
        {
            return Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
        }

        // Verifica si el TARGET específico está en combate (tiene aggro)
        public static bool IsTargetInCombat(IGameObject? target)
        {
            if (target is IBattleChara chara)
            {
                return chara.StatusFlags.HasFlag(StatusFlags.InCombat);
            }
            return false;
        }

        // Verifica la condición global: ¿Estoy peleando O le estoy pegando a algo que pelea?
        // Esto es útil para iniciar rotaciones en Dummies o bosses que no te targetean a ti.
        public static bool IsRealCombat(IPlayerCharacter player)
        {
            return IsPlayerInCombat() || IsTargetInCombat(player.TargetObject);
        }

        // =========================================================================
        // CONSULTAS DE COMBATE (DoTs)
        // =========================================================================
        public static (bool, bool, float, float) GetTargetDotStatus(IPlayerCharacter player)
        {
            if (player.TargetObject is IBattleChara targetEnemy)
            {
                bool s = false, c = false;
                float st = 0, ct = 0;

                foreach (var status in targetEnemy.StatusList)
                {
                    if (status.SourceId != player.GameObjectId) continue;

                    // Stormbite (ID normal o ID de bajo nivel 129 Windbite)
                    if (status.StatusId == BRD_IDs.Debuff_Stormbite || status.StatusId == 129)
                    {
                        s = true;
                        st = status.RemainingTime;
                    }
                    // Caustic Bite (ID normal o ID 124 Venomous Bite)
                    if (status.StatusId == BRD_IDs.Debuff_CausticBite || status.StatusId == 124)
                    {
                        c = true;
                        ct = status.RemainingTime;
                    }
                }
                return (s, c, st, ct);
            }
            return (false, false, 0, 0);
        }

        // =========================================================================
        // VALIDACIONES DE OPENER
        // =========================================================================
        public static bool CanStartOpener(ActionManager* am, IPlayerCharacter player)
        {
            // Verificamos que los CDs principales estén listos (ActionStatus == 0)
            if (am->GetActionStatus(ActionType.Action, BRD_IDs.RagingStrikes) != 0) return false;

            if (player.Level >= 50 && am->GetActionStatus(ActionType.Action, BRD_IDs.BattleVoice) != 0) return false;
            if (player.Level >= 52 && am->GetActionStatus(ActionType.Action, BRD_IDs.WanderersMinuet) != 0) return false;
            if (player.Level >= 90 && am->GetActionStatus(ActionType.Action, BRD_IDs.RadiantFinale) != 0) return false;

            return true;
        }

        public static uint GetBuffFromAction(uint actionId)
        {
            if (actionId == BRD_IDs.BattleVoice) return BRD_IDs.Status_BattleVoice;
            if (actionId == BRD_IDs.RadiantFinale) return BRD_IDs.Status_RadiantFinale;
            if (actionId == BRD_IDs.RagingStrikes) return BRD_IDs.Status_RagingStrikes;
            if (actionId == BRD_IDs.Barrage) return BRD_IDs.Status_Barrage;
            return 0;
        }

        // =========================================================================
        // UTILIDADES GENÉRICAS
        // =========================================================================
        public static bool IsKeyPressed(VirtualKey key) => (int)key != 0 && Plugin.KeyState[key];

        public static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            foreach (var s in player.StatusList)
                if (s.StatusId == statusId) return true;
            return false;
        }

        public static float GetStatusTime(IPlayerCharacter player, ushort statusId)
        {
            foreach (var s in player.StatusList)
                if (s.StatusId == statusId) return s.RemainingTime;
            return 0;
        }

        public static bool CanUse(ActionManager* am, uint id)
        {
            return am->GetActionStatus(ActionType.Action, id) == 0;
        }
    }
}
