using System.Collections.Generic;

namespace MyOwnACR.GameData.Common
{
    public class JobInfo
    {
        public uint Id { get; }
        public string Name { get; }
        public string Abbreviation { get; }

        public JobInfo(uint id, string name, string abbr)
        {
            Id = id;
            Name = name;
            Abbreviation = abbr;
        }
    }

    public static class JobDefinitions
    {
        // =========================================================================
        // CONSTANTES DE ID (uint)
        // =========================================================================

        public const uint ADV = 0; // Adventurer

        // --- CLASES BASE (Disciples of War/Magic) ---
        public const uint GLA = 1;  // Gladiator -> PLD
        public const uint PGL = 2;  // Pugilist -> MNK
        public const uint MRD = 3;  // Marauder -> WAR
        public const uint LNC = 4;  // Lancer -> DRG
        public const uint ARC = 5;  // Archer -> BRD
        public const uint CNJ = 6;  // Conjurer -> WHM
        public const uint THM = 7;  // Thaumaturge -> BLM
        public const uint ACN = 26; // Arcanist -> SMN/SCH
        public const uint ROG = 29; // Rogue -> NIN

        // --- DISCIPLES OF THE HAND (Crafters) ---
        public const uint CRP = 8;  // Carpenter
        public const uint BSM = 9;  // Blacksmith
        public const uint ARM = 10; // Armorer
        public const uint GSM = 11; // Goldsmith
        public const uint LTW = 12; // Leatherworker
        public const uint WVR = 13; // Weaver
        public const uint ALC = 14; // Alchemist
        public const uint CUL = 15; // Culinarian

        // --- DISCIPLES OF THE LAND (Gatherers) ---
        public const uint MIN = 16; // Miner
        public const uint BTN = 17; // Botanist
        public const uint FSH = 18; // Fisher

        // --- JOBS (Soul Crystal) ---
        public const uint PLD = 19;
        public const uint MNK = 20;
        public const uint WAR = 21;
        public const uint DRG = 22;
        public const uint BRD = 23;
        public const uint WHM = 24;
        public const uint BLM = 25;
        public const uint SMN = 27;
        public const uint SCH = 28;
        public const uint NIN = 30;
        public const uint MCH = 31;
        public const uint DRK = 32;
        public const uint AST = 33;
        public const uint SAM = 34;
        public const uint RDM = 35;
        public const uint BLU = 36; // Limited
        public const uint GNB = 37;
        public const uint DNC = 38;
        public const uint RPR = 39;
        public const uint SGE = 40;
        public const uint VPR = 41;
        public const uint PCT = 42;

        // =========================================================================
        // DICCIONARIO DE METADATOS
        // =========================================================================
        public static readonly Dictionary<uint, JobInfo> Jobs = new()
        {
            // Adventurer
            { ADV, new JobInfo(ADV, "Adventurer", "ADV") },

            // Base Classes
            { GLA, new JobInfo(GLA, "Gladiator", "GLA") },
            { PGL, new JobInfo(PGL, "Pugilist", "PGL") },
            { MRD, new JobInfo(MRD, "Marauder", "MRD") },
            { LNC, new JobInfo(LNC, "Lancer", "LNC") },
            { ARC, new JobInfo(ARC, "Archer", "ARC") },
            { CNJ, new JobInfo(CNJ, "Conjurer", "CNJ") },
            { THM, new JobInfo(THM, "Thaumaturge", "THM") },
            { ACN, new JobInfo(ACN, "Arcanist", "ACN") },
            { ROG, new JobInfo(ROG, "Rogue", "ROG") },

            // Crafters
            { CRP, new JobInfo(CRP, "Carpenter", "CRP") },
            { BSM, new JobInfo(BSM, "Blacksmith", "BSM") },
            { ARM, new JobInfo(ARM, "Armorer", "ARM") },
            { GSM, new JobInfo(GSM, "Goldsmith", "GSM") },
            { LTW, new JobInfo(LTW, "Leatherworker", "LTW") },
            { WVR, new JobInfo(WVR, "Weaver", "WVR") },
            { ALC, new JobInfo(ALC, "Alchemist", "ALC") },
            { CUL, new JobInfo(CUL, "Culinarian", "CUL") },

            // Gatherers
            { MIN, new JobInfo(MIN, "Miner", "MIN") },
            { BTN, new JobInfo(BTN, "Botanist", "BTN") },
            { FSH, new JobInfo(FSH, "Fisher", "FSH") },

            // Combat Jobs
            { PLD, new JobInfo(PLD, "Paladin", "PLD") },
            { MNK, new JobInfo(MNK, "Monk", "MNK") },
            { WAR, new JobInfo(WAR, "Warrior", "WAR") },
            { DRG, new JobInfo(DRG, "Dragoon", "DRG") },
            { BRD, new JobInfo(BRD, "Bard", "BRD") },
            { WHM, new JobInfo(WHM, "White Mage", "WHM") },
            { BLM, new JobInfo(BLM, "Black Mage", "BLM") },
            { SMN, new JobInfo(SMN, "Summoner", "SMN") },
            { SCH, new JobInfo(SCH, "Scholar", "SCH") },
            { NIN, new JobInfo(NIN, "Ninja", "NIN") },
            { MCH, new JobInfo(MCH, "Machinist", "MCH") },
            { DRK, new JobInfo(DRK, "Dark Knight", "DRK") },
            { AST, new JobInfo(AST, "Astrologian", "AST") },
            { SAM, new JobInfo(SAM, "Samurai", "SAM") },
            { RDM, new JobInfo(RDM, "Red Mage", "RDM") },
            { GNB, new JobInfo(GNB, "Gunbreaker", "GNB") },
            { DNC, new JobInfo(DNC, "Dancer", "DNC") },
            { RPR, new JobInfo(RPR, "Reaper", "RPR") },
            { SGE, new JobInfo(SGE, "Sage", "SGE") },
            { VPR, new JobInfo(VPR, "Viper", "VPR") },
            { PCT, new JobInfo(PCT, "Pictomancer", "PCT") },
            { BLU, new JobInfo(BLU, "Blue Mage", "BLU") }
        };

        public static string GetName(uint id) => Jobs.TryGetValue(id, out var job) ? job.Name : "Unknown";
        public static string GetAbbr(uint id) => Jobs.TryGetValue(id, out var job) ? job.Abbreviation : "???";
    }
}
