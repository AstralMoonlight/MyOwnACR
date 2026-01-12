namespace MyOwnACR.GameData
{
    public static class SAM_Levels
    {
        // --- GCDs Básicos (Single Target) ---
        public const int Hakaze = 1;
        public const int Jinpu = 4;
        public const int Enpi = 15;         // Ranged
        public const int Shifu = 18;
        public const int Gekko = 30;
        public const int Kasha = 40;
        public const int Yukikaze = 50;
        public const int Gyofu = 92;        // Upgrade de Hakaze (Dawntrail)

        // --- GCDs AoE ---
        public const int Fuga = 26;
        public const int Mangetsu = 35;
        public const int Oka = 45;
        public const int Fuko = 86;         // Upgrade de Fuga

        // --- Iaijutsu (Cast Times) ---
        public const int Iaijutsu = 30;     // Habilidad base
        public const int Higanbana = 30;
        public const int TenkaGoken = 40;
        public const int MidareSetsugekka = 50;

        // --- Kenki & oGCDs ---
        public const int HissatsuShinten = 52;
        public const int HissatsuGyoten = 54; // Gap closer
        public const int HissatsuYaten = 56;  // Backstep
        public const int HissatsuKyuten = 62; // AoE Kenki
        public const int HissatsuGuren = 70;  // AoE Big Kenki (Comparte CD con Senei)
        public const int HissatsuSenei = 72;  // ST Big Kenki

        // --- Meditación & Utility ---
        public const int ThirdEye = 6;
        public const int MeikyoShisui = 50;
        public const int Meditate = 60;
        public const int Hagakure = 68;
        public const int Ikishoten = 68;
        public const int Tengentsu = 82;      // Upgrade de Third Eye

        // --- Tsubame-gaeshi & Shoha ---
        public const int TsubameGaeshi = 74;
        public const int KaeshiGoken = 74;
        public const int KaeshiSetsugekka = 74;
        public const int Shoha = 80;
        public const int Shoha2 = 82;         // AoE version (Agregado por seguridad)

        // --- Namikiri (Level 90) ---
        public const int OgiNamikiri = 90;
        public const int KaeshiNamikiri = 90;

        // --- Dawntrail High Level (96-100) ---
        public const int Zanshin = 96;        // Follow-up de Ikishoten/Namikiri? No, gasta Kenki.
        public const int TendoGoken = 100;    // Upgrade bajo Meikyo
        public const int TendoSetsugekka = 100; // Upgrade bajo Meikyo
        public const int TendoKaeshiGoken = 100;
        public const int TendoKaeshiSetsugekka = 100;
        public const ushort Status_Tengentsu = 3853; // ID real
        public const ushort Status_Meditate = 1231;  // ID real
    }
}
