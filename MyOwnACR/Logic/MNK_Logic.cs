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

namespace MyOwnACR.Logic
{
    public static class MNK_Logic
    {
        // =========================================================================
        // CONFIGURACIÓN & VARIABLES
        // =========================================================================
        private const float MeleeRange = 5f;
        private const int AoE_Threshold = 3;

        private static DateTime lastAnyActionTime = DateTime.MinValue;
        private static DateTime LastPBTime = DateTime.MinValue;

        // --- PROTECCIÓN TRUE NORTH (NUEVO) ---
        private static DateTime LastTrueNorthTime = DateTime.MinValue;

        // Variable clave para saber en qué ventana estamos (1 o 2)
        private static bool LastActionWasGCD = true;
        private static int OgcdCount = 0; // Contador de weaves

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
            chat.Print($"[ACR] Nadi: {gauge.Nadi} | Weaves: {OgcdCount}/2 | LastWasGCD: {LastActionWasGCD}");
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
                switch (QueuedManualAction)
                {
                    case "SixSidedStar": bindToPress = config.SixSidedStar; break;
                    case "Sprint": bindToPress = config.Sprint; break;
                    case "Feint": bindToPress = config.Feint; break;
                    case "Mantra": bindToPress = config.Mantra; break;
                    case "RiddleOfEarth": bindToPress = config.RiddleOfEarth; break;
                    case "Bloodbath": bindToPress = config.Bloodbath; break;
                }
                if (bindToPress != null)
                {
                    PressBind(bindToPress);
                    lastAnyActionTime = DateTime.Now;
                    LastActionWasGCD = false;
                }
                QueuedManualAction = "";
                return;
            }

            LastProposedAction = 0;
            var now = DateTime.Now;

            // 2. WEAVE TIMING CONTROL
            int requiredDelay;
            if (LastActionWasGCD) requiredDelay = config.WeaveDelay_oGCD1_MS;
            else requiredDelay = config.WeaveDelay_oGCD2_MS;

            if ((now - lastAnyActionTime).TotalMilliseconds < requiredDelay) return;

            bool inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;

            // 3. FUERA DE COMBATE
            if (!inCombat)
            {
                OgcdCount = 0;
                bool hasAnyForm = HasStatus(player, MNK_IDs.Status_OpoOpoForm) || HasStatus(player, MNK_IDs.Status_RaptorForm) || HasStatus(player, MNK_IDs.Status_CoeurlForm) || HasStatus(player, MNK_IDs.Status_FormlessFist);

                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gauge = Plugin.JobGauges.Get<MNKGauge>();
                    if (gauge.Chakra < 5 && CanUseRecast(am, MNK_IDs.Meditation))
                    {
                        LastProposedAction = MNK_IDs.Meditation;
                        PressBind(config.Meditation);
                        lastAnyActionTime = now;
                        LastActionWasGCD = true;
                    }
                }

