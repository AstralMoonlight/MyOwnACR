using Dalamud.Game.ClientState.Objects.Types;
using MyOwnACR.Logic.Core; // Para ActionScheduler

namespace MyOwnACR.Logic.Jobs.Samurai.Skills
{
    public static class MovementLogic
    {
        // Umbral de Slidecast (0.5s antes de terminar el cast ya te puedes mover)
        private const float SLIDECAST_WINDOW = 0.5f;

        /// <summary>
        /// Determina si debemos detener el movimiento para permitir un Cast (Iaijutsu).
        /// </summary>
        public static bool ShouldStopMoving(SamuraiContext context)
        {
            // 1. Si no estamos casteando nada, no hay que parar
            if (context.Player.CastActionId == 0) return false;

            // 2. Verificar si es un cast de Samurái (Iaijutsus)
            // (Opcional: podrías filtrar por IDs específicos si quisieras, 
            // pero parar en cualquier cast es lo seguro para SAM).

            // 3. Lógica de Slidecast
            // Si falta poco para terminar el cast, NO paramos (dejamos que el usuario se mueva)
            if (context.Player.CurrentCastTime + SLIDECAST_WINDOW >= context.Player.TotalCastTime)
            {
                return false;
            }

            // 4. Si estamos a mitad del cast, pedimos STOP.
            return true;
        }
    }
}
