// Archivo: Logic/Jobs/Bard/BardRotation.cs
// VERSIÓN: V31.1 - FIX CS0535 (IMPLEMENTACIÓN DE INTERFAZ)
// DESCRIPCIÓN: Restaurados los métodos obligatorios de IJobLogic para compilar.
//              Mantiene: Anti-Early Pull, Lógica Predictiva y Gestión de oGCDs completa.

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
using MyOwnACR.Logic.Core;
using MyOwnACR.Logic.Interfaces;
using MyOwnACR.Logic.Common;
using MyOwnACR.Logic.Jobs.Bard.Skills;
using MyOwnACR.Models;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard
{
    public unsafe class BardRotation : IJobLogic
    {
        // =================================================================================
        // CONFIGURACIÓN BÁSICA (SINGLETON)
        // =================================================================================
        public static BardRotation Instance { get; } = new BardRotation();
        private BardRotation() { }

        public uint JobId => 23;
        public uint LastProposedAction { get; private set; } = 0;

        private readonly BardContext context = new();
        private DateTime lastSongTime = DateTime.MinValue;

        // =================================================================================
        // MÉTODOS DE INTERFAZ (OBLIGATORIOS PARA CORREGIR CS0535)
        // =================================================================================
        // Aunque no los usemos activamente ahora, deben existir para que el código compile.

        public void QueueManualAction(uint actionId)
        {
            // Implementación futura: Encolar acción por ID
        }

        public void QueueManualAction(string actionName)
        {
            // Implementación futura: Encolar acción por nombre
        }

        public string GetQueuedAction() => ""; // Retorna vacío por ahora

        public void PrintDebugInfo(IChatGui chat)
        {
            // Debug mínimo requerido por la interfaz
            if (chat != null)
            {
                chat.Print($"[BRD] LastGCD: {LastProposedAction} | Song: {context.CurrentSong}");
            }
        }

        // =================================================================================
        // EXECUTE: BUCLE PRINCIPAL
        // =================================================================================
        public void Execute(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            // 1. SEGURIDAD Y CONTEXTO
            if (am == null || player == null) return;
            var gauge = Plugin.JobGauges.Get<BRDGauge>();
            context.Update(am, gauge, scheduler, player);
            var op = config.Operation;

            // Detección de Combate Real (Anti-Early Pull)
            bool inCombat = Helpers.IsRealCombat(player);

            // -----------------------------------------------------------------------------
            // A. GESTIÓN DE CANCIONES (Prioridad Global - Self Buff)
            // -----------------------------------------------------------------------------
            // Se ejecuta si hay combate real O si estamos configurados para pre-cantar.
            if (config.Bard.AutoSong && !op.SaveCD && inCombat)
            {
                if ((DateTime.Now - lastSongTime).TotalSeconds > 2.0)
                {
                    var songPlan = SongLogic.GetPlan(context, player.Level);
                    if (songPlan.HasValue)
                    {
                        uint songId = songPlan.Value.ActionId;
                        if (Helpers.CanUse(am, songId))
                        {
                            bool safe = true;
                            // Protección Weaving: No cantar si el GCD va a volver en < 1.1s
                            if (am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot) < 1.1f) safe = false;

                            if (safe)
                            {
                                if (inCombat) Plugin.Instance.SendLog($"[SONG] Action: {ActionLibrary.GetName(songId)}");
                                scheduler.InjectOgcd(songId, WeavePriority.Forced);
                                lastSongTime = DateTime.Now;
                                return;
                            }
                        }
                    }
                }
            }

            // -----------------------------------------------------------------------------
            // 3. CHEQUEO DE TARGET
            // -----------------------------------------------------------------------------
            var target = player.TargetObject as IBattleChara;
            if (target == null || target.IsDead || !target.IsTargetable) return;

            // BLOQUEO DE SEGURIDAD (ANTI-PULL):
            // Si el helpers dice que no hay combate real (ni yo ni el mob peleamos), no hacemos nada.
            if (!inCombat) return;

            // -----------------------------------------------------------------------------
            // 4. LÓGICA DE ROTACIÓN (GCDs)
            // -----------------------------------------------------------------------------
            uint nextGcd = 0;

            var (hasStorm, hasCaustic, stormTime, causticTime) = Helpers.GetTargetDotStatus(player);
            bool hasBlastProc = Helpers.HasStatus(player, BRD_IDs.Status_BlastArrowReady);
            bool useAoE = op.AoE_Enabled && CombatHelpers.CountAttackableEnemiesInRange(objectTable, target, 10f) >= 3;
            // [NUEVO] Leemos el proc de Hawk's Eye / Straight Shot Ready
            bool hasRefulgentProc = Helpers.HasStatus(player, BRD_IDs.Status_StraightShotReady);

            // --- PRIORIDADES GCD ---

            // 1. DOTS
            nextGcd = DotLogic.GetAction(hasStorm, hasCaustic, stormTime, causticTime, context.RagingStrikesTimeLeft, player.Level, hasRefulgentProc);

            // 2. PROCS DE ALTA POTENCIA
            if (nextGcd == 0)
            {
                // A. Blast Arrow / Apex Arrow (Prioridad 1: Proc corto 10s o Barra llena)
                nextGcd = ApexLogic.GetAction(context, player.Level, hasBlastProc);

                // B. Resonant Arrow (Prioridad 2: Proc de Barrage - Nvl 96)
                if (nextGcd == 0)
                {
                    nextGcd = ResonantLogic.GetGcd(player);
                }

                // C. Radiant Encore (Prioridad 3: Proc largo 30s)
                if (nextGcd == 0)
                {
                    nextGcd = BuffLogic.GetEncoreGcd(context, player.Level);
                }
            }

            // 3. FILLERS
            if (nextGcd == 0)
            {
                nextGcd = FillerLogic.GetGcd(player, useAoE);
            }

            LastProposedAction = nextGcd;

            // -----------------------------------------------------------------------------
            // B. LÓGICA DE oGCD
            // -----------------------------------------------------------------------------
            List<OgcdPlan> plans = new List<OgcdPlan>();

            if (!op.SaveCD)
            {
                // 1. BUFFS (Predictivos)
                var buffPlans = BuffLogic.GetPlans(context, player.Level);
                if (buffPlans.Count > 0) plans.AddRange(buffPlans);

                // 2. BARRAGE (Tromba)
                // Usar temprano en el burst para habilitar Resonant Arrow y Refulgent.
                // BarrageLogic ya protege contra sobrescritura de procs.
                var barragePlan = BarrageLogic.GetPlan(context, player);
                if (barragePlan.HasValue) plans.Add(barragePlan.Value);

                // 3. EMPYREAL ARROW
                var eaPlan = EmpyrealArrowLogic.GetPlan(context, player.Level);
                if (eaPlan.HasValue) plans.Add(eaPlan.Value);

                // 4. SIDEWINDER
                var swPlan = SidewinderLogic.GetPlan(context, player.Level);
                if (swPlan.HasValue) plans.Add(swPlan.Value);

                // 5. PITCH PERFECT
                var ppPlan = PitchPerfectLogic.GetPlan(context);
                if (ppPlan.HasValue) plans.Add(ppPlan.Value);

                // 6. BLOODLETTER / RAIN OF DEATH
                var blPlan = BloodletterLogic.GetPlan(context, player.Level, useAoE);
                if (blPlan.HasValue) plans.Add(blPlan.Value);
            }

            // -----------------------------------------------------------------------------
            // ENVÍO AL SCHEDULER
            // -----------------------------------------------------------------------------

            // Ordenamos por prioridad (Forced > High > Normal > Low)
            var sortedPlans = plans.OrderByDescending(p => p.Priority).ToList();

            scheduler.SetNextCycle(
                nextGcd,
                sortedPlans.Count > 0 ? sortedPlans[0] : null,
                sortedPlans.Count > 1 ? sortedPlans[1] : null
            );
        }
    }
}
