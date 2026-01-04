// Archivo: Logic/MNK_Logic.cs
// VERSION: Fix Solar Nadi Logic (Unique Chakras).

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
    public class MNK_Logic : IJobLogic
    {
        // SINGLETON
        public static MNK_Logic Instance { get; } = new MNK_Logic();
        private MNK_Logic() { }

        public uint JobId => JobDefinitions.MNK;

        private uint lastProposedAction = 0;
        public uint LastProposedAction => lastProposedAction;

        // VARIABLES DE ESTADO
        private const float MeleeRange = 3.5f;
        private const int AoE_Threshold = 3;

        private readonly float actionQueueWindow = 0.3f;
        private readonly float rofPrepopWindow = 6.0f;

        private DateTime lastAnyActionTime = DateTime.MinValue;
        private DateTime lastPBTime = DateTime.MinValue;
        private DateTime lastTrueNorthTime = DateTime.MinValue;
        private DateTime lastPotionCheckTime = DateTime.MinValue;

        private bool lastActionWasGCD = true;
        private int ogcdCount = 0;
        private bool wasInCombat = false;

        private string queuedManualAction = "";

        // IMPLEMENTACIÓN DE INTERFAZ
        public void QueueManualAction(string actionName) { queuedManualAction = actionName; }
        public string GetQueuedAction() => queuedManualAction;
        public void PrintDebugInfo(IChatGui chat) { /* Silenciado */ }

        // EJECUCIÓN CENTRALIZADA
        private unsafe void ExecuteAction(
            ActionManager* am,
            uint actionId,
            KeyBind? keyBind,
            ulong targetId,
            bool useMemory)
        {
            var isGCD = ActionLibrary.IsGCD(actionId);

            if (useMemory)
            {
                am->UseAction(ActionType.Action, actionId, targetId);
            }
            else if (keyBind != null)
            {
                InputSender.Send(keyBind.Key, keyBind.Bar, isGCD);
            }

            lastProposedAction = actionId;
            lastAnyActionTime = DateTime.Now;

            if (isGCD)
            {
                lastActionWasGCD = true;
                ogcdCount = 0;
            }
            else
            {
                lastActionWasGCD = false;
                ogcdCount++;
            }
        }

        // EXECUTE (Main Loop)
        public unsafe void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            IObjectTable objectTable,
            Configuration config)
        {
            if (am == null || player == null || config == null) return;

            var mnkConfig = config.Monk;
            var operation = config.Operation;
            var now = DateTime.Now;
            var targetId = (player.TargetObject != null) ? player.TargetObject.GameObjectId : player.GameObjectId;
            var useMem = operation.UseMemoryInput;
            var inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;

            // 0. AUTO-START OPENER
            if (inCombat && !wasInCombat)
            {
                if (operation.UseOpener && !string.IsNullOrEmpty(operation.SelectedOpener) && operation.SelectedOpener != "Ninguno")
                {
                    OpenerManager.Instance.SelectOpener(operation.SelectedOpener);
                    OpenerManager.Instance.Start();
                }
            }
            wasInCombat = inCombat;

            // 1. EJECUCIÓN DE OPENER
            if (OpenerManager.Instance.IsRunning)
            {
                var (opActionId, opBind) = OpenerManager.Instance.GetNextAction(am, player, mnkConfig);

                if (opActionId != 0)
                {
                    ExecuteAction(am, opActionId, opBind, targetId, useMem);
                    return;
                }

                if (OpenerManager.Instance.IsRunning) return;
            }

            // 2. MANUAL QUEUE
            if (!string.IsNullOrEmpty(queuedManualAction))
            {
                KeyBind? bindToPress = null;
                uint manualActionId = 0;

                switch (queuedManualAction)
                {
                    case "SixSidedStar": bindToPress = mnkConfig.SixSidedStar; manualActionId = MNK_IDs.SixSidedStar; break;
                    case "Sprint": bindToPress = mnkConfig.Sprint; manualActionId = 0; break;
                    case "Feint": bindToPress = mnkConfig.Feint; manualActionId = 7549; break;
                    case "Mantra": bindToPress = mnkConfig.Mantra; manualActionId = MNK_IDs.Mantra; break;
                    case "RiddleOfEarth": bindToPress = mnkConfig.RiddleOfEarth; manualActionId = MNK_IDs.RiddleOfEarth; break;
                    case "Bloodbath": bindToPress = mnkConfig.Bloodbath; manualActionId = 7542; break;
                    case "SecondWind": bindToPress = mnkConfig.SecondWind; manualActionId = 7541; break;
                    case "TrueNorth": bindToPress = mnkConfig.TrueNorth; manualActionId = 7546; break;
                }

                if (bindToPress != null)
                {
                    ExecuteAction(am, manualActionId, bindToPress, targetId, useMem);
                }
                queuedManualAction = "";
                return;
            }

            lastProposedAction = 0;

            // 3. CONTROL DE TIEMPOS
            int requiredDelay;
            if (lastActionWasGCD) requiredDelay = mnkConfig.WeaveDelay_oGCD1_MS;
            else requiredDelay = mnkConfig.WeaveDelay_oGCD2_MS;

            if ((now - lastAnyActionTime).TotalMilliseconds < (requiredDelay - (actionQueueWindow * 1000))) return;

            var target = player.TargetObject;
            var hasTarget = target != null && target.IsValid();
            var inRange = hasTarget && IsInMeleeRange(player);

            // 4. DOWNTIME / FUERA DE RANGO
            if (!inCombat || !inRange)
            {
                if (!inCombat) ogcdCount = 0;

                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gauge = GetGauge();
                    if (gauge.Chakra < 5 && CanUseRecast(am, MNK_IDs.SteeledMeditation, 0))
                    {
                        ExecuteAction(am, MNK_IDs.SteeledMeditation, mnkConfig.Meditation, player.GameObjectId, useMem);
                        return;
                    }
                }

                if (hasTarget && inCombat)
                {
                    if (HasStatus(player, MNK_IDs.Status_FiresRumination) && CanUseRecast(am, MNK_IDs.FiresReply))
                    {
                        ExecuteAction(am, MNK_IDs.FiresReply, mnkConfig.FiresReply, targetId, useMem);
                        return;
                    }
                    if (HasStatus(player, MNK_IDs.Status_WindsRumination) && CanUseRecast(am, MNK_IDs.WindsReply))
                    {
                        ExecuteAction(am, MNK_IDs.WindsReply, mnkConfig.WindsReply, targetId, useMem);
                        return;
                    }
                }

                var hasAnyForm = HasStatus(player, MNK_IDs.Status_OpoOpoForm) ||
                                 HasStatus(player, MNK_IDs.Status_RaptorForm) ||
                                 HasStatus(player, MNK_IDs.Status_CoeurlForm) ||
                                 HasStatus(player, MNK_IDs.Status_FormlessFist) ||
                                 HasStatus(player, MNK_IDs.Status_PerfectBalance);

                if (lastProposedAction == MNK_IDs.FormShift && (now - lastAnyActionTime).TotalSeconds < 2.0f) hasAnyForm = true;

                if (player.Level >= MNK_Levels.FormShift && !hasAnyForm && CanUseRecast(am, MNK_IDs.FormShift))
                {
                    ExecuteAction(am, MNK_IDs.FormShift, mnkConfig.FormShift, player.GameObjectId, useMem);
                    return;
                }
                return;
            }

            // 5. ROTACIÓN DE COMBATE (GCD)
            var enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5f);
            var useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;

            var gcdCandidate = GetNextGcdCandidate(am, mnkConfig, player, useAoE, operation);

            var isActionReady = false;
            if (gcdCandidate.HasValue)
            {
                var checkId = gcdCandidate.Value.actionId;
                if (IsBlitz(checkId)) checkId = MNK_IDs.MasterfulBlitz;
                isActionReady = CanUseRecast(am, checkId, actionQueueWindow);
            }

            if (isActionReady && gcdCandidate.HasValue)
            {
                if (operation.TrueNorth_Auto)
                {
                    if ((now - lastTrueNorthTime).TotalSeconds > 10)
                    {
                        if (!HasStatus(player, Melee_IDs.Status_TrueNorth))
                        {
                            var id = gcdCandidate.Value.actionId;
                            var neededPos = Position.Unknown;
                            if (id == MNK_IDs.Demolish) neededPos = Position.Rear;
                            else if (id == MNK_IDs.SnapPunch || id == MNK_IDs.PouncingCoeurl) neededPos = Position.Flank;

                            var usedTN = MeleeCommon.HandleTrueNorth(am, player, operation, mnkConfig.TrueNorth, neededPos, ref lastProposedAction, ref lastAnyActionTime);

                            if (usedTN)
                            {
                                lastTrueNorthTime = now;
                                lastActionWasGCD = false;
                                ogcdCount++;
                                return;
                            }
                        }
                    }
                }

                ExecuteAction(am, gcdCandidate.Value.actionId, gcdCandidate.Value.bind, targetId, useMem);
                return;
            }

            // 6. ROTACIÓN DE COMBATE (OGCD)
            var maxWeaves = mnkConfig.EnableDoubleWeave ? 2 : 1;
            var gcdTotal = am->GetRecastTime(ActionType.Action, 11);
            var gcdElapsed = am->GetRecastTimeElapsed(ActionType.Action, 11);
            var gcdRemaining = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            var isHolding = (gcdRemaining <= 0.01f && !isActionReady);

            if (isHolding) ogcdCount = 0;

            if (ogcdCount < maxWeaves || isHolding)
            {
                if (gcdRemaining > 0.6f || lastActionWasGCD || isHolding)
                {
                    TryUseOgcd(am, mnkConfig, player, operation, targetId, useMem);
                }
            }
        }

        // =========================================================================
        // HELPERS (Static para CA1822)
        // =========================================================================

        private static unsafe bool CanUseRecast(ActionManager* am, uint id, float queueWindow = 0.5f)
        {
            var total = am->GetRecastTime(ActionType.Action, id);
            var elapsed = am->GetRecastTimeElapsed(ActionType.Action, id);
            var remaining = (total > 0) ? Math.Max(0, total - elapsed) : 0;
            return remaining <= queueWindow;
        }

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
            var distSq = Vector3.DistanceSquared(player.Position, target.Position);
            var totalRange = target.HitboxRadius + MeleeRange;
            return distSq <= (totalRange * totalRange);
        }

        private static uint GetActiveBlitzId(MNKGauge gauge)
        {
            var rawNadi = (int)gauge.Nadi;
            var hasLunar = (rawNadi & 1) != 0;
            var hasSolar = (rawNadi & 2) != 0;
            if (hasLunar && hasSolar) return MNK_IDs.PhantomRush;
            var distinctTypes = gauge.BeastChakra.Where(c => c != BeastChakra.None).Distinct().Count();
            if (distinctTypes == 3) return MNK_IDs.RisingPhoenix;
            else return MNK_IDs.ElixirBurst;
        }

        private static bool IsBlitz(uint id)
        {
            return id == MNK_IDs.PhantomRush || id == MNK_IDs.RisingPhoenix ||
                   id == MNK_IDs.ElixirBurst || id == MNK_IDs.MasterfulBlitz;
        }

        private static unsafe void GetActionCharges(ActionManager* am, uint actionId, uint level, out int current, out int max)
        {
            current = 0; max = 0;
            if (am == null) return;
            max = (int)ActionManager.GetMaxCharges(actionId, level);
            current = (int)am->GetCurrentCharges(actionId);
        }

        private static unsafe (uint actionId, KeyBind bind)? GetNextGcdCandidate(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            bool useAoE,
            OperationalSettings op)
        {
            var gauge = GetGauge();
            var isPerfectBalance = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            var lastAction = am->Combo.Action;
            var lastWasOpo = lastAction == MNK_IDs.Bootshine ||
                              lastAction == MNK_IDs.DragonKick ||
                              lastAction == MNK_IDs.LeapingOpo ||
                              lastAction == MNK_IDs.ArmOfTheDestroyer ||
                              lastAction == MNK_IDs.ShadowOfTheDestroyer;

            if (player.Level >= MNK_Levels.MasterfulBlitz)
            {
                var realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                if (realChakraCount == 3)
                {
                    var specificBlitzId = GetActiveBlitzId(gauge);

                    if (specificBlitzId == MNK_IDs.PhantomRush && op.UseBrotherhood)
                    {
                        if (player.Level >= MNK_Levels.Brotherhood)
                        {
                            var bhTotal = am->GetRecastTime(ActionType.Action, MNK_IDs.Brotherhood);
                            var bhElapsed = am->GetRecastTimeElapsed(ActionType.Action, MNK_IDs.Brotherhood);
                            var bhCD = (bhTotal > 0) ? Math.Max(0, bhTotal - bhElapsed) : 0;
                            var hasBhBuff = HasStatus(player, MNK_IDs.Status_Brotherhood);

                            if (!hasBhBuff && bhCD < 15.0f) return null;
                        }
                        return (specificBlitzId, config.PhantomRush);
                    }

                    if (specificBlitzId == MNK_IDs.RisingPhoenix) return (specificBlitzId, config.RisingPhoenix);
                    if (specificBlitzId == MNK_IDs.ElixirBurst) return (specificBlitzId, config.ElixirBurst);
                    return (specificBlitzId, config.MasterfulBlitz);
                }
            }

            if (player.Level >= MNK_Levels.PerfectBalance && isPerfectBalance)
            {
                if (player.Level < MNK_Levels.MasterfulBlitz) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                var rawNadi = (int)gauge.Nadi;
                var hasLunar = (rawNadi & 1) != 0;
                var hasSolar = (rawNadi & 2) != 0;

                // Si ya tenemos ambos, es Phantom Rush -> Spam Opo (o lo que sea más fuerte)
                if (hasLunar && hasSolar) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // Si estamos en opener con Brotherhood, generalmente spameamos Opo para Lunar
                if (HasStatus(player, MNK_IDs.Status_Brotherhood)) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // Si no tenemos Lunar, priorizamos Lunar (Spam Opo)
                if (!hasLunar) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // --- FIX SOLAR NADI ---
                // Si tenemos Lunar pero NO Solar, necesitamos 3 chakras DISTINTOS.
                if (hasLunar && !hasSolar)
                {
                    // Revisamos qué chakras tenemos YA en el gauge
                    bool hasOpo = gauge.BeastChakra.Contains(BeastChakra.OpoOpo);
                    bool hasRaptor = gauge.BeastChakra.Contains(BeastChakra.Raptor);
                    bool hasCoeurl = gauge.BeastChakra.Contains(BeastChakra.Coeurl);

                    // Usamos el que nos falte
                    if (!hasOpo) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                    if (!hasRaptor) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
                    if (!hasCoeurl) return GetBestCoeurlAction(useAoE, gauge, config, player.Level);

                    // Fallback (no debería llegar aquí si la lógica es correcta y count < 3)
                    return GetBestOpoAction(useAoE, gauge, config, player.Level);
                }
            }

            if (HasStatus(player, MNK_IDs.Status_FiresRumination))
            {
                var timeLeft = GetStatusTime(player, MNK_IDs.Status_FiresRumination);
                if (lastWasOpo || timeLeft < 3.0f) return (MNK_IDs.FiresReply, config.FiresReply);
            }
            if (HasStatus(player, MNK_IDs.Status_WindsRumination))
            {
                var timeLeft = GetStatusTime(player, MNK_IDs.Status_WindsRumination);
                if (lastWasOpo || timeLeft < 3.0f) return (MNK_IDs.WindsReply, config.WindsReply);
            }

            var isFormless = HasStatus(player, MNK_IDs.Status_FormlessFist);
            if (isFormless) return GetBestOpoAction(useAoE, gauge, config, player.Level);

            var isRaptor = HasStatus(player, MNK_IDs.Status_RaptorForm);
            var isCoeurl = HasStatus(player, MNK_IDs.Status_CoeurlForm);

            if (isRaptor) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
            else if (isCoeurl) return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
            else return GetBestOpoAction(useAoE, gauge, config, player.Level);
        }

        private unsafe bool TryUseOgcd(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            OperationalSettings op,
            ulong targetId,
            bool useMemory)
        {
            if (op.SaveCD) return false;

            var gauge = GetGauge();

            var rofTotal = am->GetRecastTime(ActionType.Action, MNK_IDs.RiddleOfFire);
            var rofElapsed = am->GetRecastTimeElapsed(ActionType.Action, MNK_IDs.RiddleOfFire);
            var rofCD = (rofTotal > 0) ? Math.Max(0, rofTotal - rofElapsed) : 0;

            var rofActive = HasStatus(player, MNK_IDs.Status_RiddleOfFire);
            var rofRemains = 0f;
            if (rofActive) rofRemains = GetStatusTime(player, MNK_IDs.Status_RiddleOfFire);

            var rofComingSoon = rofCD < rofPrepopWindow;

            if (player.Level < MNK_Levels.RiddleOfFire)
            {
                rofActive = true;
                rofComingSoon = true;
                rofRemains = 999f;
            }

            // 0. POCIÓN EN ROTACIÓN
            if (op.UsePotion && op.SelectedPotionId != 0 && player.Level >= MNK_Levels.Brotherhood)
            {
                var bhTotal = am->GetRecastTime(ActionType.Action, MNK_IDs.Brotherhood);
                var bhElapsed = am->GetRecastTimeElapsed(ActionType.Action, MNK_IDs.Brotherhood);
                var bhCD = (bhTotal > 0) ? Math.Max(0, bhTotal - bhElapsed) : 0;

                if (!rofActive && rofCD > 2.0f && rofCD < 4.0f && bhCD < 8.0f)
                {
                    if ((DateTime.Now - lastPotionCheckTime).TotalSeconds > 1.0)
                    {
                        if (InventoryManager.IsPotionReady(am, op.SelectedPotionId))
                        {
                            InventoryManager.UseSpecificPotion(am, op.SelectedPotionId);
                            lastPotionCheckTime = DateTime.Now;
                            lastAnyActionTime = DateTime.Now;
                            ogcdCount++;
                            return true;
                        }
                        lastPotionCheckTime = DateTime.Now;
                    }
                }
            }

            // 1. RIDDLE OF FIRE
            if (op.UseRoF && player.Level >= MNK_Levels.RiddleOfFire && CanUseRecast(am, MNK_IDs.RiddleOfFire, actionQueueWindow))
            {
                ExecuteAction(am, MNK_IDs.RiddleOfFire, config.RiddleOfFire, targetId, useMemory);
                return true;
            }

            // 2. BROTHERHOOD
            if (op.UseBrotherhood && player.Level >= MNK_Levels.Brotherhood)
            {
                if ((rofActive || rofCD < 5.0f) && CanUseRecast(am, MNK_IDs.Brotherhood, actionQueueWindow))
                {
                    ExecuteAction(am, MNK_IDs.Brotherhood, config.Brotherhood, targetId, useMemory);
                    return true;
                }
            }

            // 3. PERFECT BALANCE
            var pbSafe = (DateTime.Now - lastPBTime).TotalSeconds > 2.5;

            var rawNadi = (int)gauge.Nadi;
            var hasLunar = (rawNadi & 1) != 0;
            var hasSolar = (rawNadi & 2) != 0;
            var nadisEmpty = !hasLunar && !hasSolar;

            GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out var pbCharges, out var pbMax);

            var isResourceRich = (pbCharges == pbMax) && CanUseRecast(am, MNK_IDs.RiddleOfFire, 10.0f);
            var isTrueOpener = nadisEmpty && isResourceRich;

            if (player.Level < MNK_Levels.MasterfulBlitz) isTrueOpener = false;
            if (isTrueOpener) pbSafe = true;

            var inPB = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            if (op.UsePB && player.Level >= MNK_Levels.PerfectBalance && !inPB && pbCharges > 0 && pbSafe)
            {
                var lastAction = am->Combo.Action;
                var lastWasOpo = lastAction == MNK_IDs.Bootshine ||
                                  lastAction == MNK_IDs.DragonKick ||
                                  lastAction == MNK_IDs.LeapingOpo ||
                                  lastAction == MNK_IDs.ArmOfTheDestroyer ||
                                  lastAction == MNK_IDs.ShadowOfTheDestroyer;

                var shouldUse = false;

                if (isTrueOpener)
                {
                    if (lastWasOpo) shouldUse = true;
                }
                else if (rofComingSoon && rofCD > 0)
                {
                    if (lastWasOpo) shouldUse = true;
                }
                else if (rofActive)
                {
                    if (rofRemains > 8.0f)
                    {
                        if (lastWasOpo)
                        {
                            if (HasStatus(player, MNK_IDs.Status_Brotherhood) || !CanUseRecast(am, MNK_IDs.Brotherhood, actionQueueWindow))
                                shouldUse = true;
                            else if (pbCharges >= 1)
                                shouldUse = true;
                        }
                    }
                }
                else if (pbCharges == pbMax)
                {
                    if (rofCD > 30.0f && !rofActive && lastWasOpo)
                        shouldUse = true;
                }

                if (shouldUse)
                {
                    ExecuteAction(am, MNK_IDs.PerfectBalance, config.PerfectBalance, targetId, useMemory);
                    lastPBTime = DateTime.Now;
                    return true;
                }
            }

            // 4. FORBIDDEN CHAKRA
            if (op.UseForbiddenChakra && player.Level >= MNK_Levels.ForbiddenChakra && gauge.Chakra >= 5 && CanUseRecast(am, MNK_IDs.TheForbiddenChakra, actionQueueWindow))
            {
                ExecuteAction(am, MNK_IDs.TheForbiddenChakra, config.ForbiddenChakra, targetId, useMemory);
                return true;
            }

            // 5. RIDDLE OF WIND
            if (op.UseRoW && player.Level >= MNK_Levels.RiddleOfWind && CanUseRecast(am, MNK_IDs.RiddleOfWind, actionQueueWindow))
            {
                ExecuteAction(am, MNK_IDs.RiddleOfWind, config.RiddleOfWind, targetId, useMemory);
                return true;
            }

            return false;
        }
    }
}
