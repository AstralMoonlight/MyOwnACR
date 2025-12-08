// Archivo: Logic/MNK_Logic.cs
// Descripción: Lógica de combate Monk completa.
// AJUSTE: Añadido sistema de Queueing (Pre-press) de 0.5s para aprovechar la cola del servidor.

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.JobConfigs;
using MyOwnACR.GameData;
using MyOwnACR;

namespace MyOwnACR.Logic
{
    public static class MNK_Logic
    {
        // =========================================================================
        // CONFIGURACIÓN & VARIABLES
        // =========================================================================
        private const float MeleeRange = 5f;
        private const int AoE_Threshold = 3;

        // --- SISTEMA DE QUEUEING (PRE-PRESS) ---
        // Tiempo antes de que la habilidad esté lista en el que empezamos a spamear.
        // FFXIV permite encolar acciones aprox 0.3s antes.
        private static float Action_Queue_Window = 0.3f;

        // --- ANTI-DRIFT ---
        private static float RoF_Prepop_Window = 6.0f;

        private static DateTime LastAnyActionTime = DateTime.MinValue;
        private static DateTime LastPBTime = DateTime.MinValue;
        private static DateTime LastTrueNorthTime = DateTime.MinValue;

        private static bool LastActionWasGCD = true;
        private static int OgcdCount = 0;

        public static uint LastProposedAction = 0;
        private static string QueuedManualAction = "";

        // =========================================================================
        // MÉTODOS DE COMUNICACIÓN
        // =========================================================================
        public static void QueueManualAction(string actionName) { QueuedManualAction = actionName; }
        public static string GetQueuedAction() => QueuedManualAction;
        public unsafe static void PrintDebugInfo(IChatGui chat)
        {
            var player = Plugin.ObjectTable.LocalPlayer;
            if (player == null) return;
            var gauge = Plugin.JobGauges.Get<MNKGauge>();
            chat.Print($"[ACR] Nadi: {gauge.Nadi} | Weaves: {OgcdCount}/2 | LastWasGCD: {LastActionWasGCD} | Chakra: {gauge.Chakra}");
        }

        private static void ExecuteAction(uint actionId, KeyBind keyBind)
        {
            bool isGCD = ActionLibrary.IsGCD(actionId);

            // Enviamos la orden. InputSender se encargará del spam agresivo (Ansioso)
            PressBind(keyBind, isGCD);

            LastProposedAction = actionId;
            LastAnyActionTime = DateTime.Now;

            if (isGCD)
            {
                LastActionWasGCD = true;
                OgcdCount = 0;
            }
            else
            {
                LastActionWasGCD = false;
                OgcdCount++;
            }
        }

