// Archivo: Logic/MNK_Logic.cs
// Descripción: Lógica de combate Monk completa.
// VERSION: Production Ready + Opener + Input Híbrido + Potion 2min Window + Fix PB Time Check (6s).

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

        private const float MeleeRange = 3.5f;
        private const int AoE_Threshold = 3;
        private static float Action_Queue_Window = 0.6f;
        private static float RoF_Prepop_Window = 6.0f;

        private static DateTime LastAnyActionTime = DateTime.MinValue;
        private static DateTime LastPBTime = DateTime.MinValue;
        private static DateTime LastTrueNorthTime = DateTime.MinValue;

        // Control de tráfico para pociones en rotación
        private static DateTime LastPotionCheckTime = DateTime.MinValue;

        private static bool LastActionWasGCD = true;
        private static int OgcdCount = 0;

        // Estado para detectar entrada en combate (Auto-Start Opener)
        private static bool WasInCombat = false;

        public static uint LastProposedAction = 0;
        private static string QueuedManualAction = "";

        // =========================================================================
        // MÉTODOS DE COMUNICACIÓN
        // =========================================================================
        public static void QueueManualAction(string actionName) { QueuedManualAction = actionName; }
        public static string GetQueuedAction() => QueuedManualAction;
        public unsafe static void PrintDebugInfo(IChatGui chat) { /* Silenciado */ }

        // =========================================================================
        // EJECUCIÓN CENTRALIZADA (TECLADO VS MEMORIA)
        // =========================================================================
        private unsafe static void ExecuteAction(
            ActionManager* am,
            uint actionId,
            KeyBind? keyBind,
            ulong targetId,
            bool useMemory)
        {
            bool isGCD = ActionLibrary.IsGCD(actionId);

            if (useMemory)
            {
                // MODO MEMORIA: Inyección directa
                am->UseAction(ActionType.Action, actionId, targetId);
            }
            else if (keyBind != null)
            {
                // MODO TECLADO: Simulación
                PressBind(keyBind, isGCD);
            }

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
        // EXECUTE (Main Loop)
        // =========================================================================
        public unsafe static void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            JobConfig_MNK config,
            Dalamud.Plugin.Services.IObjectTable objectTable,
            OperationalSettings operation)
        {
            if (am == null || player == null || config == null) return;

            // FIX: Definir 'now' al principio para evitar errores CS0103
            var now = DateTime.Now;

            // Datos de contexto
            ulong targetId = (player.TargetObject != null) ? player.TargetObject.GameObjectId : player.GameObjectId;
            bool useMem = operation.UseMemoryInput;
            bool inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;

            // ---------------------------------------------------------------------
            // 0. AUTO-START OPENER (Solo al transicionar a combate)
            // ---------------------------------------------------------------------
            if (inCombat && !WasInCombat)
            {
                if (operation.UseOpener && !string.IsNullOrEmpty(operation.SelectedOpener) && operation.SelectedOpener != "Ninguno")
                {
                    OpenerManager.Instance.SelectOpener(operation.SelectedOpener);
                    OpenerManager.Instance.Start();
                }
            }
            WasInCombat = inCombat; // Actualizar estado

            // ---------------------------------------------------------------------
            // 1. EJECUCIÓN DE OPENER (Prioridad Absoluta)
            // ---------------------------------------------------------------------
            if (OpenerManager.Instance.IsRunning)
            {
                // CAMBIO: Ahora pasamos 'player' como segundo argumento
                var (opActionId, opBind) = OpenerManager.Instance.GetNextAction(am, player, config);

                if (opActionId != 0)
                {
                    ExecuteAction(am, opActionId, opBind, targetId, useMem);
                    return;
                }

                if (OpenerManager.Instance.IsRunning) return;
            }

            // ---------------------------------------------------------------------
            // 2. MANUAL QUEUE
            // ---------------------------------------------------------------------
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
                    ExecuteAction(am, manualActionId, bindToPress, targetId, useMem);
                }
                QueuedManualAction = "";
                return;
            }

            // Resetear propuesta
            LastProposedAction = 0;

            // ---------------------------------------------------------------------
            // 3. CONTROL DE TIEMPOS (Weave Window)
            // ---------------------------------------------------------------------
            int requiredDelay;
            if (LastActionWasGCD) requiredDelay = config.WeaveDelay_oGCD1_MS;
            else requiredDelay = config.WeaveDelay_oGCD2_MS;

            if ((now - LastAnyActionTime).TotalMilliseconds < (requiredDelay - (Action_Queue_Window * 1000))) return;

            var target = player.TargetObject;
            bool hasTarget = target != null && target.IsValid();
            bool inRange = hasTarget && IsInMeleeRange(player);

            // ---------------------------------------------------------------------
            // 4. DOWNTIME / FUERA DE RANGO
            // ---------------------------------------------------------------------
            if (!inCombat || !inRange)
            {
                if (!inCombat) OgcdCount = 0;

                // Carga de Chakra
                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gauge = Plugin.JobGauges.Get<MNKGauge>();
                    if (gauge.Chakra < 5 && CanUseRecast(am, MNK_IDs.SteeledMeditation, 0))
                    {
                        ExecuteAction(am, MNK_IDs.SteeledMeditation, config.Meditation, player.GameObjectId, useMem);
                        return;
                    }
                }

                // Replies de Rango
                if (hasTarget && inCombat)
                {
                    if (HasStatus(player, MNK_IDs.Status_FiresRumination) && CanUseRecast(am, MNK_IDs.FiresReply))
                    {
                        ExecuteAction(am, MNK_IDs.FiresReply, config.FiresReply, targetId, useMem);
                        return;
                    }
                    if (HasStatus(player, MNK_IDs.Status_WindsRumination) && CanUseRecast(am, MNK_IDs.WindsReply))
                    {
                        ExecuteAction(am, MNK_IDs.WindsReply, config.WindsReply, targetId, useMem);
                        return;
                    }
                }

                // Form Shift
                bool hasAnyForm = HasStatus(player, MNK_IDs.Status_OpoOpoForm) ||
                                  HasStatus(player, MNK_IDs.Status_RaptorForm) ||
                                  HasStatus(player, MNK_IDs.Status_CoeurlForm) ||
                                  HasStatus(player, MNK_IDs.Status_FormlessFist) ||
                                  HasStatus(player, MNK_IDs.Status_PerfectBalance);

                if (LastProposedAction == MNK_IDs.FormShift && (now - LastAnyActionTime).TotalSeconds < 2.0f) hasAnyForm = true;

                if (player.Level >= MNK_Levels.FormShift && !hasAnyForm && CanUseRecast(am, MNK_IDs.FormShift))
                {
                    ExecuteAction(am, MNK_IDs.FormShift, config.FormShift, player.GameObjectId, useMem);
                    return;
                }
                return;
            }

            // ---------------------------------------------------------------------
            // 5. ROTACIÓN DE COMBATE (GCD)
            // ---------------------------------------------------------------------
            int enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5f);
            bool useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;

            var gcdCandidate = GetNextGcdCandidate(am, config, player, useAoE, operation);

            bool isActionReady = false;
            if (gcdCandidate.HasValue)
            {
                // FIX CS0103: Definimos variable local para este bloque
                uint checkId = gcdCandidate.Value.actionId;
                if (IsBlitz(checkId)) checkId = MNK_IDs.MasterfulBlitz;

                isActionReady = CanUseRecast(am, checkId, Action_Queue_Window);
            }

            if (isActionReady && gcdCandidate.HasValue)
            {
                // Auto True North
                if (operation.TrueNorth_Auto)
                {
                    if ((now - LastTrueNorthTime).TotalSeconds > 10)
                    {
                        if (!HasStatus(player, Melee_IDs.Status_TrueNorth))
                        {
                            // FIX CS0103: Usamos gcdCandidate.Value.actionId directamente
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

                ExecuteAction(am, gcdCandidate.Value.actionId, gcdCandidate.Value.bind, targetId, useMem);
                return;
            }

            // ---------------------------------------------------------------------
            // 6. ROTACIÓN DE COMBATE (OGCD)
            // ---------------------------------------------------------------------
            int maxWeaves = config.EnableDoubleWeave ? 2 : 1;
            float gcdTotal = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 11);
            float gcdElapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 11);
            float gcdRemaining = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            bool isHolding = (gcdRemaining <= 0.01f && !isActionReady);

            if (isHolding) OgcdCount = 0; // Anti-Deadlock

            if (OgcdCount < maxWeaves || isHolding)
            {
                if (gcdRemaining > 0.6f || LastActionWasGCD || isHolding)
                {
                    TryUseOgcd(am, config, player, operation, targetId, useMem);
                }
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================
        private static void PressBind(KeyBind bind, bool isGCD) => InputSender.Send(bind.Key, bind.Bar, isGCD);

        private unsafe static bool CanUseRecast(ActionManager* am, uint id, float queueWindow = 0.5f)
        {
            var type = FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action;
            float total = am->GetRecastTime(type, id);
            float elapsed = am->GetRecastTimeElapsed(type, id);
            float remaining = (total > 0) ? Math.Max(0, total - elapsed) : 0;
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
            else return MNK_IDs.ElixirBurst;
        }

        private static bool IsBlitz(uint id)
        {
            return id == MNK_IDs.PhantomRush || id == MNK_IDs.RisingPhoenix ||
                   id == MNK_IDs.ElixirBurst || id == MNK_IDs.MasterfulBlitz;
        }

        private unsafe static void GetActionCharges(ActionManager* am, uint actionId, uint level, out int current, out int max)
        {
            current = 0; max = 0;
            if (am == null) return;
            max = (int)ActionManager.GetMaxCharges(actionId, level);
            current = (int)am->GetCurrentCharges(actionId);
        }

        // =========================================================================
        // SELECCIÓN DE GCD (Con Validaciones Estrictas)
        // =========================================================================
        private unsafe static (uint actionId, KeyBind bind)? GetNextGcdCandidate(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            bool useAoE,
            OperationalSettings op)
        {
            var gauge = Plugin.JobGauges.Get<MNKGauge>();
            bool isPerfectBalance = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            uint lastAction = am->Combo.Action;
            bool lastWasOpo = lastAction == MNK_IDs.Bootshine ||
                              lastAction == MNK_IDs.DragonKick ||
                              lastAction == MNK_IDs.LeapingOpo ||
                              lastAction == MNK_IDs.ArmOfTheDestroyer ||
                              lastAction == MNK_IDs.ShadowOfTheDestroyer;

            if (player.Level >= MNK_Levels.MasterfulBlitz)
            {
                int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                if (realChakraCount == 3)
                {
                    uint specificBlitzId = GetActiveBlitzId(gauge);

                    // HOLD LOGIC
                    if (specificBlitzId == MNK_IDs.PhantomRush && op.UseBrotherhood)
                    {
                        if (player.Level >= MNK_Levels.Brotherhood)
                        {
                            float bhTotal = am->GetRecastTime(ActionType.Action, MNK_IDs.Brotherhood);
                            float bhElapsed = am->GetRecastTimeElapsed(ActionType.Action, MNK_IDs.Brotherhood);
                            float bhCD = (bhTotal > 0) ? Math.Max(0, bhTotal - bhElapsed) : 0;
                            bool hasBhBuff = HasStatus(player, MNK_IDs.Status_Brotherhood);

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

            // REPLIES
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

        // =========================================================================
        // SELECCIÓN DE OGCD (Con Validaciones Estrictas + Poción en 2m)
        // =========================================================================
        private unsafe static bool TryUseOgcd(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            OperationalSettings op,
            ulong targetId,
            bool useMemory)
        {
            if (op.SaveCD) return false;

            var gauge = Plugin.JobGauges.Get<MNKGauge>();

            float rofTotal = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, MNK_IDs.RiddleOfFire);
            float rofElapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, MNK_IDs.RiddleOfFire);
            float rofCD = (rofTotal > 0) ? Math.Max(0, rofTotal - rofElapsed) : 0;

            bool rofActive = HasStatus(player, MNK_IDs.Status_RiddleOfFire);
            float rofRemains = 0f;
            if (rofActive) rofRemains = GetStatusTime(player, MNK_IDs.Status_RiddleOfFire);

            bool rofComingSoon = rofCD < RoF_Prepop_Window;

            if (player.Level < MNK_Levels.RiddleOfFire)
            {
                rofActive = true;
                rofComingSoon = true;
                rofRemains = 999f;
            }

            // --------------------------------------------------------------------------------
            // 0. POCIÓN EN ROTACIÓN (Estricta para ventanas de 2 minutos)
            // --------------------------------------------------------------------------------
            if (op.UsePotion && op.SelectedPotionId != 0 && player.Level >= MNK_Levels.Brotherhood)
            {
                float bhTotal = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, MNK_IDs.Brotherhood);
                float bhElapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, MNK_IDs.Brotherhood);
                float bhCD = (bhTotal > 0) ? Math.Max(0, bhTotal - bhElapsed) : 0;

                // CONDICIONES:
                // 1. RoF NO está activo.
                // 2. RoF viene pronto (entre 2 y 4 segundos).
                // 3. Brotherhood también viene pronto (< 8 segundos).
                if (!rofActive && rofCD > 2.0f && rofCD < 4.0f && bhCD < 8.0f)
                {
                    // Control de tráfico (1s)
                    if ((DateTime.Now - LastPotionCheckTime).TotalSeconds > 1.0)
                    {
                        if (InventoryManager.IsPotionReady(am, op.SelectedPotionId))
                        {
                            InventoryManager.UseSpecificPotion(am, op.SelectedPotionId);
                            LastPotionCheckTime = DateTime.Now;
                            LastAnyActionTime = DateTime.Now;
                            OgcdCount++;
                            return true;
                        }
                        LastPotionCheckTime = DateTime.Now;
                    }
                }
            }

            // 1. RIDDLE OF FIRE
            if (op.UseRoF && player.Level >= MNK_Levels.RiddleOfFire && CanUseRecast(am, MNK_IDs.RiddleOfFire, Action_Queue_Window))
            {
                ExecuteAction(am, MNK_IDs.RiddleOfFire, config.RiddleOfFire, targetId, useMemory);
                return true;
            }

            // 2. BROTHERHOOD
            if (op.UseBrotherhood && player.Level >= MNK_Levels.Brotherhood)
            {
                if (rofActive && CanUseRecast(am, MNK_IDs.Brotherhood, Action_Queue_Window))
                {
                    ExecuteAction(am, MNK_IDs.Brotherhood, config.Brotherhood, targetId, useMemory);
                    return true;
                }
            }

            // 3. PERFECT BALANCE
            bool pbSafe = (DateTime.Now - LastPBTime).TotalSeconds > 2.5;

            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;
            bool nadisEmpty = !hasLunar && !hasSolar;

            GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out int pbCharges, out int pbMax);

            // True Opener
            bool isResourceRich = (pbCharges == pbMax) && CanUseRecast(am, MNK_IDs.RiddleOfFire, 10.0f);
            bool isTrueOpener = nadisEmpty && isResourceRich;

            if (player.Level < MNK_Levels.MasterfulBlitz) isTrueOpener = false;
            if (isTrueOpener) pbSafe = true;

            bool inPB = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            if (op.UsePB && player.Level >= MNK_Levels.PerfectBalance && !inPB && pbCharges > 0 && pbSafe)
            {
                uint lastAction = am->Combo.Action;
                bool lastWasOpo = lastAction == MNK_IDs.Bootshine ||
                                  lastAction == MNK_IDs.DragonKick ||
                                  lastAction == MNK_IDs.LeapingOpo ||
                                  lastAction == MNK_IDs.ArmOfTheDestroyer ||
                                  lastAction == MNK_IDs.ShadowOfTheDestroyer;

                bool shouldUse = false;

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
                    // FIX LATE PB: 6 SEGUNDOS MÍNIMO
                    if (rofRemains > 6.0f)
                    {
                        if (lastWasOpo)
                        {
                            if (HasStatus(player, MNK_IDs.Status_Brotherhood) || !CanUseRecast(am, MNK_IDs.Brotherhood, Action_Queue_Window))
                                shouldUse = true;
                            else if (pbCharges >= 1)
                                shouldUse = true;
                        }
                    }
                }
                else if (pbCharges == pbMax)
                {
                    // Overcap: Solo si RoF está lejos (>30s)
                    if (rofCD > 30.0f && !rofActive && lastWasOpo)
                        shouldUse = true;
                }

                if (shouldUse)
                {
                    ExecuteAction(am, MNK_IDs.PerfectBalance, config.PerfectBalance, targetId, useMemory);
                    LastPBTime = DateTime.Now;
                    return true;
                }
            }

            // 4. FORBIDDEN CHAKRA
            if (op.UseForbiddenChakra && player.Level >= MNK_Levels.ForbiddenChakra && gauge.Chakra >= 5 && CanUseRecast(am, MNK_IDs.TheForbiddenChakra, Action_Queue_Window))
            {
                ExecuteAction(am, MNK_IDs.TheForbiddenChakra, config.ForbiddenChakra, targetId, useMemory);
                return true;
            }

            // 5. RIDDLE OF WIND
            if (op.UseRoW && player.Level >= MNK_Levels.RiddleOfWind && CanUseRecast(am, MNK_IDs.RiddleOfWind, Action_Queue_Window))
            {
                ExecuteAction(am, MNK_IDs.RiddleOfWind, config.RiddleOfWind, targetId, useMemory);
                return true;
            }

            return false;
        }
    }
}
