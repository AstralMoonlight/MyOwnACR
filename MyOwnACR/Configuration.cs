using Dalamud.Configuration;
using Dalamud.Plugin;
using SamplePlugin.JobConfigs;
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

    // --- NUEVA CONFIGURACIÓN DE SUPERVIVENCIA ---
    [Serializable]
    public class SurvivalConfig
    {
        public bool Enabled { get; set; } = false; // Toggle Maestro (Por defecto apagado para Ultimate)

        public int MinHp_SecondWind { get; set; } = 40; // Usar al 40% HP
        public int MinHp_Bloodbath { get; set; } = 60;  // Usar al 60% HP
    }
    // Nueva clase para ajustes operativos
    [Serializable]
    public class OperationalSettings
    {
        public bool AoE_Enabled { get; set; } = true; // Toggle Maestro de AoE (Por defecto ON)
        public bool TrueNorth_Auto { get; set; } = false; // Auto True North (Por defecto OFF para Ultimates)
        public bool SixSidedStar_Use { get; set; } = false; // Usar Six Sided Star (Por defecto OFF)
    }


    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // Configuración por Job
        public JobConfig_MNK Monk = new JobConfig_MNK();

        // Configuración Global de Supervivencia
        public SurvivalConfig Survival = new SurvivalConfig();
        public OperationalSettings Operation = new OperationalSettings();

        [NonSerialized] private IDalamudPluginInterface? pluginInterface;
        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
        public void Save() => this.pluginInterface!.SavePluginConfig(this);
    }
}
