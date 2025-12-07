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
using MyOwnACR.GameData; // Usamos la nueva carpeta de datos

namespace MyOwnACR.Logic
{
    public static class MNK_Logic
    {
        // =========================================================================
        // CONFIGURACIÓN (VALORES PROTEGIDOS)
        // =========================================================================
        private const float MeleeRange = 5f;
        private const int AoE_Threshold = 3;
        private const int MinActionDelayMs = 250;
        private static DateTime lastAnyActionTime = DateTime.MinValue;
        private static bool ogcdUsedSinceLastGcd = false;
        private static DateTime LastPBTime = DateTime.MinValue;

        public static uint LastProposedAction = 0;

        // VARIABLE PARA ACCIONES FORZADAS
        private static string QueuedManualAction = "";

        public static void QueueManualAction(string actionName)
        {
            QueuedManualAction = actionName;
        }

        public unsafe static void PrintDebugInfo(IChatGui chat)
        {
            var player = Plugin.ObjectTable.LocalPlayer;
            if (player == null) return;

            var gauge = Plugin.JobGauges.Get<MNKGauge>();
            int rawNadi = (int)gauge.Nadi;

            var am = ActionManager.Instance();
            GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out int curPB, out int maxPB);

            chat.Print($"[ACR] Lv{player.Level} | Nadi: {rawNadi} | PB: {curPB}/{maxPB}");
            chat.Print($"[ACR] Intención: {LastProposedAction}");
        }

