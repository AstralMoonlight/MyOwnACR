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

namespace MyOwnACR.Logic
{
    public static class MNK_Logic
    {
        // =========================================================================
        // CONFIGURACIÓN
        // =========================================================================
        // Alcance desde el "borde" del enemigo (Hitbox) hacia afuera.
        // 3.5f es un valor seguro para compensar latencia (max melee real es ~3y)
        private const float MeleeRange = 3.5f;

        private const int AoE_Threshold = 3;
        private const int MinActionDelayMs = 250;
        private static DateTime lastAnyActionTime = DateTime.MinValue;
        private static bool ogcdUsedSinceLastGcd = false;

        // PROTECCIÓN ANTI-SOBREESCRITURA DE PB
        private static DateTime LastPBTime = DateTime.MinValue;

        public static uint LastProposedAction = 0;

        // IDs
        private const ushort Status_PerfectBalance = 110;
        private const ushort Status_RiddleOfFire = 1181;
        private const ushort Status_Brotherhood = 1185;
        private const ushort Status_FormlessFist = 2513;
        private const ushort Status_OpoOpoForm = 107;
        private const ushort Status_RaptorForm = 108;
        private const ushort Status_CoeurlForm = 109;
        private const ushort Status_FiresRumination = 3843;
        private const ushort Status_WindsRumination = 3842;

        // =========================================================================
        // DEBUG MANUAL (/acrdebug)
        // =========================================================================
        public unsafe static void PrintDebugInfo(IChatGui chat)
        {
            var player = Plugin.ObjectTable.LocalPlayer;
            if (player == null) return;

            var gauge = GetGauge();
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;

            var am = ActionManager.Instance();
            GetActionCharges(am, MonkIDs.PerfectBalance, player.Level, out int curPB, out int maxPB);

            chat.Print($"[ACR] Nadi: L={hasLunar} S={hasSolar} | PB: {curPB}/{maxPB}");

            // Debug de Rango y Hitbox
            if (player.TargetObject is IGameObject target)
            {
                float dist = Vector3.Distance(player.Position, target.Position);
                float maxDist = target.HitboxRadius + MeleeRange;
                chat.Print($"[ACR] Distancia: {dist:F2} | Hitbox: {target.HitboxRadius:F2} | MaxRange: {maxDist:F2}");
                chat.Print($"[ACR] ¿En Rango?: {dist <= maxDist}");
            }
        }

        // =========================================================================
        // EXECUTE
        // =========================================================================
        public unsafe static void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            JobConfig_MNK config,
            IObjectTable objectTable,
            OperationalSettings operation)
        {
            if (am == null || player == null || config == null) return;

            LastProposedAction = 0;

            var now = DateTime.Now;
            if ((now - lastAnyActionTime).TotalMilliseconds < MinActionDelayMs) return;

            bool inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;

            // 0. FUERA DE COMBATE
            if (!inCombat)
            {
                bool hasAnyForm = HasStatus(player, Status_OpoOpoForm) ||
                                  HasStatus(player, Status_RaptorForm) ||
                                  HasStatus(player, Status_CoeurlForm) ||
                                  HasStatus(player, Status_FormlessFist);

                if (!hasAnyForm && CanUseRecast(am, MonkIDs.FormShift))
                {
                    LastProposedAction = MonkIDs.FormShift;
                    PressBind(config.FormShift);
                    lastAnyActionTime = now;
                    return;
                }

                var gauge = GetGauge();
                if (gauge.Chakra < 5 && CanUseRecast(am, MonkIDs.Meditation))
                {
                    LastProposedAction = MonkIDs.Meditation;
                    PressBind(config.Meditation);
                    lastAnyActionTime = now;
                }
                ogcdUsedSinceLastGcd = false;
                return;
            }

            // 0.5 FUERA DE RANGO
            // Check de Rango corregido (Hitbox + Melee)
            bool inRange = IsInMeleeRange(player);

            if (!inRange)
            {
                // Si estamos lejos, usamos los Replies o cargamos chakra
                if (HasStatus(player, Status_FiresRumination) && CanUseRecast(am, MonkIDs.FiresReply))
                {
                    LastProposedAction = MonkIDs.FiresReply;
                    PressBind(config.FiresReply);
                    lastAnyActionTime = now;
                    ogcdUsedSinceLastGcd = false;
                    return;
                }
                if (HasStatus(player, Status_WindsRumination) && CanUseRecast(am, MonkIDs.WindsReply))
                {
                    LastProposedAction = MonkIDs.WindsReply;
                    PressBind(config.WindsReply);
                    lastAnyActionTime = now;
                    ogcdUsedSinceLastGcd = false;
                    return;
                }

                var gaugeRun = GetGauge();
                if (gaugeRun.Chakra < 5 && CanUseRecast(am, MonkIDs.Meditation))
                {
                    LastProposedAction = MonkIDs.Meditation;
                    PressBind(config.Meditation);
                    lastAnyActionTime = now;
                    return;
                }
                return;
            }

            // 1. GCD LOGIC
            // AoE Check: Contamos enemigos dentro de 5y DE TI MISMO (no del target)
            int enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5.0f);
            bool useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;

