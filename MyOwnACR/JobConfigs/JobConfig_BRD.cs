// Archivo: JobConfigs/JobConfig_BRD.cs
// Descripción: Configuración de teclas y preferencias para Bard (Dawntrail).

using System;
using MyOwnACR;

namespace MyOwnACR.JobConfigs
{
    [Serializable]
    public class JobConfig_BRD
    {
        // ==================================================================================
        //  CONFIGURACIÓN COMPLETA BARD (DAWNTRAIL)
        //  Barras: 1=Base, 2=Ctrl, 3=Shift, 4=Alt, 5=Ctrl+Alt
        // ==================================================================================

        // ----------------------------------------------------------------------------------
        // 1. ROTACIÓN SINGLE TARGET (GCDs)
        // ----------------------------------------------------------------------------------
        public KeyBind BurstShot = new KeyBind(Keys.Num1, HotbarType.Barra1_Base);      // Ataque principal (Heavy Shot upgrade)
        public KeyBind RefulgentArrow = new KeyBind(Keys.Num2, HotbarType.Barra1_Base); // Proc fuerte (Straight Shot upgrade)
        public KeyBind IronJaws = new KeyBind(Keys.Num3, HotbarType.Barra1_Base);       // Refrescar DoTs
        public KeyBind ApexArrow = new KeyBind(Keys.Num4, HotbarType.Barra1_Base);      // Ataque de barra (Gauge)
        public KeyBind BlastArrow = new KeyBind(Keys.Num4, HotbarType.Barra1_Base);     // Follow-up de Apex (Mismo botón)

        // ----------------------------------------------------------------------------------
        // 2. ROTACIÓN AOE (GCDs)
        // ----------------------------------------------------------------------------------
        public KeyBind Ladonsbite = new KeyBind(Keys.Num1, HotbarType.Barra3_Shift);    // AoE Cono (Quick Nock upgrade)
        public KeyBind Shadowbite = new KeyBind(Keys.Num2, HotbarType.Barra3_Shift);    // AoE Proc

        // ----------------------------------------------------------------------------------
        // 3. DoTs (Damage over Time)
        // ----------------------------------------------------------------------------------
        public KeyBind Stormbite = new KeyBind(Keys.Num5, HotbarType.Barra1_Base);
        public KeyBind CausticBite = new KeyBind(Keys.Num6, HotbarType.Barra1_Base);

        // ----------------------------------------------------------------------------------
        // 4. CANCIONES (GCDs especiales)
        // ----------------------------------------------------------------------------------
        public KeyBind WanderersMinuet = new KeyBind(Keys.Q, HotbarType.Barra1_Base);   // Canción 1 (Burs)
        public KeyBind MagesBallad = new KeyBind(Keys.E, HotbarType.Barra1_Base);       // Canción 2 (Reset)
        public KeyBind ArmysPaeon = new KeyBind(Keys.R, HotbarType.Barra1_Base);        // Canción 3 (Haste)

        // ----------------------------------------------------------------------------------
        // 5. DAÑO INSTANTÁNEO / OGCD
        // ----------------------------------------------------------------------------------
        public KeyBind Bloodletter = new KeyBind(Keys.Num1, HotbarType.Barra2_Ctrl);    // oGCD principal (Cargas)
        public KeyBind HeartbreakShot = new KeyBind(Keys.Num1, HotbarType.Barra2_Ctrl); // Upgrade de Bloodletter (Mismo botón)

        public KeyBind RainOfDeath = new KeyBind(Keys.Num2, HotbarType.Barra2_Ctrl);    // Versión AoE de Bloodletter

        public KeyBind EmpyrealArrow = new KeyBind(Keys.Num3, HotbarType.Barra2_Ctrl);  // Flecha mágica (Garantiza proc)
        public KeyBind Sidewinder = new KeyBind(Keys.Num4, HotbarType.Barra2_Ctrl);     // Ataque fuerte (60s)

        public KeyBind PitchPerfect = new KeyBind(Keys.Q, HotbarType.Barra1_Base);      // Gasto de Minuet (Mismo botón que canción)

