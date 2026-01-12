// Archivo: GameData/SAM_ActionData.cs
// Descripción: Registro de todas las habilidades de Samurai con sus metadatos.

namespace MyOwnACR.GameData
{
    public static class SAM_ActionData
    {
        public static void Initialize()
        {
            // --- GCDs (Global Cooldown - Weaponskills) ---

            // Single Target
            Register(SAM_IDs.Hakaze, "Hakaze", ActionCooldownType.GCD);
            Register(SAM_IDs.Gyofu, "Gyofu", ActionCooldownType.GCD);
            Register(SAM_IDs.Jinpu, "Jinpu", ActionCooldownType.GCD);
            Register(SAM_IDs.Gekko, "Gekko", ActionCooldownType.GCD);
            Register(SAM_IDs.Shifu, "Shifu", ActionCooldownType.GCD);
            Register(SAM_IDs.Kasha, "Kasha", ActionCooldownType.GCD);
            Register(SAM_IDs.Yukikaze, "Yukikaze", ActionCooldownType.GCD);
            Register(SAM_IDs.Enpi, "Enpi", ActionCooldownType.GCD);

            // AoE
            Register(SAM_IDs.Fuga, "Fuga", ActionCooldownType.GCD);
            Register(SAM_IDs.Fuko, "Fuko", ActionCooldownType.GCD);
            Register(SAM_IDs.Mangetsu, "Mangetsu", ActionCooldownType.GCD);
            Register(SAM_IDs.Oka, "Oka", ActionCooldownType.GCD);

            // Iaijutsu (Cast Times - GCD)
            // Registramos el ID base (Iaijutsu) y los específicos por si acaso
            Register(SAM_IDs.Iaijutsu, "Iaijutsu", ActionCooldownType.GCD);
            Register(SAM_IDs.Higanbana, "Higanbana", ActionCooldownType.GCD);
            Register(SAM_IDs.MidareSetsugekka, "Midare Setsugekka", ActionCooldownType.GCD);
            Register(SAM_IDs.TenkaGoken, "Tenka Goken", ActionCooldownType.GCD);
            // Dawntrail Iaijutsus
            Register(SAM_IDs.TendoGoken, "Tendo Goken", ActionCooldownType.GCD);
            Register(SAM_IDs.TendoSetsugekka, "Tendo Setsugekka", ActionCooldownType.GCD);

            // Namikiri (GCD)
            Register(SAM_IDs.OgiNamikiri, "Ogi Namikiri", ActionCooldownType.GCD);


            // --- oGCDs (Off-Global Cooldown - Abilities) ---

            // Kenki Spenders
            Register(SAM_IDs.HissatsuShinten, "Hissatsu: Shinten", ActionCooldownType.oGCD, 1.0f);
            Register(SAM_IDs.HissatsuKyuten, "Hissatsu: Kyuten", ActionCooldownType.oGCD, 1.0f);
            Register(SAM_IDs.HissatsuGyoten, "Hissatsu: Gyoten", ActionCooldownType.oGCD, 10f);
            Register(SAM_IDs.HissatsuYaten, "Hissatsu: Yaten", ActionCooldownType.oGCD, 10f);
            Register(SAM_IDs.HissatsuSenei, "Hissatsu: Senei", ActionCooldownType.oGCD, 120f);
            Register(SAM_IDs.HissatsuGuren, "Hissatsu: Guren", ActionCooldownType.oGCD, 120f);

            // Shoha & Zanshin
            Register(SAM_IDs.Shoha, "Shoha", ActionCooldownType.oGCD, 1.0f);
            Register(SAM_IDs.Zanshin, "Zanshin", ActionCooldownType.oGCD, 1.0f);

            // Tsubame-gaeshi (Es una Ability/oGCD con cargas, aunque repite un GCD)
            Register(SAM_IDs.TsubameGaeshi, "Tsubame-gaeshi", ActionCooldownType.oGCD, 60f);
            Register(SAM_IDs.KaeshiGoken, "Kaeshi: Goken", ActionCooldownType.oGCD);
            Register(SAM_IDs.KaeshiSetsugekka, "Kaeshi: Setsugekka", ActionCooldownType.oGCD);
            Register(SAM_IDs.TendoKaeshiGoken, "Tendo Kaeshi Goken", ActionCooldownType.oGCD);
            Register(SAM_IDs.TendoKaeshiSetsugekka, "Tendo Kaeshi Setsugekka", ActionCooldownType.oGCD);
            Register(SAM_IDs.KaeshiNamikiri, "Kaeshi: Namikiri", ActionCooldownType.oGCD);

            // Buffs & Utility
            Register(SAM_IDs.MeikyoShisui, "Meikyo Shisui", ActionCooldownType.oGCD, 55f);
            Register(SAM_IDs.Ikishoten, "Ikishoten", ActionCooldownType.oGCD, 120f);
            Register(SAM_IDs.Hagakure, "Hagakure", ActionCooldownType.oGCD, 5f);
            Register(SAM_IDs.Meditate, "Meditate", ActionCooldownType.oGCD, 60f);
            Register(SAM_IDs.ThirdEye, "Third Eye", ActionCooldownType.oGCD, 15f);
            Register(SAM_IDs.Tengentsu, "Tengentsu", ActionCooldownType.oGCD, 15f);

            // Role Actions (Melee DPS)
            Register(7541, "Second Wind", ActionCooldownType.oGCD, 120f);
            Register(7542, "Bloodbath", ActionCooldownType.oGCD, 90f);
            Register(7546, "True North", ActionCooldownType.oGCD, 45f);
            Register(7548, "Arm's Length", ActionCooldownType.oGCD, 120f);
            Register(7549, "Feint", ActionCooldownType.oGCD, 90f);
            Register(7557, "Leg Sweep", ActionCooldownType.oGCD, 40f);
        }

        private static void Register(uint id, string name, ActionCooldownType type, float cd = 0f)
        {
            // Ignorar IDs vacíos (0)
            if (id == 0) return;
            ActionLibrary.Register(new ActionInfo(id, name, type, cd));
        }
    }
}