            var gcdCandidate = GetNextGcdCandidate(am, config, player, useAoE);

            if (gcdCandidate.HasValue)
                LastProposedAction = gcdCandidate.Value.actionId;

            bool isActionReady = false;

            if (gcdCandidate.HasValue)
            {
                uint actionId = gcdCandidate.Value.actionId;
                if (IsBlitz(actionId)) actionId = MonkIDs.MasterfulBlitz;
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
                if (TryUseOgcd(am, config, player))
                {
                    ogcdUsedSinceLastGcd = true;
                    lastAnyActionTime = now;
                    return;
                }
            }
        }

        // =========================================================================
        // SELECTOR DE GCD
        // =========================================================================
        private unsafe static (uint actionId, KeyBind bind)? GetNextGcdCandidate(
            ActionManager* am,
            JobConfig_MNK config,
            IPlayerCharacter player,
            bool useAoE)
        {
            var gauge = GetGauge();

            // 1. BLITZ CHECK
            int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);

            if (realChakraCount == 3)
            {
                uint specificBlitzId = GetActiveBlitzId(gauge);
                if (specificBlitzId == MonkIDs.PhantomRush) return (specificBlitzId, config.PhantomRush);
                if (specificBlitzId == MonkIDs.RisingPhoenix) return (specificBlitzId, config.RisingPhoenix);
                if (specificBlitzId == MonkIDs.ElixirField) return (specificBlitzId, config.ElixirField);
                return (specificBlitzId, config.MasterfulBlitz);
            }

            bool isPerfectBalance = HasStatus(player, Status_PerfectBalance);

            // 2. MODO PERFECT BALANCE
            if (isPerfectBalance)
            {
                int rawNadi = (int)gauge.Nadi;
                bool hasLunar = (rawNadi & 1) != 0;
                bool hasSolar = (rawNadi & 2) != 0;

                // A: Phantom Rush -> Spam Opo
                if (hasLunar && hasSolar) return GetBestOpoAction(useAoE, gauge, config);

                // B: Elixir Burst -> Spam Opo
                // Si hay Brotherhood activo (2min), forzamos Lunar (Opo) para alinear
                if (HasStatus(player, Status_Brotherhood))
                {
                    return GetBestOpoAction(useAoE, gauge, config);
                }

                if (!hasLunar) return GetBestOpoAction(useAoE, gauge, config);

                // C: Rising Phoenix -> 3 DISTINTOS
                if (hasLunar && !hasSolar)
                {
                    if (realChakraCount == 0) return GetBestOpoAction(useAoE, gauge, config);
                    if (realChakraCount == 1) return GetBestRaptorAction(useAoE, gauge, config);
                    return GetBestCoeurlAction(useAoE, gauge, config);
                }
            }

            // 3. REPLIES (Fire's / Wind's Reply)
            if (HasStatus(player, Status_FiresRumination))
                return (MonkIDs.FiresReply, config.FiresReply);

            if (HasStatus(player, Status_WindsRumination))
                return (MonkIDs.WindsReply, config.WindsReply);

            // 4. ROTACIÓN NORMAL
            bool isFormless = HasStatus(player, Status_FormlessFist);
            bool isRaptor = HasStatus(player, Status_RaptorForm);
            bool isCoeurl = HasStatus(player, Status_CoeurlForm);

            if (isFormless) return GetBestOpoAction(useAoE, gauge, config);
            if (isRaptor) return GetBestRaptorAction(useAoE, gauge, config);
            else if (isCoeurl) return GetBestCoeurlAction(useAoE, gauge, config);
            else return GetBestOpoAction(useAoE, gauge, config);
        }

        // =========================================================================
        // OGCD LOGIC
        // =========================================================================
        private unsafe static bool TryUseOgcd(ActionManager* am, JobConfig_MNK config, IPlayerCharacter player)
        {
            var gauge = GetGauge();
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;
            bool isOpener = !hasLunar && !hasSolar;

            // Datos de estado
            GetActionCharges(am, MonkIDs.PerfectBalance, player.Level, out int pbCharges, out int pbMax);
            bool rofActive = HasStatus(player, Status_RiddleOfFire);
            bool inPB = HasStatus(player, Status_PerfectBalance);

            // 1. RIDDLE OF FIRE (PRIORIDAD #1)
            if (CanUseRecast(am, MonkIDs.RiddleOfFire))
            {
                LastProposedAction = MonkIDs.RiddleOfFire;
                PressBind(config.RiddleOfFire);
                return true;
            }

            // 2. PERFECT BALANCE (Con protección de 20s anti-sobreescritura)
            bool pbSafe = (DateTime.Now - LastPBTime).TotalSeconds > 20.0;
            if (isOpener) pbSafe = true; // Excepción Opener

            if (!inPB && pbCharges > 0 && pbSafe)
            {
                // A) OPENER
                if (isOpener)
                {
                    LastProposedAction = MonkIDs.PerfectBalance;
                    LastPBTime = DateTime.Now;
                    PressBind(config.PerfectBalance);
                    return true;
                }

                // B) BURST
                if (rofActive)
                {
                    bool usePB = false;

                    // Si Brotherhood está activo o LISTO -> Ventana 2 min -> Gastar todo.
                    if (HasStatus(player, Status_Brotherhood) || CanUseRecast(am, MonkIDs.Brotherhood))
                    {
                        usePB = true;
                    }
                    // Si solo es RoF (1 min) -> Gastar si tenemos al menos 1 carga.
                    else if (pbCharges >= 1)
                    {
                        usePB = true;
                    }

                    if (usePB)
                    {
                        LastProposedAction = MonkIDs.PerfectBalance;
                        LastPBTime = DateTime.Now;
                        PressBind(config.PerfectBalance);
                        return true;
                    }
                }

                // C) OVERCAP
                if (pbCharges == pbMax)
                {
                    float rofCD = am->GetRecastTime(ActionType.Action, MonkIDs.RiddleOfFire);
                    if (rofCD > 10.0f && !rofActive)
                    {
                        LastProposedAction = MonkIDs.PerfectBalance;
                        LastPBTime = DateTime.Now;
                        PressBind(config.PerfectBalance);
                        return true;
                    }
                }
            }

            // 3. BROTHERHOOD
            if (CanUseRecast(am, MonkIDs.Brotherhood))
            {
                if (rofActive)
                {
                    if (pbCharges > 0 && !inPB && pbSafe) return false; // Forzar PB antes si es posible

                    LastProposedAction = MonkIDs.Brotherhood;
                    PressBind(config.Brotherhood);
                    return true;
                }
            }

            // 4. RIDDLE OF WIND
            if (rofActive && CanUseRecast(am, MonkIDs.RiddleOfWind))
            {
                LastProposedAction = MonkIDs.RiddleOfWind;
                PressBind(config.RiddleOfWind);
                return true;
            }

            // 5. FORBIDDEN CHAKRA
            if (gauge.Chakra >= 5 && CanUseRecast(am, MonkIDs.ForbiddenChakra))
            {
                LastProposedAction = MonkIDs.ForbiddenChakra;
                PressBind(config.ForbiddenChakra);
                return true;
            }

            return false;
        }

        // =========================================================================
        // HELPERS
        // =========================================================================
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

        // ----------------------------------------------------------------------------------
        // IS IN MELEE RANGE (CORREGIDO: DISTANCIA + RADIO HITBOX)
        // ----------------------------------------------------------------------------------
        private static bool IsInMeleeRange(IPlayerCharacter player)
        {
            if (player?.TargetObject is not IGameObject target) return false;

            // Distancia entre centros
            float distSq = Vector3.DistanceSquared(player.Position, target.Position);

            // Rango total permitido = Radio del enemigo + Alcance de tus golpes
            float totalRange = target.HitboxRadius + MeleeRange;

            // Comparamos al cuadrado para evitar raíz cuadrada (más eficiente)
            return distSq <= (totalRange * totalRange);
        }

        private static uint GetActiveBlitzId(MNKGauge gauge)
        {
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;

            if (hasLunar && hasSolar) return MonkIDs.PhantomRush;

            int distinctTypes = gauge.BeastChakra
                .Where(c => c != BeastChakra.None)
                .Distinct()
                .Count();

            if (distinctTypes == 3) return MonkIDs.RisingPhoenix;
            else return MonkIDs.ElixirField;
        }

        private static bool IsBlitz(uint id)
        {
            return id == MonkIDs.PhantomRush || id == MonkIDs.RisingPhoenix ||
                   id == MonkIDs.ElixirField || id == MonkIDs.MasterfulBlitz;
        }

        private static (uint, KeyBind) GetBestOpoAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config)
        {
            if (useAoE) return (MonkIDs.ArmOfTheDestroyer, config.ArmOfTheDestroyer);
            if (gauge.OpoOpoFury > 0) return (MonkIDs.Bootshine, config.Bootshine);
            else return (MonkIDs.DragonKick, config.DragonKick);
        }

        private static (uint, KeyBind) GetBestRaptorAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config)
        {
            if (useAoE) return (MonkIDs.FourPointFury, config.FourPointFury);
            if (gauge.RaptorFury > 0) return (MonkIDs.TrueStrike, config.TrueStrike);
            else return (MonkIDs.TwinSnakes, config.TwinSnakes);
        }

        private static (uint, KeyBind) GetBestCoeurlAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config)
        {
            if (useAoE) return (MonkIDs.Rockbreaker, config.Rockbreaker);
            if (gauge.CoeurlFury > 0) return (MonkIDs.SnapPunch, config.SnapPunch);
            else return (MonkIDs.Demolish, config.Demolish);
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
