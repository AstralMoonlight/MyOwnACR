// Archivo: GameData/Food_IDs.cs
// Descripción: IDs de Comidas (Meals) para buffs de estadísticas.
// HQ ID = Base ID + 1,000,000.

namespace MyOwnACR.GameData
{
    public static class Food_IDs
    {
        // --- DAWNTRAIL (7.x) ---

        // Critical Hit / Determination (iLvl 710)
        public const uint CaramelPopcorn_Base = 49240;
        public const uint CaramelPopcorn_HQ = 1049240;

        // Critical Hit / Determination (iLvl 690)
        public const uint MateCookie_Base = 46003;
        public const uint MateCookie_HQ = 1046003;

        /// <summary>
        /// Lista de prioridad de comidas para Monk (Crit > Det/DH).
        /// </summary>
        public static readonly uint[] BestMonkFood = new uint[]
        { 
            // Prioridad 1: Caramel Popcorn (Mejor Stat)
            CaramelPopcorn_HQ,
            CaramelPopcorn_Base,

            // Prioridad 2: Mate Cookie (Budget)
            MateCookie_HQ,
            MateCookie_Base
        };
    }
}
