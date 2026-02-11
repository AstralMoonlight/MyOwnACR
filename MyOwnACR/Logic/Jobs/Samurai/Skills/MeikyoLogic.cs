using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class MeikyoLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, int level)
        {
            if (level < SAM_Levels.MeikyoShisui) return null;

            // =================================================================
            // 1. CHEQUEOS BÁSICOS
            // =================================================================
            if (ctx.HasMeikyoShisui) return null; // Ya activo
            if (ctx.SenCount == 3) return null;   // Barra llena, toca Midare

            // PROTECCIÓN DE COMBO MANUAL
            // Si ya empezaste un combo, termínalo a mano antes de activar Meikyo.
            if (ctx.LastComboAction == SAM_IDs.Gyofu ||
                ctx.LastComboAction == SAM_IDs.Jinpu ||
                ctx.LastComboAction == SAM_IDs.Shifu)
            {
                return null;
            }

            // =================================================================
            // 2. LECTURA DE TIEMPOS
            // =================================================================
            float timeToBurst = ctx.IkishotenCD;

            // BURST: Si Ikishoten > 100, estamos en la ventana de daño.
            bool inBurstWindow = (timeToBurst > 100.0f);

            // OVERCAP: Emergencia si tenemos 2 cargas y el CD parado.
            bool isHardCapped = ctx.MeikyoCD <= 0.1f;

            // FILLER SEGURO: Faltan más de 60s para el burst.
            bool isSafeTimeForFiller = (timeToBurst > 60.0f);

            // =================================================================
            // 3. ESTRATEGIA "YUKIKAZE BRIDGE"
            // =================================================================

            // REGLA DE ORO: Solo activar Meikyo si YA tenemos Setsu (Nieve).
            // Esto obliga al bot a hacer Gyofu -> Yukikaze manualmente primero.
            // Al tener Setsu, las 3 cargas de Meikyo irán forzosamente a Gekko/Kasha.
            bool hasSetsuCondition = ctx.HasSetsu;

            // EXCEPCIÓN: Si estamos en el Opener (0 Sen, sin buffs), necesitamos arrancar rápido.
            // (Opcional: Si quieres forzar Yukikaze incluso en opener, quita esta línea)
            bool isOpener = !ctx.HasSetsu && !ctx.HasGetsu && !ctx.HasKa && !inBurstWindow && isSafeTimeForFiller;

            // =================================================================
            // 4. DECISIÓN
            // =================================================================

            // A. PRIORIDAD MÁXIMA: OVERCAP (Evitar perder uso)
            if (isHardCapped)
            {
                return new OgcdPlan(SAM_IDs.MeikyoShisui, WeavePriority.High);
            }

            // B. USO ESTÁNDAR (Burst o Filler)
            // Solo usamos si es seguro por tiempo O estamos en burst.
            if (inBurstWindow || isSafeTimeForFiller)
            {
                // APLICAMOS TU ESTRATEGIA:
                // Solo activamos si ya tenemos la Nieve (o si es el opener total).
                if (hasSetsuCondition || isOpener)
                {
                    // Verificamos que no tengamos YA los 3 (aunque el check de arriba lo cubre).
                    // Si tenemos Nieve, nos faltan Luna y Flor -> Perfecto para Meikyo.
                    return new OgcdPlan(SAM_IDs.MeikyoShisui, WeavePriority.High);
                }
            }

            // Si no tenemos Nieve, retornamos null.
            // Esto hará que el ComboLogic diga: "Falta Nieve -> Usa Gyofu -> Usa Yukikaze".
            // Una vez tengas Nieve, en el siguiente frame, esta lógica dirá "¡Ahora sí, Meikyo!".
            return null;
        }
    }
}
