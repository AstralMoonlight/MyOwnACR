// Archivo: Logic/Jobs/Bard/Skills/EmpyrealArrowLogic.cs
// Descripción: Módulo de decisión para Empyreal Arrow.
// CORRECCIÓN: Adaptado para CD simple (15s) sin cargas múltiples.

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    /// <summary>
    /// Contiene la lógica de evaluación para la habilidad 'Empyreal Arrow'.
    /// Gestiona el CD de 15s y los efectos del Trait Nvl 68.
    /// </summary>
    public static class EmpyrealArrowLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx, int playerLevel)
        {
            // 1. CHEQUEOS BÁSICOS
            // Si no tenemos nivel, fuera.
            if (playerLevel < BRD_Levels.EmpyrealArrow) return null;

            // Verificar si está en Cooldown. 
            // Buffer de 0.6s para compensar latencia/animación (Ready).
            if (ctx.EmpyrealCD > 0.6f) return null;

            // Configuración por defecto
            var priority = WeavePriority.Normal;
            var slotPref = WeaveSlot.Any;

            // 2. LÓGICA DEL TRAIT NIVEL 68 (Enhanced Empyreal Arrow)
            // "Garantiza el trigger del efecto de la canción actual".
            if (playerLevel >= 68)
            {
                // --- ESCENARIO A: WANDERER'S MINUET ---
                // Efecto: Genera 1 Stack de Pitch Perfect.
                if (ctx.CurrentSong == Song.Wanderer)
                {
                    // BLOQUEO (Overcap): Si ya tenemos 3 stacks, usar EA quema el proc.
                    // Retornamos null para que el RotationManager use Pitch Perfect primero.
                    if (ctx.Repertoire >= 3)
                    {
                        return null;
                    }

                    // OPTIMIZACIÓN: Si tenemos 2 stacks, EA nos lleva a 3.
                    // Preferimos SlotB para dejar espacio a procs naturales en SlotA.
                    if (ctx.Repertoire == 2)
                    {
                        slotPref = WeaveSlot.SlotB;
                    }
                }

                // --- ESCENARIO B: MAGE'S BALLAD ---
                // Efecto: Reduce el CD de Bloodletter/Rain of Death en 7.5s.
                if (ctx.CurrentSong == Song.Mage)
                {
                    int maxBL = (playerLevel >= 84) ? 3 : 2;

                    // BLOQUEO (Overcap): Si Bloodletter está lleno, la reducción se pierde.
                    // Bloqueamos EA para forzar el gasto de Bloodletter.
                    if (ctx.BloodletterCharges >= maxBL)
                    {
                        return null;
                    }
                }
            }

            // 3. PRIORIDAD
            // Como es una habilidad de CD corto (15s) que genera recursos, 
            // siempre queremos que salga lo antes posible (High Priority)
            // para no driftar el CD a lo largo de la pelea.
            priority = WeavePriority.High;

            return new OgcdPlan(BRD_IDs.EmpyrealArrow, priority, slotPref);
        }
    }
}
