using System.Collections.Generic;

namespace MyOwnACR.GameData
{
    public static class JobPotionMapping
    {
        // Diccionario: JobID -> PotionStat Recomendada
        private static readonly Dictionary<uint, PotionStat> JobMap = new()
        {
            // --- STRENGTH (Melee: Striking/Maiming + Tanks) ---
            { JobDefinitions.MNK, PotionStat.Strength },
            { JobDefinitions.DRG, PotionStat.Strength },
            { JobDefinitions.SAM, PotionStat.Strength },
            { JobDefinitions.RPR, PotionStat.Strength },
            // { JobDefinitions.VPR, PotionStat.Strength }, <--- ERROR ELIMINADO
            { JobDefinitions.PLD, PotionStat.Strength },
            { JobDefinitions.WAR, PotionStat.Strength },
            { JobDefinitions.DRK, PotionStat.Strength },
            { JobDefinitions.GNB, PotionStat.Strength },

            // --- DEXTERITY (Phys Ranged + Ninja + Viper) ---
            { JobDefinitions.NIN, PotionStat.Dexterity },
            { JobDefinitions.VPR, PotionStat.Dexterity }, // <--- CORREGIDO: Viper va aquí
            { JobDefinitions.BRD, PotionStat.Dexterity },
            { JobDefinitions.MCH, PotionStat.Dexterity },
            { JobDefinitions.DNC, PotionStat.Dexterity },

            // --- INTELLIGENCE (Casters) ---
            { JobDefinitions.BLM, PotionStat.Intelligence },
            { JobDefinitions.SMN, PotionStat.Intelligence },
            { JobDefinitions.RDM, PotionStat.Intelligence },
            { JobDefinitions.PCT, PotionStat.Intelligence },
            { JobDefinitions.BLU, PotionStat.Intelligence },

            // --- MIND (Healers) ---
            { JobDefinitions.WHM, PotionStat.Mind },
            { JobDefinitions.SCH, PotionStat.Mind },
            { JobDefinitions.AST, PotionStat.Mind },
            { JobDefinitions.SGE, PotionStat.Mind },
        };

        public static PotionStat GetMainStat(uint jobId)
        {
            return JobMap.TryGetValue(jobId, out var stat) ? stat : PotionStat.None;
        }
    }
}