        // =========================================================================
        // EXECUTE
        // =========================================================================
        public unsafe static void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            JobConfig_MNK config,
            Dalamud.Plugin.Services.IObjectTable objectTable,
            OperationalSettings operation)
        {
            if (am == null || player == null || config == null) return;

            // 1. MANUAL
            if (!string.IsNullOrEmpty(QueuedManualAction))
            {
                KeyBind? bindToPress = null;
                uint manualActionId = 0;

                switch (QueuedManualAction)
                {
                    case "SixSidedStar": bindToPress = config.SixSidedStar; manualActionId = MNK_IDs.SixSidedStar; break;
                    case "Sprint": bindToPress = config.Sprint; manualActionId = 0; break;
                    case "Feint": bindToPress = config.Feint; manualActionId = 7549; break;
                    case "Mantra": bindToPress = config.Mantra; manualActionId = MNK_IDs.Mantra; break;
                    case "RiddleOfEarth": bindToPress = config.RiddleOfEarth; manualActionId = MNK_IDs.RiddleOfEarth; break;
                    case "Bloodbath": bindToPress = config.Bloodbath; manualActionId = 7542; break;
                    case "SecondWind": bindToPress = config.SecondWind; manualActionId = 7541; break;
                    case "TrueNorth": bindToPress = config.TrueNorth; manualActionId = 7546; break;
                }

                if (bindToPress != null)
                {
                    bool isGCD = (manualActionId != 0) && ActionLibrary.IsGCD(manualActionId);

                    InputSender.Send(bindToPress.Key, bindToPress.Bar, isGCD);
                    LastAnyActionTime = DateTime.Now;

                    if (isGCD) { LastActionWasGCD = true; OgcdCount = 0; }
                    else { LastActionWasGCD = false; }

                    Plugin.Instance.SendLog($"Ejecutando Manual: {QueuedManualAction}");
                }
                QueuedManualAction = "";
                return;
            }

            LastProposedAction = 0;
            var now = DateTime.Now;

            // 2. WEAVE TIMING CONTROL
            // Importante: Si estamos usando Queueing (pre-press), permitimos entrar un poco antes del delay estricto
            // para que InputSender empiece a trabajar.
            int requiredDelay;
            if (LastActionWasGCD) requiredDelay = config.WeaveDelay_oGCD1_MS;
            else requiredDelay = config.WeaveDelay_oGCD2_MS;

            // Si falta muy poco para cumplir el delay (menos de la ventana de queue), dejamos pasar
            if ((now - LastAnyActionTime).TotalMilliseconds < (requiredDelay - (Action_Queue_Window * 1000))) return;

            bool inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;
            var target = player.TargetObject;
            bool hasTarget = target != null && target.IsValid();

            // 3. DOWNTIME
            bool inRange = hasTarget && IsInMeleeRange(player);

            if (!inCombat || !inRange)
            {
                if (!inCombat) OgcdCount = 0;

                // Meditation (Sin queueing agresivo fuera de combate, usamos check normal)
                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gauge = Plugin.JobGauges.Get<MNKGauge>();
                    if (gauge.Chakra < 5 && CanUseRecast(am, MNK_IDs.Meditation, 0)) // 0 tolerancia fuera combate
                    {
                        ExecuteAction(MNK_IDs.Meditation, config.Meditation);
                        return;
                    }
                }

                if (hasTarget)
                {
                    if (HasStatus(player, MNK_IDs.Status_FiresRumination) && CanUseRecast(am, MNK_IDs.FiresReply))
                    {
                        ExecuteAction(MNK_IDs.FiresReply, config.FiresReply);
                        return;
                    }
                    if (HasStatus(player, MNK_IDs.Status_WindsRumination) && CanUseRecast(am, MNK_IDs.WindsReply))
                    {
                        ExecuteAction(MNK_IDs.WindsReply, config.WindsReply);
                        return;
                    }
                }

                bool hasAnyForm = HasStatus(player, MNK_IDs.Status_OpoOpoForm) || HasStatus(player, MNK_IDs.Status_RaptorForm) || HasStatus(player, MNK_IDs.Status_CoeurlForm) || HasStatus(player, MNK_IDs.Status_FormlessFist);
                if (player.Level >= MNK_Levels.FormShift && !hasAnyForm && CanUseRecast(am, MNK_IDs.FormShift))
                {
                    ExecuteAction(MNK_IDs.FormShift, config.FormShift);
                    return;
                }
                return;
            }

