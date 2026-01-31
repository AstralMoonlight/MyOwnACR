// Archivo: Logic/Jobs/Bard/BardRotation.cs
// VERSIÓN: V20.0 - FINAL DOCUMENTED
// DESCRIPCIÓN: Orquestador principal de la lógica del Bardo.
// INTEGRACIÓN: Usa Helpers.cs para consultas y módulos (Skills/*.cs) para decisiones.

using System;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using MyOwnACR.JobConfigs;
using MyOwnACR.GameData;
using MyOwnACR.Logic.Core;
using MyOwnACR.Logic.Interfaces;
using MyOwnACR.Logic.Common;
using MyOwnACR.Logic.Jobs.Bard.Skills;
using MyOwnACR.Models;
using InventoryManager = MyOwnACR.Logic.Core.InventoryManager;

namespace MyOwnACR.Logic.Jobs.Bard
{
    public unsafe class BardRotation : IJobLogic
    {
        public static BardRotation Instance { get; } = new BardRotation();
        private BardRotation() { }

        // --- IJobLogic Properties ---
        public uint JobId => 23;
        public uint LastProposedAction { get; private set; } = 0;

        // --- STATE MANAGEMENT ---
        // Contexto: Memoria rápida del frame actual
        private readonly BardContext context = new();

        // Flags de Combate y Control
        private bool wasInCombat = false;
        private uint queuedAction = 0;
        private DateTime queueExpire = DateTime.MinValue;
        private bool isManualInputActive = false;
        private uint lastOpenerActionId = 0;

        // Timers locales (Throttling para evitar spam de paquetes)
        private DateTime lastIronJawsTime = DateTime.MinValue;
        private DateTime lastSongTime = DateTime.MinValue;

        // --- Métodos de Interfaz (UI & Debug) ---
        public void QueueManualAction(uint actionId) { queuedAction = actionId; queueExpire = DateTime.Now.AddSeconds(2.0); }
        public void QueueManualAction(string actionName) { var id = ActionLibrary.GetIdByName(actionName); if (id != 0) QueueManualAction(id); }
        public string GetQueuedAction() => queuedAction != 0 ? ActionLibrary.GetName(queuedAction) : "";
        public void PrintDebugInfo(IChatGui chat) { chat.Print($"Song={context.CurrentSong} | Charges={context.BloodletterCharges} | EncoreTimer={context.RadiantEncoreTimeLeft:F1}s"); }

