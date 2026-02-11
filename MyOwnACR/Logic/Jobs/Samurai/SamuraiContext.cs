using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types; // Necesario para IBattleChara
using FFXIVClientStructs.FFXIV.Client.Game;
using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.Logic.Core;

namespace MyOwnACR.Logic.Jobs.Samurai
{
    /// <summary>
    /// Clase contenedora que captura el estado completo del Samurái en un frame específico.
    /// Sirve como fuente de verdad para todas las decisiones lógicas.
    /// </summary>
    public unsafe class SamuraiContext
    {
        // =========================================================================
        // 1. RECURSOS (GAUGES)
        // =========================================================================
        public int Kenki { get; private set; }
        public int MeditationStacks { get; private set; } // Shoha (0-3)

        // Sistema de Sen (Pegatinas)
        public bool HasSetsu { get; private set; } // Nieve (Yukikaze)
        public bool HasGetsu { get; private set; } // Luna (Gekko)
        public bool HasKa { get; private set; }    // Flor (Kasha)
        public int SenCount { get; private set; }  // Total (0-3)

        // =========================================================================
        // 2. BUFFS PERSONALES (SELF-BUFFS)
        // =========================================================================
        public float FugetsuTimeLeft { get; private set; } // Buff de Daño (13%+)
        public float FukaTimeLeft { get; private set; }    // Buff de Velocidad (13%+)

        // Meikyo Shisui
        public bool HasMeikyoShisui { get; private set; }
        public int MeikyoStacks { get; private set; }

        // Procs de Habilidades Especiales
        public bool HasOgiReady { get; private set; }      // Ogi Namikiri disponible
        public bool HasTsubameReady { get; private set; }  // Tsubame Gaeshi disponible (Legacy)
        public bool HasZanshinReady { get; private set; }  // Zanshin disponible (Nvl 96+)
        public bool HasTendoReady { get; private set; }    // Tendo Setsugekka disponible (Nvl 100)

        // Buffs de Remate (Kaeshi / Tsubame)
        // Estos buffs indican que el botón de Tsubame se ha transformado en un ataque de daño.
        public bool HasKaeshiSetsuReady { get; private set; }      // Kaeshi Setsugekka Normal (Buff 4216)
        public bool HasTendoKaeshiSetsuReady { get; private set; } // Tendo Kaeshi Setsugekka (Buff 4218)

        // =========================================================================
        // 3. ESTADO DEL OBJETIVO (TARGET DEBUFFS)
        // =========================================================================
        public float HiganbanaTimeLeft { get; private set; } // Tiempo restante del DoT en el target actual

        // =========================================================================
        // 4. COOLDOWNS (TIEMPOS DE RECARGA)
        // =========================================================================
        // Usamos float para representar segundos restantes. 0.0f significa "Listo".
        public float MeikyoCD { get; private set; }
        public float IkishotenCD { get; private set; }
        public float TsubameCD { get; private set; }
        public float SeneiCD { get; private set; } // Compartido con Guren

        // =========================================================================
        // 5. ESTADO DE COMBATE & CASTING
        // =========================================================================
        public uint LastComboAction { get; private set; } // Última acción exitosa del combo
        public float GCD { get; private set; }            // Tiempo total del GCD actual (ej: 2.14s)
        public float CombatTime { get; private set; }     // Tiempo en combate (útil para openers)

        // Estado de Casting (Importante para Slidecast y Queueing)
        public bool IsCasting { get; private set; }
        public uint CastActionId { get; private set; }
        public float CastTimeRemaining { get; private set; }

