// Archivo: MyOwnACR/JobConfigs/JobConfig_SAM.cs
// Descripción: Configuración de rotación y teclas específicas para Samurai (Dawntrail).
// Define KeyBinds, preferencias de Weaving y tiempos de animación.

using System;
using MyOwnACR; // Necesario para 'Keys' y 'HotbarType'

namespace MyOwnACR.JobConfigs
{
    [Serializable]
    public class JobConfig_SAM
    {
        // ==================================================================================
        //  CONFIGURACIÓN COMPLETA SAMURAI (DAWNTRAIL)
        //  Barras: 1=Base, 2=Ctrl, 3=Shift, 4=Alt, 5=Ctrl+Alt
        // ==================================================================================

        // ----------------------------------------------------------------------------------
        // 1. COMBOS SEN (Barra 1 - Base / Ctrl)
        // ----------------------------------------------------------------------------------
        // Combo Base (Getsu / Ka)
        public KeyBind Gyofu = new KeyBind(Keys.Num1, HotbarType.Barra1_Base); // Reemplaza a Hakaze en DT

        // Rama Jinpu (Buff daño) -> Gekko (Dorsal)
        public KeyBind Jinpu = new KeyBind(Keys.Num2, HotbarType.Barra1_Base);
        public KeyBind Gekko = new KeyBind(Keys.Num3, HotbarType.Barra1_Base);

        // Rama Shifu (Buff velocidad) -> Kasha (Flanco)
        public KeyBind Shifu = new KeyBind(Keys.Num2, HotbarType.Barra2_Ctrl);
        public KeyBind Kasha = new KeyBind(Keys.Num3, HotbarType.Barra2_Ctrl);

        // Rama Yukikaze (Nieve)
        public KeyBind Yukikaze = new KeyBind(Keys.Num4, HotbarType.Barra1_Base);

        // ----------------------------------------------------------------------------------
        // 2. IAIJUTSU & TSUBAME (Barra 1 / Ctrl)
        // ----------------------------------------------------------------------------------
        // Botón de Cast (Midare / Tenka / Higanbana)
        public KeyBind Iaijutsu = new KeyBind(Keys.E, HotbarType.Barra1_Base);

        // Repetición del ataque (Tsubame-Gaeshi)
        public KeyBind TsubameGaeshi = new KeyBind(Keys.E, HotbarType.Barra2_Ctrl);

        // ----------------------------------------------------------------------------------
        // 3. ROTACIÓN AOE (Barra 3 - Shift)
        // ----------------------------------------------------------------------------------
        public KeyBind Fuko = new KeyBind(Keys.Num1, HotbarType.Barra3_Shift); // Reemplaza Fuga
        public KeyBind Mangetsu = new KeyBind(Keys.Num2, HotbarType.Barra3_Shift); // AoE Luna
        public KeyBind Oka = new KeyBind(Keys.Num3, HotbarType.Barra3_Shift); // AoE Flor

        // ----------------------------------------------------------------------------------
        // 4. KENKI SPENDERS (Barra 1 / Ctrl / Shift)
        // ----------------------------------------------------------------------------------
        // Single Target
        public KeyBind HissatsuShinten = new KeyBind(Keys.F, HotbarType.Barra1_Base); // Spam Kenki
        public KeyBind HissatsuSenei = new KeyBind(Keys.F, HotbarType.Barra2_Ctrl);   // Big Hit (2 min)

        // AoE
        public KeyBind HissatsuKyuten = new KeyBind(Keys.F, HotbarType.Barra3_Shift); // Spam AoE
        public KeyBind HissatsuGuren = new KeyBind(Keys.F, HotbarType.Barra4_Alt);    // Big AoE (comparte CD con Senei)

        // ----------------------------------------------------------------------------------
        // 5. MEDITACIÓN & SHOHA (Barra 2)
        // ----------------------------------------------------------------------------------
        public KeyBind Meditate = new KeyBind(Keys.Z, HotbarType.Barra2_Ctrl); // Cargar en downtime
        public KeyBind Shoha = new KeyBind(Keys.G, HotbarType.Barra1_Base);    // Single Target (Stacks)
        public KeyBind Shoha2 = new KeyBind(Keys.G, HotbarType.Barra3_Shift);  // AoE (Stacks)
        public KeyBind Hagakure = new KeyBind(Keys.Z, HotbarType.Barra1_Base); // Convertir Sen en Kenki

        // ----------------------------------------------------------------------------------
        // 6. COOLDOWNS & BURST (Barra 1 / Ctrl)
        // ----------------------------------------------------------------------------------
        public KeyBind MeikyoShisui = new KeyBind(Keys.Q, HotbarType.Barra1_Base); // Habilitar combos
        public KeyBind Ikishoten = new KeyBind(Keys.R, HotbarType.Barra1_Base);    // +50 Kenki + Ogi Ready

        // Ogi Namikiri (Nvl 90/100)
        public KeyBind OgiNamikiri = new KeyBind(Keys.R, HotbarType.Barra2_Ctrl);

        // ----------------------------------------------------------------------------------
        // 7. MOVILIDAD & UTILIDAD
        // ----------------------------------------------------------------------------------
        public KeyBind HissatsuGyoten = new KeyBind(Keys.Barrita, HotbarType.Barra1_Base); // Dash In
        public KeyBind HissatsuYaten = new KeyBind(Keys.Barrita, HotbarType.Barra2_Ctrl);  // Backstep
        public KeyBind Tengentsu = new KeyBind(Keys.Num5, HotbarType.Barra1_Base); // Mitigación (Ex Third Eye)
        public KeyBind TrueNorth = new KeyBind(Keys.F, HotbarType.Barra1_Base);    // (Suele compartirse, ajustar si necesario)
        public KeyBind Feint = new KeyBind(Keys.MenorQue, HotbarType.Barra5_CtrlAlt);
        public KeyBind SecondWind = new KeyBind(Keys.Num2, HotbarType.Barra3_Shift);
        public KeyBind Bloodbath = new KeyBind(Keys.Q, HotbarType.Barra3_Shift);
        public KeyBind LegSweep = new KeyBind(Keys.Num1, HotbarType.Barra4_Alt);

        // ----------------------------------------------------------------------------------
        // 8. CONSUMIBLES
        // ----------------------------------------------------------------------------------
        public KeyBind Pocion = new KeyBind(Keys.MenorQue, HotbarType.Barra4_Alt);
        public KeyBind Comida = new KeyBind(Keys.Barrita, HotbarType.Barra4_Alt);

        // =========================================================================
        // AJUSTES DE LÓGICA ESPECÍFICA (Dashboard)
        // =========================================================================

        public bool UseMeikyo = true;      // Usar Meikyo Shisui automáticamente
        public bool UseHagakure = false;   // Usar Hagakure para limpiar Sen antes de downtime
        public bool UseTrueNorth = true;   // Usar True North en posicionales
        public bool SpendKenki = true;     // Gastar Kenki automáticamente (Shinten)

        // =========================================================================
        // AJUSTES DE WEAVING
        // =========================================================================

        public bool EnableDoubleWeave = true;

        // El Samurai suele preferir weave tardío para no retrasar el GCD por animación de Iaijutsu
        public WeaveSlotPreference SingleWeaveSlotPreference = WeaveSlotPreference.Second;

        // Tiempos de animación (Iaijutsu tiene un lock diferente, manejado en lógica)
        public int AnimationLock_MS = 620;
        public int WeaveDelay_oGCD1_MS = 640;
        public int WeaveDelay_oGCD2_MS = 640;
    }
}
