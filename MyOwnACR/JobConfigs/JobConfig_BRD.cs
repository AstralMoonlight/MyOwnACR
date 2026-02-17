// Archivo: JobConfigs/JobConfig_BRD.cs
// Descripción: Configuración de teclas y preferencias para Bard (Dawntrail).
// VERSION: v18.6 - Fix VirtualKey.None error.

using System;
using Dalamud.Game.ClientState.Keys;
using MyOwnACR;

namespace MyOwnACR.JobConfigs
{
    [Serializable]
    public class JobConfig_BRD
    {
        // ==================================================================================
        //  CONFIGURACIÓN COMPLETA BARD (DAWNTRAIL)
        // ==================================================================================

        // 1. ROTACIÓN SINGLE TARGET
        public KeyBind BurstShot = new KeyBind(Keys.Num1, HotbarType.Barra1_Base);
        public KeyBind RefulgentArrow = new KeyBind(Keys.Q, HotbarType.Barra1_Base);
        public KeyBind IronJaws = new KeyBind(Keys.Barrita, HotbarType.Barra1_Base);
        public KeyBind ApexArrow = new KeyBind(Keys.Num4, HotbarType.Barra1_Base);
        public KeyBind BlastArrow = new KeyBind(Keys.Num4, HotbarType.Barra1_Base);

        // 2. ROTACIÓN AOE
        public KeyBind Ladonsbite = new KeyBind(Keys.Num1, HotbarType.Barra3_Shift);
        public KeyBind Shadowbite = new KeyBind(Keys.Num2, HotbarType.Barra3_Shift);

        // 3. DoTs
        public KeyBind Stormbite = new KeyBind(Keys.Num5, HotbarType.Barra1_Base);
        public KeyBind CausticBite = new KeyBind(Keys.Num6, HotbarType.Barra1_Base);

        // 4. CANCIONES
        public KeyBind WanderersMinuet = new KeyBind(Keys.Q, HotbarType.Barra1_Base);
        public KeyBind MagesBallad = new KeyBind(Keys.E, HotbarType.Barra1_Base);
        public KeyBind ArmysPaeon = new KeyBind(Keys.R, HotbarType.Barra1_Base);

        // 5. DAÑO INSTANTÁNEO / OGCD
        public KeyBind Bloodletter = new KeyBind(Keys.Num1, HotbarType.Barra2_Ctrl);
        public KeyBind HeartbreakShot = new KeyBind(Keys.Num1, HotbarType.Barra2_Ctrl);
        public KeyBind RainOfDeath = new KeyBind(Keys.Num2, HotbarType.Barra2_Ctrl);
        public KeyBind EmpyrealArrow = new KeyBind(Keys.Num3, HotbarType.Barra2_Ctrl);
        public KeyBind Sidewinder = new KeyBind(Keys.Num4, HotbarType.Barra2_Ctrl);
        public KeyBind PitchPerfect = new KeyBind(Keys.Q, HotbarType.Barra1_Base);

        // 6. BUFFS OFENSIVOS
        public KeyBind RagingStrikes = new KeyBind(Keys.Z, HotbarType.Barra1_Base);
        public KeyBind BattleVoice = new KeyBind(Keys.X, HotbarType.Barra1_Base);
        public KeyBind RadiantFinale = new KeyBind(Keys.C, HotbarType.Barra1_Base);
        public KeyBind RadiantEncore = new KeyBind(Keys.C, HotbarType.Barra1_Base);
        public KeyBind Barrage = new KeyBind(Keys.Num3, HotbarType.Barra1_Base);
        public KeyBind ResonantArrow = new KeyBind(Keys.V, HotbarType.Barra1_Base);

        // 7. UTILIDAD Y DEFENSA
        public KeyBind Troubadour = new KeyBind(Keys.MenorQue, HotbarType.Barra5_CtrlAlt);
        public KeyBind NaturesMinne = new KeyBind(Keys.F, HotbarType.Barra5_CtrlAlt);
        public KeyBind WardensPaean = new KeyBind(Keys.F3, HotbarType.Barra5_CtrlAlt);
        public KeyBind SecondWind = new KeyBind(Keys.Num2, HotbarType.Barra3_Shift);
        public KeyBind RepellingShot = new KeyBind(Keys.Barrita, HotbarType.Barra5_CtrlAlt);
        public KeyBind HeadGraze = new KeyBind(Keys.Num1, HotbarType.Barra4_Alt);
        public KeyBind LegGraze = new KeyBind(Keys.Num2, HotbarType.Barra4_Alt);
        public KeyBind FootGraze = new KeyBind(Keys.Num3, HotbarType.Barra4_Alt);
        public KeyBind Peloton = new KeyBind(Keys.Num3, HotbarType.Barra5_CtrlAlt);

        // Arm's Length (Click Rueda Mouse = 4)
       // public KeyBind ArmsLength = new KeyBind((VirtualKey)4, HotbarType.Barra1_Base);

        // 8. CONSUMIBLES & EXTRAS
        public KeyBind Pocion = new KeyBind(Keys.MenorQue, HotbarType.Barra4_Alt);
        public KeyBind Sprint = new KeyBind(Keys.Barrita, HotbarType.Barra2_Ctrl);
        public KeyBind LimitBreak = new KeyBind(Keys.Num1, HotbarType.Barra5_CtrlAlt);

        // ==================================================================================
        // --- TECLAS PARA INYECCIÓN MANUAL (LISTENER) ---
        // (VirtualKey)0 representa "Ninguna tecla"
        // ==================================================================================
        public VirtualKey Key_Troubadour { get; set; } = (VirtualKey)0;
        public VirtualKey Key_NaturesMinne { get; set; } = (VirtualKey)0;
        public VirtualKey Key_WardensPaean { get; set; } = (VirtualKey)0;
        public VirtualKey Key_RepellingShot { get; set; } = (VirtualKey)0;
        public VirtualKey Key_HeadGraze { get; set; } = (VirtualKey)0;
        public VirtualKey Key_ArmsLength { get; set; } = (VirtualKey)0;
        public VirtualKey Key_Sprint { get; set; } = (VirtualKey)0;

        // =========================================================================
        // AJUSTES DE LÓGICA
        // =========================================================================
        public bool EnableDoubleWeave = true;
        public WeaveSlotPreference SingleWeaveSlotPreference = WeaveSlotPreference.First;
        public int AnimationLock_MS = 650;
        public int WeaveDelay_oGCD1_MS = 650;
        public int WeaveDelay_oGCD2_MS = 650;
        public int ApexArrow_Threshold = 80;
        public int SongCutoff_Minuet = 3;
        public int SongCutoff_Ballad = 6;
        public int SongCutoff_Paeon = 9;
        public bool AutoSong = true;
        public bool AutoDotMaintenance = true;
        
        // [NUEVO] Propiedades para Dashboard
        public bool UseApexArrow { get; set; } = true; 
        public bool AlignBuffs { get; set; } = true; 
    }
}