        // =================================================================================
        // EXECUTE (Bucle Principal ~60fps)
        // =================================================================================
        public void Execute(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            // Seguridad: Evitamos crasheos por leer memoria inválida
            if (am == null || player == null) return;

            // 0. UPDATE CONTEXT
            // Actualizamos la "foto" del estado actual del juego
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            context.Update(am, gauge, scheduler, player);

            // 1. INPUT MANUAL (PRIORIDAD ABSOLUTA)
            // Si el usuario presiona una tecla mapeada, interrumpimos la lógica automática.
            CheckManualInput(config.Bard);
            if (queuedAction != 0)
            {
                if (DateTime.Now > queueExpire)
                {
                    queuedAction = 0; // Expiró la orden manual
                }
                else
                {
                    scheduler.InjectOgcd(queuedAction, WeavePriority.Forced);
                    LastProposedAction = queuedAction;
                    // Si el juego confirma que entró en cooldown, limpiamos la cola
                    if (am->GetRecastTimeElapsed(ActionType.Action, queuedAction) > 0) queuedAction = 0;
                    return;
                }
            }

            // -----------------------------------------------------------------------------
            // 2. OPENER STATE MANAGEMENT (INTELIGENTE)
            // -----------------------------------------------------------------------------
            // Detectamos combate tanto en el jugador como en el objetivo para reaccionar rápido.
            var target = player.TargetObject as IBattleChara;
            bool playerInCombat = Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            bool targetInCombat = target != null && target.StatusFlags.HasFlag(StatusFlags.InCombat);
            bool actualCombatState = playerInCombat || targetInCombat;

            // Lógica de Inicio (Flanco de Subida: No Combate -> Combate)
            if (actualCombatState && !wasInCombat)
            {
                if (config.Operation.UseOpener && !config.Operation.SaveCD && target != null && target.IsTargetable)
                {
                    // VALIDACIÓN DE RECURSOS:
                    // Si venimos de una muerte o cambio de fase, verificamos que tengamos los CDs.
                    if (Helpers.CanStartOpener(am, player))
                    {
                        OpenerManager.Instance.SelectOpener(config.Operation.SelectedOpener);
                        OpenerManager.Instance.Start();
                        Plugin.Log.Debug("[ACR] Opener Iniciado (Condiciones Óptimas)");
                    }
                    else
                    {
                        Plugin.Log.Debug("[ACR] Opener OMITIDO: CDs en enfriamiento (Recuperación/Faseo).");
                    }
                }
            }

            // Lógica de Fin (Flanco de Bajada: Combate -> No Combate)
            if (!actualCombatState && wasInCombat)
            {
                OpenerManager.Instance.Stop();
                Plugin.Log.Debug("[ACR] Combate finalizado. Reset.");
            }
            wasInCombat = actualCombatState;

            // -----------------------------------------------------------------------------
            // 3. EJECUTAR OPENER
            // -----------------------------------------------------------------------------
            // Si el Opener está corriendo, tiene control total. Salimos del método.
            if (OpenerManager.Instance.IsRunning)
            {
                ExecuteOpenerLogic(scheduler, am, player, config);
                return;
            }

            // 4. TARGET CHECK
            // Si no estamos en Opener y no hay combate real, no hacemos nada.
            if (!actualCombatState) return;
            if (target == null || target.IsDead) return;

            // =============================================================================
            // 5. ROTACIÓN ESTÁNDAR
            // =============================================================================
            var op = config.Operation;
            uint nextGcd = 0;
            List<OgcdPlan> plans = new List<OgcdPlan>();

            // -----------------------------------------------------------------------------
            // A. CANCIONES (Prioridad Crítica)
            // -----------------------------------------------------------------------------
            if (config.Bard.AutoSong && !op.SaveCD)
            {
                // Throttle de 2s para no spamear decisiones de canción
                if ((DateTime.Now - lastSongTime).TotalSeconds > 2.0)
                {
                    var songPlan = SongLogic.GetPlan(context, player.Level);

                    if (songPlan.HasValue)
                    {
                        uint songId = songPlan.Value.ActionId;
                        if (Helpers.CanUse(am, songId))
                        {
                            bool safe = true;
                            // Late-Weave check: Si toca Minuet, aseguramos que haya espacio en el GCD
                            if (songId == BRD_IDs.WanderersMinuet && am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot) < 1.1f) safe = false;

                            if (safe) { scheduler.InjectOgcd(songId, WeavePriority.Forced); lastSongTime = DateTime.Now; return; }
                        }
                    }
                }
            }

            // -----------------------------------------------------------------------------
            // B. LÓGICA DE GCD (Acciones Principales)
            // -----------------------------------------------------------------------------

            // 1. ANÁLISIS DE ESTADO (DOTS)
            var (hasStorm, hasCaustic, stormTime, causticTime) = Helpers.GetTargetDotStatus(player);

            // Throttle de seguridad (4.0s) para Iron Jaws
            bool ironJawsSafe = (DateTime.Now - lastIronJawsTime).TotalSeconds > 4.0;

            // Definimos IDs según nivel (Downgrade automático)
            uint actionStorm = player.Level >= 64 ? BRD_IDs.Stormbite : BRD_IDs.Windbite;
            uint actionCaustic = player.Level >= 64 ? BRD_IDs.CausticBite : BRD_IDs.VenomousBite;

            // 2. SISTEMA DE PRIORIDADES (Cascada)

