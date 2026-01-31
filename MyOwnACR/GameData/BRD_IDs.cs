    namespace MyOwnACR.GameData
    {
        public static class BRD_IDs
        {
            // --- GCDs (Single Target) ---
            public const uint HeavyShot = 97;
            public const uint StraightShot = 98;
            public const uint BurstShot = 16495;        // Base: Heavy Shot (97)
            public const uint RefulgentArrow = 7409;    // Proc: Straight Shot (98)
            public const uint IronJaws = 3560;
            public const uint ApexArrow = 16496;
            public const uint BlastArrow = 25784;       // Follow-up de Apex (No salió en logs, pero es vital)
            public const uint Windbite = 113;

            // --- GCDs (AoE) ---
            public const uint QuickNock = 106;
            public const uint Ladonsbite = 25783;       // Base: Quick Nock (106)
            public const uint Shadowbite = 16494;
            public const uint WideVolley = 115;

            // --- DoTs ---
            public const uint Stormbite = 7407;         // Base: Windbite (113)
            public const uint CausticBite = 7406;       // Base: Venomous Bite (100)
            public const uint VenomousBite = 100;

        // --- SONGS ---
        public const uint WanderersMinuet = 3559;
            public const uint MagesBallad = 114;
            public const uint ArmysPaeon = 116;

            // --- oGCDs (Damage) ---
            public const uint Bloodletter = 110;
            public const uint HeartbreakShot = 36975;   // Upgrade de Bloodletter (Dawntrail)
            public const uint RainOfDeath = 117;
            public const uint EmpyrealArrow = 3558;
            public const uint Sidewinder = 3562;
            public const uint PitchPerfect = 7404;

            // --- oGCDs (Buffs & Follow-ups) ---
            public const uint RagingStrikes = 101;
            public const uint BattleVoice = 118;

            public const uint RadiantFinale = 25785;
            public const uint RadiantEncore = 36977;    // Follow-up de Radiant Finale (Dawntrail)

            public const uint Barrage = 107;
            public const uint ResonantArrow = 36976;    // Follow-up de Barrage (Dawntrail)


            // --- Utility ---
            public const uint WardensPaean = 3561;
            public const uint Troubadour = 7405;
            public const uint NaturesMinne = 7408;
            public const uint RepellingShot = 112;

            // --- STATUS BUFFS (Para lógica) ---
            public const ushort Status_RagingStrikes = 125;
            public const ushort Status_Barrage = 128;
            public const ushort Status_BattleVoice = 141;
            public const ushort Status_RadiantFinale = 2964;
            public const ushort ArmysMuse = 1932; //AA delay as well as weaponskill and spell cast and recast time are reduced

            // Procs
            public const ushort Status_StraightShotReady = 122; // Permite Refulgent Arrow
            public const ushort Status_ShadowbiteReady = 3002;  // Permite Shadowbite
            public const ushort Status_BlastArrowReady = 2692;  // Permite Blast Arrow
            public const ushort Status_HawksEye = 3861;         // Nuevo buff de Dawntrail para refrescos garantizados
            public const ushort Status_ResonantArrowReady = 3862;     // Permite usar Resonant Arrow
            public const ushort Status_RadiantEncoreReady = 3863;     // Permite usar Radiant Encore

            // Canciones (Buffs en el jugador)
            public const ushort Status_WanderersMinuet = 2216;
            public const ushort Status_MagesBallad = 2217;
            public const ushort Status_ArmysPaeon = 2218;

            // Debuffs en Enemigo
            public const ushort Debuff_CausticBite = 1200;
            public const ushort Debuff_Stormbite = 1201;

            // --- HELPERS ---
            public static bool IsSong(uint id)
            {
                return id == WanderersMinuet || id == MagesBallad || id == ArmysPaeon;
            }
        }
    }
