// Archivo: Logic/Common/InputSender.cs
// Descripción: Gestor de inputs con cola dedicada.
// ACTUALIZADO: Añadido CastAction para soporte de ActionScheduler.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.Logic.Core; // Para acceder a Plugin.Instance.Config
using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.GameData.Common;

namespace MyOwnACR.Logic.Common // Nota: Asegúrate que el namespace sea correcto (Common o Core)
{
    public struct InputTask
    {
        public byte Key;
        public HotbarType BarType;
        public bool IsGCD;
        public string Source;

        public InputTask(byte key, HotbarType barType, bool isGCD, string source = "")
        {
            Key = key; BarType = barType; IsGCD = isGCD; Source = source;
        }
    }

    public static class InputSender
    {
        // ... (DLL Imports y Constantes se mantienen igual) ...
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12;

        // ... (Tus constantes de Tiempos Relaxed/Anxious se mantienen igual) ...
        private const int RelaxedDelayMs = 350;
        private const int RelaxedJitterMs = 100;
        private const int AnxiousDelayMs = 180;
        private const int AnxiousJitterMs = 50;
        private const int MinSpamCount = 2;
        private const int MaxSpamCount = 4;
        private const int SpamIntervalMs = 160;
        private const int SpamIntervalJitterMs = 40;
        private const int KeyHoldMs = 90;
        private const int KeyHoldJitterMs = 25;
        private const int RelaxedPrePressMin = 800;
        private const int RelaxedPrePressMax = 1200;
        private const int AnxiousPrePressMin = 500;
        private const int AnxiousPrePressMax = 800;

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
            // Plugin.Instance?.SendLog("[InputSender] Iniciado.");
        }

        public static void Dispose()
        {
            if (!Initialized) return;
            Cts.Cancel();
            InputQueue.CompleteAdding();
            try { WorkerTask.Wait(1000); } catch { }
            InputQueue.Dispose();
            Cts.Dispose();
            Initialized = false;
        }

        // =========================================================================
        // NUEVO MÉTODO: CAST ACTION
        // Usado por ActionScheduler para disparar habilidades por ID
        // =========================================================================
        public static unsafe bool CastAction(uint actionId)
        {
            if (actionId == 0) return false;

            // 1. Verificar si usamos Memoria Directa (Opción Dashboard)
            if (Plugin.Instance.Config.Operation.UseMemoryInput_v2)
            {
                var am = ActionManager.Instance();
                var player = Plugin.ObjectTable.LocalPlayer;
                if (am != null && player != null)
                {
                    ulong targetId = player.TargetObject?.GameObjectId ?? player.GameObjectId;
                    return am->UseAction(ActionType.Action, actionId, targetId);
                }
                return false;
            }

            // 2. Modo Teclado (Simulación)
            // Necesitamos encontrar la tecla asociada a este ActionID
            var bind = FindKeyBindForAction(actionId);

            if (bind != null && bind.Key != 0)
            {
                bool isGcd = ActionLibrary.IsGCD(actionId);
                Send((byte)bind.Key, (HotbarType)bind.Bar, isGcd);
                return true;
            }

            // Si no hay tecla mapeada, intentamos usar memoria como fallback silencioso
            // Plugin.Log.Debug($"[Input] No key for {actionId}, trying memory fallback.");
            var amFallback = ActionManager.Instance();
            var pFallback = Plugin.ObjectTable.LocalPlayer;
            if (amFallback != null && pFallback != null)
            {
                ulong tId = pFallback.TargetObject?.GameObjectId ?? pFallback.GameObjectId;
                return amFallback->UseAction(ActionType.Action, actionId, tId);
            }

            return false;
        }

        // Helper para buscar en la configuración qué tecla corresponde al ID
        private static KeyBind? FindKeyBindForAction(uint actionId)
        {
            var config = Plugin.Instance.Config;
            var player = Plugin.ObjectTable.LocalPlayer;
            if (player == null) return null;

            // Determinar Job actual para saber qué config mirar
            if (player.ClassJob.RowId == JobDefinitions.MNK)
            {
                return FindBindInObject(config.Monk, actionId);
            }
            else if (player.ClassJob.RowId == JobDefinitions.BRD)
            {
                return FindBindInObject(config.Bard, actionId);
            }

            return null;
        }

        private static KeyBind? FindBindInObject(object jobConfig, uint actionId)
        {
            // Estrategia: Obtener el nombre de la acción por ID y buscar propiedad con ese nombre
            // Esto asume que el nombre de la propiedad en JobConfig coincide con el nombre en ActionLibrary (ej: "Bootshine")
            // Es lento (Reflection), pero solo se llama al iniciar la acción.

            // Un método más rápido sería tener un Diccionario<uint, KeyBind> cacheado en RotationManager.
            // Por simplicidad ahora usaremos Reflection inverso.

            // ... Implementación simplificada: ...
            // Como ActionLibrary tiene nombres "Bootshine", y Config tiene "Bootshine", podemos cruzarlo.

            // 1. Obtener nombre string del ID
            string actionName = ActionLibrary.GetName(actionId);
            if (string.IsNullOrEmpty(actionName)) return null;

            // 2. Buscar propiedad en config
            var prop = jobConfig.GetType().GetField(actionName);
            if (prop != null) return prop.GetValue(jobConfig) as KeyBind;

            var prop2 = jobConfig.GetType().GetProperty(actionName);
            if (prop2 != null) return prop2.GetValue(jobConfig) as KeyBind;

            return null;
        }

        // =========================================================================

        public static void Send(byte key, HotbarType barType, bool isGCD)
        {
            if (!Initialized) Initialize();

            var now = DateTime.UtcNow;
            // Anti-Duplicate rápido (si mandamos la misma tecla muy seguido)
            if (key == LastInputKey && (now - LastInputAddedTime).TotalMilliseconds < 100)
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

        // ... (El resto del WorkerLoop y lógica de Press/Release se mantiene igual) ...

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
            catch (Exception) { }
        }

        private static void ProcessTask(InputTask task)
        {
            // ... (Copia tu lógica de timing original aquí) ...
            // Para ahorrar espacio en esta respuesta, mantén tu lógica de Relaxed/Anxious tal cual la tenías.
            // Solo asegúrate de que ProcessTask llame a keybd_event.

            bool isRelaxedTransition = LastWasGCD && task.IsGCD;
            LastWasGCD = task.IsGCD;

            int delayBase = isRelaxedTransition ? RelaxedDelayMs : AnxiousDelayMs;
            int delayJitter = isRelaxedTransition ? RelaxedJitterMs : AnxiousJitterMs;
            int calculatedDelay = delayBase + Rng.Next(-delayJitter, delayJitter + 1);
            int prePressWindow = isRelaxedTransition ? Rng.Next(RelaxedPrePressMin, RelaxedPrePressMax) : Rng.Next(AnxiousPrePressMin, AnxiousPrePressMax);

            calculatedDelay -= prePressWindow;
            if (calculatedDelay < 5) calculatedDelay = 5;

            var elapsed = (DateTime.UtcNow - LastSentTime).TotalMilliseconds;
            if (elapsed < calculatedDelay) Thread.Sleep(calculatedDelay - (int)elapsed);
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
