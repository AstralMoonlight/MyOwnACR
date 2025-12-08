// Archivo: MyOwnACR/InputSender.cs
// Descripción: Gestor de inputs con cola dedicada.
// AJUSTES: Tiempos personalizados del usuario + Filtro Anti-Duplicación (Debounce).

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
        // 1. CONFIGURACIÓN DE TIEMPOS (TUS VALORES PERSONALIZADOS)
        // =====================================================================================

        // --- RITMO RELAJADO (GCD -> GCD) ---
        // Se aplica solo cuando vienes de un GCD y vas a otro GCD (ej. Combo normal).
        private const int RelaxedDelayMs = 150;
        private const int RelaxedJitterMs = 30;

        // --- MODO ANSIOSO (Weaving / Queueing) ---
        // Se aplica en cualquier otro caso (GCD->oGCD, oGCD->oGCD).
        // NOTA: Si MNK_Logic usa Queueing (0.5s antes), este delay se suma.
        // Con 150ms, el worker esperará un poco antes de empezar el spam.
        private const int AnxiousDelayMs = 70;
        private const int AnxiousJitterMs = 15;

        // --- SPAM (MACHACAR BOTÓN) ---
        private const int MinSpamCount = 2;
        private const int MaxSpamCount = 3;

        // Intervalo entre pulsaciones (Spam lento para evitar saturación)
        private const int SpamIntervalMs = 80;
        private const int SpamIntervalJitterMs = 15;

        // --- FÍSICA ---
        private const int KeyHoldMs = 80;
        private const int KeyHoldJitterMs = 15;

        // =====================================================================================

        private static BlockingCollection<InputTask> _inputQueue = new BlockingCollection<InputTask>();

        // Inicialización segura con null!
        private static CancellationTokenSource _cts = null!;
        private static Task _workerTask = null!;
        private static bool _initialized = false;

        // Estado interno
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
            _workerTask = Task.Factory.StartNew(WorkerLoop, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _initialized = true;
            Plugin.Instance?.SendLog("[InputSender] Worker iniciado (Cola dedicada).");
        }

        public static void Dispose()
        {
            if (!_initialized) return;
            _cts.Cancel();
            _inputQueue.CompleteAdding();
            try { _workerTask.Wait(1000); } catch { }
            _inputQueue.Dispose();
            _cts.Dispose();
            _initialized = false;
        }

        public static void Send(byte key, HotbarType barType, bool isGCD)
        {
            if (!_initialized) Initialize();

            // --- FILTRO ANTI-DUPLICACIÓN (DEBOUNCE) ---
            // Si intentamos enviar la MISMA tecla en menos de 200ms, asumimos que es 
            // la lógica ejecutándose dos veces antes de bloquearse. Ignoramos la segunda.
            var now = DateTime.UtcNow;
            if (key == _lastInputKey && (now - _lastInputAddedTime).TotalMilliseconds < 200)
            {
                return; // Ignorar duplicado
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
            catch (OperationCanceledException) { }
            catch (Exception ex) { Plugin.Instance?.SendLog($"[InputSender Error] {ex.Message}"); }
        }

        private static void ProcessTask(InputTask task)
        {
            // --- 1. LÓGICA DE RITMO CONTEXTUAL ---
            // Solo usamos modo relajado si es GCD puro -> GCD puro.
            // Si hay weaving o pre-pull, usamos modo ansioso.
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

            // --- 2. EJECUCIÓN FÍSICA ---
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
