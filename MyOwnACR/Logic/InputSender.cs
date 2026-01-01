// Archivo: MyOwnACR/InputSender.cs
// Descripción: Gestor de inputs con cola dedicada.
// AJUSTES: Issue #3 - Manejo explícito de excepciones y logging en Dispose/Worker.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MyOwnACR.Logic
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

        private static BlockingCollection<InputTask> InputQueue = new BlockingCollection<InputTask>();

        // Inicialización segura con null!
        private static CancellationTokenSource Cts = null!;
        private static Task WorkerTask = null!;
        private static bool Initialized = false;

        private static DateTime LastSentTime = DateTime.MinValue;
        private static bool LastWasGCD = true;

        // Variables para Anti-Duplicación (Debounce)
        private static DateTime LastInputAddedTime = DateTime.MinValue;
        private static byte LastInputKey = 0;

        private static readonly Random Rng = new Random();

        public static void Initialize()
        {
            if (Initialized) return;
            Cts = new CancellationTokenSource();
            InputQueue = new BlockingCollection<InputTask>();

            // Usamos LongRunning para asegurar un hilo dedicado
            WorkerTask = Task.Factory.StartNew(WorkerLoop, Cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Initialized = true;
            Plugin.Instance?.SendLog("[InputSender] Worker iniciado.");
            Plugin.Log.Debug("[InputSender] Sistema inicializado.");
        }

        public static void Dispose()
        {
            if (!Initialized) return;

            Cts.Cancel();
            InputQueue.CompleteAdding();

            try
            {
                // Esperar a que el worker termine (máx 1s)
                WorkerTask.Wait(1000);
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

            InputQueue.Dispose();
            Cts.Dispose();
            Initialized = false;
        }

        public static void Send(byte key, HotbarType barType, bool isGCD)
        {
            if (!Initialized) Initialize();

            // Anti-Duplicación
            var now = DateTime.UtcNow;
            if (key == LastInputKey && (now - LastInputAddedTime).TotalMilliseconds < 200)
            {
                return;
            }

            if (!InputQueue.IsAddingCompleted)
            {
                InputQueue.Add(new InputTask(key, barType, isGCD));
                LastInputAddedTime = now;
                LastInputKey = key;
            }
        }

        private static void WorkerLoop()
        {
            try
            {
                foreach (var task in InputQueue.GetConsumingEnumerable(Cts.Token))
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
            bool isRelaxedTransition = LastWasGCD && task.IsGCD;
            LastWasGCD = task.IsGCD;

            int delayBase = isRelaxedTransition ? RelaxedDelayMs : AnxiousDelayMs;
            int delayJitter = isRelaxedTransition ? RelaxedJitterMs : AnxiousJitterMs;

            int calculatedDelay = delayBase + Rng.Next(-delayJitter, delayJitter + 1);
            if (calculatedDelay < 5) calculatedDelay = 5;

            var elapsed = (DateTime.UtcNow - LastSentTime).TotalMilliseconds;
            if (elapsed < calculatedDelay)
            {
                Thread.Sleep(calculatedDelay - (int)elapsed);
            }

            LastSentTime = DateTime.UtcNow;

            PressModifiers(task.BarType);

            int clicks = Rng.Next(MinSpamCount, MaxSpamCount + 1);

            for (int i = 0; i < clicks; i++)
            {
                if (Cts.Token.IsCancellationRequested) break;

                int hold = KeyHoldMs + Rng.Next(-KeyHoldJitterMs, KeyHoldJitterMs + 1);
                keybd_event(task.Key, 0, 0, 0);
                Thread.Sleep(hold);
                keybd_event(task.Key, 0, KEYEVENTF_KEYUP, 0);

                if (i < clicks - 1)
                {
                    int gap = SpamIntervalMs + Rng.Next(-SpamIntervalJitterMs, SpamIntervalJitterMs + 1);
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
