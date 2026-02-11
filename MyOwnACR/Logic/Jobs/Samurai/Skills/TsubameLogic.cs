using FFXIVClientStructs.FFXIV.Client.Game; // Necesario para ActionManager
using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public unsafe static class TsubameLogic
    {
        // [IMPORTANTE] Añadimos ActionManager* am a los parámetros
        public static uint GetAction(SamuraiContext ctx, int level, ActionManager* am)
        {
            // =================================================================
            // 1. PREDICCIÓN (QUEUEING) - MANTENEMOS ESTO
            // =================================================================
            // Si estamos casteando AHORA MISMO, encolamos el siguiente golpe.
            // Esto cubre los últimos 0.5s del cast.
            if (ctx.IsCasting)
            {
                if (ctx.CastActionId == SAM_IDs.OgiNamikiri) return SAM_IDs.KaeshiNamikiri;
                if (ctx.CastActionId == SAM_IDs.TendoSetsugekka) return SAM_IDs.TendoKaeshiSetsugekka;

                // Midare clásico
                if (ctx.CastActionId == SAM_IDs.MidareSetsugekka && (ctx.HasTsubameReady || level >= 76))
                    return SAM_IDs.TsubameGaeshi;
            }

            // =================================================================
            // 2. CONSULTA DIRECTA (ACTION REPLACEMENT) - SOLUCIÓN REAL
            // =================================================================
            // En lugar de mirar "LastComboAction" (que tiene lag), preguntamos al juego:
            // "¿En qué se ha convertido el botón de Ogi Namikiri?"

            if (am != null)
            {
                // -- CHECK OGI NAMIKIRI --
                // El ID 25781 (Ogi) se transforma internamente en 25782 (Kaeshi) si el combo está listo.
                uint ogiButtonState = am->GetAdjustedActionId(SAM_IDs.OgiNamikiri);

                if (ogiButtonState == SAM_IDs.KaeshiNamikiri)
                {
                    return SAM_IDs.KaeshiNamikiri;
                }

                // -- CHECK TSUBAME GAESHI (Midare / Tendo) --
                // El botón de Tsubame (16486) se transforma en Kaeshi Setsugekka (o Tendo Kaeshi).
                // Nota: GetAdjustedActionId maneja la lógica interna de si tienes cargas o buffs.
                uint tsubameButtonState = am->GetAdjustedActionId(SAM_IDs.TsubameGaeshi);

                // Caso Tendo Kaeshi (Lvl 100)
                if (tsubameButtonState == SAM_IDs.TendoKaeshiSetsugekka)
                {
                    return SAM_IDs.TendoKaeshiSetsugekka;
                }

                // Caso Kaeshi Setsugekka (Normal)
                // El ID ajustado suele ser el ID base de Kaeshi (16486 se transforma en la versión de daño).
                // Pero para estar seguros, si tenemos el buff y el botón está habilitado, lo usamos.
                if (ctx.HasKaeshiSetsuReady || ctx.HasTendoKaeshiSetsuReady)
                {
                    // Si el juego dice que el botón es usable, lo devolvemos.
                    return tsubameButtonState;
                }
            }

            // =================================================================
            // 3. FALLBACK (POR SI ACASO)
            // =================================================================
            // Si todo lo anterior falla (raro), miramos el historial como última opción.

            if (ctx.LastComboAction == SAM_IDs.OgiNamikiri) return SAM_IDs.KaeshiNamikiri;

            return 0;
        }
    }
}
