// Archivo: Logic/Core/MovementLogic.cs
using MyOwnACR.Logic.Jobs.Samurai; // O el contexto genérico si lo abstraes

namespace MyOwnACR.Logic.Core
{
    public static class MovementLogic
    {
        // TIEMPO DE SLIDECAST: 0.5 segundos.
        // Si falta menos de esto para terminar el cast, el servidor ya registró el daño.
        // Puedes moverte libremente.
        private const float SLIDECAST_WINDOW = 0.5f;

        /// <summary>
        /// Determina si el jugador debe detenerse INMEDIATAMENTE para proteger un casteo.
        /// </summary>
        public static bool ShouldStopMoving(SamuraiContext ctx)
        {
            // 1. Si no estamos casteando, somos libres.
            if (!ctx.IsCasting) return false;

            // 2. Si estamos casteando, revisamos cuánto falta.
            // Si falta MUCHO (más de 0.5s), debemos detenernos.
            if (ctx.CastTimeRemaining > SLIDECAST_WINDOW)
            {
                return true; // ¡ALTO! Freno de mano.
            }

            // 3. Si falta POCO (0.4s, 0.3s...), es zona de Slidecast.
            // Podemos movernos mientras termina la animación.
            return false;
        }
    }
}
