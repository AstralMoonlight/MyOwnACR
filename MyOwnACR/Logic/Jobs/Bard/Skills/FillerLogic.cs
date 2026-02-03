// Archivo: Logic/Jobs/Bard/Skills/FillerLogic.cs
// VERSIÓN: V2.1 - LEVEL CHECK FIX
// DESCRIPCIÓN: Selección de ataques básicos respetando niveles y procs.

using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.GameData;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class FillerLogic
    {
        public static uint GetGcd(IPlayerCharacter player, bool useAoE)
        {
            // -----------------------------------------------------------------
            // 1. MODO ÁREA (AoE)
            // -----------------------------------------------------------------
            if (useAoE)
            {
                // Shadowbite (Nvl 72+) requiere proc
                if (player.Level >= BRD_Levels.Shadowbite && Helpers.HasStatus(player, BRD_IDs.Status_ShadowbiteReady))
                {
                    return BRD_IDs.Shadowbite;
                }

                // Ladonsbite (Nvl 82+) vs Quick Nock
                return (player.Level >= BRD_Levels.Ladonsbite) ? BRD_IDs.Ladonsbite : BRD_IDs.QuickNock;
            }

            // -----------------------------------------------------------------
            // 2. MODO SINGLE TARGET
            // -----------------------------------------------------------------

            // Verificamos Procs (Straight Shot Ready / Hawk's Eye)
            bool hasRefulgentProc = Helpers.HasStatus(player, BRD_IDs.Status_StraightShotReady) ||
                                    Helpers.HasStatus(player, BRD_IDs.Status_HawksEye);

            if (hasRefulgentProc)
            {
                // CORRECCIÓN: Validamos si tenemos nivel para Refulgent Arrow (70).
                // Si no, usamos Straight Shot.
                return (player.Level >= BRD_Levels.RefulgentArrow) ? BRD_IDs.RefulgentArrow : BRD_IDs.StraightShot;
            }

            // Combo básico: Burst Shot (76+) vs Heavy Shot
            return (player.Level >= BRD_Levels.BurstShot) ? BRD_IDs.BurstShot : BRD_IDs.HeavyShot;
        }
    }
}