        // =========================================================================
        // MÉTODO UPDATE (ACTUALIZAR DATOS)
        // =========================================================================
        /// <summary>
        /// Actualiza todos los valores del contexto leyendo la memoria del juego.
        /// Debe llamarse al inicio de cada ciclo de decisión (Execute).
        /// </summary>
        public void Update(ActionManager* am, SAMGauge gauge, ActionScheduler scheduler, IPlayerCharacter player)
        {
            if (player == null || am == null) return;

            // --- 1. GAUGE ---
            Kenki = gauge.Kenki;
            MeditationStacks = gauge.MeditationStacks;

            HasSetsu = gauge.HasSetsu;
            HasGetsu = gauge.HasGetsu;
            HasKa = gauge.HasKa;

            SenCount = 0;
            if (HasSetsu) SenCount++;
            if (HasGetsu) SenCount++;
            if (HasKa) SenCount++;

            // --- 2. BUFFS PROPIOS ---
            FugetsuTimeLeft = Helpers.GetStatusTimeLeft(player, SAM_IDs.Status_Fugetsu);
            FukaTimeLeft = Helpers.GetStatusTimeLeft(player, SAM_IDs.Status_Fuka);

            HasMeikyoShisui = Helpers.HasStatus(player, SAM_IDs.Status_MeikyoShisui);
            MeikyoStacks = Helpers.GetStatusStacks(player, SAM_IDs.Status_MeikyoShisui);

            HasOgiReady = Helpers.HasStatus(player, SAM_IDs.Status_OgiNamikiriReady);
            HasTsubameReady = Helpers.HasStatus(player, SAM_IDs.Status_TsubameGaeshiReady_Legacy);

            // Buffs Dawntrail
            HasZanshinReady = Helpers.HasStatus(player, SAM_IDs.Status_ZanchinReady);
            HasTendoReady = Helpers.HasStatus(player, SAM_IDs.Status_TendoReady);

            // Buffs Kaeshi (ID exactos para lógica de reemplazo)
            HasKaeshiSetsuReady = Helpers.HasStatus(player, SAM_IDs.Status_KaeshiSetsuReady);
            HasTendoKaeshiSetsuReady = Helpers.HasStatus(player, SAM_IDs.Status_TendoKaeshiSetsuReady);

            // --- 3. TARGET (HIGANBANA) ---
            HiganbanaTimeLeft = 0;
            var target = player.TargetObject as IBattleChara;
            if (target != null)
            {
                // Obtenemos el tiempo restante del DoT aplicado por NOSOTROS (SourceID check)
                HiganbanaTimeLeft = Helpers.GetDebuffTimeLeft(target, SAM_IDs.Dot_Higanbana, (uint)player.GameObjectId);
            }

            // --- 4. COOLDOWNS ---
            MeikyoCD = Helpers.GetCooldown(am, SAM_IDs.MeikyoShisui);
            IkishotenCD = Helpers.GetCooldown(am, SAM_IDs.Ikishoten);
            TsubameCD = Helpers.GetCooldown(am, SAM_IDs.TsubameGaeshi);
            SeneiCD = Helpers.GetCooldown(am, SAM_IDs.HissatsuSenei);

            // --- 5. COMBATE ---
            LastComboAction = am->Combo.Action;

            // Calculamos el GCD base usando Hakaze como referencia
            GCD = am->GetRecastTime(ActionType.Action, SAM_IDs.Hakaze);

            // Tiempo de combate (estimado, útil para opener logic)
            // Si quieres precisión absoluta, necesitarías un timer externo en Plugin.cs
            CombatTime = 0; // Placeholder si no tienes un CombatTimer global

            // --- 6. CASTING STATE ---
            // Leemos del cliente si estamos casteando algo (Midare, Ogi, etc.)
            // Importante para la lógica de predicción de botones (TsubameLogic)
            if (player.IsCasting)
            {
                IsCasting = true;
                CastActionId = player.CastActionId;

                // Calculamos tiempo restante: Total - Actual
                CastTimeRemaining = player.TotalCastTime - player.CurrentCastTime;
            }
            else
            {
                IsCasting = false;
                CastActionId = 0;
                CastTimeRemaining = 0;
            }
        }
    }
}
