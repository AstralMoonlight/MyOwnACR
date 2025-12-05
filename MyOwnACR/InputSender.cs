using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MyOwnACR
{
    public static class InputSender
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12; // Alt

        // Control de ritmo / random
        private static readonly object SendLock = new();
        private static DateTime LastSent = DateTime.MinValue;
        private static readonly Random Rng = new();

        // --- CONFIGURACIÓN DE REPETICIÓN (TUS VALORES) ---
        private const int MinSpamClicks = 2;  // Mínimo de clicks por acción
        private const int MaxSpamClicks = 3;  // Máximo de clicks por acción

        // Tiempo entre clicks del mismo spam (rápido, como tecleo frenético)
        private const int SpamIntervalMs = 150;
        private const int SpamIntervalJitterMs = 110;

        // Parámetros base (se les aplica jitter)
        private const int BaseMinDelayBetweenKeysMs = 300; // intervalo medio entre habilidades distintas
        private const int MinDelayJitterMs = 70;
        private const int BaseHoldMs = 75;  // tiempo medio con la tecla abajo
        private const int HoldJitterMs = 45;

        // Método maestro para presionar teclas según tu sistema de barras
        public static void Send(byte key, HotbarType barType)
        {
            // No bloquear el hilo de Dalamud
            ThreadPool.QueueUserWorkItem(_ => SendInternal(key, barType));
        }

        private static void SendInternal(byte key, HotbarType barType)
        {
            lock (SendLock)
            {
                var now = DateTime.UtcNow;

                // --- LÓGICA DE "GOLPE ANSIOSO" (ANXIOUS PRESS) ---
                // Probabilidad del 40% de ignorar el delay base de 300ms y pulsar casi de inmediato.
                // Esto simula el intentar meter la habilidad en la cola (queueing) antes de que esté lista.
                bool anxiousPress = Rng.NextDouble() < 0.40;
                    
                int minDelay;

                if (anxiousPress)
                {
                    // Delay muy corto (10-40ms) para simular impaciencia/queueing
                    minDelay = Rng.Next(10, 40);
                }
                else
                {
                    // Ritmo "humano" normal usando tu configuración base (400ms)
                    minDelay = BaseMinDelayBetweenKeysMs + Rng.Next(-MinDelayJitterMs, MinDelayJitterMs + 1);
                    if (minDelay < 60) minDelay = 60;
                    if (minDelay > 1000) minDelay = 1000; // Cap de seguridad
                }

                var diff = (now - LastSent).TotalMilliseconds;
                if (diff < minDelay)
                    Thread.Sleep(minDelay - (int)diff);

                LastSent = DateTime.UtcNow;

                // 2) Pulsar modificadores
                PressModifiers(barType);

                // 3) Determinar cuántas veces vamos a "spamear" la tecla (2 a 4)
                int clicks = Rng.Next(MinSpamClicks, MaxSpamClicks + 1);

                // 4) Bucle de pulsaciones
                for (int i = 0; i < clicks; i++)
                {
                    // Duración aleatoria de ESTA pulsación específica
                    int holdMs = BaseHoldMs + Rng.Next(-HoldJitterMs, HoldJitterMs + 1);
                    if (holdMs < 40) holdMs = 40;
                    if (holdMs > 150) holdMs = 150;

                    keybd_event(key, 0, 0, 0);                  // key down
                    Thread.Sleep(holdMs);                       // mantener
                    keybd_event(key, 0, KEYEVENTF_KEYUP, 0);    // key up

                    // Si no es el último click, esperamos según tu intervalo configurado
                    if (i < clicks - 1)
                    {
                        // Si fue un "anxious press", reducimos un poco el intervalo del spam también para que parezca más frenético
                        int currentInterval = anxiousPress ? (SpamIntervalMs - 40) : SpamIntervalMs;

                        int gap = currentInterval + Rng.Next(-SpamIntervalJitterMs, SpamIntervalJitterMs + 1);
                        if (gap < 30) gap = 30; // Mínimo físico razonable

                        Thread.Sleep(gap);
                    }
                }

                // 5) Soltar modificadores
                ReleaseModifiers(barType);

                // 6) Pequeña pausa aleatoria “de salida”
                // Si fue anxious, salimos más rápido
                int tailPause = Rng.Next(0, anxiousPress ? 20 : 50);
                if (tailPause > 0)
                    Thread.Sleep(tailPause);
            }
        }

        private static void PressModifiers(HotbarType barType)
        {
            switch (barType)
            {
                case HotbarType.Barra2_Ctrl:
                    keybd_event(VK_CONTROL, 0, 0, 0);
                    break;
                case HotbarType.Barra3_Shift:
                    keybd_event(VK_SHIFT, 0, 0, 0);
                    break;
                case HotbarType.Barra4_Alt:
                    keybd_event(VK_MENU, 0, 0, 0);
                    break;
                case HotbarType.Barra5_CtrlAlt:
                    keybd_event(VK_CONTROL, 0, 0, 0);
                    keybd_event(VK_MENU, 0, 0, 0);
                    break;
            }
        }

        private static void ReleaseModifiers(HotbarType barType)
        {
            switch (barType)
            {
                case HotbarType.Barra2_Ctrl:
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                    break;
                case HotbarType.Barra3_Shift:
                    keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                    break;
                case HotbarType.Barra4_Alt:
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
                    break;
                case HotbarType.Barra5_CtrlAlt:
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                    break;
            }
        }
    }
}