            // 4. COMBAT ROTATION
            int enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5f);
            bool useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;

            var gcdCandidate = GetNextGcdCandidate(am, config, player, useAoE);

            bool isActionReady = false;
            if (gcdCandidate.HasValue)
            {
                uint actionId = gcdCandidate.Value.actionId;
                if (IsBlitz(actionId)) actionId = MNK_IDs.MasterfulBlitz;
                // Usamos Action_Queue_Window aquí para el GCD
                isActionReady = CanUseRecast(am, actionId, Action_Queue_Window);
            }

            if (isActionReady && gcdCandidate.HasValue)
            {
                if (operation.TrueNorth_Auto)
                {
                    if ((now - LastTrueNorthTime).TotalSeconds > 10)
                    {
                        if (!HasStatus(player, Melee_IDs.Status_TrueNorth))
                        {
                            uint id = gcdCandidate.Value.actionId;
                            Position neededPos = Position.Unknown;
                            if (id == MNK_IDs.Demolish) neededPos = Position.Rear;
                            else if (id == MNK_IDs.SnapPunch || id == MNK_IDs.PouncingCoeurl) neededPos = Position.Flank;

                            bool usedTN = MeleeCommon.HandleTrueNorth(am, player, operation, config.TrueNorth, neededPos, ref LastProposedAction, ref LastAnyActionTime);

                            if (usedTN)
                            {
                                LastTrueNorthTime = now;
                                LastActionWasGCD = false;
                                OgcdCount++;
                                return;
                            }
                        }
                    }
                }

                ExecuteAction(gcdCandidate.Value.actionId, gcdCandidate.Value.bind);
                return;
            }

            // 6. OGCD LOGIC
            int maxWeaves = config.EnableDoubleWeave ? 2 : 1;

            // Calculamos cuánto falta para el siguiente GCD
            float gcdTotal = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 11);
            float gcdElapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 11);
            float gcdRemaining = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            if (OgcdCount < maxWeaves)
            {
                // Permitimos weaving si hay tiempo (más de 0.6s) o si acabamos de hacer GCD (inicio ventana)
                if (gcdRemaining > 0.6f || LastActionWasGCD)
                {
                    TryUseOgcd(am, config, player, operation);
                }
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================
        private static void PressBind(KeyBind bind, bool isGCD) => InputSender.Send(bind.Key, bind.Bar, isGCD);

        // --- CORE: Check con soporte de Queueing ---
        private unsafe static bool CanUseRecast(ActionManager* am, uint id, float queueWindow = 0.5f)
        {
            var type = FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action;

            // Obtenemos el tiempo total y el transcurrido
            float total = am->GetRecastTime(type, id);
            float elapsed = am->GetRecastTimeElapsed(type, id);
            float remaining = (total > 0) ? Math.Max(0, total - elapsed) : 0;

            // Si falta menos que nuestra ventana de queueing, consideramos que está "listo"
            // para empezar a spamear la tecla.
            return remaining <= queueWindow;
        }

        // ... (Resto de Helpers sin cambios: GetStatusTime, GetBestOpoAction, etc.) ...

        private static float GetStatusTime(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return 0;
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return s.RemainingTime;
            return 0;
        }

        private static (uint, KeyBind) GetBestOpoAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            if (useAoE && level >= MNK_Levels.ArmOfTheDestroyer) return (MNK_IDs.ArmOfTheDestroyer, config.ArmOfTheDestroyer);
            if (level < MNK_Levels.DragonKick) return (MNK_IDs.Bootshine, config.Bootshine);
            if (gauge.OpoOpoFury > 0) return (MNK_IDs.Bootshine, config.Bootshine);
            else return (MNK_IDs.DragonKick, config.DragonKick);
        }

        private static (uint, KeyBind) GetBestRaptorAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            if (useAoE && level >= MNK_Levels.FourPointFury) return (MNK_IDs.FourPointFury, config.FourPointFury);
            if (level < MNK_Levels.TwinSnakes) return (MNK_IDs.TrueStrike, config.TrueStrike);
            if (gauge.RaptorFury > 0) return (MNK_IDs.TrueStrike, config.TrueStrike);
            else return (MNK_IDs.TwinSnakes, config.TwinSnakes);
        }

        private static (uint, KeyBind) GetBestCoeurlAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            if (useAoE && level >= MNK_Levels.Rockbreaker) return (MNK_IDs.Rockbreaker, config.Rockbreaker);
            if (level < MNK_Levels.Demolish) return (MNK_IDs.SnapPunch, config.SnapPunch);
            if (gauge.CoeurlFury > 0) return (MNK_IDs.SnapPunch, config.SnapPunch);
            else return (MNK_IDs.Demolish, config.Demolish);
        }

        private static MNKGauge GetGauge() => Plugin.JobGauges.Get<MNKGauge>();

        private static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return false;
            foreach (var s in player.StatusList) if (s.StatusId == statusId) return true;
            return false;
        }

        private static bool IsInMeleeRange(IPlayerCharacter player)
        {
            if (player?.TargetObject is not IGameObject target) return false;
            float distSq = Vector3.DistanceSquared(player.Position, target.Position);
            float totalRange = target.HitboxRadius + MeleeRange;
            return distSq <= (totalRange * totalRange);
        }

        private static uint GetActiveBlitzId(MNKGauge gauge)
        {
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;
            if (hasLunar && hasSolar) return MNK_IDs.PhantomRush;
            int distinctTypes = gauge.BeastChakra.Where(c => c != BeastChakra.None).Distinct().Count();
            if (distinctTypes == 3) return MNK_IDs.RisingPhoenix;
            else return MNK_IDs.ElixirField;
        }

        private static bool IsBlitz(uint id)
        {
            return id == MNK_IDs.PhantomRush || id == MNK_IDs.RisingPhoenix ||
                   id == MNK_IDs.ElixirField || id == MNK_IDs.MasterfulBlitz;
        }

        private unsafe static void GetActionCharges(ActionManager* am, uint actionId, uint level, out int current, out int max)
        {
            current = 0; max = 0;
            if (am == null) return;
            max = (int)ActionManager.GetMaxCharges(actionId, level);
            current = (int)am->GetCurrentCharges(actionId);
        }

        private unsafe static (uint actionId, KeyBind bind)? GetNextGcdCandidate(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            bool useAoE)
        {
            var gauge = Plugin.JobGauges.Get<MNKGauge>();
            bool isPerfectBalance = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            if (player.Level >= MNK_Levels.MasterfulBlitz)
            {
                int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                if (realChakraCount == 3)
                {
                    uint specificBlitzId = GetActiveBlitzId(gauge);
                    if (specificBlitzId == MNK_IDs.PhantomRush) return (specificBlitzId, config.PhantomRush);
                    if (specificBlitzId == MNK_IDs.RisingPhoenix) return (specificBlitzId, config.RisingPhoenix);
                    if (specificBlitzId == MNK_IDs.ElixirField) return (specificBlitzId, config.ElixirField);
                    return (specificBlitzId, config.MasterfulBlitz);
                }
            }

            if (player.Level >= MNK_Levels.PerfectBalance && isPerfectBalance)
            {
                if (player.Level < MNK_Levels.MasterfulBlitz) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                int rawNadi = (int)gauge.Nadi;
                bool hasLunar = (rawNadi & 1) != 0;
                bool hasSolar = (rawNadi & 2) != 0;

                if (hasLunar && hasSolar) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                if (HasStatus(player, MNK_IDs.Status_Brotherhood)) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                if (!hasLunar) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                if (hasLunar && !hasSolar)
                {
                    int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                    if (realChakraCount == 0) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                    if (realChakraCount == 1) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
                    return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
                }
            }

            uint lastAction = am->Combo.Action;
            bool lastWasOpo = lastAction == MNK_IDs.Bootshine || lastAction == MNK_IDs.DragonKick || lastAction == MNK_IDs.LeapingOpo || lastAction == MNK_IDs.ArmOfTheDestroyer || lastAction == MNK_IDs.ShadowOfTheDestroyer;

            if (HasStatus(player, MNK_IDs.Status_FiresRumination))
            {
                float timeLeft = GetStatusTime(player, MNK_IDs.Status_FiresRumination);
                if (lastWasOpo || timeLeft < 3.0f) return (MNK_IDs.FiresReply, config.FiresReply);
            }
            if (HasStatus(player, MNK_IDs.Status_WindsRumination))
            {
                float timeLeft = GetStatusTime(player, MNK_IDs.Status_WindsRumination);
                if (lastWasOpo || timeLeft < 3.0f) return (MNK_IDs.WindsReply, config.WindsReply);
            }

            bool isFormless = HasStatus(player, MNK_IDs.Status_FormlessFist);
            if (isFormless) return GetBestOpoAction(useAoE, gauge, config, player.Level);

            bool isRaptor = HasStatus(player, MNK_IDs.Status_RaptorForm);
            bool isCoeurl = HasStatus(player, MNK_IDs.Status_CoeurlForm);

            if (isRaptor) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
            else if (isCoeurl) return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
            else return GetBestOpoAction(useAoE, gauge, config, player.Level);
        }

        private unsafe static bool TryUseOgcd(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            OperationalSettings op)
        {
            if (op.SaveCD) return false;

            var gauge = Plugin.JobGauges.Get<MNKGauge>();

            float rofTotal = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, MNK_IDs.RiddleOfFire);
            float rofElapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, MNK_IDs.RiddleOfFire);
            float rofCD = (rofTotal > 0) ? Math.Max(0, rofTotal - rofElapsed) : 0;

            bool rofActive = HasStatus(player, MNK_IDs.Status_RiddleOfFire);
            bool rofComingSoon = rofCD < RoF_Prepop_Window;

            if (player.Level < MNK_Levels.RiddleOfFire)
            {
                rofActive = true;
                rofComingSoon = true;
            }

            // 1. RIDDLE OF FIRE (Usar Queueing de 0.5s)
            if (player.Level >= MNK_Levels.RiddleOfFire && CanUseRecast(am, MNK_IDs.RiddleOfFire, Action_Queue_Window))
            {
                ExecuteAction(MNK_IDs.RiddleOfFire, config.RiddleOfFire);
                Plugin.Instance.SendLog("Burst Iniciado: Riddle of Fire");
                return true;
            }

            // 2. BROTHERHOOD
            if (player.Level >= MNK_Levels.Brotherhood && CanUseRecast(am, MNK_IDs.Brotherhood, Action_Queue_Window))
            {
                if (rofActive || player.Level < MNK_Levels.RiddleOfFire)
                {
                    ExecuteAction(MNK_IDs.Brotherhood, config.Brotherhood);
                    Plugin.Instance.SendLog("Buff Raid: Brotherhood");
                    return true;
                }
            }

            // 3. PERFECT BALANCE
            bool pbSafe = (DateTime.Now - LastPBTime).TotalSeconds > 2.5;
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;
            bool isOpener = !hasLunar && !hasSolar;

            if (player.Level < MNK_Levels.MasterfulBlitz) isOpener = false;
            if (isOpener) pbSafe = true;

            GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out int pbCharges, out int pbMax);
            bool inPB = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            if (player.Level >= MNK_Levels.PerfectBalance && !inPB && pbCharges > 0 && pbSafe)
            {
                uint lastAction = am->Combo.Action;
                bool lastWasOpo = lastAction == MNK_IDs.Bootshine || lastAction == MNK_IDs.DragonKick || lastAction == MNK_IDs.LeapingOpo || lastAction == MNK_IDs.ArmOfTheDestroyer || lastAction == MNK_IDs.ShadowOfTheDestroyer;

                bool shouldUse = false;

                if (isOpener)
                {
                    if (lastWasOpo) shouldUse = true;
                }
                else if (rofComingSoon && rofCD > 0)
                {
                    if (lastWasOpo)
                    {
                        shouldUse = true;
                        Plugin.Instance.SendLog($"Pre-pop Anti-Drift: PB Activado (RoF en {rofCD:F1}s)");
                    }
                }
                else if (rofActive)
                {
                    if (lastWasOpo)
                    {
                        if (HasStatus(player, MNK_IDs.Status_Brotherhood) || !CanUseRecast(am, MNK_IDs.Brotherhood, Action_Queue_Window)) shouldUse = true;
                        else if (pbCharges >= 1) shouldUse = true;
                    }
                }
                else if (pbCharges == pbMax)
                {
                    if (rofCD > 10.0f && !rofActive) shouldUse = true;
                }

                if (shouldUse)
                {
                    ExecuteAction(MNK_IDs.PerfectBalance, config.PerfectBalance);
                    LastPBTime = DateTime.Now;
                    return true;
                }
            }

            // 4. FORBIDDEN CHAKRA
            if (player.Level >= MNK_Levels.ForbiddenChakra && gauge.Chakra >= 5 && CanUseRecast(am, MNK_IDs.TheForbiddenChakra, Action_Queue_Window))
            {
                ExecuteAction(MNK_IDs.TheForbiddenChakra, config.ForbiddenChakra);
                return true;
            }

            // 5. RIDDLE OF WIND
            if (player.Level >= MNK_Levels.RiddleOfWind && rofActive && CanUseRecast(am, MNK_IDs.RiddleOfWind, Action_Queue_Window))
            {
                ExecuteAction(MNK_IDs.RiddleOfWind, config.RiddleOfWind);
                return true;
            }

            return false;
        }
    }
}
