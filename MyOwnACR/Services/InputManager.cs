using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using System;

namespace MyOwnACR.Services
{
    public class InputManager
    {
        private readonly Plugin _plugin;
        private readonly IKeyState _keyState;

        // Estado para evitar rebotes (debounce)
        private bool _isHotkeyDown = false;
        private bool _isSaveCdKeyDown = false;

        public InputManager(Plugin plugin, IKeyState keyState)
        {
            _plugin = plugin;
            _keyState = keyState;
        }

        public void Update()
        {
            // 1. START / STOP (Configurable)
            // ----------------------------------------------------
            var mainToggleState = _keyState[_plugin.Config.ToggleHotkey];
            if (mainToggleState && !_isHotkeyDown)
            {
                _isHotkeyDown = true;
                _plugin.ToggleRunning();
            }
            else if (!mainToggleState)
            {
                _isHotkeyDown = false;
            }

            // 2. SAVE CD TOGGLE Numpad "-"
            // ----------------------------------------------------
            bool isMinusPressed = _keyState[VirtualKey.SUBTRACT];

            if (isMinusPressed && !_isSaveCdKeyDown)
            {
                _isSaveCdKeyDown = true;

                // Cambiamos el valor
                _plugin.Config.Operation.SaveCD = !_plugin.Config.Operation.SaveCD;
                _plugin.Config.Save();

                // Feedback visual
                var status = _plugin.Config.Operation.SaveCD ? "ACTIVADO (No Burst)" : "DESACTIVADO (Burst ON)";
                _plugin.SendLog($"[HOTKEY] Save Cooldowns: {status}");
            }
            else if (!isMinusPressed)
            {
                _isSaveCdKeyDown = false;
            }
        }
    }
}
