using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Keys; // <--- NECESARIO PARA VirtualKey
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
        public bool AoE_Enabled { get; set; } = true;
        public bool TrueNorth_Auto { get; set; } = false;
        public bool SixSidedStar_Use { get; set; } = false;

        public bool SaveCD { get; set; } = false;
        public bool UsePB { get; set; } = true;
        public bool UseRoF { get; set; } = true;
        public bool UseRoW { get; set; } = true;
        public bool UseBrotherhood { get; set; } = true;
        public bool UseForbiddenChakra { get; set; } = true;
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // --- NUEVO: HOTKEY GLOBAL (Default F8) ---
        public VirtualKey ToggleHotkey = VirtualKey.F8;

        public JobConfig_MNK Monk = new JobConfig_MNK();
        public SurvivalConfig Survival = new SurvivalConfig();
        public OperationalSettings Operation = new OperationalSettings();

        [NonSerialized] private IDalamudPluginInterface? pluginInterface;
        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
        public void Save() => this.pluginInterface!.SavePluginConfig(this);
    }
}
