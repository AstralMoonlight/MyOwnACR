using Dalamud.Configuration;
using Dalamud.Plugin;
using MyOwnACR.JobConfigs;
using System;

namespace MyOwnACR
{
    [Serializable]
    public class KeyBind
    {
        public byte Key { get; set; }
        public HotbarType Bar { get; set; }
        public KeyBind(byte k, HotbarType b) { Key = k; Bar = b; }
        public KeyBind() { }
    }

    // --- CONFIGURACIÓN DE SUPERVIVENCIA ---
    [Serializable]
    public class SurvivalConfig
    {
        public bool Enabled { get; set; } = false; // Toggle Maestro

        public int MinHp_SecondWind { get; set; } = 40;
        public int MinHp_Bloodbath { get; set; } = 60;
    }

    // --- AJUSTES OPERATIVOS Y TOGGLES DE COMBATE ---
    [Serializable]
    public class OperationalSettings
    {
        // Opciones Generales
        public bool AoE_Enabled { get; set; } = true;
        public bool TrueNorth_Auto { get; set; } = false;
        public bool SixSidedStar_Use { get; set; } = false;

        // Nuevos Toggles para el Dashboard (Gestión de Cooldowns)
        public bool SaveCD { get; set; } = false; // Bloqueo maestro de CDs (para fases de downtime)

        public bool UsePB { get; set; } = true;              // Perfect Balance
        public bool UseRoF { get; set; } = true;             // Riddle of Fire
        public bool UseRoW { get; set; } = true;             // Riddle of Wind
        public bool UseBrotherhood { get; set; } = true;     // Brotherhood
        public bool UseForbiddenChakra { get; set; } = true; // The Forbidden Chakra
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // Configuración por Job
        public JobConfig_MNK Monk = new JobConfig_MNK();

        // Configuración Global
        public SurvivalConfig Survival = new SurvivalConfig();
        public OperationalSettings Operation = new OperationalSettings();

        [NonSerialized] private IDalamudPluginInterface? pluginInterface;
        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
        public void Save() => this.pluginInterface!.SavePluginConfig(this);
    }
}
