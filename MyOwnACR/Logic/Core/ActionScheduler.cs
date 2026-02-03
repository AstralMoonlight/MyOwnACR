// Archivo: Logic/Core/ActionScheduler.cs
// VERSIÓN: V3.1 - FRAME PERFECT / ANTI-DRIFT
// DESCRIPCIÓN: Implementación de Action Queue real para eliminar retrasos (GCD 2.50s exactos).

using System;
using System.Diagnostics; // CRÍTICO: Alta precisión para medir frames
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using MyOwnACR.Logic.Common;

#pragma warning disable IDE1006

namespace MyOwnACR.Logic.Core
{
    public enum WeavePriority { Normal, High, Forced }
    public enum WeaveSlot { Any, SlotA, SlotB }

    public struct OgcdPlan
    {
        public uint ActionId;
        public WeavePriority Priority;
        public WeaveSlot PreferredSlot;
        public OgcdPlan(uint id, WeavePriority prio = WeavePriority.Normal, WeaveSlot slot = WeaveSlot.Any)
        {
            ActionId = id; Priority = prio; PreferredSlot = slot;
        }
    }

    public class CombatCycle
    {
        public uint GcdActionId { get; set; } = 0;
        public OgcdPlan? Ogcd1 { get; set; } = null;
        public OgcdPlan? Ogcd2 { get; set; } = null;

        public void Set(uint gcd, OgcdPlan? o1 = null, OgcdPlan? o2 = null)
        {
            GcdActionId = gcd; Ogcd1 = o1; Ogcd2 = o2;
        }
    }

    public unsafe class ActionScheduler
    {
        // --- CONSTANTES DE TIMING (Fix Float vs Double) ---

        // Zona de peligro: Si falta menos de esto para el GCD, NO usar oGCDs para no retrasar el golpe principal.
        // Aumentado a 0.60s para seguridad con pings medios.
        private const float DANGER_ZONE = 0.60f;

        // Ventana de Cola del Servidor: FFXIV acepta comandos 0.5s antes.
        private const float GCD_QUEUE_WINDOW = 0.5f;

        // Límite de Animación para GCD: Si la animación es larguísima (>0.6s), esperamos un poco.
        // Pero si es normal (<0.6s), enviamos el GCD para que entre en cola.
        private const float ANIM_LOCK_MAX_FOR_GCD = 0.6f;

        // Límite estricto para oGCDs: Debemos estar quietos para usar oGCDs.
        private const float ANIM_LOCK_STRICT = 0.05f;

        // --- INTERVALOS DE SPAM (Stopwatch usa long/enteros) ---
        // 16ms = 1 frame a 60FPS. Ponemos 15ms para asegurar el disparo en cada frame posible.
        private const long SPAM_MS_GCD = 15;
        private const long SPAM_MS_OGCD = 40; // oGCDs un poco más relajados para no saturar input.

        private readonly Stopwatch _spamTimer = new Stopwatch();

        public CombatCycle CurrentCycle { get; private set; } = new CombatCycle();
        private OgcdPlan? injectedOgcd = null;

        private readonly IChatGui _chat;
        private readonly IDataManager _dataManager;

        public ActionScheduler(IDalamudPluginInterface pluginInterface, IChatGui chat, IDataManager dataManager)
        {
            _chat = chat;
            _dataManager = dataManager;
            _spamTimer.Start();
        }

