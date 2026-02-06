namespace MyOwnACR.GameData.Jobs.Samurai
{
    public static class SAM_IDs
    {
        // =========================================================================
        // 1. COMBO SINGLE TARGET (GCD)
        // =========================================================================
        public const uint Hakaze = 7477;
        public const uint Jinpu = 7478;
        public const uint Gekko = 7481;
        public const uint Shifu = 7479;
        public const uint Kasha = 7482;
        public const uint Yukikaze = 7480;
        public const uint Enpi = 7486;
        public const uint Gyofu = 36963;

        // =========================================================================
        // 2. COMBO AOE (GCD)
        // =========================================================================
        public const uint Fuga = 7483;
        public const uint Mangetsu = 7484;
        public const uint Oka = 7485;
        public const uint Fuko = 25780;

        // =========================================================================
        // 3. IAIJUTSU & TSUBAME (CAST / SPECIAL GCD)
        // =========================================================================
        public const uint Iaijutsu = 7867;
        public const uint TsubameGaeshi = 16483;

        // IDs Específicos
        public const uint Higanbana = 7489;
        public const uint TenkaGoken = 7488;
        public const uint MidareSetsugekka = 7487;

        public const uint TendoGoken = 36965;
        public const uint TendoSetsugekka = 36966;

        // Kaeshi IDs
        public const uint KaeshiGoken = 16485;
        public const uint KaeshiSetsugekka = 16486;
        public const uint TendoKaeshiGoken = 36967;
        public const uint TendoKaeshiSetsugekka = 36968;

        // Namikiri
        public const uint OgiNamikiri = 25781;
        public const uint KaeshiNamikiri = 25782;

        // =========================================================================
        // 4. KENKI & OGCDs (OFF-GLOBAL)
        // =========================================================================
        public const uint HissatsuShinten = 7490;
        public const uint HissatsuKyuten = 7491;    // Detectado en log
        public const uint HissatsuGyoten = 7492;
        public const uint HissatsuYaten = 7493;
        public const uint HissatsuSenei = 16481;
        public const uint HissatsuGuren = 7496;     // Detectado en log
        public const uint Shoha = 16487;
        public const uint Shoha2 = 0;               // No existe / No detectadoz

        // =========================================================================
        // 5. BUFFS & UTILITY
        // =========================================================================
        public const uint MeikyoShisui = 7499;
        public const uint Ikishoten = 16482;
        public const uint Zanshin = 36964;
        public const uint Hagakure = 7495;
        public const uint Meditate = 7497;
        public const uint ThirdEye = 7498;
        public const uint Tengentsu = 36962;
        public const uint TrueNorth = 7546;

        // =========================================================================
        // STATUSES (BUFFS) - En blanco (0) hasta confirmar
        // =========================================================================
        public const ushort Status_MeikyoShisui = 0;
        public const ushort Status_Fugetsu = 0;
        public const ushort Status_Fuka = 0;
        public const ushort Status_OgiNamikiriReady = 0;
        public const ushort Status_TsubameReady = 4216;
        public const ushort Status_TendoReady = 3856;
        public const ushort Status_ThirdEye = 0;
        public const ushort Status_Tengentsu = 3853; // ID real
        public const ushort Status_Meditate = 1231;  // ID real
        
    }
}
