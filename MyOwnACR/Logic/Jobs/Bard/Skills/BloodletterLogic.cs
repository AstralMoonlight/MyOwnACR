// Archivo: Logic/Jobs/Bard/Skills/BloodletterLogic.cs
// Descripción: Lógica de Pooling para Heartbreak Shot / Bloodletter.
// REGLA: "Guardar 2 cargas para Raging Strikes, gastar solo para evitar Overcap".

using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class BloodletterLogic
    {
        public static OgcdPlan? GetPlan(BardContext ctx, int playerLevel, bool useAoE)
        {
            // 1. ID CORRECTO
            uint actionId;
            if (useAoE)
                actionId = (playerLevel >= BRD_Levels.RainOfDeath) ? BRD_IDs.RainOfDeath : BRD_IDs.Bloodletter;
            else
                actionId = (playerLevel >= BRD_Levels.HeartbreakShot) ? BRD_IDs.HeartbreakShot : BRD_IDs.Bloodletter;

            // Sin cargas, nada que hacer
            if (ctx.BloodletterCharges == 0) return null;

            int maxCharges = (playerLevel >= 84) ? 3 : 2;

            // -------------------------------------------------------------------------
            // ESCENARIO 1: BURST (Raging Strikes Activo) -> GASTAR TODO
            // -------------------------------------------------------------------------
            if (ctx.IsRagingStrikesActive)
            {
                // Prioridad High para vaciar rápido dentro de la ventana de buffs
                return new OgcdPlan(actionId, WeavePriority.High, WeaveSlot.Any);
            }

            // -------------------------------------------------------------------------
            // ESCENARIO 2: MAGE'S BALLAD (Procs Aleatorios) -> NO POOLING
            // -------------------------------------------------------------------------
            // En Balada, los procs resetean/reducen el CD. Si guardamos cargas, 
            // es muy probable que perdamos procs por overcap.
            // Estrategia: Mantener vacío.
            if (ctx.CurrentSong == Song.Mage)
            {
                // Gastar siempre que tengamos algo, para hacer espacio al siguiente proc.
                return new OgcdPlan(actionId, WeavePriority.Normal, WeaveSlot.Any);
            }

            // -------------------------------------------------------------------------
            // ESCENARIO 3: POOLING (Army's Paeon / Minuet sin Burst)
            // -------------------------------------------------------------------------
            // Aquí queremos llegar al próximo Raging Strikes con 3 cargas (o casi).

            // A. Tenemos 3 Cargas (Lleno) -> Gastar UNA obligatoriamente para activar el timer.
            if (ctx.BloodletterCharges >= maxCharges)
            {
                return new OgcdPlan(actionId, WeavePriority.High, WeaveSlot.Any);
            }

            // B. Tenemos 2 Cargas -> Zona de Peligro
            // Solo gastamos si la 3ra carga está a punto de llegar (< 4s).
            // Si falta mucho para la 3ra carga, GUARDAMOS las 2 actuales.
            if (ctx.BloodletterCharges == maxCharges - 1)
            {
                // ctx.BloodletterCD es el tiempo que falta para recuperar una carga.
                if (ctx.BloodletterCD < 4.0f)
                {
                    return new OgcdPlan(actionId, WeavePriority.Normal, WeaveSlot.Any);
                }
                return null; // HOLD
            }

            // C. Tenemos 0 o 1 Carga -> GUARDAR (HOLD)
            return null;
        }
    }
}
