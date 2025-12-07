namespace MyOwnACR.GameData
{
    public static class MNK_IDs
    {
        // =========================================================================
        // 1. COMBO SINGLE TARGET (GCD)
        // Usa siempre la ID Base; el juego la actualiza sola.
        // =========================================================================

        // Forma 1 (Opo-Opo)
        public const uint Bootshine = 53;    // Nv 100: Leaping Opo
        public const uint LeapingOpo = 36945; //(Evolución de Bootshine)
        public const uint DragonKick = 74;    // (Opo-Opo alternativo)

        // Forma 2 (Raptor)
        public const uint TrueStrike = 54;    // Nv 100: Rising Raptor
        public const uint TwinSnakes = 61;    // Nv 100: Rising Raptor (Buff de daño)
        public const uint RisingRaptor = 36946; //(Evolución de True Strike / Twin Snakes)

        // Forma 3 (Coeurl)
        public const uint SnapPunch = 56;    // Nv 100: Pouncing Coeurl
        public const uint Demolish = 66;    // Nv 100: Pouncing Coeurl (DoT)
        public const uint PouncingCoeurl = 36947; //(Evolución de Snap Punch / Demolish)

        // =========================================================================
        // 2. COMBO AOE (GCD)
        // =========================================================================
        public const uint ArmOfTheDestroyer = 62;    // Nv 82: Shadow of the Destroyer (Forma 1)
        public const uint FourPointFury = 16473; // (Forma 2)
        public const uint Rockbreaker = 70;    // (Forma 3)
        public const uint ShadowOfTheDestroyer = 25767;

        // =========================================================================
        // 3. MASTERFUL BLITZ (GCD Especial)
        // El juego usa una ID genérica para el botón, y IDs específicas para el ataque.
        // Normalmente basta con presionar la Genérica (25764).
        // =========================================================================
        public const uint MasterfulBlitz = 25764; // El botón que presionas

        // IDs de los ataques reales (útil si quieres leer cuál va a salir)
        public const uint ElixirField = 25765; // Lunar Nadi
        public const uint RisingPhoenix = 25768; // Solar Nadi
        public const uint PhantomRush = 25763; // Solar + Lunar (El más fuerte)
        public const uint CelestialRevolution = 25761; // El ataque de "fallaste el combo"

        // =========================================================================
        // 4. OFF-GLOBALS (OGCD) - Daño y Utilidad
        // =========================================================================

        // Chakras
        public const uint TheForbiddenChakra = 3547;  // Single Target (5 Chakras)
        public const uint Enlightenment = 16474; // AoE (5 Chakras)
        public const uint Meditation = 3546;  // Cargar Chakra (fuera de combate)
        public const uint InCombatMeditation = 36970; // (Steeled Meditation)

        // Cooldowns de Daño (Buffs)
        public const uint RiddleOfFire = 7395;  // +Daño propio
        public const uint Brotherhood = 7396;  // +Daño Party + Generación Chakra
        public const uint RiddleOfWind = 25766; // +Velocidad auto-ataque

        // Follow-ups de Dawntrail (Nuevos Nivel 100)
        // Estos se activan tras usar los Riddles
        public const uint FiresReply = 36972; // Ataque tras Riddle of Fire
        public const uint WindsReply = 36971; // Ataque tras Riddle of Wind

        // Utilidad / Defensivos
        public const uint PerfectBalance = 69;    // Permite usar cualquier forma (3 stacks)
        public const uint RiddleOfEarth = 7394;  // Reducción de daño + Cura
        public const uint Mantra = 65;    // Aumenta curación recibida
        public const uint Thunderclap = 25762; // Dash (Acercarse al enemigo/aliado)
        public const uint FormShift = 4262;  // Mantener forma fuera de combate
        public const uint SixSidedStar = 16476; // GCD de salida (da velocidad de movimiento)

        // =========================================================================
        // 5. ROLE ACTIONS (Comunes a todos los Melee)
        // =========================================================================
        public const uint SecondWind = 7541;  // Autocura instantánea
        public const uint LegSweep = 7540;  // Stun
        public const uint Bloodbath = 7542;  // Robo de vida al pegar
        public const uint Feint = 7549;  // Reducir daño físico del boss
        public const uint ArmsLength = 7548;  // Anti-empuje
        public const uint TrueNorth = 7546;  // Ignorar posicionales

        // =========================================================================
        // STATUSES (BUFFS) - ushort
        // =========================================================================
        public const ushort Status_PerfectBalance = 110;
        public const ushort Status_RiddleOfFire = 1181;
        public const ushort Status_Brotherhood = 1185;
        public const ushort Status_FormlessFist = 2513;
        public const ushort Status_OpoOpoForm = 107;
        public const ushort Status_RaptorForm = 108;
        public const ushort Status_CoeurlForm = 109;
        public const ushort Status_FiresRumination = 3843; // Buff para usar Fire's Reply
        public const ushort Status_WindsRumination = 3842; // Buff para usar Wind's Reply
        public const ushort Status_TrueNorth = 1250;
    }
}
