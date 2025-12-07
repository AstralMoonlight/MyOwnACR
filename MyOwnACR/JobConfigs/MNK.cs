using System;
using MyOwnACR; // NECESARIO para encontrar 'Keys' y 'HotbarType'

namespace MyOwnACR.JobConfigs
{
    [Serializable]
    public class JobConfig_MNK
    {
        // ==================================================================================
        //  CONFIGURACIÓN COMPLETA MONK (DAWNTRAIL)
        //  Barras: 1=Base, 2=Ctrl, 3=Shift, 4=Alt, 5=Ctrl+Alt
        // ==================================================================================

        // ----------------------------------------------------------------------------------
        // 1. ROTACIÓN SINGLE TARGET (Barra 1 - Base)
        // ----------------------------------------------------------------------------------
        // El combo básico 1-2-3 y sus alternativos
        public KeyBind Bootshine = new KeyBind(Keys.Num1, HotbarType.Barra1_Base); // Leaping Opo
        public KeyBind DragonKick = new KeyBind(Keys.Num1, HotbarType.Barra2_Ctrl);

        public KeyBind TrueStrike = new KeyBind(Keys.Num2, HotbarType.Barra1_Base); // Raptor A
        public KeyBind TwinSnakes = new KeyBind(Keys.Num2, HotbarType.Barra2_Ctrl); // Raptor B (Mismo botón)

        public KeyBind SnapPunch = new KeyBind(Keys.Num3, HotbarType.Barra1_Base); // Coeurl A
        public KeyBind Demolish = new KeyBind(Keys.Num3, HotbarType.Barra2_Ctrl); // Coeurl B (Mismo botón)

        public KeyBind SixSidedStar = new KeyBind(Keys.G, HotbarType.Barra1_Base); // Finisher de velocidad SSS

        // ----------------------------------------------------------------------------------
        // 2. ROTACIÓN AOE (Barra 3 - Shift)
        // ----------------------------------------------------------------------------------
        public KeyBind ArmOfTheDestroyer = new KeyBind(Keys.Num1, HotbarType.Barra4_Alt); // AoE 1 (Shadow of the Destroyer)
        public KeyBind FourPointFury = new KeyBind(Keys.Num2, HotbarType.Barra4_Alt); // AoE 2
        public KeyBind Rockbreaker = new KeyBind(Keys.Num3, HotbarType.Barra4_Alt); // AoE 3

        // ----------------------------------------------------------------------------------
        // 3. DAÑO INSTANTÁNEO / OGCD (Barra 1 y 2)
        // ----------------------------------------------------------------------------------
        public KeyBind ForbiddenChakra = new KeyBind(Keys.E, HotbarType.Barra1_Base); // Ataque fuerte (Chakra)
        public KeyBind Enlightenment = new KeyBind(Keys.R, HotbarType.Barra4_Alt); // Versión AoE de Chakra

        // Blitz Keys: Todas apuntan a la misma tecla física (donde tengas el botón de Blitz),
        // pero necesitamos propiedades separadas para la lógica interna.
        public KeyBind MasterfulBlitz = new KeyBind(Keys.E, HotbarType.Barra2_Ctrl);
        public KeyBind ElixirField = new KeyBind(Keys.E, HotbarType.Barra2_Ctrl);
        public KeyBind RisingPhoenix = new KeyBind(Keys.E, HotbarType.Barra2_Ctrl);
        public KeyBind PhantomRush = new KeyBind(Keys.E, HotbarType.Barra2_Ctrl);
        public KeyBind CelestialRevolution = new KeyBind(Keys.E, HotbarType.Barra2_Ctrl);

        // Meditación (Cargar Chakra)
        public KeyBind Meditation = new KeyBind(Keys.E, HotbarType.Barra1_Base);

        // ----------------------------------------------------------------------------------
        // 4. BUFFS OFENSIVOS (Barra 2 - Ctrl)
        // ----------------------------------------------------------------------------------
        public KeyBind PerfectBalance = new KeyBind(Keys.Q, HotbarType.Barra1_Base); // Activa Blitz
        public KeyBind RiddleOfFire = new KeyBind(Keys.R, HotbarType.Barra1_Base); // +Daño
        public KeyBind Brotherhood = new KeyBind(Keys.F, HotbarType.Barra2_Ctrl); // Party Buff
        public KeyBind RiddleOfWind = new KeyBind(Keys.Num4, HotbarType.Barra1_Base); // Auto-ataques

        // ----------------------------------------------------------------------------------
        // 5. MOVILIDAD Y UTILIDAD (Mezclado)
        // ----------------------------------------------------------------------------------
        public KeyBind Thunderclap = new KeyBind(Keys.Barrita, HotbarType.Barra1_Base); // Dash (Acercarse)
        public KeyBind TrueNorth = new KeyBind(Keys.F, HotbarType.Barra1_Base); // Ignorar posicionales
        public KeyBind LegSweep = new KeyBind(Keys.Num1, HotbarType.Barra3_Shift); // Stun (Patada baja)
        public KeyBind Sprint = new KeyBind(Keys.Barrita, HotbarType.Barra2_Ctrl);
        public KeyBind FormShift = new KeyBind(Keys.Q, HotbarType.Barra2_Ctrl);

        // ----------------------------------------------------------------------------------
        // 6. DEFENSIVOS Y CURACIÓN (Barra 4 - Alt)
        // ----------------------------------------------------------------------------------
        public KeyBind SecondWind = new KeyBind(Keys.Num2, HotbarType.Barra3_Shift);  // Cura propia
        public KeyBind Bloodbath = new KeyBind(Keys.Q, HotbarType.Barra3_Shift);  // Robo de vida
        public KeyBind RiddleOfEarth = new KeyBind(Keys.Q, HotbarType.Barra5_CtrlAlt);  // Escudo/Mitigación
        public KeyBind Mantra = new KeyBind(Keys.F, HotbarType.Barra5_CtrlAlt);  // Buff de curación recibida
        public KeyBind Feint = new KeyBind(Keys.MenorQue, HotbarType.Barra5_CtrlAlt);  // Reducir daño del boss
        public KeyBind ArmsLength = new KeyBind(Keys.F10, HotbarType.Barra1_Base);  // Anti-empuje

        // ----------------------------------------------------------------------------------
        // 7. DAWNTRAIL FOLLOW-UPS (Nivel 100)
        // ----------------------------------------------------------------------------------
        public KeyBind FiresReply = new KeyBind(Keys.R, HotbarType.Barra1_Base); // Mismo que Riddle of Fire
        public KeyBind WindsReply = new KeyBind(Keys.Num4, HotbarType.Barra1_Base); // Mismo que Riddle of Wind

        // ----------------------------------------------------------------------------------
        // 8 CONSUMIBLES
        // ----------------------------------------------------------------------------------
        public KeyBind Pocion = new KeyBind(Keys.MenorQue, HotbarType.Barra4_Alt);
        public KeyBind Comida = new KeyBind(Keys.Barrita, HotbarType.Barra4_Alt);

        // ----------------------------------------------------------------------------------
        // 9 Extras
        // ----------------------------------------------------------------------------------
        public KeyBind LimitBreak = new KeyBind(Keys.Num1, HotbarType.Barra5_CtrlAlt);
    }
}
