// Archivo: Logic/Jobs/Bard/BardRotation.cs
// VERSIÓN: V24.0 - FINAL MONK INTEGRATION
// DESCRIPCIÓN: Rotación principal con depuración constante y lógica de pociones corregida.

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
        // Singleton Instance
        public static BardRotation Instance { get; } = new BardRotation();
        private BardRotation() { }

        // Identificador del Job (23 = Bardo)
        public uint JobId => 23;

        // Última acción propuesta por la lógica (para evitar repeticiones innecesarias)
        public uint LastProposedAction { get; private set; } = 0;

        // --- GESTIÓN DE ESTADO ---
        private readonly BardContext context = new();
        private bool wasInCombat = false;

        // --- INPUT MANUAL ---
        private uint queuedAction = 0;
        private DateTime queueExpire = DateTime.MinValue;
        private bool isManualInputActive = false;
        private uint lastOpenerActionId = 0;

        // --- TIMERS INTERNOS (Throttling) ---
        // Evitan spam de decisiones en milisegundos consecutivos
        private DateTime lastIronJawsTime = DateTime.MinValue;
        private DateTime lastSongTime = DateTime.MinValue;

        // Timer de Debug (Controla la velocidad de los logs en consola)
        private DateTime _lastDebugTime = DateTime.MinValue;

        // Métodos de Interfaz para Inyección Manual
        public void QueueManualAction(uint actionId) { queuedAction = actionId; queueExpire = DateTime.Now.AddSeconds(2.0); }
        public void QueueManualAction(string actionName) { var id = ActionLibrary.GetIdByName(actionName); if (id != 0) QueueManualAction(id); }
        public string GetQueuedAction() => queuedAction != 0 ? ActionLibrary.GetName(queuedAction) : "";
        public void PrintDebugInfo(IChatGui chat) { chat.Print($"Song={context.CurrentSong} | Charges={context.BloodletterCharges}"); }

        // =================================================================================
        // EXECUTE: Bucle Principal (Se ejecuta cada frame del juego)
        // =================================================================================
        public void Execute(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            // Validaciones de seguridad para evitar crasheos
            if (am == null || player == null) return;

            // -----------------------------------------------------------------------------
            // 0. ACTUALIZACIÓN DE CONTEXTO
            // -----------------------------------------------------------------------------
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            // Leemos memoria, calculamos CDs y actualizamos el estado interno
            context.Update(am, gauge, scheduler, player);

            // -----------------------------------------------------------------------------
            // DIAGNÓSTICO EN TIEMPO REAL (ALWAYS ON DEBUG)
            // -----------------------------------------------------------------------------
            var target = player.TargetObject as IBattleChara;
            bool playerInCombat = Plugin.Condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            bool targetInCombat = target != null && target.StatusFlags.HasFlag(StatusFlags.InCombat);
            bool actualCombatState = playerInCombat || targetInCombat;

            // Logueamos cada 0.5 segundos si estamos en combate
            bool logFrame = actualCombatState && (DateTime.Now - _lastDebugTime).TotalSeconds > 0.5;

            if (logFrame)
            {
                Plugin.Instance.SendLog("--- [BRD CYCLE START] ---");
                // Verificamos si el bot detecta el Burst (RS Active) y los Recursos
                Plugin.Instance.SendLog($"STATE: RS_Active={context.IsRagingStrikesActive} | Repertoire={context.Repertoire} | SoulVoice={context.SoulVoice}");
                // Verificamos si los Cooldowns están en 0.0 (Listos) o contando (Bloqueados)
                Plugin.Instance.SendLog($"CDs: Barrage={context.BarrageCD:F1} | SW={context.SidewinderCD:F1} | EA={context.EmpyrealCD:F1} | BL={context.BloodletterCD:F1}");
                _lastDebugTime = DateTime.Now;
            }

            // -----------------------------------------------------------------------------
            // 1. INPUT MANUAL (Prioridad Absoluta)
            // -----------------------------------------------------------------------------
            CheckManualInput(config.Bard);
            if (queuedAction != 0)
            {
                if (DateTime.Now > queueExpire) queuedAction = 0;
                else
                {
                    scheduler.InjectOgcd(queuedAction, WeavePriority.Forced);
                    if (am->GetRecastTimeElapsed(ActionType.Action, queuedAction) > 0) queuedAction = 0;
                    return; // Si hay input manual, salimos.
                }
            }

            // -----------------------------------------------------------------------------
            // 2. GESTIÓN DE OPENER
            // -----------------------------------------------------------------------------
            if (actualCombatState && !wasInCombat)
            {
                if (config.Operation.UseOpener && !config.Operation.SaveCD && target != null && target.IsTargetable)
                {
                    if (Helpers.CanStartOpener(am, player))
                    {
                        OpenerManager.Instance.SelectOpener(config.Operation.SelectedOpener);
                        OpenerManager.Instance.Start();
                        Plugin.Instance.SendLog("[ACR] Opener Iniciado");
                    }
                }
            }
            // Reset si salimos de combate
            if (!actualCombatState && wasInCombat) OpenerManager.Instance.Stop();
            wasInCombat = actualCombatState;

            // 3. EJECUTAR OPENER (Si está activo, toma control total)
            if (OpenerManager.Instance.IsRunning)
            {
                ExecuteOpenerLogic(scheduler, am, player, config);
                return;
            }

            // 4. CHEQUEO DE TARGET (Si no hay opener ni target, no hacemos nada)
            if (!actualCombatState || target == null || target.IsDead) return;

            // =============================================================================
            // 5. ROTACIÓN ESTÁNDAR
            // =============================================================================
            var op = config.Operation;
            uint nextGcd = 0;
            List<OgcdPlan> plans = new List<OgcdPlan>();

            // -----------------------------------------------------------------------------
            // A. CANCIONES (Logic/Jobs/Bard/Skills/SongLogic.cs)
            // -----------------------------------------------------------------------------
            if (config.Bard.AutoSong && !op.SaveCD)
            {
                // Throttle de 2s para evitar spam
                if ((DateTime.Now - lastSongTime).TotalSeconds > 2.0)
                {
                    var songPlan = SongLogic.GetPlan(context, player.Level);
                    if (songPlan.HasValue)
                    {
                        uint songId = songPlan.Value.ActionId;
                        if (Helpers.CanUse(am, songId))
                        {
                            bool safe = true;
                            // Late-Weave Protection: No usar canción si el GCD está a punto de volver (< 1.1s)
                            if (songId == BRD_IDs.WanderersMinuet && am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot) < 1.1f) safe = false;

                            if (safe)
                            {
                                if (logFrame) Plugin.Instance.SendLog($"[DECISION] SONG: {ActionLibrary.GetName(songId)} (Return)");
                                scheduler.InjectOgcd(songId, WeavePriority.Forced);
                                lastSongTime = DateTime.Now;
                                return; // Prioridad crítica, salimos para ejecutarla ya.
                            }
                        }
                    }
                }
            }

            // -----------------------------------------------------------------------------
            // B. LÓGICA DE GCD (Acciones Principales)
            // -----------------------------------------------------------------------------
            var (hasStorm, hasCaustic, stormTime, causticTime) = Helpers.GetTargetDotStatus(player);

            // Procs de Refulgent/Shadowbite
            bool hasRefulgentProc = Helpers.HasStatus(player, BRD_IDs.Status_StraightShotReady) || Helpers.HasStatus(player, BRD_IDs.Status_HawksEye);
            bool hasShadowbiteProc = Helpers.HasStatus(player, BRD_IDs.Status_ShadowbiteReady);

            // Safety para Iron Jaws (no spamear renovaciones)
            bool ironJawsSafe = (DateTime.Now - lastIronJawsTime).TotalSeconds > 4.0;

            // Selección automática de IDs según nivel
            uint actionStorm = player.Level >= 64 ? BRD_IDs.Stormbite : BRD_IDs.Windbite;
            uint actionCaustic = player.Level >= 64 ? BRD_IDs.CausticBite : BRD_IDs.VenomousBite;
            bool useAoE = op.AoE_Enabled && CombatHelpers.CountAttackableEnemiesInRange(objectTable, target, 10f) >= 3;

            // --- SISTEMA DE PRIORIDADES GCD ---

            // 1. Blast Arrow (Proc de nivel 86+)
            if (Helpers.HasStatus(player, BRD_IDs.Status_BlastArrowReady))
                nextGcd = BRD_IDs.BlastArrow;

            // 1.5 Limpieza de Barrage (FIX ATASCO)
            // Si Barrage está listo y tenemos un Proc que estorba, lo gastamos YA.
            else if (context.IsRagingStrikesActive && context.BarrageCD < 0.6f && (hasRefulgentProc || hasShadowbiteProc))
            {
                nextGcd = (useAoE && player.Level >= 72) ? BRD_IDs.Shadowbite : BRD_IDs.RefulgentArrow;
                if (logFrame) Plugin.Instance.SendLog("[DECISION] GCD: Dumping Proc for Barrage");
            }

            // 2. Iron Jaws (Mantenimiento de DoTs)
            else if (player.Level >= 56 && hasStorm && hasCaustic)
            {
                float rsTime = Helpers.GetStatusTime(player, BRD_IDs.Status_RagingStrikes);
                bool isSnapshot = rsTime > 0 && rsTime < 3.5f; // Renovar al final de los buffs
                bool isFalling = stormTime < 6.0f || causticTime < 6.0f; // Renovar si se acaban

                if ((isSnapshot || isFalling) && ironJawsSafe)
                {
                    nextGcd = BRD_IDs.IronJaws;
                    if (Helpers.CanUse(am, BRD_IDs.IronJaws)) lastIronJawsTime = DateTime.Now;
                }
            }
            // 2.5 Aplicación Inicial de DoTs
            else if (ironJawsSafe)
            {
                if (!hasStorm) nextGcd = actionStorm;
                else if (!hasCaustic) nextGcd = actionCaustic;
            }

            // 3. Procs de Daño
            if (nextGcd == 0)
            {
                if (Helpers.HasStatus(player, BRD_IDs.Status_ResonantArrowReady)) nextGcd = BRD_IDs.ResonantArrow;
                else
                {
                    uint encoreGcd = BuffLogic.GetEncoreGcd(context, player.Level);
                    if (encoreGcd != 0) nextGcd = encoreGcd;
                }
            }

            // 4. Apex Arrow (Gestión de Barra)
            if (nextGcd == 0 && player.Level >= 80)
            {
                bool overcap = gauge.SoulVoice == 100;
                bool burstDump = context.IsRagingStrikesActive && gauge.SoulVoice >= 80;
                if (overcap || burstDump) nextGcd = BRD_IDs.ApexArrow;
            }

            // 5. Relleno (Fillers)
            if (nextGcd == 0)
            {
                if (useAoE)
                {
                    if (player.Level >= 72 && Helpers.HasStatus(player, BRD_IDs.Status_ShadowbiteReady)) nextGcd = BRD_IDs.Shadowbite;
                    else nextGcd = (player.Level >= 82) ? BRD_IDs.Ladonsbite : BRD_IDs.QuickNock;
                }
                else
                {
                    nextGcd = hasRefulgentProc ? BRD_IDs.RefulgentArrow : (player.Level >= 76 ? BRD_IDs.BurstShot : BRD_IDs.HeavyShot);
                }
            }
            LastProposedAction = nextGcd;

            // -----------------------------------------------------------------------------
            // C. LÓGICA DE oGCD (Weaving)
            // -----------------------------------------------------------------------------

            // 1. POCIÓN (Integración Monk-Style)
            // Pasamos las variables sueltas para evitar errores de tipo.
            // Si la poción falla por más de 3s, el módulo devuelve null y desbloquea el burst.
            var potionPlan = PotionLogic.GetPlan(context, op.UsePotion, op.SelectedPotionId, op.SaveCD, am);
            if (potionPlan.HasValue)
            {
                plans.Add(potionPlan.Value);
                if (logFrame) Plugin.Instance.SendLog("[PLAN] Potion Requested");
            }

            // 2. BUFFS (Raging Strikes, Battle Voice, Radiant Finale)
            if (!op.SaveCD && op.UseRoF)
            {
                var buffPlan = BuffLogic.GetPlan(context, player.Level);
                if (buffPlan.HasValue)
                {
                    plans.Add(buffPlan.Value);
                    if (logFrame) Plugin.Instance.SendLog($"[PLAN] Buff Added: {buffPlan.Value.ActionId}");
                }
            }

            // 3. BARRAGE (Daño Masivo)
            if (!op.SaveCD)
            {
                var barragePlan = BarrageLogic.GetPlan(context, player);
                if (barragePlan.HasValue)
                {
                    plans.Add(barragePlan.Value);
                    if (logFrame) Plugin.Instance.SendLog("[PLAN] Barrage Added");
                }
            }

            // 4. PITCH PERFECT (Mecánica de Canción)
            var ppPlan = PitchPerfectLogic.GetPlan(context);
            if (ppPlan.HasValue)
            {
                plans.Add(ppPlan.Value);
                if (logFrame) Plugin.Instance.SendLog("[PLAN] Pitch Perfect Added");
            }

            // 5. DAÑO PURO (Sidewinder, Empyreal, Bloodletter)
            if (!op.SaveCD)
            {
                var swPlan = SidewinderLogic.GetPlan(context, player.Level);
                if (swPlan.HasValue)
                {
                    plans.Add(swPlan.Value);
                    if (logFrame) Plugin.Instance.SendLog("[PLAN] Sidewinder Added");
                }

                var eaPlan = EmpyrealArrowLogic.GetPlan(context, player.Level);
                if (eaPlan.HasValue)
                {
                    plans.Add(eaPlan.Value);
                    if (logFrame) Plugin.Instance.SendLog("[PLAN] Empyreal Added");
                }

                bool aoe = op.AoE_Enabled && CombatHelpers.CountAttackableEnemiesInRange(objectTable, target, 10f) >= 3;
                var blPlan = BloodletterLogic.GetPlan(context, player.Level, aoe);
                if (blPlan.HasValue)
                {
                    plans.Add(blPlan.Value);
                    if (logFrame) Plugin.Instance.SendLog("[PLAN] Bloodletter Added");
                }
            }

            // -----------------------------------------------------------------------------
            // ENVÍO AL SCHEDULER
            // -----------------------------------------------------------------------------
            // Ordenamos por prioridad (Forced > High > Normal > Low)
            var sortedPlans = plans.OrderByDescending(p => p.Priority).ToList();

            if (logFrame)
            {
                Plugin.Instance.SendLog($"[SCHEDULER] GCD: {ActionLibrary.GetName(nextGcd)}");
                if (sortedPlans.Count > 0) Plugin.Instance.SendLog($"[SCHEDULER] oGCD 1: {ActionLibrary.GetName(sortedPlans[0].ActionId)} (Prio: {sortedPlans[0].Priority})");
                if (sortedPlans.Count > 1) Plugin.Instance.SendLog($"[SCHEDULER] oGCD 2: {ActionLibrary.GetName(sortedPlans[1].ActionId)} (Prio: {sortedPlans[1].Priority})");
                Plugin.Instance.SendLog("--- [BRD CYCLE END] ---");
            }

            // Enviamos 1 GCD y hasta 2 oGCDs para double-weaving
            scheduler.SetNextCycle(nextGcd, sortedPlans.Count > 0 ? sortedPlans[0] : null, sortedPlans.Count > 1 ? sortedPlans[1] : null);
        }

        // =================================================================================
        // HELPERS PRIVADOS
        // =================================================================================

        // Ejecución paso a paso del Opener pre-programado
        private void ExecuteOpenerLogic(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player, Configuration config)
        {
            var step = OpenerManager.Instance.GetCurrentStep();
            if (step == null) return;
            if (CheckStepCompletion(am, step, player, config)) { OpenerManager.Instance.Advance(); lastOpenerActionId = 0; return; }

            uint gcd = ActionLibrary.IsGCD(step.ActionId) ? step.ActionId : 0;
            OgcdPlan? o1 = (gcd == 0) ? new OgcdPlan(step.ActionId, WeavePriority.Forced) : null;
            if (gcd != 0)
            {
                var next1 = OpenerManager.Instance.PeekNextStep(1);
                if (next1 != null && !ActionLibrary.IsGCD(next1.ActionId) && next1.Type != "Potion")
                    o1 = new OgcdPlan(next1.ActionId, WeavePriority.Forced);
            }
            scheduler.SetNextCycle(gcd, o1);
            LastProposedAction = (gcd != 0) ? gcd : step.ActionId;
        }

        // Verificación de si un paso del Opener se ha completado con éxito
        private bool CheckStepCompletion(ActionManager* am, OpenerStep step, IPlayerCharacter player, Configuration config)
        {
            // Si es poción y no la usamos, la saltamos.
            if (step.Type == "Potion") { var ops = config.Operation; return (!ops.UsePotion || ops.SelectedPotionId == 0) || am->GetRecastGroupDetail(58)->IsActive; }
            // Si hay animación, asumimos éxito.
            if (am->AnimationLock > 0.1f) return true;
            // Chequeo de GCD vs oGCD
            if (ActionLibrary.IsGCD(step.ActionId)) return am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) > 0 && am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) < 1.5f;
            return Helpers.HasStatus(player, (ushort)Helpers.GetBuffFromAction(step.ActionId)) || (am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) > 0 && am->GetRecastTimeElapsed(ActionType.Action, step.ActionId) < 1.0f);
        }

        // Detector de teclas para intervención manual del usuario
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

            if (q != 0) { if (!isManualInputActive) { QueueManualAction(q); isManualInputActive = true; } } else isManualInputActive = false;
        }
    }
}
