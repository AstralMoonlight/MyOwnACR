// Archivo: Logic/Jobs/Bard/Skills/BarrageLogic.cs
// Descripción: Gestión de Barrage.
// Lógica: Solo usar bajo Raging Strikes y SIN sobreescribir procs existentes.

using Dalamud.Game.ClientState.Objects.Types;
using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;

using Dalamud.Game.ClientState.Objects.SubKinds;



namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class BarrageLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx, IPlayerCharacter player)
        {
            // 1. Chequeo Básico (Nivel y Cooldown)
            if (player.Level < BRD_Levels.Barrage) return null;
            if (ctx.BarrageCD > 0.6f) return null;

            // 2. Alineación con Burst
            // Barrage es tan fuerte que SIEMPRE debe ir dentro de Raging Strikes.
            if (!ctx.IsRagingStrikesActive) return null;

            // 3. Protección de Procs (IMPORTANTE)
            // Barrage nos regala un proc de "Straight Shot Ready".
            // Si YA tenemos ese proc (o el de Shadowbite), debemos gastarlo con un GCD antes de usar Barrage.
            // De lo contrario, "chancamos" (overwrite) el proc y perdemos daño.

            bool hasRefulgentProc = Helpers.HasStatus(player, BRD_IDs.Status_StraightShotReady);
            bool hasShadowbiteProc = Helpers.HasStatus(player, BRD_IDs.Status_ShadowbiteReady);

            // Excepción: Si Raging Strikes está a punto de morir (< 1s), lo tiramos igual por pánico.
            // Pero en condiciones normales, si hay proc, esperamos al siguiente turno.
            if ((hasRefulgentProc || hasShadowbiteProc) && ctx.RagingStrikesTimeLeft > 2.0f)
            {
                return null;
            }

            // 4. Ejecución
            // Usamos WeaveSlot.Any para que entre donde sea necesario.
            return new OgcdPlan(BRD_IDs.Barrage, WeavePriority.High, WeaveSlot.Any);
        }
    }
}