                if (player.Level >= MNK_Levels.FormShift && !hasAnyForm && CanUseRecast(am, MNK_IDs.FormShift))
                {
                    LastProposedAction = MNK_IDs.FormShift;
                    PressBind(config.FormShift);
                    lastAnyActionTime = now;
                    LastActionWasGCD = true;
                    return;
                }

                
                return;
            }

            // 4. RANGO
            int enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5f);
            bool useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;
            bool inRange = IsInMeleeRange(player);

            if (!inRange)
            {
                if (HasStatus(player, MNK_IDs.Status_FiresRumination) && CanUseRecast(am, MNK_IDs.FiresReply)) { LastProposedAction = MNK_IDs.FiresReply; PressBind(config.FiresReply); lastAnyActionTime = now; LastActionWasGCD = false; OgcdCount++; return; }
                if (HasStatus(player, MNK_IDs.Status_WindsRumination) && CanUseRecast(am, MNK_IDs.WindsReply)) { LastProposedAction = MNK_IDs.WindsReply; PressBind(config.WindsReply); lastAnyActionTime = now; LastActionWasGCD = false; OgcdCount++; return; }
                if (player.Level >= MNK_Levels.Meditation) { var gaugeRun = Plugin.JobGauges.Get<MNKGauge>(); if (gaugeRun.Chakra < 5 && CanUseRecast(am, MNK_IDs.Meditation)) { LastProposedAction = MNK_IDs.Meditation; PressBind(config.Meditation); lastAnyActionTime = now; LastActionWasGCD = true; return; } }
                return;
            }

            // 5. GCD LOGIC
            var gcdCandidate = GetNextGcdCandidate(am, config, player, useAoE);
            if (gcdCandidate.HasValue) LastProposedAction = gcdCandidate.Value.actionId;

            bool isActionReady = false;
            if (gcdCandidate.HasValue)
            {
                uint actionId = gcdCandidate.Value.actionId;
                if (IsBlitz(actionId)) actionId = MNK_IDs.MasterfulBlitz;
                isActionReady = CanUseRecast(am, actionId);
            }

            if (isActionReady)
            {
                // Auto True North con Protección de 10s
                if (operation.TrueNorth_Auto)
                {
                    // PROTECCIÓN: Si usamos TN hace menos de 10s, ignoramos esta lógica
                    if ((now - LastTrueNorthTime).TotalSeconds > 10)
                    {
                        if (!HasStatus(player, Melee_IDs.Status_TrueNorth))
                        {
                            uint id = gcdCandidate.Value.actionId;
                            Position neededPos = Position.Unknown;
                            if (id == MNK_IDs.Demolish) neededPos = Position.Rear;
                            else if (id == MNK_IDs.SnapPunch || id == MNK_IDs.PouncingCoeurl) neededPos = Position.Flank;

                            bool usedTN = MeleeCommon.HandleTrueNorth(am, player, operation, config.TrueNorth, neededPos, ref LastProposedAction, ref lastAnyActionTime);

                            if (usedTN)
                            {
                                LastTrueNorthTime = now; // <--- ACTUALIZAMOS TIMER
                                LastActionWasGCD = false;
                                OgcdCount++;
                                return;
                            }
                        }
                    }
                }

                PressBind(gcdCandidate.Value.bind);
                lastAnyActionTime = now;
                LastActionWasGCD = true;
                OgcdCount = 0;
                return;
            }

            // 6. OGCD LOGIC
            int maxWeaves = config.EnableDoubleWeave ? 2 : 1;
            float gcdRemaining = am->GetRecastTime(ActionType.Action, 11);

            if (OgcdCount < maxWeaves)
            {
                if (gcdRemaining > 0.6f || LastActionWasGCD)
                {
                    if (TryUseOgcd(am, config, player, operation))
                    {
                        lastAnyActionTime = now;
                        LastActionWasGCD = false;
                        OgcdCount++;
                        return;
                    }
                }
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================
        private static void PressBind(KeyBind bind) => InputSender.Send(bind.Key, bind.Bar);

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
        private unsafe static bool CanUseRecast(ActionManager* am, uint id) => am->GetRecastTime(ActionType.Action, id) == 0;

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

            // 1. BLITZ CHECK
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

            // 2. MODO PERFECT BALANCE
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

            // 3. REPLIES
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

            // 4. ROTACIÓN NORMAL
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
            bool rofActive = HasStatus(player, MNK_IDs.Status_RiddleOfFire);
            if (player.Level < MNK_Levels.RiddleOfFire) rofActive = true;

            // 1. FORBIDDEN CHAKRA
            if (player.Level >= MNK_Levels.ForbiddenChakra && gauge.Chakra >= 5 && CanUseRecast(am, MNK_IDs.TheForbiddenChakra))
            {
                LastProposedAction = MNK_IDs.TheForbiddenChakra;
                PressBind(config.ForbiddenChakra);
                return true;
            }

            // 2. RIDDLE OF FIRE
            if (player.Level >= MNK_Levels.RiddleOfFire && CanUseRecast(am, MNK_IDs.RiddleOfFire))
            {
                LastProposedAction = MNK_IDs.RiddleOfFire;
                PressBind(config.RiddleOfFire);
                return true;
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
                if (isOpener) { if (lastWasOpo) shouldUse = true; }
                else if (rofActive)
                {
                    if (lastWasOpo)
                    {
                        if (HasStatus(player, MNK_IDs.Status_Brotherhood) || CanUseRecast(am, MNK_IDs.Brotherhood)) shouldUse = true;
                        else if (pbCharges >= 1) shouldUse = true;
                    }
                }
                else if (pbCharges == pbMax)
                {
                    float rofCD = am->GetRecastTime(ActionType.Action, MNK_IDs.RiddleOfFire);
                    if (rofCD > 10.0f && !rofActive) shouldUse = true;
                }

                if (shouldUse)
                {
                    LastProposedAction = MNK_IDs.PerfectBalance;
                    LastPBTime = DateTime.Now;
                    PressBind(config.PerfectBalance);
                    return true;
                }
            }

            // 4. BROTHERHOOD
            if (player.Level >= MNK_Levels.Brotherhood && CanUseRecast(am, MNK_IDs.Brotherhood))
            {
                if (rofActive)
                {
                    if (player.Level >= MNK_Levels.PerfectBalance && pbCharges > 0 && !inPB && pbSafe) return false;
                    LastProposedAction = MNK_IDs.Brotherhood;
                    PressBind(config.Brotherhood);
                    return true;
                }
            }

            // 5. RIDDLE OF WIND
            if (player.Level >= MNK_Levels.RiddleOfWind && rofActive && CanUseRecast(am, MNK_IDs.RiddleOfWind))
            {
                LastProposedAction = MNK_IDs.RiddleOfWind;
                PressBind(config.RiddleOfWind);
                return true;
            }

            return false;
        }
    }
}