            // PRIORIDAD 1: BLAST ARROW (Nvl 86+)
            // Golpe más fuerte, proc de corta duración.
            if (Helpers.HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                nextGcd = BRD_IDs.BlastArrow;

            // PRIORIDAD 2: MANTENIMIENTO DE DOTS (IRON JAWS)
            else if (player.Level >= 56 && hasStorm && hasCaustic)
            {
                // Snapshotting: Renovar si Raging Strikes está por acabar (< 3.5s)
                float rsTime = Helpers.GetStatusTime(player, BRD_IDs.Status_RagingStrikes);
                bool isSnapshot = rsTime > 0 && rsTime < 3.5f;

                // Pandemic: Renovar si quedan < 6s
                bool isFalling = stormTime < 6.0f || causticTime < 6.0f;

                if ((isSnapshot || isFalling) && ironJawsSafe)
                {
                    nextGcd = BRD_IDs.IronJaws;
                    if (Helpers.CanUse(am, BRD_IDs.IronJaws)) lastIronJawsTime = DateTime.Now;
                }
            }
            // PRIORIDAD 2.5: APLICACIÓN INICIAL
            else if (ironJawsSafe)
            {
                if (!hasStorm) nextGcd = actionStorm;
                else if (!hasCaustic) nextGcd = actionCaustic;
            }

            // PRIORIDAD 3: PROCS DE DAÑO DIRECTO
            if (nextGcd == 0)
            {
                // Resonant Arrow (Proc de Barrage)
                if (Helpers.HasStatus(player, BRD_IDs.Status_ResonantArrowReady))
                    nextGcd = BRD_IDs.ResonantArrow;
                else
                {
                    // Radiant Encore (Proc de Radiant Finale - Decisión delegada a BuffLogic)
                    uint encoreGcd = BuffLogic.GetEncoreGcd(context, player.Level);
                    if (encoreGcd != 0) nextGcd = encoreGcd;
                }
            }

            // PRIORIDAD 4: GESTIÓN DE BARRA (APEX ARROW)
            if (nextGcd == 0 && player.Level >= 80)
            {
                // Caso A: Barra llena (100) -> Overcap
                bool overcap = gauge.SoulVoice == 100;
                // Caso B: Burst Dumping -> Buffs activos + Barra alta
                bool burstDump = context.IsRagingStrikesActive && gauge.SoulVoice >= 80;

                if (overcap || burstDump) nextGcd = BRD_IDs.ApexArrow;
            }

            // 3. FILLERS (RELLENO)
            if (nextGcd == 0)
            {
                bool useAoE = op.AoE_Enabled && CombatHelpers.CountAttackableEnemiesInRange(objectTable, target, 10f) >= 3;

                if (useAoE)
                {
                    // AoE Moderno: Shadowbite (Proc) > Ladonsbite/QuickNock
                    if (player.Level >= 72 && Helpers.HasStatus(player, BRD_IDs.Status_ShadowbiteReady))
                        nextGcd = BRD_IDs.Shadowbite;
                    else
                        nextGcd = (player.Level >= 82) ? BRD_IDs.Ladonsbite : BRD_IDs.QuickNock;
                }
                else
                {
                    // Single Target: Refulgent (Proc) > Burst/Heavy
                    bool proc = Helpers.HasStatus(player, BRD_IDs.Status_StraightShotReady) || Helpers.HasStatus(player, BRD_IDs.Status_HawksEye);
                    nextGcd = proc ? BRD_IDs.RefulgentArrow : (player.Level >= 76 ? BRD_IDs.BurstShot : BRD_IDs.HeavyShot);
                }
            }
            LastProposedAction = nextGcd;

            // -----------------------------------------------------------------------------
            // C. LÓGICA DE oGCD (Weaving)
            // -----------------------------------------------------------------------------

            // 1. GESTIÓN DE POCIONES (ESTRATEGIA: ALINEACIÓN > DURACIÓN)
            if (!op.SaveCD && op.UseRoF && op.UsePotion && op.SelectedPotionId != 0)
            {
                if (InventoryManager.IsPotionReady(am, op.SelectedPotionId))
                {
                    float rsCD = context.RagingStrikesCD;

                    // CASO A: Anticipación (El hueco ideal pre-burst 1.2s - 6.0s)
                    if (rsCD > 1.2f && rsCD < 6.0f)
                    {
                        plans.Add(new OgcdPlan(op.SelectedPotionId, WeavePriority.High, WeaveSlot.Any));
                    }
                    // CASO B: Recuperación Ordenada (RS > BV > RF > Potion)
                    else if (context.IsRagingStrikesActive)
                    {
                        bool bvDone = player.Level < BRD_Levels.BattleVoice || context.IsBattleVoiceActive;
                        bool rfDone = player.Level < BRD_Levels.RadiantFinale || context.IsRadiantFinaleActive;

                        if (bvDone && rfDone)
                            plans.Add(new OgcdPlan(op.SelectedPotionId, WeavePriority.High, WeaveSlot.Any));
                    }
                }
            }

            // 2. BUFFS DE DAÑO (BuffLogic)
            // Lanza RS, BV y RF en cascada.
            if (!op.SaveCD && op.UseRoF)
            {
                var buffPlan = BuffLogic.GetPlan(context, player.Level);
                if (buffPlan.HasValue) plans.Add(buffPlan.Value);
            }

            // 3. MECÁNICAS DE JOB
            // Pitch Perfect: Alta prioridad para evitar perder stacks
            var ppPlan = PitchPerfectLogic.GetPlan(context);
            if (ppPlan.HasValue) plans.Add(ppPlan.Value);

            // 4. HABILIDADES DE DAÑO (Priority: Sidewinder > Empyreal > Bloodletter)
            if (!op.SaveCD)
            {
                // A. SIDEWINDER (Top Priority)
                var swPlan = SidewinderLogic.GetPlan(context, player.Level);
                if (swPlan.HasValue) plans.Add(swPlan.Value);

                // B. EMPYREAL ARROW
                var eaPlan = EmpyrealArrowLogic.GetPlan(context, player.Level);
                if (eaPlan.HasValue) plans.Add(eaPlan.Value);

                // C. BLOODLETTER / HEARTBREAK (Con Pooling Logic)
                bool aoe = op.AoE_Enabled && CombatHelpers.CountAttackableEnemiesInRange(objectTable, target, 10f) >= 3;
                var blPlan = BloodletterLogic.GetPlan(context, player.Level, aoe);
                if (blPlan.HasValue) plans.Add(blPlan.Value);
            }

            // -----------------------------------------------------------------------------
            // ENVÍO AL SCHEDULER
            // -----------------------------------------------------------------------------
            // Ordenamos la lista por prioridad (Forced > High > Normal).
            // El Scheduler intentará meter tantas acciones como sea posible en el hueco actual.
            var sortedPlans = plans.OrderByDescending(p => p.Priority).ToList();

            scheduler.SetNextCycle(nextGcd,
                sortedPlans.Count > 0 ? sortedPlans[0] : null,
                sortedPlans.Count > 1 ? sortedPlans[1] : null);
        }

