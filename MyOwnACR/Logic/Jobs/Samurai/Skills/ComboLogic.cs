using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class ComboLogic
    {
        public static uint GetAction(SamuraiContext ctx, int level)
        {
            // =================================================================
            // 1. LÓGICA BAJO MEIKYO SHISUI (LA ZONA VIP)
            // =================================================================
            // Aquí queremos GASTO EFICIENTE.
            // Yukikaze manual = 2 GCDs (Barato).
            // Gekko/Kasha manual = 3 GCDs (Caro).
            // Con Meikyo, todo cuesta 1 GCD. Por tanto, compramos lo "Caro".

            if (ctx.HasMeikyoShisui && ctx.MeikyoStacks > 0)
            {
                // A. Mantenimiento de Buffs (Seguridad)
                // Si se van a caer en menos de 6s, los renovamos con prioridad.
                bool dangerFugetsu = ctx.FugetsuTimeLeft < 6.0f;
                bool dangerFuka = ctx.FukaTimeLeft < 6.0f;

                if (dangerFugetsu && !ctx.HasGetsu) return SAM_IDs.Gekko;
                if (dangerFuka && !ctx.HasKa) return SAM_IDs.Kasha;

                // B. Prioridad de Daño (Luna y Flor)
                // Gekko y Kasha tienen mayor potencia (330) vs Yukikaze (300).
                if (!ctx.HasGetsu) return SAM_IDs.Gekko;
                if (!ctx.HasKa) return SAM_IDs.Kasha;

                // C. El Último Recurso (Nieve)
                // Solo si YA tenemos Luna y Flor, y nos falta Nieve, usamos Yukikaze.
                // (Gracias a MeikyoLogic, esto casi nunca pasará, perfecto).
                if (!ctx.HasSetsu) return SAM_IDs.Yukikaze;

                // D. Dumping (3ra Carga sobrante)
                // Si tienes las 3 pegatinas y te sobra una carga (ej: después de Tendo),
                // Gastamos en Gekko/Kasha para ganar Sen para la siguiente ronda.
                return SAM_IDs.Gekko;
            }

            // =================================================================
            // 2. FINALIZADORES DE COMBO (MANUAL)
            // =================================================================
            // Si ya estamos brillando en un paso intermedio, lo terminamos.

            // Ruta Luna
            if (ctx.LastComboAction == SAM_IDs.Jinpu)
            {
                if (level >= SAM_Levels.Gekko) return SAM_IDs.Gekko;
                return 0;
            }
            // Ruta Flor
            if (ctx.LastComboAction == SAM_IDs.Shifu)
            {
                if (level >= SAM_Levels.Kasha) return SAM_IDs.Kasha;
                return 0;
            }
            // Ruta Yukikaze (Desde Gyofu)
            if (ctx.LastComboAction == SAM_IDs.Hakaze || ctx.LastComboAction == SAM_IDs.Gyofu)
            {
                // Aquí decidimos qué camino tomar desde el inicio del combo.

                // A. Buffs en peligro (Prioridad Máxima)
                if (ctx.FugetsuTimeLeft < 6.0f && level >= SAM_Levels.Jinpu) return SAM_IDs.Jinpu;
                if (ctx.FukaTimeLeft < 6.0f && level >= SAM_Levels.Shifu) return SAM_IDs.Shifu;

                // B. CONSTRUYENDO EL PUENTE (Yukikaze Bridge)
                // Si no tenemos Nieve, priorizamos ir a por ella manualmente.
                // Esto prepara el terreno para que MeikyoLogic se active.
                if (!ctx.HasSetsu && level >= SAM_Levels.Yukikaze)
                {
                    return SAM_IDs.Yukikaze;
                }

                // C. Resto de Sen
                if (!ctx.HasGetsu && level >= SAM_Levels.Jinpu) return SAM_IDs.Jinpu;
                if (!ctx.HasKa && level >= SAM_Levels.Shifu) return SAM_IDs.Shifu;

                // D. Filler Standard
                if (level >= SAM_Levels.Shifu) return SAM_IDs.Shifu; // Kasha route preferida por velocidad
                if (level >= SAM_Levels.Jinpu) return SAM_IDs.Jinpu;
            }

            // =================================================================
            // 3. INICIADORES
            // =================================================================
            if (level >= SAM_Levels.Gyofu) return SAM_IDs.Gyofu;
            return SAM_IDs.Hakaze;
        }
    }
}
