// Archivo: Logic/Jobs/Bard/Skills/EmpyrealArrowLogic.cs
// VERSIÓN: V2.1 - ANTI-DRIFT PRIORITY
// DESCRIPCIÓN: Prioriza mantener EA en cooldown sobre la protección de recursos.
//              Si el drift es inminente, sacrifica stacks/cargas para mantener el ciclo.

using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;
using System;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class EmpyrealArrowLogic
    {
        private const int TRAIT_LEVEL_ENHANCED_EA = 68;


        // Si EA lleva listo más de este tiempo, entrar en modo pánico y usarlo sí o sí.
        // 2.0s = Casi un GCD entero de retraso.
        private const float MAX_TOLERATED_DRIFT = 2.0f;

        public static OgcdPlan? GetPlan(BardContext ctx, int playerLevel)
        {
            // 1. CHEQUEOS BÁSICOS
            if (playerLevel < BRD_Levels.EmpyrealArrow) return null;

            // Si el CD es mayor a la latencia esperada, nada que hacer.
            if (ctx.EmpyrealCD > 0.6f) return null;

            // -----------------------------------------------------------------
            // MODO PÁNICO (ANTI-DRIFT)
            // -----------------------------------------------------------------
            // Si EA está listo (CD=0) desde hace rato (técnicamente difícil de medir exacto sin historia, 
            // pero podemos asumir que si la rotación nos llama y CD es 0, deberíamos usarlo).
            // Lo que haremos es: Si la protección lógica nos dice "NO", verificamos si vale la pena.

            // Por defecto, prioridad Alta.
            var priority = WeavePriority.High;

            // 2. LÓGICA DEL TRAIT (Protección de Recursos)
            if (playerLevel >= TRAIT_LEVEL_ENHANCED_EA)
            {
                // --- ESCENARIO A: WANDERER'S MINUET ---
                if (ctx.CurrentSong == Song.Wanderer)
                {
                    // Si tenemos 3 stacks, lo ideal es gastar Pitch Perfect primero.
                    if (ctx.Repertoire >= 3)
                    {
                        // PERO: Si EA se va a usar en el Slot 2 (Double Weave), 
                        // podemos asumir que Pitch Perfect iría en el Slot 1.
                        // Sin embargo, como esta lógica solo retorna un plan para EA, 
                        // si retornamos null, perdemos el turno.

                        // DECISIÓN: Retornamos null SOLO si confiamos en que Pitch Perfect saldrá AHORA.
                        // Si no, bloqueamos el EA.

                        // Para evitar el drift de 3s:
                        // Si estamos en Minuet, EA DEBE salir. 
                        // Es preferible "comerse" un stack que retrasar EA.
                        // Solo bloqueamos si estamos MUY seguros de que PP sale ya.

                        // Cambio V2.1: NO BLOQUEAR.
                        // PitchPerfectLogic tiene prioridad más baja en BardRotation, PERO
                        // si devolvemos EA aquí, el Scheduler intentará meterlo.
                        // La solución real es que PitchPerfect debe ir ANTES que EA en BardRotation.cs 
                        // si estamos llenos. (Revisaremos eso abajo).

                        // Por ahora, en esta lógica aislada: NO BLOQUEAR.
                        // Tirar EA con 3 stacks es DPS loss, pero driftar EA 3s es peor.
                    }
                }

                // --- ESCENARIO B: MAGE'S BALLAD ---
                if (ctx.CurrentSong == Song.Mage)
                {
                    int maxBL = (playerLevel >= 84) ? 3 : 2;

                    // Si estamos llenos de cargas, usar EA desperdicia la reducción de CD.
                    if (ctx.BloodletterCharges >= maxBL)
                    {
                        // Misma lógica: Es mejor desperdiciar la reducción que retrasar EA.
                        // NO retornamos null. Lo usamos.
                    }
                }
            }

            // 3. RETORNO INCONDICIONAL (Si está listo, ÚSALO)
            // Hemos eliminado los bloqueos "return null". 
            // La gestión de no sobrellenar debe hacerse vaciando agresivamente ANTES de que EA esté listo,
            // no bloqueando EA cuando ya llegó su hora.

            return new OgcdPlan(BRD_IDs.EmpyrealArrow, priority, WeaveSlot.Any);
        }
    }
}
