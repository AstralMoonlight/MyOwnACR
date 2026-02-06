// Archivo: Logic/Jobs/Bard/Skills/BarrageLogic.cs
// DESCRIPCIÓN: Gestión de Barrage.
// Lógica: Solo usar bajo Raging Strikes y SIN sobreescribir procs existentes.

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using MyOwnACR.Logic.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class BarrageLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx, IPlayerCharacter player)
        {
            // 1. Validaciones Básicas
            if (player.Level < BRD_Levels.Barrage) return null;
            if (ctx.BarrageCD > 0.6f) return null;

            // 2. Alineación con Burst (Raging Strikes)
            // Barrage triplica el daño. Es obligatorio que esté dentro del buff de daño (+15%).
            if (!ctx.IsRagingStrikesActive) return null;

            // 3. Protección de Procs (ANTI-OVERWRITE)
            // Barrage otorga "Straight Shot Ready".
            // Si ya tenemos ese proc, debemos gastarlo con un GCD antes de activar Barrage.
            bool hasRefulgentProc = Helpers.HasStatus(player, BRD_IDs.Status_StraightShotReady);
            bool hasShadowbiteProc = Helpers.HasStatus(player, BRD_IDs.Status_ShadowbiteReady);

            // EXCEPCIÓN: Pánico
            // Si Raging Strikes está a punto de acabarse (< 1s), ignoramos la protección
            // y lanzamos Barrage igual para no perder el CD de 2 minutos.
            bool panicMode = ctx.RagingStrikesTimeLeft < 1.5f;

            if ((hasRefulgentProc || hasShadowbiteProc) && !panicMode)
            {
                // Retornamos null para que la rotación use primero el proc (Refulgent Arrow)
                // y en el siguiente ciclo activaremos Barrage.
                return null;
            }

            // 4. Ejecución
            // Barrage es prioritario dentro del burst.
            return new OgcdPlan(BRD_IDs.Barrage, WeavePriority.High, WeaveSlot.Any);
        }
    }
}
