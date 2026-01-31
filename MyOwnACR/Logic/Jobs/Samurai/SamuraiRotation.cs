// Archivo: Logic/Jobs/Samurai/SamuraiRotation.cs
// Descripción: Lógica de Samurai adaptada a la nueva arquitectura (Scheduler Central).
// VERSION: v2.0 - Scheduler Integration.

using System;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData;
using MyOwnACR.JobConfigs;
using MyOwnACR.Logic.Core;
using MyOwnACR.Logic.Interfaces;

using InventoryManager = MyOwnACR.Logic.Core.InventoryManager;

#pragma warning disable IDE1006 

namespace MyOwnACR.Logic.Jobs.Samurai
{
    public unsafe class SamuraiRotation : IJobLogic
    {
        // =========================================================================
        // SINGLETON & ESTADO
        // =========================================================================
        public static SamuraiRotation Instance { get; } = new SamuraiRotation();
        private SamuraiRotation() { }

        public uint JobId => JobDefinitions.SAM;
        public uint LastProposedAction { get; private set; } = 0;

        // RUTAS DE COMBO
        private readonly uint[] _rutaKasha = { SAM_IDs.Hakaze, SAM_IDs.Shifu, SAM_IDs.Kasha };
        private readonly uint[] _rutaGekko = { SAM_IDs.Hakaze, SAM_IDs.Jinpu, SAM_IDs.Gekko };
        private readonly uint[] _rutaYukikaze = { SAM_IDs.Hakaze, SAM_IDs.Yukikaze };

        // ESTADO INTERNO
        private uint[] _comboActivo = { SAM_IDs.Hakaze, SAM_IDs.Shifu, SAM_IDs.Kasha }; // Inicialización segura
        private int _step = 0;
        private bool _esperandoConfirmacion = false;

        // DEPENDENCIAS
        private SAMGauge? _gauge;
        private DateTime _lastDebugPrint = DateTime.MinValue;

        // INYECCIÓN MANUAL
        private uint _queuedAction = 0;
        private DateTime _queueExpire = DateTime.MinValue;

        // =========================================================================
        // INTERFAZ IJobLogic
        // =========================================================================

        public void QueueManualAction(uint actionId)
        {
            _queuedAction = actionId;
            _queueExpire = DateTime.Now.AddSeconds(2.0);
            Plugin.Instance.SendLog($"[SAM] Inyección manual: {actionId}");
        }

        public void QueueManualAction(string actionName) { } // Legacy
        public string GetQueuedAction() => _queuedAction != 0 ? $"INJECT: {_queuedAction}" : "AUTO";
        public void PrintDebugInfo(IChatGui chat) { }

        // =========================================================================
        // EXECUTE (Loop Principal)
        // =========================================================================
        public void Execute(ActionScheduler scheduler, ActionManager* am, IPlayerCharacter player,
                            IObjectTable objectTable, Configuration config)
        {
            if (am == null || player == null) return;

            _gauge = Plugin.JobGauges.Get<SAMGauge>();

            // 1. INYECCIÓN MANUAL
            if (_queuedAction != 0)
            {
                if (DateTime.Now > _queueExpire) _queuedAction = 0;
                else
                {
                    // Prioridad Forzada para que salga inmediatamente
                    scheduler.InjectOgcd(_queuedAction, WeavePriority.Forced);
                    LastProposedAction = _queuedAction;
                    _queuedAction = 0;
                    return;
                }
            }

            // 2. LÓGICA AUTOMÁTICA
            // Calculamos qué queremos hacer y se lo decimos al scheduler
            UpdateLogic(am, scheduler, _gauge, player);
        }

