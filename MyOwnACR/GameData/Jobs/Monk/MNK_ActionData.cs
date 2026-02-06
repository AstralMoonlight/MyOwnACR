// Archivo: GameData/MNK_ActionData.cs
// Descripción: Registro de todas las habilidades de Monk con sus metadatos.
// CAMBIO: Uso de ActionCooldownType.

using MyOwnACR.Logic.Common;

namespace MyOwnACR.GameData.Jobs.Monk
{
    public static class MNK_ActionData
    {
        public static void Initialize()
        {
            // --- GCDs (Global Cooldown) ---
            Register(MNK_IDs.Bootshine, "Bootshine", ActionCooldownType.GCD);
            Register(MNK_IDs.DragonKick, "Dragon Kick", ActionCooldownType.GCD);
            Register(MNK_IDs.TrueStrike, "True Strike", ActionCooldownType.GCD);
            Register(MNK_IDs.TwinSnakes, "Twin Snakes", ActionCooldownType.GCD);
            Register(MNK_IDs.SnapPunch, "Snap Punch", ActionCooldownType.GCD);
            Register(MNK_IDs.Demolish, "Demolish", ActionCooldownType.GCD);
            Register(MNK_IDs.SixSidedStar, "Six-sided Star", ActionCooldownType.GCD, 4.0f);

            // AoE
            Register(MNK_IDs.ArmOfTheDestroyer, "Arm of the Destroyer", ActionCooldownType.GCD);
            Register(MNK_IDs.ShadowOfTheDestroyer, "Shadow of the Destroyer", ActionCooldownType.GCD);
            Register(MNK_IDs.FourPointFury, "Four-point Fury", ActionCooldownType.GCD);
            Register(MNK_IDs.Rockbreaker, "Rockbreaker", ActionCooldownType.GCD);

            // Blitz / Masterful
            Register(MNK_IDs.MasterfulBlitz, "Masterful Blitz", ActionCooldownType.GCD);
            Register(MNK_IDs.ElixirBurst, "Elixir Burst", ActionCooldownType.GCD);
            Register(MNK_IDs.RisingPhoenix, "Rising Phoenix", ActionCooldownType.GCD);
            Register(MNK_IDs.PhantomRush, "Phantom Rush", ActionCooldownType.GCD);
            Register(MNK_IDs.CelestialRevolution, "Celestial Revolution", ActionCooldownType.GCD);

            // Dawntrail Follow-ups
            Register(MNK_IDs.FiresReply, "Fire's Reply", ActionCooldownType.GCD);
            Register(MNK_IDs.WindsReply, "Wind's Reply", ActionCooldownType.GCD);

            // Utility GCDs
            Register(MNK_IDs.SteeledMeditation, "SteeledMeditation", ActionCooldownType.GCD, 1.0f);
            Register(MNK_IDs.FormShift, "Form Shift", ActionCooldownType.GCD);


            // --- oGCDs (Off-Global Cooldown) ---
            Register(MNK_IDs.TheForbiddenChakra, "The Forbidden Chakra", ActionCooldownType.oGCD, 1.0f);
            Register(MNK_IDs.Enlightenment, "Enlightenment", ActionCooldownType.oGCD, 1.0f);
            Register(MNK_IDs.RiddleOfFire, "Riddle of Fire", ActionCooldownType.oGCD, 60f);
            Register(MNK_IDs.RiddleOfWind, "Riddle of Wind", ActionCooldownType.oGCD, 90f);
            Register(MNK_IDs.Brotherhood, "Brotherhood", ActionCooldownType.oGCD, 120f);
            Register(MNK_IDs.PerfectBalance, "Perfect Balance", ActionCooldownType.oGCD, 40f);

            // Defensive / Utility
            Register(MNK_IDs.RiddleOfEarth, "Riddle of Earth", ActionCooldownType.oGCD, 120f);
            Register(MNK_IDs.Mantra, "Mantra", ActionCooldownType.oGCD, 90f);
            Register(MNK_IDs.Thunderclap, "Thunderclap", ActionCooldownType.oGCD, 30f);

            // Role Actions
            Register(7541, "Second Wind", ActionCooldownType.oGCD, 120f);
            Register(7542, "Bloodbath", ActionCooldownType.oGCD, 90f);
            Register(7546, "True North", ActionCooldownType.oGCD, 45f);
            Register(7548, "Arm's Length", ActionCooldownType.oGCD, 120f);
            Register(7549, "Feint", ActionCooldownType.oGCD, 90f);
            Register(7557, "Leg Sweep", ActionCooldownType.oGCD, 40f);
        }

        private static void Register(uint id, string name, ActionCooldownType type, float cd = 0f)
        {
            ActionLibrary.Register(new ActionInfo(id, name, type, cd));
        }
    }
}
