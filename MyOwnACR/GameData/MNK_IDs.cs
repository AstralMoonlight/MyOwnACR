namespace MyOwnACR.GameData
{
    public static class MNK_IDs
    {
        // =========================================================================
        // 1. COMBO SINGLE TARGET (GCD)
        // =========================================================================
        public const uint Bootshine = 53;         // -> Leaping Opo (36945)
        public const uint DragonKick = 74;
        public const uint TrueStrike = 54;        // -> Rising Raptor (36946)
        public const uint TwinSnakes = 61;
        public const uint SnapPunch = 56;         // -> Pouncing Coeurl (36947)
        public const uint Demolish = 66;

        // IDs Evolucionados (Solo referencia, el juego ajusta)
        public const uint LeapingOpo = 36945;
        public const uint RisingRaptor = 36946;
        public const uint PouncingCoeurl = 36947;

        // =========================================================================
        // 2. COMBO AOE (GCD)
        // =========================================================================
        public const uint ArmOfTheDestroyer = 62;      // -> Shadow of the Destroyer (25767)
        public const uint FourPointFury = 16473;
        public const uint Rockbreaker = 70;
        public const uint ShadowOfTheDestroyer = 25767;

        // =========================================================================
        // 3. MASTERFUL BLITZ
        // =========================================================================
        public const uint MasterfulBlitz = 25764; // Botón Genérico -> Se transforma en todo

        // IDs Reales de Blitz (Referencias)
        //public const uint ElixirField = 25765;
        public const uint RisingPhoenix = 25768;
        public const uint PhantomRush = 25769;
        public const uint ElixirBurst = 36948;
        public const uint CelestialRevolution = 25765;

        // =========================================================================
        // 4. OFF-GLOBALS (OGCD)
        // =========================================================================

        // CHAKRAS
        // CORRECCIÓN CRÍTICA: ID Base para "Meditation" a nivel alto no es 3546.
        // Según tu log es 36942 (Forbidden Meditation).
        // Usaremos 3546 como fallback, pero la lógica priorizará el ajustado.
        public const uint SteeledMeditation = 36940;
        public const uint ForbiddenMeditation = 36942; // ID Base real a nivel alto

        public const uint TheForbiddenChakra = 3547;   // Ataque (5 Stacks)
        public const uint Enlightenment = 16474;       // AoE (5 Stacks)

        // BUFFS
        public const uint RiddleOfFire = 7395;
        public const uint Brotherhood = 7396;
        public const uint RiddleOfWind = 25766;
        public const uint PerfectBalance = 69;
        public const uint RiddleOfEarth = 7394;
        public const uint Mantra = 65;
        public const uint FormShift = 4262;
        public const uint Thunderclap = 25762;
        public const uint SixSidedStar = 16476;

        // FOLLOW-UPS (Nivel 100)
        public const uint FiresReply = 36950; // Corregido según log (36972 era incorrecto?)
        public const uint WindsReply = 36949; // Corregido según log (36971 era incorrecto?)

        // =========================================================================
        // STATUSES
        // =========================================================================
        public const ushort Status_PerfectBalance = 110;
        public const ushort Status_RiddleOfFire = 1181;
        public const ushort Status_RiddleOfEarth = 1179;
        public const ushort Status_RiddleOfWind = 2687;
        public const ushort Status_Brotherhood = 1185;
        public const ushort Status_FormlessFist = 2513;
        public const ushort Status_OpoOpoForm = 107;
        public const ushort Status_RaptorForm = 108;
        public const ushort Status_CoeurlForm = 109;
        public const ushort Status_FiresRumination = 3843;
        public const ushort Status_WindsRumination = 3842;
        public const ushort Status_Mantra = 102;
    }
}
