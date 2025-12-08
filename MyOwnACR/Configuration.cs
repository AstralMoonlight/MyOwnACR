// Archivo: MyOwnACR/Configuration.cs
// Descripción: Clase principal de configuración que implementa IPluginConfiguration.
// Gestiona la persistencia de datos (Guardar/Cargar) a través de Dalamud.

using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Keys;
using MyOwnACR.JobConfigs; // Importamos las configs de Jobs (MNK.cs)
using System;

namespace MyOwnACR
{
    // Nota: Las clases KeyBind y HotbarType se han movido a KeyCodes.cs 
    // para estar disponibles globalmente en el namespace MyOwnACR.

    /// <summary>
    /// Configuración para lógica de auto-supervivencia (pociones, skills de cura).
    /// </summary>
    [Serializable]
    public class SurvivalConfig
    {
        public bool Enabled { get; set; } = false;
        public int MinHp_SecondWind { get; set; } = 40; // % de HP para activar Second Wind
        public int MinHp_Bloodbath { get; set; } = 60;  // % de HP para activar Bloodbath
    }

    /// <summary>
    /// Ajustes operativos en tiempo real (Logic Toggles).
    /// </summary>
    [Serializable]
    public class OperationalSettings
    {
        public bool AoE_Enabled { get; set; } = true;    // Activar rotación de área
        public bool TrueNorth_Auto { get; set; } = false; // Uso automático de True North
        public bool SixSidedStar_Use { get; set; } = false;

        public bool SaveCD { get; set; } = false;        // Si true, guarda cooldowns (no usa bursts)
        public bool UsePB { get; set; } = true;          // Perfect Balance
        public bool UseRoF { get; set; } = true;         // Riddle of Fire
        public bool UseRoW { get; set; } = true;         // Riddle of Wind
        public bool UseBrotherhood { get; set; } = true;
        public bool UseForbiddenChakra { get; set; } = true;
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
        // JobConfig_MNK se encuentra en JobConfigs/MNK.cs
        public JobConfig_MNK Monk = new JobConfig_MNK();

        public SurvivalConfig Survival = new SurvivalConfig();
        public OperationalSettings Operation = new OperationalSettings();

        // Referencia a la interfaz del plugin (no se serializa)
        [NonSerialized] private IDalamudPluginInterface? pluginInterface;

        /// <summary>
        /// Inicializa la configuración vinculándola a la interfaz de Dalamud.
        /// </summary>
        public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;

        /// <summary>
        /// Guarda la configuración actual en el archivo JSON del usuario.
        /// </summary>
        public void Save() => this.pluginInterface!.SavePluginConfig(this);
    }
}