        // =================================================================================
        // HELPERS PRIVADOS (Control de Flujo)
        // =================================================================================

        // Ejecución paso a paso del Opener
        private void ExecuteOpenerLogic(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player, Configuration config)
        {
            var step = OpenerManager.Instance.GetCurrentStep();
            if (step == null) return;

            // Verificamos si el paso actual se completó
            if (CheckStepCompletion(am, step, player, config))
            {
                OpenerManager.Instance.Advance();
                lastOpenerActionId = 0;
                return;
            }

            uint gcd = ActionLibrary.IsGCD(step.ActionId) ? step.ActionId : 0;
            // Si es GCD, el oGCD es null (por defecto). Si es oGCD, va en el slot o1.
            OgcdPlan? o1 = (gcd == 0) ? new OgcdPlan(step.ActionId, WeavePriority.Forced) : null;

            // Lookahead simple: Si el paso actual es GCD, miramos si el siguiente es un oGCD para encolarlo junto
            if (gcd != 0)
            {
                var next1 = OpenerManager.Instance.PeekNextStep(1);
                if (next1 != null && !ActionLibrary.IsGCD(next1.ActionId) && next1.Type != "Potion")
                    o1 = new OgcdPlan(next1.ActionId, WeavePriority.Forced);
            }
            scheduler.SetNextCycle(gcd, o1);
            LastProposedAction = (gcd != 0) ? gcd : step.ActionId;
        }

        // Verificación de si la acción del Opener tuvo éxito
        private bool CheckStepCompletion(ActionManager* am, OpenerStep step, IPlayerCharacter player, Configuration config)
        {
            if (step.Type == "Potion")
            {
                var ops = config.Operation;
                // Si la poción está desactivada en config, la damos por completada instantáneamente
                return (!ops.UsePotion || ops.SelectedPotionId == 0) || am->GetRecastGroupDetail(58)->IsActive;
            }

            if (am->AnimationLock > 0.1f) return true; // Si estamos en animación, asumimos que está saliendo

            if (ActionLibrary.IsGCD(step.ActionId))
                return am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) > 0 && am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) < 1.5f;

            // Para oGCDs, chequeamos si tenemos el buff (si aplica) o si entró en CD
            return Helpers.HasStatus(player, (ushort)Helpers.GetBuffFromAction(step.ActionId)) ||
                   (am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) > 0 && am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) < 1.0f);
        }

        // Detector de teclas para inyección manual
        private void CheckManualInput(JobConfig_BRD config)
        {
            uint q = 0;
            if (Helpers.IsKeyPressed((VirtualKey)config.Troubadour.Key)) q = BRD_IDs.Troubadour;
            else if (Helpers.IsKeyPressed((VirtualKey)config.NaturesMinne.Key)) q = BRD_IDs.NaturesMinne;
            else if (Helpers.IsKeyPressed((VirtualKey)config.WardensPaean.Key)) q = BRD_IDs.WardensPaean;
            else if (Helpers.IsKeyPressed((VirtualKey)config.RepellingShot.Key)) q = BRD_IDs.RepellingShot;
            else if (Helpers.IsKeyPressed((VirtualKey)config.HeadGraze.Key)) q = 7554;
            else if (Helpers.IsKeyPressed((VirtualKey)config.Sprint.Key)) q = All_IDs.Sprint;
            else if (Helpers.IsKeyPressed((VirtualKey)config.LimitBreak.Key)) q = All_IDs.LimitBreak;

            if (q != 0)
            {
                if (!isManualInputActive)
                {
                    QueueManualAction(q);
                    isManualInputActive = true;
                }
            }
            else isManualInputActive = false;
        }
    }
}
