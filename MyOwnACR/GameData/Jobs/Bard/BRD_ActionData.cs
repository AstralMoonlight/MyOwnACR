using MyOwnACR.GameData.Common;
using MyOwnACR.Logic.Common;
using MyOwnACR.Logic.Core;

// Archivo: GameData/BRD_ActionData.cs
// CORRECCIÓN: Los nombres (Strings) ahora coinciden con los KeyName del JSON (Sin espacios).

namespace MyOwnACR.GameData.Jobs.Bard
{
    public static class BRD_ActionData
    {
        public static void Initialize()
        {
            Plugin.Log.Info("--- INICIALIZANDO DATOS DE BARDO ---");

            // =========================================================================
            // GENERAL
            // =========================================================================
            Register(All_IDs.Sprint, "Sprint", ActionCooldownType.GCD);
            Register(All_IDs.LimitBreak, "LimitBreak", ActionCooldownType.GCD);

            // =========================================================================
            // GCDs - Single Target
            // =========================================================================
            Register(BRD_IDs.BurstShot, "BurstShot", ActionCooldownType.GCD);
            Register(BRD_IDs.RefulgentArrow, "RefulgentArrow", ActionCooldownType.GCD);
            
            // ¡ESTOS FALTABAN Y SON CRÍTICOS PARA EL OPENER!
            Register(BRD_IDs.Stormbite, "Stormbite", ActionCooldownType.GCD); 
            Register(BRD_IDs.CausticBite, "CausticBite", ActionCooldownType.GCD); 
            
            Register(BRD_IDs.IronJaws, "IronJaws", ActionCooldownType.GCD);
            Register(BRD_IDs.ApexArrow, "ApexArrow", ActionCooldownType.GCD);
            Register(BRD_IDs.BlastArrow, "BlastArrow", ActionCooldownType.GCD);

            // =========================================================================
            // GCDs - AoE
            // =========================================================================
            Register(BRD_IDs.Ladonsbite, "Ladonsbite", ActionCooldownType.GCD);
            Register(BRD_IDs.Shadowbite, "Shadowbite", ActionCooldownType.GCD);
            Register(BRD_IDs.QuickNock, "QuickNock", ActionCooldownType.GCD);
            Register(BRD_IDs.WideVolley, "WideVolley", ActionCooldownType.GCD);

            // Dawntrail Follow-ups
            Register(BRD_IDs.ResonantArrow, "ResonantArrow", ActionCooldownType.GCD);
            Register(BRD_IDs.RadiantEncore, "RadiantEncore", ActionCooldownType.GCD);

            // =========================================================================
            // SONGS (Nombres sin espacios para coincidir con JSON)
            // =========================================================================
            Register(BRD_IDs.WanderersMinuet, "WanderersMinuet", ActionCooldownType.oGCD, 120f);
            Register(BRD_IDs.MagesBallad, "MagesBallad", ActionCooldownType.oGCD, 120f);
            Register(BRD_IDs.ArmysPaeon, "ArmysPaeon", ActionCooldownType.oGCD, 120f);

            // =========================================================================
            // oGCDs - RECURSOS
            // =========================================================================
            Register(BRD_IDs.EmpyrealArrow, "EmpyrealArrow", ActionCooldownType.oGCD, 15f, 2);
            Register(BRD_IDs.Bloodletter, "Bloodletter", ActionCooldownType.oGCD, 15f, 3);
            Register(BRD_IDs.HeartbreakShot, "HeartbreakShot", ActionCooldownType.oGCD, 15f, 3);
            Register(BRD_IDs.RainOfDeath, "RainOfDeath", ActionCooldownType.oGCD, 15f, 3);
            Register(BRD_IDs.Sidewinder, "Sidewinder", ActionCooldownType.oGCD, 60f);
            Register(BRD_IDs.PitchPerfect, "PitchPerfect", ActionCooldownType.oGCD, 1.0f);

            // =========================================================================
            // oGCDs - BUFFS (Burst Window)
            // =========================================================================
            Register(BRD_IDs.RagingStrikes, "RagingStrikes", ActionCooldownType.oGCD, 120f);
            Register(BRD_IDs.BattleVoice, "BattleVoice", ActionCooldownType.oGCD, 120f);
            Register(BRD_IDs.RadiantFinale, "RadiantFinale", ActionCooldownType.oGCD, 110f);
            Register(BRD_IDs.Barrage, "Barrage", ActionCooldownType.oGCD, 120f);

            // =========================================================================
            // UTILIDAD
            // =========================================================================
            Register(BRD_IDs.WardensPaean, "WardensPaean", ActionCooldownType.oGCD, 45f);
            Register(BRD_IDs.NaturesMinne, "NaturesMinne", ActionCooldownType.oGCD, 120f);
            Register(BRD_IDs.Troubadour, "Troubadour", ActionCooldownType.oGCD, 120f);
            Register(BRD_IDs.RepellingShot, "RepellingShot", ActionCooldownType.oGCD, 30f);

            // =========================================================================
            // ROLE ACTIONS
            // =========================================================================
            Register(Melee_IDs.SecondWind, "SecondWind", ActionCooldownType.oGCD, 120f);
            Register(Melee_IDs.ArmsLength, "ArmsLength", ActionCooldownType.oGCD, 120f);
            Register(7551, "HeadGraze", ActionCooldownType.oGCD, 30f);
            Register(7553, "FootGraze", ActionCooldownType.oGCD, 30f);
            Register(7554, "LegGraze", ActionCooldownType.oGCD, 30f);
            Register(7557, "Peloton", ActionCooldownType.oGCD, 5f);
        }

        private static void Register(uint id, string name, ActionCooldownType type, float cd = 0f, int maxCharges = 0)
        {
            // Usamos tu método existente que soporta ActionInfo
            ActionLibrary.Register(new ActionInfo(id, name, type, cd));
            // Si tu ActionLibrary usa "Register" con ActionInfo, usa esa línea en su lugar.
            // Pero asegúrate de que el STRING 'name' sea el que se guarda en el diccionario.
        }
    }
}
