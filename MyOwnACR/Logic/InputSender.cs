// Archivo: MyOwnACR/InputSender.cs
// Descripción: Gestor de inputs con cola dedicada.
// AJUSTES: v2.4 - Ultra Relaxed / Safe Human Profile.

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
        // 1. CONFIGURACIÓN DE TIEMPOS (PERFIL ULTRA RELAJADO - SAFE)
        // =====================================================================================

        /* --- BACKUP: CONFIGURACIÓN v2.3 (Human Competitive) ---
        private const int RelaxedDelayMs = 250;
        private const int RelaxedJitterMs = 80;
        private const int AnxiousDelayMs = 100;
        private const int AnxiousJitterMs = 30;
        private const int MinSpamCount = 2;
        private const int MaxSpamCount = 5;
        private const int SpamIntervalMs = 120;
        private const int SpamIntervalJitterMs = 40;
        private const int KeyHoldMs = 70;
        private const int KeyHoldJitterMs = 20;
        private const int RelaxedPrePressMin = 750;
        private const int RelaxedPrePressMax = 950;
        private const int AnxiousPrePressMin = 650;
        private const int AnxiousPrePressMax = 800;
        */

        // --- RITMO RELAJADO (GCD -> GCD) ---
        // Esperamos visualmente a que la animación termine.
        private const int RelaxedDelayMs = 350;
        private const int RelaxedJitterMs = 100;

        // --- MODO WEAVING (oGCDs) ---
        // No nos estresamos por el clipping, preferimos seguridad.
        private const int AnxiousDelayMs = 180;
        private const int AnxiousJitterMs = 50;

        // --- SPAM (Button Mashing) ---
        // Mashing lento y deliberado.
        private const int MinSpamCount = 2;
        private const int MaxSpamCount = 4;
        private const int SpamIntervalMs = 160; // ~6 clicks por segundo (Lento)
        private const int SpamIntervalJitterMs = 40;

        // --- FÍSICA DE TECLA ---
        // Pulsación larga y marcada.
        private const int KeyHoldMs = 90;
        private const int KeyHoldJitterMs = 25;

        // --- PRE-PRESS (ANTICIPACIÓN) ---
        // Empezamos a apretar con mucha antelación (casi 1 segundo), 
        // como quien espera el cooldown con el dedo en el botón.

        // Relaxed: 0.8s a 1.2s antes.
        private const int RelaxedPrePressMin = 800;
        private const int RelaxedPrePressMax = 1200;

        // Anxious: 0.5s a 0.8s antes.
        private const int AnxiousPrePressMin = 500;
        private const int AnxiousPrePressMax = 800;

        // =====================================================================================

        private static BlockingCollection<InputTask> InputQueue = new BlockingCollection<InputTask>();
        private static CancellationTokenSource Cts = null!;
        private static Task WorkerTask = null!;
        private static bool Initialized = false;

        private static DateTime LastSentTime = DateTime.MinValue;
        private static bool LastWasGCD = true;

        private static DateTime LastInputAddedTime = DateTime.MinValue;
        private static byte LastInputKey = 0;

        private static readonly Random Rng = new Random();

        public static void Initialize()
        {
            if (Initialized) return;
            Cts = new CancellationTokenSource();
            InputQueue = new BlockingCollection<InputTask>();

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
                WorkerTask.Wait(1000);
            }
            catch (AggregateException ae)
            {
                ae.Handle(e => e is TaskCanceledException);
            }
            catch (Exception ex)
            {
                if (Plugin.Log != null) Plugin.Log.Warning($"Error cerrando InputSender: {ex.Message}");
            }

            InputQueue.Dispose();
            Cts.Dispose();
            Initialized = false;
        }

        public static void Send(byte key, HotbarType barType, bool isGCD)
        {
            if (!Initialized) Initialize();

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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "InputSender Worker ha fallado.");
                Plugin.Instance?.SendLog($"[InputSender Error] {ex.Message}");
            }
        }

        private static void ProcessTask(InputTask task)
        {
            // Determinamos si estamos en modo "Relajado" (GCD tras GCD) o "Ansioso" (Weaving)
            bool isRelaxedTransition = LastWasGCD && task.IsGCD;
            LastWasGCD = task.IsGCD;

            // 1. Calcular el Delay Base (Tiempo de reacción visual + latencia mental)
            int delayBase = isRelaxedTransition ? RelaxedDelayMs : AnxiousDelayMs;
            int delayJitter = isRelaxedTransition ? RelaxedJitterMs : AnxiousJitterMs;
            int calculatedDelay = delayBase + Rng.Next(-delayJitter, delayJitter + 1);

            // 2. Calcular Pre-Press (Adelantarse al CD)
            int prePressWindow = isRelaxedTransition
                ? Rng.Next(RelaxedPrePressMin, RelaxedPrePressMax)
                : Rng.Next(AnxiousPrePressMin, AnxiousPrePressMax);

            calculatedDelay -= prePressWindow;

            if (calculatedDelay < 5) calculatedDelay = 5;

            // Esperar el tiempo calculado
            var elapsed = (DateTime.UtcNow - LastSentTime).TotalMilliseconds;
            if (elapsed < calculatedDelay)
            {
                Thread.Sleep(calculatedDelay - (int)elapsed);
            }

            LastSentTime = DateTime.UtcNow;

            // Ejecutar pulsaciones
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
