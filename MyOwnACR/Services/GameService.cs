// Archivo: Services/GameService.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace MyOwnACR.Services
{
    public class GameService
    {
        private readonly Plugin _plugin;
        private readonly IntPtr _gameWindowHandle;

        // =========================================================================
        // P/INVOKE (Windows API)
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

            // Cacheamos el handle de la ventana al iniciar para ahorrar CPU
            try
            {
                using var process = Process.GetCurrentProcess();
                _gameWindowHandle = process.MainWindowHandle;
            }
            catch { _gameWindowHandle = IntPtr.Zero; }
        }

        /// <summary>
        /// Trae la ventana del juego al frente.
        /// </summary>
        public void FocusGame()
        {
            if (_gameWindowHandle == IntPtr.Zero) return;

            try
            {
                SetForegroundWindow(_gameWindowHandle);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error enfocando ventana del juego");
            }
        }

        /// <summary>
        /// Verifica si es seguro ejecutar acciones (Bot).
        /// </summary>
        public bool IsSafeToAct(out string failReason)
        {
            failReason = "";

            // 1. Checks de estado del Juego (Crucial para evitar crashes o bugs)
            // No actuar en pantallas de carga o cinemáticas
            if (Plugin.Condition[ConditionFlag.BetweenAreas] ||
                Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent])
            {
                failReason = "Juego ocupado (Carga/Cinemática)";
                return false;
            }

            // 2. Lógica de Foco de Ventana
            // Si usamos memoria, permitimos ejecutar en segundo plano (Alt-Tab)
            if (_plugin.Config.Operation.UseMemoryInput_v2)
            {
                return true;
            }

            // Si NO usamos memoria (teclas legacy), verificamos el foco de Windows
            var activeHandle = GetForegroundWindow();

            if (_gameWindowHandle != IntPtr.Zero && activeHandle != _gameWindowHandle)
            {
                failReason = $"Ventana Inactiva (Juego: {_gameWindowHandle}, Foco: {activeHandle})";
                return false;
            }

            return true;
        }
    }
}
