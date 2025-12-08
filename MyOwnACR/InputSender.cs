// Archivo: MyOwnACR/InputSender.cs
// Descripción: Gestor de inputs con cola dedicada.
// AJUSTES: Issue #3 - Manejo explícito de excepciones y logging en Dispose/Worker.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MyOwnACR
{
    public struct InputTask
    {
        public byte Key;
        public HotbarType BarType;
        public bool IsGCD;
        public string Source;

        public InputTask(byte key, HotbarType barType, bool isGCD, string source = "")
        {
            Key = key;
            BarType = barType;
            IsGCD = isGCD;
            Source = source;
        }
    }

    public static class InputSender
    {
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12;

        // =====================================================================================
        // 1. CONFIGURACIÓN DE TIEMPOS
        // =====================================================================================

        // --- RITMO RELAJADO (GCD -> GCD) ---
        private const int RelaxedDelayMs = 200;
        private const int RelaxedJitterMs = 50;

        // --- MODO ANSIOSO (Weaving / Queueing) ---
        private const int AnxiousDelayMs = 150;
        private const int AnxiousJitterMs = 25;

        // --- SPAM ---
        private const int MinSpamCount = 2;
        private const int MaxSpamCount = 3;
        private const int SpamIntervalMs = 100;
        private const int SpamIntervalJitterMs = 15;

        // --- FÍSICA ---
        private const int KeyHoldMs = 100;
        private const int KeyHoldJitterMs = 15;

        // =====================================================================================

        private static BlockingCollection<InputTask> _inputQueue = new BlockingCollection<InputTask>();

        // Inicialización segura con null!
        private static CancellationTokenSource _cts = null!;
        private static Task _workerTask = null!;
        private static bool _initialized = false;

        private static DateTime _lastSentTime = DateTime.MinValue;
        private static bool _lastWasGCD = true;

        // Variables para Anti-Duplicación (Debounce)
        private static DateTime _lastInputAddedTime = DateTime.MinValue;
        private static byte _lastInputKey = 0;

        private static readonly Random _rng = new Random();

        public static void Initialize()
        {
            if (_initialized) return;
            _cts = new CancellationTokenSource();
            _inputQueue = new BlockingCollection<InputTask>();

            // Usamos LongRunning para asegurar un hilo dedicado
            _workerTask = Task.Factory.StartNew(WorkerLoop, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            _initialized = true;
            Plugin.Instance?.SendLog("[InputSender] Worker iniciado.");
            Plugin.Log.Debug("[InputSender] Sistema inicializado.");
        }

        public static void Dispose()
        {
            if (!_initialized) return;

            _cts.Cancel();
            _inputQueue.CompleteAdding();

            try
            {
                // Esperar a que el worker termine (máx 1s)
                _workerTask.Wait(1000);
            }
            catch (AggregateException ae)
            {
                // Manejar la excepción esperada de cancelación
                ae.Handle(e => e is TaskCanceledException);
            }
            catch (Exception ex)
            {
                // Loguear cualquier otro error inesperado al cerrar
                if (Plugin.Log != null) Plugin.Log.Warning($"Error cerrando InputSender: {ex.Message}");
            }

            _inputQueue.Dispose();
            _cts.Dispose();
            _initialized = false;
        }

        public static void Send(byte key, HotbarType barType, bool isGCD)
        {
            if (!_initialized) Initialize();

            // Anti-Duplicación
            var now = DateTime.UtcNow;
            if (key == _lastInputKey && (now - _lastInputAddedTime).TotalMilliseconds < 200)
            {
                return;
            }

            if (!_inputQueue.IsAddingCompleted)
            {
                _inputQueue.Add(new InputTask(key, barType, isGCD));
                _lastInputAddedTime = now;
                _lastInputKey = key;
            }
        }

        private static void WorkerLoop()
        {
            try
            {
                foreach (var task in _inputQueue.GetConsumingEnumerable(_cts.Token))
                {
                    ProcessTask(task);
                }
            }
            catch (OperationCanceledException) { /* Normal al cerrar */ }
            catch (Exception ex)
            {
                // Loguear error crítico en ambas consolas
                Plugin.Log.Error(ex, "InputSender Worker ha fallado.");
                Plugin.Instance?.SendLog($"[InputSender Error] {ex.Message}");
            }
        }

        private static void ProcessTask(InputTask task)
        {
            bool isRelaxedTransition = _lastWasGCD && task.IsGCD;
            _lastWasGCD = task.IsGCD;

            int delayBase = isRelaxedTransition ? RelaxedDelayMs : AnxiousDelayMs;
            int delayJitter = isRelaxedTransition ? RelaxedJitterMs : AnxiousJitterMs;

            int calculatedDelay = delayBase + _rng.Next(-delayJitter, delayJitter + 1);
            if (calculatedDelay < 5) calculatedDelay = 5;

            var elapsed = (DateTime.UtcNow - _lastSentTime).TotalMilliseconds;
            if (elapsed < calculatedDelay)
            {
                Thread.Sleep(calculatedDelay - (int)elapsed);
            }

            _lastSentTime = DateTime.UtcNow;

            PressModifiers(task.BarType);

            int clicks = _rng.Next(MinSpamCount, MaxSpamCount + 1);

            for (int i = 0; i < clicks; i++)
            {
                if (_cts.Token.IsCancellationRequested) break;

                int hold = KeyHoldMs + _rng.Next(-KeyHoldJitterMs, KeyHoldJitterMs + 1);
                keybd_event(task.Key, 0, 0, 0);
                Thread.Sleep(hold);
                keybd_event(task.Key, 0, KEYEVENTF_KEYUP, 0);

                if (i < clicks - 1)
                {
                    int gap = SpamIntervalMs + _rng.Next(-SpamIntervalJitterMs, SpamIntervalJitterMs + 1);
                    Thread.Sleep(gap);
                }
            }

            ReleaseModifiers(task.BarType);
        }

        private static void PressModifiers(HotbarType barType)
        {
            switch (barType)
            {
                case HotbarType.Barra2_Ctrl: keybd_event(VK_CONTROL, 0, 0, 0); break;
                case HotbarType.Barra3_Shift: keybd_event(VK_SHIFT, 0, 0, 0); break;
                case HotbarType.Barra4_Alt: keybd_event(VK_MENU, 0, 0, 0); break;
                case HotbarType.Barra5_CtrlAlt: keybd_event(VK_CONTROL, 0, 0, 0); keybd_event(VK_MENU, 0, 0, 0); break;
            }
        }

        private static void ReleaseModifiers(HotbarType barType)
        {
            switch (barType)
            {
                case HotbarType.Barra2_Ctrl: keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); break;
                case HotbarType.Barra3_Shift: keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0); break;
                case HotbarType.Barra4_Alt: keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0); break;
                case HotbarType.Barra5_CtrlAlt: keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0); keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); break;
            }
        }
    }
}
