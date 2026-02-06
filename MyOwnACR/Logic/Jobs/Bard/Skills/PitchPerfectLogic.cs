// Archivo: Logic/Jobs/Bard/Skills/PitchPerfectLogic.cs
// VERSIÓN: V2.1 - ANTI-DRIFT SAFETY
// DESCRIPCIÓN: Gestión de Pitch Perfect con zona de seguridad estricta para proteger la canción.

using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.JobGauge.Enums;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class PitchPerfectLogic
    {
        // CORTE DE CANCIÓN: 3000ms (3s)

        // VENTANA DE DUMP (Vaciado):
        // Empezamos a vaciar cuando falten 6s (6000ms).
        // DEJAMOS de vaciar cuando falten 3.8s (3800ms).
        // Esto deja 0.8s libres antes del corte de canción para asegurar que el slot esté vacío.

        private const float DUMP_START_MS = 6000f;
        private const float DUMP_STOP_MS = 3800f; // Zona de Exclusión

        public static OgcdPlan? GetPlan(BardContext ctx)
        {
            // Solo activo en Minuet
            if (ctx.CurrentSong != Song.Wanderer) return null;
            if (ctx.Repertoire == 0) return null;

            // 1. PRIORIDAD MÁXIMA: 3 Stacks
            // Si tenemos 3, disparamos siempre (High Priority).
            // EXCEPCIÓN: Si estamos en la "Zona de Exclusión" (< 3.8s), 
            // es debatible, pero generalmente preferimos perder 3 stacks a driftar la canción.
            // Para seguridad total, aplicamos la restricción de tiempo también aquí si es MUY crítico.
            // Pero normalmente 3 stacks valen el riesgo. Dejaremos los 3 stacks libres, 
            // pero el Dump de 1-2 stacks lo restringiremos.

            if (ctx.Repertoire == 3)
            {
                // Si estamos a punto de cortar la canción (ej. < 3.5s), NO disparamos.
                // Preferimos perder los stacks a retrasar el Army's Paeon/Ballad.
                if (ctx.SongTimerMS < DUMP_STOP_MS) return null;

                return new OgcdPlan(BRD_IDs.PitchPerfect, WeavePriority.High, WeaveSlot.Any);
            }

            // 2. DUMP FINAL (Vaciado de 1 o 2 stacks)
            // Lógica: "Si estamos en la ventana de salida, PERO aun hay tiempo seguro".

            // Si falta menos de 6s Y falta más de 3.8s
            if (ctx.SongTimerMS < DUMP_START_MS && ctx.SongTimerMS > DUMP_STOP_MS)
            {
                // Usamos prioridad High para intentar sacarlo en este weave window.
                return new OgcdPlan(BRD_IDs.PitchPerfect, WeavePriority.High, WeaveSlot.Any);
            }

            // 3. ZONA MUERTA (< 3.8s)
            // Si llegamos aquí y quedan 3.5s, retornamos null.
            // El bot no hará nada con PP, dejando el hueco libre para que SongLogic entre a los 3.0s.

            return null;
        }
    }
}
