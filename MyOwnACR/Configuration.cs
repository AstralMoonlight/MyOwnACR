// Archivo: MyOwnACR/Configuration.cs
// Descripción: Clase principal de configuración.
// VERSION: Multi-Job Ready (MNK + BRD + SAM).

using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Keys;
using MyOwnACR.JobConfigs;
using System;

namespace MyOwnACR
{
    [Serializable]
    public class SurvivalConfig
    {
        public bool Enabled { get; set; } = false;
        public int MinHp_SecondWind { get; set; } = 40;
        public int MinHp_Bloodbath { get; set; } = 60;
    }

    [Serializable]
    public class OperationalSettings
    {
        public bool UseMemoryInput_v2 { get; set; } = true;
        public bool AoE_Enabled { get; set; } = true;
        public bool TrueNorth_Auto { get; set; } = false;
        public bool SaveCD { get; set; } = false;

        // Legacy Monk Toggles (Se mantienen por compatibilidad)
        public bool SixSidedStar_Use { get; set; } = false;
        public bool UsePB { get; set; } = true;
        public bool UseRoF { get; set; } = true;
        public bool UseRoW { get; set; } = true;
        public bool UseBrotherhood { get; set; } = true;
        public bool UseForbiddenChakra { get; set; } = true;

        public bool UsePotion { get; set; } = false;
        public uint SelectedPotionId { get; set; } = 0;

        public bool UseOpener { get; set; } = false;
        public string SelectedOpener { get; set; } = "Ninguno";
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public VirtualKey ToggleHotkey = VirtualKey.F8;

        // --- CONFIGURACIONES POR JOB ---
        public JobConfig_MNK Monk = new JobConfig_MNK();
        public JobConfig_BRD Bard = new JobConfig_BRD();

        // [NUEVO] Agregamos Samurai para corregir CS1061
        public JobConfig_SAM Samurai = new JobConfig_SAM();

        public SurvivalConfig Survival = new SurvivalConfig();
        public OperationalSettings Operation = new OperationalSettings();

        [NonSerialized] private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
        public void Save() => this.pluginInterface!.SavePluginConfig(this);
    }
}