        // ----------------------------------------------------------------------------------
        // 6. BUFFS OFENSIVOS (oGCDs)
        // ----------------------------------------------------------------------------------
        public KeyBind RagingStrikes = new KeyBind(Keys.Z, HotbarType.Barra1_Base);     // +Daño
        public KeyBind BattleVoice = new KeyBind(Keys.X, HotbarType.Barra1_Base);       // +Direct Hit (Party)
        public KeyBind RadiantFinale = new KeyBind(Keys.C, HotbarType.Barra1_Base);     // +Daño (Party)
        public KeyBind RadiantEncore = new KeyBind(Keys.C, HotbarType.Barra1_Base);     // Follow-up de Finale (Mismo botón)

        public KeyBind Barrage = new KeyBind(Keys.V, HotbarType.Barra1_Base);           // Triple ataque (Para Refulgent)
        public KeyBind ResonantArrow = new KeyBind(Keys.V, HotbarType.Barra1_Base);     // Follow-up de Barrage (Mismo botón)

        // ----------------------------------------------------------------------------------
        // 7. UTILIDAD Y DEFENSA
        // ----------------------------------------------------------------------------------
        public KeyBind Troubadour = new KeyBind(Keys.F1, HotbarType.Barra1_Base);       // Mitigación Party
        public KeyBind NaturesMinne = new KeyBind(Keys.F2, HotbarType.Barra1_Base);     // +Curación recibida
        public KeyBind WardensPaean = new KeyBind(Keys.F3, HotbarType.Barra1_Base);     // Limpieza (Esuna)
        public KeyBind SecondWind = new KeyBind(Keys.Num5, HotbarType.Barra3_Shift);    // Cura propia
        public KeyBind RepellingShot = new KeyBind(Keys.G, HotbarType.Barra1_Base);     // Salto atrás
        public KeyBind HeadGraze = new KeyBind(Keys.F, HotbarType.Barra1_Base);         // Interrumpir
        public KeyBind LegGraze = new KeyBind(Keys.T, HotbarType.Barra1_Base);          // Heavy (Opcional)
        public KeyBind FootGraze = new KeyBind(Keys.Barrita, HotbarType.Barra4_Alt);         // Bind (Opcional)
        public KeyBind Peloton = new KeyBind(Keys.Num3, HotbarType.Barra5_CtrlAlt);           // Velocidad fuera de combate

        // ----------------------------------------------------------------------------------
        // 8. CONSUMIBLES & EXTRAS
        // ----------------------------------------------------------------------------------
        public KeyBind Pocion = new KeyBind(Keys.MenorQue, HotbarType.Barra4_Alt);
        public KeyBind Sprint = new KeyBind(Keys.Barrita, HotbarType.Barra2_Ctrl);
        public KeyBind LimitBreak = new KeyBind(Keys.Num1, HotbarType.Barra5_CtrlAlt);

        // =========================================================================
        // AJUSTES DE WEAVING Y LÓGICA (BARD SPECIFIC)
        // =========================================================================

        public bool EnableDoubleWeave = true;
        public WeaveSlotPreference SingleWeaveSlotPreference = WeaveSlotPreference.First; // Bard prefiere First para no retrasar Empyreal

        // Tiempos de seguridad (Milisegundos)
        public int AnimationLock_MS = 650;      // Bard es rápido, 600ms suele ir bien
        public int WeaveDelay_oGCD1_MS = 650;
        public int WeaveDelay_oGCD2_MS = 650;

        // Preferencias de Rotación
        public int ApexArrow_Threshold = 80;    // Usar Apex Arrow al 80+ de Soul Voice (para evitar overcap)
        public int SongCutoff_Minuet = 3;  // Cambiar a Ballad cuando Minuet < 3s
        public int SongCutoff_Ballad = 6;  // Cambiar a Paeon cuando Ballad < 6s
        public int SongCutoff_Paeon = 9;   // Cambiar a Minuet cuando Paeon < 9s
        public bool AutoSong = true;            // Rotar canciones automáticamente
        public bool AutoDotMaintenance = true;  // Usar Iron Jaws automáticamente
    }
}