        public void Update(ActionManager* am, IPlayerCharacter player)
        {
            if (am == null || player == null) return;
            ulong targetId = player.TargetObject?.GameObjectId ?? player.GameObjectId;

            // 1. CÁLCULO DE TIEMPOS (Lectura directa de memoria)
            float totalGcd = am->GetRecastTime(ActionType.Action, 11);
            float elapsedGcd = am->GetRecastTimeElapsed(ActionType.Action, 11);
            // Math.Max para evitar negativos flotantes raros
            float remainingGcd = (totalGcd > 0) ? Math.Max(0, totalGcd - elapsedGcd) : 0;
            float animLock = am->AnimationLock;

            // 2. INYECCIÓN (Prioridad Absoluta - Interrupciones, Stuns)
            if (injectedOgcd.HasValue && injectedOgcd.Value.Priority == WeavePriority.Forced)
            {
                // Permitimos inyectar incluso con un poco de anim lock si es FORZADO (ej. Stun de emergencia)
                if (animLock <= 0.1f)
                {
                    if (UseAction(am, injectedOgcd.Value.ActionId, targetId)) { injectedOgcd = null; return; }
                }
            }

            // =================================================================================
            // 3. LÓGICA DE GCD (EL FIX DEL DRIFT)
            // =================================================================================
            // Si estamos en los últimos 0.5s del cooldown... ¡A la cola!
            if (remainingGcd <= GCD_QUEUE_WINDOW)
            {
                if (CurrentCycle.GcdActionId != 0)
                {
                    // CRITICAL ZONE: Si faltan < 0.1s (100ms), ignoramos el timer y spameamos a velocidad CPU.
                    // Esto asegura cazar el frame 0.000 exacto.
                    bool criticalZone = remainingGcd < 0.1f;

                    if (criticalZone || _spamTimer.ElapsedMilliseconds >= SPAM_MS_GCD)
                    {
                        // AQUÍ ESTÁ EL CAMBIO CLAVE:
                        // Permitimos enviar el GCD aunque 'animLock' no sea 0, siempre que sea razonable (< 0.6s).
                        // Esto llena el "Action Queue" del servidor.
                        if (animLock < ANIM_LOCK_MAX_FOR_GCD)
                        {
                            if (UseAction(am, CurrentCycle.GcdActionId, targetId))
                            {
                                _spamTimer.Restart();
                            }
                        }
                    }
                }
                // IMPORTANTE: Si estamos en ventana de GCD, NUNCA intentamos weavear oGCDs.
                // Retornamos para proteger el GCD.
                return;
            }

            // =================================================================================
            // 4. LÓGICA DE WEAVING (oGCDs)
            // =================================================================================

            // Para oGCDs (Bloodletter, Empyreal) somos estrictos: 
            // NO se puede usar si hay AnimLock activo.
            if (animLock > ANIM_LOCK_STRICT) return;

            // Respetamos el throttle para oGCDs
            if (_spamTimer.ElapsedMilliseconds < SPAM_MS_OGCD) return;

            uint actionToUse = 0;
            bool consumedSlot1 = false;
            bool consumedInjected = false;

            // Prioridad: Inyectado > Slot 1 > Slot 2
            if (injectedOgcd.HasValue)
            {
                actionToUse = ResolveOgcd(am, injectedOgcd.Value, remainingGcd);
                if (actionToUse != 0) consumedInjected = true;
            }
            else if (CurrentCycle.Ogcd1.HasValue)
            {
                actionToUse = ResolveOgcd(am, CurrentCycle.Ogcd1.Value, remainingGcd);
                if (actionToUse != 0) consumedSlot1 = true;
            }
            else if (CurrentCycle.Ogcd1 == null && CurrentCycle.Ogcd2.HasValue)
            {
                actionToUse = ResolveOgcd(am, CurrentCycle.Ogcd2.Value, remainingGcd);
                if (actionToUse != 0) CurrentCycle.Ogcd2 = null;
            }

            // Ejecución de oGCD
            if (actionToUse != 0)
            {
                if (UseAction(am, actionToUse, targetId))
                {
                    _spamTimer.Restart();

                    // Limpieza de slots usados
                    if (consumedInjected) injectedOgcd = null;
                    if (consumedSlot1)
                    {
                        CurrentCycle.Ogcd1 = null;
                        // Si usamos Slot 1, movemos Slot 2 a Slot 1 para el siguiente ciclo
                        if (CurrentCycle.Ogcd2.HasValue)
                        {
                            CurrentCycle.Ogcd1 = CurrentCycle.Ogcd2;
                            CurrentCycle.Ogcd2 = null;
                        }
                    }
                }
            }
        }

        // Envía la acción al juego a través del InputSender
        private bool UseAction(ActionManager* am, uint actionId, ulong targetId)
        {
            // Asumimos que InputSender maneja la llamada a memoria o teclado
            return InputSender.CastAction(actionId);
        }

        // Decide si es seguro usar un oGCD
        private uint ResolveOgcd(ActionManager* am, OgcdPlan plan, float remainingGcd)
        {
            // 1. ¿Está en cooldown?
            if (am->GetActionStatus(ActionType.Action, plan.ActionId) != 0) return 0;

            // 2. Si es FORZADO, ignoramos Danger Zone
            if (plan.Priority == WeavePriority.Forced) return plan.ActionId;

            // 3. DANGER ZONE: 
            // Si falta poco para el GCD (ej. 0.6s), NO tirar oGCDs.
            // Esto evita que el oGCD retrase el siguiente GCD (Clipping).
            if (remainingGcd < DANGER_ZONE && plan.Priority != WeavePriority.High) return 0;

            return plan.ActionId;
        }

        // --- MÉTODOS PÚBLICOS ---

        public void SetNextCycle(uint gcd, OgcdPlan? ogcd1 = null, OgcdPlan? ogcd2 = null)
        {
            // Evitar re-asignación si el plan es idéntico (micro-optimización)
            if (CurrentCycle.GcdActionId == gcd && IsPlanEqual(CurrentCycle.Ogcd1, ogcd1) && IsPlanEqual(CurrentCycle.Ogcd2, ogcd2)) return;
            CurrentCycle.Set(gcd, ogcd1, ogcd2);
        }

        public void InjectOgcd(uint actionId, WeavePriority prio = WeavePriority.Forced)
        {
            injectedOgcd = new OgcdPlan(actionId, prio, WeaveSlot.Any);
        }

        private bool IsPlanEqual(OgcdPlan? a, OgcdPlan? b)
        {
            if (!a.HasValue && !b.HasValue) return true;
            if (!a.HasValue || !b.HasValue) return false;
            return a.Value.ActionId == b.Value.ActionId;
        }
    }
}