        // =========================================================================
        // LÓGICA DE DECISIÓN
        // =========================================================================
        private void UpdateLogic(ActionManager* am, ActionScheduler scheduler, SAMGauge? gauge, IPlayerCharacter player)
        {
            // Referencia al GCD base (Hakaze) para ver tiempos
            float total = am->GetRecastTime(ActionType.Action, SAM_IDs.Hakaze);
            bool gcdActivo = total > 0;
            uint lvl = player.Level;
            uint nextGcd = 0;

            // --- MÁQUINA DE ESTADOS ---

            if (!gcdActivo)
            {
                // CASO A: LISTO PARA PEGAR (GCD Disponible)
                if (_esperandoConfirmacion)
                {
                    _esperandoConfirmacion = false;
                }

                // -------------------------------------------------------------
                // 1. PRIORIDAD: TSUBAME-GAESHI
                // -------------------------------------------------------------
                // Si tenemos el buff "Tsubame Ready" (Setsugekka anterior), lo copiamos.
                if (HasStatus(player, SAM_IDs.Status_TsubameReady))
                {
                    nextGcd = SAM_IDs.TsubameGaeshi;
                    _step = 0; // Reiniciar combo tras un finisher
                }

                // -------------------------------------------------------------
                // 2. PRIORIDAD: IAIJUTSU (Midare Setsugekka)
                // -------------------------------------------------------------
                // Si tenemos los 3 sellos (Setsu, Getsu, Ka).
                else if (nextGcd == 0 && gauge != null &&
                         gauge.HasSetsu && gauge.HasGetsu && gauge.HasKa && lvl >= SAM_Levels.Iaijutsu)
                {
                    nextGcd = SAM_IDs.Iaijutsu;
                }

                // -------------------------------------------------------------
                // 3. PRIORIDAD: COMBO NORMAL
                // -------------------------------------------------------------
                if (nextGcd == 0)
                {
                    // Si estamos al principio, decidir qué ruta tomar según buffs/sellos faltantes
                    if (_step == 0) DecidirRuta(gauge, lvl);

                    // Protección de rango del array
                    if (_step >= _comboActivo.Length) _step = 0;

                    nextGcd = _comboActivo[_step];
                }
            }
            else
            {
                // CASO B: RECARGANDO (GCD en Cooldown)
                if (!_esperandoConfirmacion)
                {
                    _esperandoConfirmacion = true;

                    // Avanzamos el paso del combo SOLO si lo que acabamos de tirar NO fue especial.
                    // Tsubame y Iaijutsu no avanzan el combo básico (1-2-3).
                    bool esEspecial = HasStatus(player, SAM_IDs.Status_TsubameReady) ||
                                      (gauge != null && gauge.HasSetsu && gauge.HasGetsu && gauge.HasKa);

                    if (!esEspecial)
                    {
                        AdvanceStep();
                    }
                }
                nextGcd = 0;
            }

            // Actualizamos propiedad para UI
            if (nextGcd != 0) LastProposedAction = nextGcd;

            // --- DEBUG EN CHAT (Opcional) ---
            /*
            if ((DateTime.Now - _lastDebugPrint).TotalSeconds > 1.5)
            {
                // PrintDebugInfo(gcdActivo, nextGcd, player);
                _lastDebugPrint = DateTime.Now;
            }
            */

            // --- ENVÍO AL SCHEDULER ---
            // Le decimos al motor central: "Este es mi GCD deseado".
            // Por ahora, no enviamos oGCDs automáticos (Kenki) en esta versión simple,
            // pero podrías agregarlos aquí pasando 'ogcdPlan' en lugar de null.
            scheduler.SetNextCycle(nextGcd, null, null);
        }

        // =========================================================================
        // HELPERS & UTILS
        // =========================================================================

        private void DecidirRuta(SAMGauge? gauge, uint lvl)
        {
            if (gauge == null) return;

            // Lógica simple de prioridad de Sellos: Getsu (Luna) -> Ka (Flor) -> Setsu (Nieve)
            // También deberíamos chequear los buffs (Fugetsu/Fuka), pero priorizar sellos suele cubrirlos.

            if (!gauge.HasGetsu && lvl >= SAM_Levels.Jinpu) _comboActivo = _rutaGekko;
            else if (!gauge.HasKa && lvl >= SAM_Levels.Shifu) _comboActivo = _rutaKasha;
            else if (!gauge.HasSetsu && lvl >= SAM_Levels.Yukikaze) _comboActivo = _rutaYukikaze;
            else
            {
                // Si tenemos todo (o nada desbloqueado), fallback a Kasha (Daño/Gauge)
                if (lvl >= SAM_Levels.Jinpu) _comboActivo = _rutaGekko;
                else _comboActivo = _rutaKasha;
            }
        }

        private void AdvanceStep()
        {
            _step++;
            if (_step >= _comboActivo.Length) _step = 0;
        }

        private bool HasStatus(IPlayerCharacter player, ushort statusId)
        {
            if (player.StatusList == null) return false;
            foreach (var status in player.StatusList)
            {
                if (status.StatusId == statusId) return true;
            }
            return false;
        }

        private bool IsSelfBuff(uint actionId)
        {
            return actionId == SAM_IDs.MeikyoShisui || actionId == SAM_IDs.Ikishoten ||
                   actionId == SAM_IDs.ThirdEye || actionId == SAM_IDs.Tengentsu ||
                   actionId == SAM_IDs.Meditate;
        }

        private string GetActionName(uint id)
        {
            if (id == 0) return "-";
            if (id == SAM_IDs.Hakaze) return "Hakaze";
            if (id == SAM_IDs.Jinpu) return "Jinpu";
            if (id == SAM_IDs.Gekko) return "Gekko";
            if (id == SAM_IDs.Shifu) return "Shifu";
            if (id == SAM_IDs.Kasha) return "Kasha";
            if (id == SAM_IDs.Yukikaze) return "Yukikaze";
            if (id == SAM_IDs.Iaijutsu) return "Midare";
            if (id == SAM_IDs.TsubameGaeshi) return "Tsubame";
            return id.ToString();
        }
    }
}
