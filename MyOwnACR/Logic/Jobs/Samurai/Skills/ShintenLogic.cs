using System; // Necesario para Math.Max
using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;
using MyOwnACR.Models;

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class ShintenLogic
    {
        public static OgcdPlan? GetPlan(SamuraiContext ctx, int level)
        {
            // Requisito básico: Nivel 52
            if (level < SAM_Levels.HissatsuShinten) return null;

            // =================================================================
            // 1. CÁLCULO DE RESERVAS (OBLIGATORIAS)
            // =================================================================
            // Kenki que NO podemos tocar bajo ninguna circunstancia.

            int reservedKenki = 0;

            // Reserva para Senei/Guren (Cuesta 25, CD 120s)
            // Si está a punto de volver, guardamos para él.
            if (level >= SAM_Levels.HissatsuSenei && ctx.SeneiCD < 10.0f)
            {
                reservedKenki = Math.Max(reservedKenki, 25);
            }

            // Reserva para Zanshin (Cuesta 50)
            // Prioridad absoluta.
            if (ctx.HasZanshinReady)
            {
                reservedKenki = Math.Max(reservedKenki, 50);
            }

            // =================================================================
            // 2. UMBRAL DINÁMICO (TU PEDIDO)
            // =================================================================
            // Aquí definimos "cuánto Kenki es demasiado".
            // Si superamos este número, usamos Shinten para bajar.

            int dumpThreshold = 85; // Estándar: Evitar llegar a 100 por generación natural.

            // LÓGICA PRE-BURST:
            // Ikishoten nos da +50 Kenki. 
            // Si Ikishoten está por volver (< 15s), debemos vaciar la barra hasta 50.
            // Si tenemos 60 y tiramos Ikishoten -> 110 (Perdemos 10).
            if (ctx.IkishotenCD < 15.0f)
            {
                dumpThreshold = 50;
            }

            // =================================================================
            // 3. DECISIÓN
            // =================================================================
            int shintenCost = 25;

            // CONDICIÓN A: Tener suficiente para pagar Shinten + Reservas.
            bool canAfford = ctx.Kenki >= (reservedKenki + shintenCost);

            // CONDICIÓN B: Estar por encima del umbral deseado.
            bool needsDump = ctx.Kenki > dumpThreshold;

            // Si cumplimos ambas, quemamos Kenki.
            if (canAfford && needsDump)
            {
                return new OgcdPlan(SAM_IDs.HissatsuShinten, WeavePriority.Low);
            }

            return null;
        }
    }
}
