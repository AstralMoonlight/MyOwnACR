using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaTerritory = Lumina.Excel.Sheets.TerritoryType;

namespace MyOwnACR.Test
{
    public class EnemyLogger
    {
        // Este diccionario es el verdadero "Filtro Personal":
        // Guarda qué está haciendo CADA enemigo individualmente.
        // Si el enemigo [A] ya está casteando el hechizo [123], no lo volvemos a escribir.
        private Dictionary<ulong, uint> activeCasts = new Dictionary<ulong, uint>();

        private string? logPath = null;
        private uint currentTerritoryId = 0;

        // Variables de Tiempo y Wipe
        private DateTime? pullStartTime = null;
        private bool wasInCombat = false;
        private int framesWithoutEnemies = 0;

        public EnemyLogger() { }

        public void AnalyzeCombatArea(IObjectTable objectTable, IDataManager dataManager, IClientState clientState)
        {
            // 1. CAMBIO DE INSTANCIA
            uint actualTerritory = clientState.TerritoryType;
            if (currentTerritoryId != actualTerritory && actualTerritory != 0)
            {
                currentTerritoryId = actualTerritory;
                logPath = null;
                pullStartTime = null;
                wasInCombat = false;
                activeCasts.Clear();
            }

            if (logPath == null) CreateLogFile(dataManager, clientState);

            // 2. ESCANEO Y DETECCIÓN
            var localPlayer = objectTable.Length > 0 ? objectTable[0] as IPlayerCharacter : null;
            bool playerFlag = localPlayer?.StatusFlags.HasFlag(StatusFlags.InCombat) ?? false;
            bool anyEnemyFighting = false;

            foreach (var obj in objectTable)
            {
                if (obj is not IBattleChara character) continue;
                if (obj.ObjectKind == ObjectKind.Player) continue;

                // Verificamos combate para la lógica de Wipe
                if (character.StatusFlags.HasFlag(StatusFlags.InCombat)) anyEnemyFighting = true;

                // --- LÓGICA DE FILTRADO ---
                if (character.IsCasting)
                {
                    uint spellId = character.CastActionId;
                    ulong objectId = character.GameObjectId; // El DNI único de este mob

                    // ¿Este mob específico (objectId) ya estaba casteando este hechizo?
                    // SI YA ESTABA: No hacemos nada (Filtro activado).
                    // SI ES NUEVO O DIFERENTE: Entramos al if.
                    if (!activeCasts.ContainsKey(objectId) || activeCasts[objectId] != spellId)
                    {
                        // Registramos el casteo
                        activeCasts[objectId] = spellId;
                        LogCast(character, spellId, dataManager);
                    }
                }
                else
                {
                    // Si deja de castear, lo borramos de la lista para que pueda volver a lanzar otro skill
                    if (activeCasts.ContainsKey(character.GameObjectId))
                    {
                        activeCasts.Remove(character.GameObjectId);
                    }
                }
            }

            // 3. LÓGICA DE WIPE / TIEMPO (Igual que antes)
            bool effectiveCombat = false;
            if (playerFlag)
            {
                if (anyEnemyFighting)
                {
                    effectiveCombat = true;
                    framesWithoutEnemies = 0;
                }
                else
                {
                    framesWithoutEnemies++;
                    if (framesWithoutEnemies < 60) effectiveCombat = true;
                    else effectiveCombat = false;
                }
            }
            else
            {
                effectiveCombat = false;
                framesWithoutEnemies = 0;
            }

            if (effectiveCombat && !wasInCombat)
            {
                pullStartTime = DateTime.Now;
                LogToFile("-----------------------------------------");
                LogToFile("=== INICIO DE COMBATE (00:00) ===");
            }
            else if (!effectiveCombat && wasInCombat)
            {
                pullStartTime = null;
                LogToFile("=== FIN DE COMBATE / WIPE ===");
                LogToFile("-----------------------------------------");
                activeCasts.Clear(); // Limpiamos la memoria de casteos al wipear
            }

            wasInCombat = effectiveCombat;
        }

        private void LogCast(IBattleChara entity, uint spellId, IDataManager dataManager)
        {
            float totalTime = entity.TotalCastTime;
            string spellName = "Desconocido";

            try
            {
                var actionSheet = dataManager.GetExcelSheet<LuminaAction>();
                if (actionSheet.HasRow(spellId))
                {
                    var row = actionSheet.GetRow(spellId);
                    spellName = row.Name.ToString();
                }
            }
            catch { }

            string timeString;
            if (pullStartTime.HasValue)
            {
                TimeSpan diff = DateTime.Now - pullStartTime.Value;
                timeString = diff.ToString(@"mm\:ss\.f");
            }
            else
            {
                timeString = "PRE-PULL";
            }

            string uniqueName = $"{entity.Name} [{entity.GameObjectId:X}]";

            string logLine = $"[{timeString}] " +
                             $"[Enemy: {uniqueName}] " +
                             $"ID: {spellId} | " +
                             $"Action: {spellName} | " +
                             $"CastTime: {totalTime:F2}s";

            LogToFile(logLine);
        }

        private void CreateLogFile(IDataManager dataManager, IClientState clientState)
        {
            string instanceName = "Unknown_Zone";
            try
            {
                var territorySheet = dataManager.GetExcelSheet<LuminaTerritory>();
                if (territorySheet.HasRow(currentTerritoryId))
                {
                    var territory = territorySheet.GetRow(currentTerritoryId);
                    instanceName = territory.PlaceName.Value.Name.ToString();
                }
            }
            catch { }

            instanceName = instanceName.Replace(" ", "_");
            foreach (char c in Path.GetInvalidFileNameChars()) instanceName = instanceName.Replace(c, '-');

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ACR_Logs");
            Directory.CreateDirectory(folder);

            string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{instanceName}.txt";
            logPath = Path.Combine(folder, fileName);

            LogToFile($"=== LOG CREADO: {instanceName} (ID: {currentTerritoryId}) ===");
        }

        private void LogToFile(string line)
        {
            if (logPath == null) return;
            try { File.AppendAllText(logPath, line + Environment.NewLine); } catch { }
        }
    }
}
