// Archivo: Logic/MeleeCommon.cs
// Descripción: Lógica compartida para clases Melee (Posicionales, True North).
// ESTADO: CORREGIDO (Añadido parámetro isGCD=false para True North).

using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData;
using MyOwnACR;

namespace MyOwnACR.Logic
{
    public static class MeleeCommon
    {
        // IDs
        public const uint TrueNorth = 7546;
        public const ushort Status_TrueNorth = 1250;

        /// <summary>
        /// Maneja la lógica automática de True North.
        /// Retorna true si se usó True North (y por tanto consumió un weave/acción).
        /// </summary>
        public static unsafe bool HandleTrueNorth(
            ActionManager* am,
            IPlayerCharacter player,
            OperationalSettings op,
            KeyBind trueNorthBind,
            Position neededPos,
            ref uint lastAction,
            ref DateTime lastTime)
        {
            if (!op.TrueNorth_Auto) return false;
            if (neededPos == Position.Unknown || neededPos == Position.Front) return false;

            // Si ya tenemos el buff, no hacer nada
            if (HasStatus(player, Status_TrueNorth)) return false;

            // Verificamos si estamos en posición
            bool inPosition = IsInPosition(player, neededPos);

            // Si NO estamos en posición y True North está listo
            if (!inPosition)
            {
                // ActionType se refiere a FFXIVClientStructs (asumiendo que renombramos el nuestro a ActionCooldownType)
                if (am->GetRecastTime(ActionType.Action, TrueNorth) == 0)
                {
                    // FIX: Añadido 'false' porque True North es un oGCD
                    InputSender.Send(trueNorthBind.Key, trueNorthBind.Bar, false);

                    lastAction = TrueNorth;
                    lastTime = DateTime.Now;
                    Plugin.Instance.SendLog("Auto True North: Activado (Fuera de posición)");
                    return true;
                }
            }

            return false;
        }

        // --- Helpers de Posición ---

        private static bool IsInPosition(IPlayerCharacter player, Position needed)
        {
            if (player.TargetObject == null) return true;

            var target = player.TargetObject;
            float angle = GetTargetRelativeAngle(player, target);

            if (needed == Position.Rear)
                return angle >= 135 || angle <= -135; // Trasera (90 grados atrás)

            if (needed == Position.Flank)
                return (angle >= 45 && angle <= 135) || (angle <= -45 && angle >= -135); // Flancos

            return false;
        }

        private static float GetTargetRelativeAngle(IPlayerCharacter player, Dalamud.Game.ClientState.Objects.Types.IGameObject target)
        {
            var faceVec = new System.Numerics.Vector2((float)Math.Sin(target.Rotation), (float)Math.Cos(target.Rotation));
            var dirVec = new System.Numerics.Vector2(player.Position.X - target.Position.X, player.Position.Z - target.Position.Z);

            dirVec = System.Numerics.Vector2.Normalize(dirVec);

            float dot = (faceVec.X * dirVec.X) + (faceVec.Y * dirVec.Y);
            float det = (faceVec.X * dirVec.Y) - (faceVec.Y * dirVec.X);

            double angle = Math.Atan2(det, dot);
            return (float)(angle * (180.0 / Math.PI));
        }

        private static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return false;
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return true;
            return false;
        }
    }
}
