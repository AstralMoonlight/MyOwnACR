// Archivo: Logic/Jobs/Bard/BardContext.cs
// VERSIÓN: V22.1 - COMBAT TIMER & HELPER INTEGRATION
// Descripción: Contexto con cálculo de tiempo de combate y corrección de CDs.

using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;
using MyOwnACR.Logic.Core;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using MyOwnACR.GameData.Jobs.Bard;

namespace MyOwnACR.Logic.Jobs.Bard
{
    public unsafe class BardContext
    {
        // --- GAUGE ---
        public Song CurrentSong;
        public float SongTimerMS;
        public int Repertoire;
        public int SoulVoice;
        public byte CodaCount;

        // --- COOLDOWNS ---
        public float GCD_Total;
        public float GCD_Elapsed;
        public float GCD_Remaining => Math.Max(0, GCD_Total - GCD_Elapsed);

        public int BloodletterCharges;
        public float BloodletterCD;
        public float EmpyrealCD;
        public float MinuetCD;
        public float SidewinderCD;

        // Buffs Cooldowns
        public float RagingStrikesCD;
        public float BattleVoiceCD;
        public float RadiantFinaleCD;
        public float BarrageCD;

        // --- ESTADO LÓGICO (BUFFS REALES) ---
        public bool IsRagingStrikesActive;
        public float RagingStrikesTimeLeft;

        public bool IsRadiantFinaleActive;
        public float RadiantFinaleTimeLeft;

        public bool IsBattleVoiceActive;
        public float RadiantEncoreTimeLeft;

        // --- ESTADO DE COMBATE (NUEVO) ---
        public float CombatTime { get; private set; }
        private DateTime combatStart = DateTime.MinValue;

        public void Update(ActionManager* am, BRDGauge gauge, ActionScheduler scheduler, IPlayerCharacter player)
        {
            if (am == null || player == null) return;

            // 0. CÁLCULO DE TIEMPO DE COMBATE
            // Usamos el helper para saber si estamos en combate real (targeteando hostil)
            bool inCombat = Helpers.IsRealCombat(player);

            if (inCombat)
            {
                // Si acabamos de entrar en combate, guardamos la hora
                if (combatStart == DateTime.MinValue)
                {
                    combatStart = DateTime.Now;
                }
                // Calculamos diferencia en segundos
                CombatTime = (float)(DateTime.Now - combatStart).TotalSeconds;
            }
            else
            {
                // Si no estamos en combate, reseteamos
                combatStart = DateTime.MinValue;
                CombatTime = 0f;
            }

            // 1. GAUGE
            CurrentSong = gauge.Song;
            SongTimerMS = gauge.SongTimer;
            Repertoire = gauge.Repertoire;
            SoulVoice = gauge.SoulVoice;

            // Coda
            CodaCount = 0;
            if (gauge.Coda[0] != Song.None) CodaCount++;
            if (gauge.Coda[1] != Song.None) CodaCount++;
            if (gauge.Coda[2] != Song.None) CodaCount++;

            // 2. GCD
            GCD_Total = am->GetRecastTime(ActionType.Action, BRD_IDs.BurstShot);
            GCD_Elapsed = am->GetRecastTimeElapsed(ActionType.Action, BRD_IDs.BurstShot);

            // 3. RECURSOS
            BloodletterCharges = (int)am->GetCurrentCharges(BRD_IDs.Bloodletter);

            // 4. COOLDOWNS (Usando el método corregido)
            BloodletterCD = GetCooldownRemaining(am, BRD_IDs.Bloodletter);
            EmpyrealCD = GetCooldownRemaining(am, BRD_IDs.EmpyrealArrow);
            MinuetCD = GetCooldownRemaining(am, BRD_IDs.WanderersMinuet);
            SidewinderCD = GetCooldownRemaining(am, BRD_IDs.Sidewinder);

            RagingStrikesCD = GetCooldownRemaining(am, BRD_IDs.RagingStrikes);
            BattleVoiceCD = GetCooldownRemaining(am, BRD_IDs.BattleVoice);
            RadiantFinaleCD = GetCooldownRemaining(am, BRD_IDs.RadiantFinale);
            BarrageCD = GetCooldownRemaining(am, BRD_IDs.Barrage);

            // 5. BUFFS
            UpdateBuffs(player);
        }

        private void UpdateBuffs(IPlayerCharacter player)
        {
            IsRagingStrikesActive = false;
            RagingStrikesTimeLeft = 0;
            IsRadiantFinaleActive = false;
            RadiantFinaleTimeLeft = 0;
            IsBattleVoiceActive = false;
            RadiantEncoreTimeLeft = 0;

            foreach (var status in player.StatusList)
            {
                if (status.StatusId == BRD_IDs.Status_RagingStrikes)
                {
                    IsRagingStrikesActive = true;
                    RagingStrikesTimeLeft = status.RemainingTime;
                }
                else if (status.StatusId == BRD_IDs.Status_RadiantFinale)
                {
                    IsRadiantFinaleActive = true;
                    RadiantFinaleTimeLeft = status.RemainingTime;
                }
                else if (status.StatusId == BRD_IDs.Status_BattleVoice)
                {
                    IsBattleVoiceActive = true;
                }
                else if (status.StatusId == BRD_IDs.Status_RadiantEncoreReady)
                {
                    RadiantEncoreTimeLeft = status.RemainingTime;
                }
            }
        }

        // =========================================================================
        // EL FIX MAESTRO (Sin cambios, funciona perfecto)
        // =========================================================================
        private float GetCooldownRemaining(ActionManager* am, uint actionId)
        {
            // 1. PREGUNTA DIRECTA AL JUEGO (Vía Helper)
            if (Helpers.CanUse(am, actionId)) return 0;

            // 2. CÁLCULO DE RESPALDO
            float elapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
            float total = am->GetRecastTime(ActionType.Action, actionId);

            if (elapsed == 0) return total;

            return Math.Max(0, total - elapsed);
        }
    }
}
