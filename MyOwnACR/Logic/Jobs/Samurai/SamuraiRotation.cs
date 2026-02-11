using System;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.JobGauge.Types;
using MyOwnACR.JobConfigs;
using MyOwnACR.Logic.Core;
using MyOwnACR.Logic.Interfaces;
using MyOwnACR.Logic.Jobs.Samurai.Skills;
using MyOwnACR.Models;
using MyOwnACR.GameData.Jobs.Samurai;

namespace MyOwnACR.Logic.Jobs.Samurai
{
    public unsafe class SamuraiRotation : IJobLogic
    {
        // =================================================================================
        // CONFIGURACIÓN BÁSICA (SINGLETON)
        // =================================================================================
        public static SamuraiRotation Instance { get; } = new SamuraiRotation();
        private SamuraiRotation() { }

        public uint JobId => 34; // Samurai
        public uint LastProposedAction { get; private set; } = 0;

        private readonly SamuraiContext context = new();

        // =================================================================================
        // MÉTODOS DE INTERFAZ
        // =================================================================================
        public void QueueManualAction(uint actionId) { }
        public void QueueManualAction(string actionName) { }
        public string GetQueuedAction() => "";

        public void PrintDebugInfo(IChatGui chat)
        {
            Plugin.Instance.SendLog($"[SAM] Sen: {context.SenCount} (S:{context.HasSetsu} G:{context.HasGetsu} K:{context.HasKa}) | Kenki: {context.Kenki}");
            Plugin.Instance.SendLog($"[SAM] Buffs: Fugetsu {context.FugetsuTimeLeft:F1}s | Fuka {context.FukaTimeLeft:F1}s");
            Plugin.Instance.SendLog($"[SAM] Casting: {context.IsCasting} ({context.CastActionId} - {context.CastTimeRemaining:F2}s)");
        }

        // =================================================================================
        // EXECUTE: BUCLE PRINCIPAL
        // =================================================================================
        public void Execute(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player, IObjectTable objectTable, Configuration config)
        {
            // 1. SEGURIDAD Y CONTEXTO
            if (am == null || player == null) return;
            var gauge = Plugin.JobGauges.Get<SAMGauge>();

            // Actualizamos todo el contexto
            context.Update(am, gauge, scheduler, player);
            var op = config.Operation;

            // 2. CHEQUEO DE TARGET
            var target = player.TargetObject as IBattleChara;
            if (target == null || target.IsDead || !target.IsTargetable) return;

            // 3. ANTI-EARLY PULL
            bool inCombat = Helpers.IsRealCombat(player);
            if (!inCombat) return;

            // 4. GESTIÓN DE MOVIMIENTO (SLIDECASTING)
            if (MovementLogic.ShouldStopMoving(context))
            {
                scheduler.RequestStop();
            }

            // -----------------------------------------------------------------------------
            // 5. LÓGICA DE ROTACIÓN (GCDs)
            // -----------------------------------------------------------------------------
            uint nextGcd = 0;

            // A. BURST GCDS (Instantáneos: Tsubame, Kaeshi Namikiri)
            nextGcd = TsubameLogic.GetAction(context, player.Level, am);

            // B. IAIJUTSU / CASTS (Ogi Namikiri, Midare, Higanbana)
            if (nextGcd == 0)
            {
                nextGcd = IaijutsuLogic.GetAction(context);
            }

            // C. COMBOS BÁSICOS (1-2-3)
            if (nextGcd == 0)
            {
                nextGcd = ComboLogic.GetAction(context, player.Level);
            }

            LastProposedAction = nextGcd;

            // -----------------------------------------------------------------------------
            // 6. LÓGICA DE oGCD (Off-Global Cooldowns)
            // -----------------------------------------------------------------------------
            List<OgcdPlan> plans = new List<OgcdPlan>();
            OgcdPlan? potionPlan = null; // Variable especial para la poción

            if (!op.SaveCD)
            {
                // [NUEVO] 0. POCIÓN (Prioridad Suprema)
                // Verificamos si el usuario la activó en el Dashboard y si seleccionó un ID.
                if (op.UsePotion && op.SelectedPotionId != 0)
                {
                    // Consultamos la lógica de alineación (Ikishoten) y disponibilidad (Inventario)
                    potionPlan = PotionLogic.GetPlan(context, am, op.SelectedPotionId);
                }

                // Si NO vamos a usar poción en este ciclo, calculamos el resto de habilidades.
                // (Si usamos poción, saltamos esto para ahorrar CPU y evitar conflictos).
                if (potionPlan == null)
                {
                    // 1. IKISHOTEN
                    var ikiPlan = IkishotenLogic.GetPlan(context, player.Level);
                    if (ikiPlan.HasValue) plans.Add(ikiPlan.Value);

                    // 2. MEIKYO SHISUI
                    var meikyoPlan = MeikyoLogic.GetPlan(context, player.Level);
                    if (meikyoPlan.HasValue) plans.Add(meikyoPlan.Value);

                    // 3. ZANSHIN
                    var zanshinPlan = ZanshinLogic.GetPlan(context, player.Level);
                    if (zanshinPlan.HasValue) plans.Add(zanshinPlan.Value);

                    // 4. SHOHA
                    var shohaPlan = ShohaLogic.GetPlan(context, player.Level);
                    if (shohaPlan.HasValue) plans.Add(shohaPlan.Value);

                    // 5. SENEI
                    var seneiPlan = SeneiLogic.GetPlan(context, player.Level);
                    if (seneiPlan.HasValue) plans.Add(seneiPlan.Value);

                    // 6. SHINTEN
                    var shintenPlan = ShintenLogic.GetPlan(context, player.Level);
                    if (shintenPlan.HasValue) plans.Add(shintenPlan.Value);
                }
            }

            // -----------------------------------------------------------------------------
            // 7. ENVÍO AL SCHEDULER (Gestión de Weaving)
            // -----------------------------------------------------------------------------

            if (potionPlan.HasValue)
            {
                // CASO ESPECIAL: POCIÓN ACTIVA
                // La poción tiene un animation lock de ~1.2s.
                // Es imposible meter otra habilidad en el mismo GCD sin perder daño (clipping).
                // Por tanto, forzamos el segundo slot a NULL.

                scheduler.SetNextCycle(nextGcd, potionPlan.Value, null);
            }
            else
            {
                // CASO ESTÁNDAR
                // Ordenamos por prioridad y permitimos Double Weave (2 habilidades).
                var sortedPlans = plans.OrderByDescending(p => p.Priority).ToList();

                scheduler.SetNextCycle(
                    nextGcd,
                    sortedPlans.Count > 0 ? sortedPlans[0] : null,
                    sortedPlans.Count > 1 ? sortedPlans[1] : null
                );
            }
        }
    }
}
