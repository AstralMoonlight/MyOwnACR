// Archivo: Logic/CombatHelpers.cs
// Descripción: Utilidades matemáticas y de targeting.
// VERSION: Fix CS8604 (Nullable centerObj).

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;

namespace MyOwnACR.Logic
{
    // Enum para identificar dónde estamos parados
    public enum Position { Front, Flank, Rear, Unknown }

    public static class CombatHelpers
    {
        // =========================================================================
        // CONTEO DE ENEMIGOS
        // =========================================================================

        // FIX CS8604: Agregado '?' a 'IGameObject?' para indicar que acepta nulos explícitamente.
        public static int CountAttackableEnemiesInRange(IObjectTable objectTable, IGameObject? centerObj, float range)
        {
            // La validación interna ya existía, pero ahora la firma coincide con la realidad.
            if (centerObj == null || objectTable == null) return 0;

            int enemyCount = 0;
            Vector3 centerPos = centerObj.Position;

            foreach (IGameObject obj in objectTable)
            {
                if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
                if (obj is not IBattleNpc enemy) continue;
                if (enemy.BattleNpcKind != BattleNpcSubKind.Enemy) continue;
                if (!enemy.IsTargetable || enemy.CurrentHp <= 0) continue;

                float distance = Vector3.Distance(centerPos, enemy.Position);

                if (distance <= range + enemy.HitboxRadius)
                {
                    enemyCount++;
                }
            }

            return enemyCount;
        }

        // =========================================================================
        // CÁLCULO DE POSICIONALES
        // =========================================================================
        public static Position GetPosition(IPlayerCharacter player)
        {
            if (player == null || player.TargetObject == null) return Position.Unknown;

            var target = player.TargetObject;

            // 1. Vector y Ángulos
            float dx = player.Position.X - target.Position.X;
            float dz = player.Position.Z - target.Position.Z;
            double angleToPlayer = Math.Atan2(dx, dz);
            double targetRotation = target.Rotation;

            // 2. Diferencia relativa
            double relativeAngle = angleToPlayer - targetRotation;
            while (relativeAngle < -Math.PI) relativeAngle += 2 * Math.PI;
            while (relativeAngle > Math.PI) relativeAngle -= 2 * Math.PI;

            double absAngle = Math.Abs(relativeAngle);

            // FRONT (Frente): +/- 45 grados (0.785 rad) desde el centro
            if (absAngle <= 0.785398f) return Position.Front;

            // REAR (Espalda): El cuarto trasero (> 135 grados o 2.35 rad)
            if (absAngle >= 2.356194f) return Position.Rear;

            // Si no es Frente ni Espalda, es Flanco
            return Position.Flank;
        }
    }
}
