// Archivo: GameData/Melee_IDs.cs
// Descripción: IDs de Acciones de Rol (Role Actions) compartidas por todos los DPS Melee.
// Actualizado para Dawntrail (7.0).

namespace MyOwnACR.GameData.Common
{
    public static class Melee_IDs
    {
        // =========================================================================
        // ROLE ACTIONS (Botones)
        // =========================================================================

        /// <summary>
        /// Recupera HP instantáneamente. (Recast: 120s)
        /// </summary>
        public const uint SecondWind = 7541;

        /// <summary>
        /// Aturde al objetivo. (Recast: 40s)
        /// </summary>
        public const uint LegSweep = 7863;

        /// <summary>
        /// Convierte parte del daño físico infligido en HP. (Recast: 90s)
        /// </summary>
        public const uint Bloodbath = 7542;

        /// <summary>
        /// Reduce el daño físico infligido por el objetivo. (Recast: 90s)
        /// </summary>
        public const uint Feint = 7549;

        /// <summary>
        /// Crea una barrera que previene la mayoría de efectos de empuje y atracción.
        /// También aplica Slow a los atacantes físicos. (Recast: 120s)
        /// </summary>
        public const uint ArmsLength = 7548;

        /// <summary>
        /// Permite ejecutar acciones sin requisitos direccionales. (Recast: 45s, 2 Cargas)
        /// </summary>
        public const uint TrueNorth = 7546;

        // =========================================================================
        // STATUSES (Buffs) - IDs de Estado (ushort)
        // =========================================================================

        /// <summary>
        /// Buff activo de True North (Ignora posicionales).
        /// </summary>
        public const ushort Status_TrueNorth = 1250;

        /// <summary>
        /// Buff activo de Bloodbath (Robo de vida).
        /// </summary>
        public const ushort Status_Bloodbath = 490;

        /// <summary>
        /// Debuff aplicado al enemigo por Feint (Daño reducido).
        /// </summary>
        public const ushort Status_Feint = 1195;

        /// <summary>
        /// Buff activo de Arm's Length (Anti-knockback).
        /// </summary>
        public const ushort Status_ArmsLength = 1209;
    }
}
