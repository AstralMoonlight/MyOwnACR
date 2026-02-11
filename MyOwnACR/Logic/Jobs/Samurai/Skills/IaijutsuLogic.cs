using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class IaijutsuLogic
    {
        public static uint GetAction(SamuraiContext ctx)
        {
            // =================================================================
            // 1. OGI NAMIKIRI (Prioridad Absoluta - Nvl 90+)
            // =================================================================
            if (ctx.HasOgiReady)
            {
                // [GUARDIA DE SEGURIDAD]
                // Si la última acción fue Ogi, significa que toca Kaeshi.
                // NO castees Ogi de nuevo (IaijutsuLogic debe ceder el paso a TsubameLogic).
                // También si estamos casteando Ogi actualmente, no pedirlo de nuevo.
                if (ctx.LastComboAction == SAM_IDs.OgiNamikiri) return 0;
                if (ctx.IsCasting && ctx.CastActionId == SAM_IDs.OgiNamikiri) return 0;

                return SAM_IDs.OgiNamikiri;
            }

            // =================================================================
            // 2. HIGANBANA (DoT)
            // =================================================================
            // Se usa si tenemos 1 Sen y el DoT está por caerse.
            // (Pequeño ajuste: < 14s es buen timing estándar)
            if (ctx.SenCount == 1 && ctx.HiganbanaTimeLeft < 14.0f)
            {
                return SAM_IDs.Higanbana;
            }

            // =================================================================
            // 3. MIDARE / TENDO SETSUGEKKA (Burst)
            // =================================================================
            if (ctx.SenCount == 3)
            {
                // [GUARDIA DE SEGURIDAD]
                // Si tenemos el buff de Kaeshi pendiente, NO castees Midare/Tendo de nuevo.
                // Primero gasta el golpe extra (TsubameLogic se encarga).
                if (ctx.HasTendoKaeshiSetsuReady || ctx.HasKaeshiSetsuReady) return 0;

                // Si estamos casteando ya, no pedirlo de nuevo.
                if (ctx.IsCasting && (ctx.CastActionId == SAM_IDs.TendoSetsugekka || ctx.CastActionId == SAM_IDs.MidareSetsugekka)) return 0;

                // [DAWNTRAIL - NIVEL 100]
                if (ctx.HasTendoReady)
                {
                    return SAM_IDs.TendoSetsugekka; // ID 36966
                }

                // Versión Estándar
                return SAM_IDs.MidareSetsugekka;
            }

            return 0;
        }
    }
}
