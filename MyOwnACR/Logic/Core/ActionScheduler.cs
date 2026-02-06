// Archivo: Logic/Core/ActionScheduler.cs
// VERSIÓN: V3.3 - HIGH PING OPTIMIZED (LATAM MODE)
// DESCRIPCIÓN: Estrategia "Fire & Forget".
//              1. GCD: Se spamea directo a la cola del servidor (Queue) sin esperar animación local.
//              2. oGCD: Se sacrifica agresivamente si pone en riesgo el GCD principal.

using System;
using System.Diagnostics;
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
        // --- CONSTANTES DE TIMING (Modo Latencia Alta) ---

        // DANGER ZONE (Zona de Peligro): 0.85s
        // Si falta menos de esto para que vuelva el GCD, NO lanzamos oGCDs.
        // Ping 150ms + Anim 600ms = 750ms "reales". Dejamos 100ms de margen.
        private const float DANGER_ZONE = 0.85f;

        // GCD QUEUE WINDOW: 0.6s
        // Tiempo antes del GCD en el que empezamos a spamear la habilidad al servidor.
        private const float GCD_QUEUE_WINDOW = 0.6f;

        // Intervalo de Spam para oGCDs (ms)
        private const long SPAM_MS_OGCD = 20;

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

            // 1. CÁLCULO DE TIEMPOS
            float totalGcd = am->GetRecastTime(ActionType.Action, 11);
            float elapsedGcd = am->GetRecastTimeElapsed(ActionType.Action, 11);
            float remainingGcd = (totalGcd > 0) ? Math.Max(0, totalGcd - elapsedGcd) : 0;
            float animLock = am->AnimationLock;

            // 2. INYECCIÓN (Stuns, Interrupciones - Prioridad Absoluta)
            if (injectedOgcd.HasValue && injectedOgcd.Value.Priority == WeavePriority.Forced)
            {
                // Permitimos inyectar incluso si hay bloqueo leve
                if (animLock <= 0.3f)
                {
                    if (UseAction(am, injectedOgcd.Value.ActionId, targetId)) { injectedOgcd = null; return; }
                }
            }

            // =================================================================================
            // 3. LÓGICA DE GCD (MODO "SIN CONFIRMACIÓN")
            // =================================================================================

            // Si estamos en la ventana de cola (últimos 0.6s)...
            if (remainingGcd <= GCD_QUEUE_WINDOW)
            {
                if (CurrentCycle.GcdActionId != 0)
                {
                    // AQUÍ ESTÁ EL CAMBIO CLAVE:
                    // No verificamos 'animLock'. Asumimos que si estamos en los últimos 0.6s,
                    // cualquier animación anterior ya debería estar terminando en el servidor.
                    // Enviamos la orden para que entre a la cola (Action Queue) del juego.

                    // Lógica de spam rápido en los últimos 0.2s para asegurar el frame
                    bool criticalZone = remainingGcd < 0.2f;

                    if (criticalZone || _spamTimer.ElapsedMilliseconds >= 20)
                    {
                        if (UseAction(am, CurrentCycle.GcdActionId, targetId))
                        {
                            _spamTimer.Restart();
                        }
                    }
                }

                // RETORNO: Protegemos el GCD. No permitimos oGCDs en esta ventana.
                return;
            }

            // =================================================================================
            // 4. LÓGICA DE WEAVING (oGCDs)
            // =================================================================================

            // Esperamos un mínimo de desbloqueo de animación (muy breve).
            if (animLock > 0.1f) return;

            // Throttle para no saturar
            if (_spamTimer.ElapsedMilliseconds < SPAM_MS_OGCD) return;

            uint actionToUse = 0;
            bool consumedSlot1 = false;
            bool consumedInjected = false;

            // Selección de Acción
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
            // Double Weave (Slot 2)
            else if (CurrentCycle.Ogcd1 == null && CurrentCycle.Ogcd2.HasValue)
            {
                // Filtro extra de seguridad para el segundo weave con ping alto
                // Si falta menos de 1.25s, es arriesgado meter un segundo oGCD.
                if (remainingGcd > 1.25f)
                {
                    actionToUse = ResolveOgcd(am, CurrentCycle.Ogcd2.Value, remainingGcd);
                    if (actionToUse != 0) CurrentCycle.Ogcd2 = null;
                }
            }

            // Ejecución
            if (actionToUse != 0)
            {
                if (UseAction(am, actionToUse, targetId))
                {
                    _spamTimer.Restart();

                    // Limpieza de slots tras uso exitoso
                    if (consumedInjected) injectedOgcd = null;
                    if (consumedSlot1)
                    {
                        CurrentCycle.Ogcd1 = null;
                        // Movemos Slot 2 a Slot 1
                        if (CurrentCycle.Ogcd2.HasValue)
                        {
                            CurrentCycle.Ogcd1 = CurrentCycle.Ogcd2;
                            CurrentCycle.Ogcd2 = null;
                        }
                    }
                }
            }
        }

        private bool UseAction(ActionManager* am, uint actionId, ulong targetId)
        {
            return InputSender.CastAction(actionId);
        }

        private uint ResolveOgcd(ActionManager* am, OgcdPlan plan, float remainingGcd)
        {
            // 1. Chequeo de Cooldown real
            if (am->GetActionStatus(ActionType.Action, plan.ActionId) != 0) return 0;

            // 2. Forced Pass (Ignora Danger Zone)
            if (plan.Priority == WeavePriority.Forced) return plan.ActionId;

            // 3. DANGER ZONE CHECK (El guardián del Drift)
            // Si falta poco para el GCD, abortamos el oGCD.

            float threshold = DANGER_ZONE; // 0.85s por defecto

            // Si es High Priority, nos arriesgamos un poco más (0.70s)
            if (plan.Priority == WeavePriority.High) threshold = 0.70f;

            if (remainingGcd < threshold) return 0;

            return plan.ActionId;
        }

        // --- MÉTODOS PÚBLICOS ---
        public void SetNextCycle(uint gcd, OgcdPlan? ogcd1 = null, OgcdPlan? ogcd2 = null)
        {
            // Evitar reasignación si no hay cambios
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
