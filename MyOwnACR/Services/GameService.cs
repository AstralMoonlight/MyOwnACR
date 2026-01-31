// Archivo: Services/GameService.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;

namespace MyOwnACR.Services
{
    public class GameService
    {
        private readonly Plugin _plugin;

        // =========================================================================
        // P/INVOKE (Windows API) - Ahora viven aquí, no ensucian el Plugin principal
        // =========================================================================
#pragma warning disable SYSLIB1054 
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
#pragma warning restore SYSLIB1054

        public GameService(Plugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>
        /// Trae la ventana del juego al frente. Útil cuando se envían comandos desde la Web.
        /// </summary>
        public void FocusGame()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error enfocando ventana del juego");
            }
        }

        /// <summary>
        /// Verifica si es seguro ejecutar acciones (Bot).
        /// Revisa si la ventana está activa o si se permite input en segundo plano.
        /// </summary>
        public bool IsSafeToAct(out string failReason)
        {
            failReason = "";

            // 1. Si usamos memoria, siempre es seguro (no requiere foco)
            if (_plugin.Config.Operation.UseMemoryInput) return true;

            // 2. Verificar Foco de Ventana
            var gameHandle = Process.GetCurrentProcess().MainWindowHandle;
            var activeHandle = GetForegroundWindow();

            // Si hay una ventana activa y NO es el juego
            if (activeHandle != IntPtr.Zero && gameHandle != activeHandle)
            {
                failReason = $"Ventana Inactiva (Juego: {gameHandle}, Foco: {activeHandle})";
                return false;
            }

            return true;
        }
    }
}
