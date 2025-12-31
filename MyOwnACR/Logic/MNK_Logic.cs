// Archivo: Logic/MNK_Logic.cs
// Descripción: Lógica de combate completa para el Job Monk (MNK).
// Versión: Production Ready (Fix Brotherhood Priority & Phantom Rush Alignment)
//
// CARACTERÍSTICAS PRINCIPALES:
// 1. Prioridad de Burst: Alineación estricta de Riddle of Fire -> Brotherhood.
// 2. Phantom Rush Hold: Espera inteligente (<15s) para alinearse con Brotherhood.
// 3. Anti-Deadlock: Sistema que permite spamear OGCDs críticos incluso si el contador de weaves está lleno durante un Hold.
// 4. Riddle of Wind: Uso optimizado "On Cooldown" para no perder usos en peleas largas.

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
    /// <summary>
    /// Clase estática que contiene toda la toma de decisiones para el Monk.
    /// </summary>
    public static class MNK_Logic
    {
        // =========================================================================
        // CONFIGURACIÓN & VARIABLES DE ESTADO
        // =========================================================================

        private const float MeleeRange = 3.5f; // Rango máximo para considerar que estamos "pegando"
        private const int AoE_Threshold = 3;   // Cantidad de enemigos para cambiar a rotación de área

        // Ventana de tiempo (segundos) antes del recast para enviar la siguiente acción al servidor.
        // 0.6s es seguro para latencias medias/altas sin perder GCDs.
        private static float Action_Queue_Window = 0.6f;

        private static float RoF_Prepop_Window = 6.0f; // Tiempo antes del burst para preparar recursos

        // Variables de seguimiento temporal
        private static DateTime LastAnyActionTime = DateTime.MinValue;
        private static DateTime LastPBTime = DateTime.MinValue;
        private static DateTime LastTrueNorthTime = DateTime.MinValue;

        // Variables de control de Weaving (GCD vs OGCD)
        private static bool LastActionWasGCD = true;
        private static int OgcdCount = 0; // Contador de habilidades usadas entre GCDs

        public static uint LastProposedAction = 0; // Última acción enviada por el bot
        private static string QueuedManualAction = ""; // Cola para acciones forzadas por el usuario

        // =========================================================================
        // MÉTODOS DE COMUNICACIÓN
        // =========================================================================

        /// <summary>
        /// Encola una acción manual para ser ejecutada con prioridad absoluta en el siguiente ciclo.
        /// </summary>
        public static void QueueManualAction(string actionName) { QueuedManualAction = actionName; }

        public static string GetQueuedAction() => QueuedManualAction;

        public unsafe static void PrintDebugInfo(IChatGui chat) { /* Silenciado para producción */ }

        /// <summary>
        /// Ejecuta una acción física o mágica, actualizando los contadores de Weave.
        /// </summary>
        private static void ExecuteAction(uint actionId, KeyBind keyBind)
        {
            bool isGCD = ActionLibrary.IsGCD(actionId);
            PressBind(keyBind, isGCD);

            LastProposedAction = actionId;
            LastAnyActionTime = DateTime.Now;

            if (isGCD)
            {
                LastActionWasGCD = true;
                OgcdCount = 0; // Resetear contador de weaves al usar un GCD
            }
            else
            {
                LastActionWasGCD = false;
                OgcdCount++; // Incrementar contador de weaves
            }
        }

        // =========================================================================
        // LÓGICA PRINCIPAL (EXECUTE)
        // =========================================================================

        /// <summary>
        /// Método principal llamado en cada frame/ciclo del juego. Decide qué botón pulsar.
        /// </summary>
        public unsafe static void Execute(
            ActionManager* am,
            IPlayerCharacter player,
            JobConfig_MNK config,
            Dalamud.Plugin.Services.IObjectTable objectTable,
            OperationalSettings operation)
        {
            if (am == null || player == null || config == null) return;

            // ---------------------------------------------------------------------
            // 1. PRIORIDAD ABSOLUTA: ACCIONES MANUALES
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
                    bool isGCD = (manualActionId != 0) && ActionLibrary.IsGCD(manualActionId);
                    InputSender.Send(bindToPress.Key, bindToPress.Bar, isGCD);
                    LastAnyActionTime = DateTime.Now;
                    if (isGCD) { LastActionWasGCD = true; OgcdCount = 0; }
                    else { LastActionWasGCD = false; }
                }
                QueuedManualAction = "";
                return;
            }

            LastProposedAction = 0;
            var now = DateTime.Now;

            // ---------------------------------------------------------------------
            // 2. CONTROL DE TIEMPOS (WEAVE TIMING)
            // Evita spamear teclas demasiado rápido (clipping).
            // ---------------------------------------------------------------------
            int requiredDelay;
            if (LastActionWasGCD) requiredDelay = config.WeaveDelay_oGCD1_MS;
            else requiredDelay = config.WeaveDelay_oGCD2_MS;

            if ((now - LastAnyActionTime).TotalMilliseconds < (requiredDelay - (Action_Queue_Window * 1000))) return;

            bool inCombat = Plugin.Condition?[ConditionFlag.InCombat] ?? false;
            var target = player.TargetObject;
            bool hasTarget = target != null && target.IsValid();
            bool inRange = hasTarget && IsInMeleeRange(player);

            // ---------------------------------------------------------------------
            // 3. LÓGICA DE DOWNTIME / FUERA DE RANGO
            // Carga de Chakras y mantenimiento de buffs fuera de combate.
            // ---------------------------------------------------------------------
            if (!inCombat || !inRange)
            {
                if (!inCombat) OgcdCount = 0; // Resetear weaves si salimos de combate

                // Carga de Chakra (Steeled Meditation)
                if (player.Level >= MNK_Levels.Meditation)
                {
                    var gauge = Plugin.JobGauges.Get<MNKGauge>();
                    // Usamos ID 0 para CanUseRecast porque Meditation es especial (GCD pero sin recast compartido estándar)
                    if (gauge.Chakra < 5 && CanUseRecast(am, MNK_IDs.SteeledMeditation, 0))
                    {
                        ExecuteAction(MNK_IDs.SteeledMeditation, config.Meditation);
                        return;
                    }
                }

                // Uso de Blitzes de rango (Fires/Winds Reply) si tenemos target
                if (hasTarget && inCombat)
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

                // Form Shift para mantener Formas activas
                bool hasAnyForm = HasStatus(player, MNK_IDs.Status_OpoOpoForm) ||
                                  HasStatus(player, MNK_IDs.Status_RaptorForm) ||
                                  HasStatus(player, MNK_IDs.Status_CoeurlForm) ||
                                  HasStatus(player, MNK_IDs.Status_FormlessFist) ||
                                  HasStatus(player, MNK_IDs.Status_PerfectBalance);

                if (LastProposedAction == MNK_IDs.FormShift && (now - LastAnyActionTime).TotalSeconds < 2.0f) hasAnyForm = true;

                if (player.Level >= MNK_Levels.FormShift && !hasAnyForm && CanUseRecast(am, MNK_IDs.FormShift))
                {
                    ExecuteAction(MNK_IDs.FormShift, config.FormShift);
                    return;
                }
                return;
            }

            // ---------------------------------------------------------------------
            // 4. ROTACIÓN DE COMBATE (GCD)
            // ---------------------------------------------------------------------
            int enemyCount = CombatHelpers.CountAttackableEnemiesInRange(objectTable, player, 5f);
            bool useAoE = operation.AoE_Enabled && enemyCount >= AoE_Threshold;

            // Obtener el siguiente GCD óptimo
            var gcdCandidate = GetNextGcdCandidate(am, config, player, useAoE, operation);

            bool isActionReady = false;
            if (gcdCandidate.HasValue)
            {
                uint actionId = gcdCandidate.Value.actionId;
                // Si es un Blitz, verificamos el CD del Masterful Blitz genérico
                if (IsBlitz(actionId)) actionId = MNK_IDs.MasterfulBlitz;
                isActionReady = CanUseRecast(am, actionId, Action_Queue_Window);
            }

            if (isActionReady && gcdCandidate.HasValue)
            {
                // Lógica de True North Automático para Posicionales
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

                // Ejecutar el GCD seleccionado
                ExecuteAction(gcdCandidate.Value.actionId, gcdCandidate.Value.bind);
                return;
            }

            // ---------------------------------------------------------------------
            // 6. ROTACIÓN DE COMBATE (OGCD - Off Global Cooldown)
            // ---------------------------------------------------------------------
            int maxWeaves = config.EnableDoubleWeave ? 2 : 1;

            // Cálculos del tiempo restante de GCD
            float gcdTotal = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 11);
            float gcdElapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 11);
            float gcdRemaining = (gcdTotal > 0) ? Math.Max(0, gcdTotal - gcdElapsed) : 0;

            // Detección de Estado "HOLD" (Esperando):
            // Si el GCD está listo (casi 0) pero GetNextGcdCandidate devolvió null (indicando espera), estamos en Hold.
            bool isHolding = (gcdRemaining <= 0.01f && !isActionReady);

            // FIX CRÍTICO: Prevención de Deadlock.
            // Normalmente, si OgcdCount >= maxWeaves, dejamos de intentar OGCDs para no clippear el siguiente GCD.
            // PERO, si estamos en "HOLD", significa que el GCD está parado esperando algo (ej. Brotherhood).
            // En ese caso, ignoramos el límite de weaves y permitimos intentar OGCDs infinitamente hasta que salga el buff requerido.
            if (OgcdCount < maxWeaves || isHolding)
            {
                // Solo intentamos OGCDs si:
                // 1. Hay espacio suficiente en la ventana de GCD (> 0.6s).
                // 2. O acabamos de usar un GCD (inicio de la ventana).
                // 3. O estamos en HOLD (parados).
                if (gcdRemaining > 0.6f || LastActionWasGCD || isHolding)
                {
                    TryUseOgcd(am, config, player, operation);
                }
            }
        }

        // =========================================================================
        // HELPERS GENÉRICOS
        // =========================================================================

        private static void PressBind(KeyBind bind, bool isGCD) => InputSender.Send(bind.Key, bind.Bar, isGCD);

        /// <summary>
        /// Verifica si una acción está lista para usarse (considerando Recast y Cola).
        /// </summary>
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

        // --- Helpers de Selección de Forma (Opo-Opo, Raptor, Coeurl) ---

        private static (uint, KeyBind) GetBestOpoAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            if (useAoE && level >= MNK_Levels.ArmOfTheDestroyer) return (MNK_IDs.ArmOfTheDestroyer, config.ArmOfTheDestroyer);
            if (level < MNK_Levels.DragonKick) return (MNK_IDs.Bootshine, config.Bootshine);
            if (gauge.OpoOpoFury > 0) return (MNK_IDs.Bootshine, config.Bootshine); // Prioridad con Leaping Opo
            else return (MNK_IDs.DragonKick, config.DragonKick);
        }

        private static (uint, KeyBind) GetBestRaptorAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            if (useAoE && level >= MNK_Levels.FourPointFury) return (MNK_IDs.FourPointFury, config.FourPointFury);
            if (level < MNK_Levels.TwinSnakes) return (MNK_IDs.TrueStrike, config.TrueStrike);
            if (gauge.RaptorFury > 0) return (MNK_IDs.TrueStrike, config.TrueStrike); // Prioridad con Rising Raptor
            else return (MNK_IDs.TwinSnakes, config.TwinSnakes);
        }

        private static (uint, KeyBind) GetBestCoeurlAction(bool useAoE, MNKGauge gauge, JobConfig_MNK config, uint level)
        {
            if (useAoE && level >= MNK_Levels.Rockbreaker) return (MNK_IDs.Rockbreaker, config.Rockbreaker);
            if (level < MNK_Levels.Demolish) return (MNK_IDs.SnapPunch, config.SnapPunch);
            if (gauge.CoeurlFury > 0) return (MNK_IDs.SnapPunch, config.SnapPunch); // Prioridad con Pouncing Coeurl
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

        /// <summary>
        /// Determina qué Blitz está disponible según los Nadis y Chakras acumulados.
        /// </summary>
        private static uint GetActiveBlitzId(MNKGauge gauge)
        {
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;

            // Si tenemos ambos Nadis -> Phantom Rush
            if (hasLunar && hasSolar) return MNK_IDs.PhantomRush;

            // Si no, depende de los Chakras (3 distintos = Solar/Phoenix, 3 iguales = Lunar/Elixir)
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
        // SELECCIÓN DE GCD (GetNextGcdCandidate)
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

            // --- 1. MASTERFUL BLITZ LOGIC ---
            if (player.Level >= MNK_Levels.MasterfulBlitz)
            {
                int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                if (realChakraCount == 3)
                {
                    uint specificBlitzId = GetActiveBlitzId(gauge);

                    // ----------------------------------------------------------------------------
                    // HOLD LOGIC: ALINEACIÓN DE PHANTOM RUSH
                    // Objetivo: Asegurar que Phantom Rush entre bajo el buff de Brotherhood.
                    // ----------------------------------------------------------------------------
                    if (specificBlitzId == MNK_IDs.PhantomRush && op.UseBrotherhood)
                    {
                        if (player.Level >= MNK_Levels.Brotherhood)
                        {
                            float bhTotal = am->GetRecastTime(ActionType.Action, MNK_IDs.Brotherhood);
                            float bhElapsed = am->GetRecastTimeElapsed(ActionType.Action, MNK_IDs.Brotherhood);
                            float bhCD = (bhTotal > 0) ? Math.Max(0, bhTotal - bhElapsed) : 0;

                            bool hasBhBuff = HasStatus(player, MNK_IDs.Status_Brotherhood);

                            // REGLA DE ALINEACIÓN:
                            // Si NO tenemos el buff Y Brotherhood está cerca (menos de 15s) -> ESPERAR (HOLD).
                            // Si Brotherhood está lejos (> 15s, drift severo) -> USAR (No detener rotación).
                            if (!hasBhBuff && bhCD < 15.0f)
                            {
                                return null; // Retornar null detiene el GCD y fuerza el chequeo de OGCDs
                            }
                        }
                        return (specificBlitzId, config.PhantomRush);
                    }
                    // ----------------------------------------------------------------------------

                    if (specificBlitzId == MNK_IDs.RisingPhoenix) return (specificBlitzId, config.RisingPhoenix);
                    if (specificBlitzId == MNK_IDs.ElixirBurst) return (specificBlitzId, config.ElixirField);
                    return (specificBlitzId, config.MasterfulBlitz);
                }
            }

            // --- 2. PERFECT BALANCE LOGIC ---
            if (player.Level >= MNK_Levels.PerfectBalance && isPerfectBalance)
            {
                if (player.Level < MNK_Levels.MasterfulBlitz) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                int rawNadi = (int)gauge.Nadi;
                bool hasLunar = (rawNadi & 1) != 0;
                bool hasSolar = (rawNadi & 2) != 0;

                // Si ya tenemos Solar y Lunar, o estamos bajo Brotherhood, spameamos Opo-Opo (Bootshine/Dragon Kick)
                if (hasLunar && hasSolar) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                if (HasStatus(player, MNK_IDs.Status_Brotherhood)) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // Prioridad: Obtener Lunar primero (spameando Opo)
                if (!hasLunar) return GetBestOpoAction(useAoE, gauge, config, player.Level);

                // Si tenemos Lunar, buscamos Solar (3 acciones distintas)
                if (hasLunar && !hasSolar)
                {
                    int realChakraCount = gauge.BeastChakra.Count(c => c != BeastChakra.None);
                    if (realChakraCount == 0) return GetBestOpoAction(useAoE, gauge, config, player.Level);
                    if (realChakraCount == 1) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
                    return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
                }
            }

            // --- 3. COMBOS ESTÁNDAR ---
            uint lastAction = am->Combo.Action;
            bool lastWasOpo = lastAction == MNK_IDs.Bootshine || lastAction == MNK_IDs.DragonKick || lastAction == MNK_IDs.LeapingOpo || lastAction == MNK_IDs.ArmOfTheDestroyer || lastAction == MNK_IDs.ShadowOfTheDestroyer;

            // Prioridad: Ruminations (procs de rango tras Blitz)
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

            // Selección de forma estándar
            bool isFormless = HasStatus(player, MNK_IDs.Status_FormlessFist);
            if (isFormless) return GetBestOpoAction(useAoE, gauge, config, player.Level);

            bool isRaptor = HasStatus(player, MNK_IDs.Status_RaptorForm);
            bool isCoeurl = HasStatus(player, MNK_IDs.Status_CoeurlForm);

            if (isRaptor) return GetBestRaptorAction(useAoE, gauge, config, player.Level);
            else if (isCoeurl) return GetBestCoeurlAction(useAoE, gauge, config, player.Level);
            else return GetBestOpoAction(useAoE, gauge, config, player.Level);
        }

        // =========================================================================
        // SELECCIÓN DE OGCD (TryUseOgcd)
        // =========================================================================
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

            // ----------------------------------------------------------------------
            // 1. RIDDLE OF FIRE (Burst Starter)
            // ----------------------------------------------------------------------
            if (op.UseRoF && player.Level >= MNK_Levels.RiddleOfFire && CanUseRecast(am, MNK_IDs.RiddleOfFire, Action_Queue_Window))
            {
                ExecuteAction(MNK_IDs.RiddleOfFire, config.RiddleOfFire);
                return true;
            }

            // ----------------------------------------------------------------------
            // 2. BROTHERHOOD
            // Condición simple: Si está disponible Y Riddle of Fire está activo, USAR.
            // ----------------------------------------------------------------------
            if (op.UseBrotherhood && player.Level >= MNK_Levels.Brotherhood)
            {
                if (rofActive && CanUseRecast(am, MNK_IDs.Brotherhood, Action_Queue_Window))
                {
                    ExecuteAction(MNK_IDs.Brotherhood, config.Brotherhood);
                    return true;
                }
            }

            // ----------------------------------------------------------------------
            // 3. PERFECT BALANCE
            // ----------------------------------------------------------------------
            bool pbSafe = (DateTime.Now - LastPBTime).TotalSeconds > 2.5;
            int rawNadi = (int)gauge.Nadi;
            bool hasLunar = (rawNadi & 1) != 0;
            bool hasSolar = (rawNadi & 2) != 0;
            bool isOpener = !hasLunar && !hasSolar;

            if (player.Level < MNK_Levels.MasterfulBlitz) isOpener = false;
            if (isOpener) pbSafe = true;

            GetActionCharges(am, MNK_IDs.PerfectBalance, player.Level, out int pbCharges, out int pbMax);
            bool inPB = HasStatus(player, MNK_IDs.Status_PerfectBalance);

            if (op.UsePB && player.Level >= MNK_Levels.PerfectBalance && !inPB && pbCharges > 0 && pbSafe)
            {
                uint lastAction = am->Combo.Action;
                bool lastWasOpo = lastAction == MNK_IDs.Bootshine || lastAction == MNK_IDs.DragonKick || lastAction == MNK_IDs.LeapingOpo || lastAction == MNK_IDs.ArmOfTheDestroyer || lastAction == MNK_IDs.ShadowOfTheDestroyer;

                bool shouldUse = false;

                // Lógica de uso de PB según fase de combate
                if (isOpener)
                {
                    if (lastWasOpo) shouldUse = true; // En opener, usar tras Opo
                }
                else if (rofComingSoon && rofCD > 0)
                {
                    if (lastWasOpo) shouldUse = true; // Pre-pop antes de RoF (Anti-Drift)
                }
                else if (rofActive)
                {
                    if (lastWasOpo)
                    {
                        // Si BH ya se usó O está en CD -> Usar PB
                        if (HasStatus(player, MNK_IDs.Status_Brotherhood) || !CanUseRecast(am, MNK_IDs.Brotherhood, Action_Queue_Window)) shouldUse = true;
                        else if (pbCharges >= 1) shouldUse = true;
                    }
                }
                else if (pbCharges == pbMax)
                {
                    // Evitar sobrellenado de cargas (Overcap protection)
                    if (rofCD > 10.0f && !rofActive) shouldUse = true;
                }

                if (shouldUse)
                {
                    ExecuteAction(MNK_IDs.PerfectBalance, config.PerfectBalance);
                    LastPBTime = DateTime.Now;
                    return true;
                }
            }

            // ----------------------------------------------------------------------
            // 4. FORBIDDEN CHAKRA
            // ----------------------------------------------------------------------
            if (op.UseForbiddenChakra && player.Level >= MNK_Levels.ForbiddenChakra && gauge.Chakra >= 5 && CanUseRecast(am, MNK_IDs.TheForbiddenChakra, Action_Queue_Window))
            {
                ExecuteAction(MNK_IDs.TheForbiddenChakra, config.ForbiddenChakra);
                return true;
            }

            // ----------------------------------------------------------------------
            // 5. RIDDLE OF WIND
            // Se usa "On Cooldown" (apenas disponible) para maximizar usos en la pelea.
            // Independiente de RoF.
            // ----------------------------------------------------------------------
            if (op.UseRoW && player.Level >= MNK_Levels.RiddleOfWind && CanUseRecast(am, MNK_IDs.RiddleOfWind, Action_Queue_Window))
            {
                ExecuteAction(MNK_IDs.RiddleOfWind, config.RiddleOfWind);
                return true;
            }

            return false;
        }
    }
}