        public unsafe static void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            JobConfig_MNK config,
            IObjectTable objectTable,
            OperationalSettings operation)
        {
            if (am == null || player == null || config == null) return;

            // 1. MANEJO DE ACCIONES FORZADAS
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
                }
                QueuedManualAction = "";
                return;
            }

            LastProposedAction = 0;
            var now = DateTime.Now;
            if ((now - lastAnyActionTime).TotalMilliseconds < MinActionDelayMs) return;

            bool inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;

            // 0. FUERA DE COMBATE
            if (!inCombat)
            {
                bool hasAnyForm = HasStatus(player, MNK_IDs.Status_OpoOpoForm) ||
                                  HasStatus(player, MNK_IDs.Status_RaptorForm) ||
                                  HasStatus(player, MNK_IDs.Status_CoeurlForm) ||
                                  HasStatus(player, MNK_IDs.Status_FormlessFist);

                // Form Shift (Lv 52)
                if (player.Level >= MNK_Levels.FormShift && !hasAnyForm && CanUseRecast(am, MNK_IDs.FormShift))
                {
                    LastProposedAction = MNK_IDs.FormShift;
                    PressBind(config.FormShift);
                    lastAnyActionTime = now;
                    return;
                }

                // Meditation (Lv 15)
                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gauge = Plugin.JobGauges.Get<MNKGauge>();
                    if (gauge.Chakra < 5 && CanUseRecast(am, MNK_IDs.Meditation))
                    {
                        LastProposedAction = MNK_IDs.Meditation;
                        PressBind(config.Meditation);
                        lastAnyActionTime = now;
                    }
                }
                ogcdUsedSinceLastGcd = false;
                return;
            }

            // 0.5 FUERA DE RANGO
            int enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5f);
            bool useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;
            bool inRange = IsInMeleeRange(player);

            if (!inRange)
            {
                // Replies (Solo si tienes el nivel/buff)
                if (HasStatus(player, MNK_IDs.Status_FiresRumination) && CanUseRecast(am, MNK_IDs.FiresReply))
                {
                    LastProposedAction = MNK_IDs.FiresReply;
                    PressBind(config.FiresReply);
                    lastAnyActionTime = now;
                    ogcdUsedSinceLastGcd = false;
                    return;
                }
                if (HasStatus(player, MNK_IDs.Status_WindsRumination) && CanUseRecast(am, MNK_IDs.WindsReply))
                {
                    LastProposedAction = MNK_IDs.WindsReply;
                    PressBind(config.WindsReply);
                    lastAnyActionTime = now;
                    ogcdUsedSinceLastGcd = false;
                    return;
                }

                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gaugeRun = Plugin.JobGauges.Get<MNKGauge>();
                    if (gaugeRun.Chakra < 5 && CanUseRecast(am, MNK_IDs.Meditation))
                    {
                        LastProposedAction = MNK_IDs.Meditation;
                        PressBind(config.Meditation);
                        lastAnyActionTime = now;
                        return;
                    }
                }
                return;
            }

            // 1. GCD LOGIC
            var gcdCandidate = GetNextGcdCandidate(am, config, player, useAoE);

            if (gcdCandidate.HasValue)
                LastProposedAction = gcdCandidate.Value.actionId;

            bool isActionReady = false;
            if (gcdCandidate.HasValue)
            {
                uint actionId = gcdCandidate.Value.actionId;
                if (IsBlitz(actionId)) actionId = MNK_IDs.MasterfulBlitz;
                isActionReady = CanUseRecast(am, actionId);
            }

            if (isActionReady)
            {
                PressBind(gcdCandidate.Value.bind);
                lastAnyActionTime = now;
                ogcdUsedSinceLastGcd = false;
                return;
            }

            // 2. OGCD LOGIC
            if (!ogcdUsedSinceLastGcd)
            {
                if (TryUseOgcd(am, config, player, operation))
                {
                    ogcdUsedSinceLastGcd = true;
                    lastAnyActionTime = now;
                    return;
                }
            }
        }

        // =========================================================================
        // SELECTOR DE GCD (CON NIVEL)
        // =========================================================================
        private unsafe static (uint actionId, KeyBind bind)? GetNextGcdCandidate(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            bool useAoE)
        {
            var gauge = Plugin.JobGauges.Get<MNKGauge>();
            bool isPerfectBalance = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            // 1. BLITZ CHECK (Nivel 60+)
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

            // 2. MODO PERFECT BALANCE (Nivel 50+)
            if (player.Level >= MNK_Levels.PerfectBalance && isPerfectBalance)
            {
                // Si somos < 60, no hay Nadis, solo spameamos daño (Opo)
                if (player.Level < MNK_Levels.MasterfulBlitz)
                {
                    return GetBestOpoAction(useAoE, gauge, config, player.Level);
                }

                int rawNadi = (int)gauge.Nadi;
                bool hasLunar = (rawNadi & 1) != 0;
                bool hasSolar = (rawNadi & 2) != 0;

                // A: Phantom Rush -> Spam Opo
                if (hasLunar && hasSolar) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // B: Elixir Burst -> Spam Opo
                if (HasStatus(player, MNK_IDs.Status_Brotherhood)) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                if (!hasLunar) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // C: Rising Phoenix -> 3 DISTINTOS
                if (hasLunar && !hasSolar)
                {
                    int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                    if (realChakraCount == 0) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                    if (realChakraCount == 1) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
                    return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
                }
            }

            // 3. REPLIES
            if (HasStatus(player, MNK_IDs.Status_FiresRumination)) return (MNK_IDs.FiresReply, config.FiresReply);
            if (HasStatus(player, MNK_IDs.Status_WindsRumination)) return (MNK_IDs.WindsReply, config.WindsReply);

            // 4. ROTACIÓN NORMAL
            bool isFormless = HasStatus(player, MNK_IDs.Status_FormlessFist);
            bool isRaptor = HasStatus(player, MNK_IDs.Status_RaptorForm);
            bool isCoeurl = HasStatus(player, MNK_IDs.Status_CoeurlForm);

            if (isFormless) return GetBestOpoAction(useAoE, gauge, config, player.Level);
            if (isRaptor) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
            else if (isCoeurl) return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
            else return GetBestOpoAction(useAoE, gauge, config, player.Level);
        }

        // =========================================================================
        // OGCD LOGIC (CON NIVEL)
        // =========================================================================
        private unsafe static bool TryUseOgcd(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            OperationalSettings op)
        {
            if (op.SaveCD) return false;

            var gauge = Plugin.JobGauges.Get<MNKGauge>();
            bool rofActive = HasStatus(player, MNK_IDs.Status_RiddleOfFire);

            // SIMULACIÓN DE ROF PARA NIVEL BAJO (50-67)
            // Si no tenemos RoF desbloqueado, simulamos que está activo para que PB se use.
            if (player.Level < MNK_Levels.RiddleOfFire) rofActive = true;

            // 1. RIDDLE OF FIRE (Lv 68+)
            if (op.UseRoF && player.Level >= MNK_Levels.RiddleOfFire && CanUseRecast(am, MNK_IDs.RiddleOfFire))
            {
                LastProposedAction = MNK_IDs.RiddleOfFire;
                PressBind(config.RiddleOfFire);
                return true;
            }

            // 2. PERFECT BALANCE (Lv 50+)
            if (op.UsePB && player.Level >= MNK_Levels.PerfectBalance)
            {
                // Check de seguridad: 2.5s para no spamear si falla
                bool pbSafe = (DateTime.Now - LastPBTime).TotalSeconds > 2.5;

                int rawNadi = (int)gauge.Nadi;
                bool hasLunar = (rawNadi & 1) != 0;
                bool hasSolar = (rawNadi & 2) != 0;
                bool isOpener = !hasLunar && !hasSolar;

                // Si no hay Blitz (Lv < 60), no existe concepto de "Opener" ni "Nadi", solo uso
                if (player.Level < MNK_Levels.MasterfulBlitz) isOpener = false;
                if (isOpener) pbSafe = true;

                GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out int pbCharges, out int pbMax);
                bool inPB = HasStatus(player, MNK_IDs.Status_PerfectBalance);

                if (!inPB && pbCharges > 0 && pbSafe)
                {
                    if (isOpener)
                    {
                        LastProposedAction = MNK_IDs.PerfectBalance;
                        LastPBTime = DateTime.Now;
                        PressBind(config.PerfectBalance);
                        return true;
                    }

                    if (rofActive)
                    {
                        bool usePB = false;
                        if (HasStatus(player, MNK_IDs.Status_Brotherhood) || CanUseRecast(am, MNK_IDs.Brotherhood)) usePB = true;
                        else if (pbCharges >= 1) usePB = true;

                        if (usePB)
                        {
                            LastProposedAction = MNK_IDs.PerfectBalance;
                            LastPBTime = DateTime.Now;
                            PressBind(config.PerfectBalance);
                            return true;
                        }
                    }

                    if (pbCharges == pbMax) // Overcap
                    {
                        float rofCD = am->GetRecastTime(ActionType.Action, MNK_IDs.RiddleOfFire);
                        if (rofCD > 10.0f && !rofActive)
                        {
                            LastProposedAction = MNK_IDs.PerfectBalance;
                            LastPBTime = DateTime.Now;
                            PressBind(config.PerfectBalance);
                            return true;
                        }
                    }
                }
            }

            // 3. BROTHERHOOD (Lv 70+)
            if (op.UseBrotherhood && player.Level >= MNK_Levels.Brotherhood && CanUseRecast(am, MNK_IDs.Brotherhood))
            {
                if (rofActive)
                {
                    GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out int pbCharges, out int pbMax);
                    bool inPB = HasStatus(player, MNK_IDs.Status_PerfectBalance);
                    bool pbSafe = (DateTime.Now - LastPBTime).TotalSeconds > 2.5;

                    // Si toca PB pero no ha salido, espera (solo si somos 50+)
                    if (player.Level >= MNK_Levels.PerfectBalance && pbCharges > 0 && !inPB && pbSafe) return false;

                    LastProposedAction = MNK_IDs.Brotherhood;
                    PressBind(config.Brotherhood);
                    return true;
                }
            }

            // 4. RIDDLE OF WIND (Lv 72+)
            if (op.UseRoW && player.Level >= MNK_Levels.RiddleOfWind && rofActive && CanUseRecast(am, MNK_IDs.RiddleOfWind))
            {
                LastProposedAction = MNK_IDs.RiddleOfWind;
                PressBind(config.RiddleOfWind);
                return true;
            }

            // 6. FORBIDDEN CHAKRA (Lv 54+)
            if (op.UseForbiddenChakra && player.Level >= MNK_Levels.ForbiddenChakra && gauge.Chakra >= 5 && CanUseRecast(am, MNK_IDs.TheForbiddenChakra))
            {
                LastProposedAction = MNK_IDs.TheForbiddenChakra;
                PressBind(config.ForbiddenChakra);
                return true;
            }

            return false;
        }

        // =========================================================================
        // HELPERS CON NIVEL (Sustitución de habilidades)
        // =========================================================================
        private static (uint, KeyBind) GetBestOpoAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            // AoE: Arm of the Destroyer (26)
            if (useAoE && level >= MNK_Levels.ArmOfTheDestroyer) return (MNK_IDs.ArmOfTheDestroyer, config.ArmOfTheDestroyer);

            // Si < 50 no hay Dragon Kick -> Bootshine
            if (level < MNK_Levels.DragonKick) return (MNK_IDs.Bootshine, config.Bootshine);

            if (gauge.OpoOpoFury > 0) return (MNK_IDs.Bootshine, config.Bootshine);
            else return (MNK_IDs.DragonKick, config.DragonKick);
        }

        private static (uint, KeyBind) GetBestRaptorAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            // AoE: Four Point Fury (45)
            if (useAoE && level >= MNK_Levels.FourPointFury) return (MNK_IDs.FourPointFury, config.FourPointFury);

            // Si < 18 no hay Twin Snakes -> True Strike
            if (level < MNK_Levels.TwinSnakes) return (MNK_IDs.TrueStrike, config.TrueStrike);

            if (gauge.RaptorFury > 0) return (MNK_IDs.TrueStrike, config.TrueStrike);
            else return (MNK_IDs.TwinSnakes, config.TwinSnakes);
        }

        private static (uint, KeyBind) GetBestCoeurlAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            // AoE: Rockbreaker (30)
            if (useAoE && level >= MNK_Levels.Rockbreaker) return (MNK_IDs.Rockbreaker, config.Rockbreaker);

            // Si < 30 no hay Demolish -> Snap Punch
            if (level < MNK_Levels.Demolish) return (MNK_IDs.SnapPunch, config.SnapPunch);

            if (gauge.CoeurlFury > 0) return (MNK_IDs.SnapPunch, config.SnapPunch);
            else return (MNK_IDs.Demolish, config.Demolish);
        }

        // ... [RESTO DE HELPERS SIN CAMBIOS] ...
        private static MNKGauge GetGauge() => Plugin.JobGauges.Get<MNKGauge>();
        private static void PressBind(KeyBind bind) => InputSender.Send(bind.Key, bind.Bar);
        private unsafe static bool CanUseRecast(ActionManager* am, uint id) => am->GetRecastTime(ActionType.Action, id) == 0;

        private static bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player == null) return false;
            foreach (var s in player.StatusList)
                if (s.StatusId == statusId) return true;
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
            current = 0;
            max = 0;
            if (am == null) return;
            max = (int)ActionManager.GetMaxCharges(actionId, level);
            current = (int)am->GetCurrentCharges(actionId);
        }
    }
}
