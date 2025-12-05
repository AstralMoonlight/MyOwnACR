using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace MyOwnACR.Logic
{
    public static class CombatHelpers
    {
        public static int CountAttackableEnemiesInRange(IObjectTable objectTable, IPlayerCharacter player, float range)
        {
            if (player == null || objectTable == null)
                return 0;

            int enemyCount = 0;
            Vector3 playerPos = player.Position;

            foreach (IGameObject obj in objectTable)
            {
                // Solo NPCs de combate
                if (obj.ObjectKind != ObjectKind.BattleNpc)
                    continue;

                // Cast a IBattleNpc para acceder a HP, tipo, etc.
                if (obj is not IBattleNpc enemy)
                    continue;

                // Tipo “Enemy”
                if (enemy.BattleNpcKind != BattleNpcSubKind.Enemy)
                    continue;

                if (!enemy.IsTargetable)
                    continue;

                if (enemy.CurrentHp <= 0)
                    continue;

                float distance = Vector3.Distance(playerPos, enemy.Position);
                if (distance <= range)
                    enemyCount++;
            }

            return enemyCount;
        }
    }
}
