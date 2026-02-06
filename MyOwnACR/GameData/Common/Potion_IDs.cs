using System.Collections.Generic;
using System.Linq;

namespace MyOwnACR.GameData.Common
{
    public static class Potion_IDs
    {
        // Agrupación por Tipo de Stat
        public static readonly Dictionary<PotionStat, Dictionary<string, uint>> PotionsByStat = new()
        {
            // --- FUERZA (MNK, DRG, SAM, RPR, VPR, TANKS) ---
            {
                PotionStat.Strength, new Dictionary<string, uint>
                {
                    { "Grade 4 Gemdraught of Strength (HQ)", 1049234 },
                    { "Grade 4 Gemdraught of Strength (NQ)", 49234 },
                    { "Grade 3 Gemdraught of Strength (HQ)", 1045995 },
                    { "Grade 8 Tincture of Strength (HQ)", 1039727 },
                }
            },

            // --- DESTREZA (NIN, BRD, MCH, DNC) ---
            {
                PotionStat.Dexterity, new Dictionary<string, uint>
                {
                    { "Grade 4 Gemdraught of Dex (HQ)", 1049235 },
                    { "Grade 4 Gemdraught of Dex (NQ)", 49235 },
                    { "Grade 3 Gemdraught of Dex (HQ)", 1045996 },
                    { "Grade 3 Gemdraught of Dex (NQ)", 45996 },
                    { "Grade 8 Tincture of Dex (HQ)", 1039728 },
                    { "Grade 8 Tincture of Dex (NQ)", 39728 },
                }
            },

            // --- INTELIGENCIA (CASTERS) ---
            {
                PotionStat.Intelligence, new Dictionary<string, uint>
                {
                    // Placeholder
                    { "Grade 4 Gemdraught of Intelligence (HQ)", 0 },
                }
            },

            // --- MENTE (HEALERS) ---
            {
                PotionStat.Mind, new Dictionary<string, uint>
                {
                    // Placeholder
                    { "Grade 4 Gemdraught of Mind (HQ)", 0 },
                }
            }
        };

        /// <summary>
        /// Obtiene la lista plana de IDs para InventoryManager (Busqueda general).
        /// </summary>
        public static uint[] GetAllIds()
        {
            return PotionsByStat.Values
                .SelectMany(dict => dict.Values)
                .Where(id => id != 0)
                .ToArray();
        }

        /// <summary>
        /// Obtiene el diccionario Nombre->ID para una estadística específica.
        /// </summary>
        public static Dictionary<string, uint> GetListForStat(PotionStat stat)
        {
            if (PotionsByStat.TryGetValue(stat, out var list))
            {
                return list;
            }
            return new Dictionary<string, uint>();
        }
    }
}
