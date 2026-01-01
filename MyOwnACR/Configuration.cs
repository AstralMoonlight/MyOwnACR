// Archivo: MyOwnACR/Configuration.cs
// Descripción: Clase principal de configuración. Actualizada para incluir Toggle de Input de Memoria.

using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Keys;
using MyOwnACR.JobConfigs;
using System;

namespace MyOwnACR
{
    /// <summary>
    /// Configuración para lógica de auto-supervivencia (pociones, skills de cura).
    /// </summary>
    [Serializable]
    public class SurvivalConfig
    {
        public bool Enabled { get; set; } = false;
        public int MinHp_SecondWind { get; set; } = 40;
        public int MinHp_Bloodbath { get; set; } = 60;
    }

    /// <summary>
    /// Ajustes operativos en tiempo real (Logic Toggles).
    /// </summary>
    [Serializable]
    public class OperationalSettings
    {
        // --- NUEVO: OPCIÓN DE MODO DE INPUT ---
        // false = Simulación de Teclas (Legacy/Seguro)
        // true = Inyección Directa a Memoria (Rápido/No interfiere con chat)
        public bool UseMemoryInput { get; set; } = false;

        public bool AoE_Enabled { get; set; } = true;
        public bool TrueNorth_Auto { get; set; } = false;
        public bool SixSidedStar_Use { get; set; } = false;

        public bool SaveCD { get; set; } = false;
        public bool UsePB { get; set; } = true;
        public bool UseRoF { get; set; } = true;
        public bool UseRoW { get; set; } = true;
        public bool UseBrotherhood { get; set; } = true;
        public bool UseForbiddenChakra { get; set; } = true;

        
        // --- CONFIGURACIÓN DE POCIONES ---
        public bool UsePotion { get; set; } = false; // Toggle Master
        public uint SelectedPotionId { get; set; } = 0; // ID de la poción elegida


        // --- CONFIGURACIÓN DE OPENER ---
        public bool UseOpener { get; set; } = false;
        public string SelectedOpener { get; set; } = "Ninguno";
    
    }

    /// <summary>
    /// Clase raíz de configuración del Plugin.
    /// </summary>
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        // Configuración de tecla global para Pausar/Reanudar (Default F8)
        public VirtualKey ToggleHotkey = VirtualKey.F8;

        // Instancias de configuración por módulos
        public JobConfig_MNK Monk = new JobConfig_MNK();

        public SurvivalConfig Survival = new SurvivalConfig();
        public OperationalSettings Operation = new OperationalSettings();

        // Referencia a la interfaz del plugin (no se serializa)
        [NonSerialized] private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;

        public void Save() => this.pluginInterface!.SavePluginConfig(this);
    }
}
