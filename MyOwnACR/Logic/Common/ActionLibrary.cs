// Archivo: Logic/Common/ActionLibrary.cs
// Descripción: Repositorio Híbrido. 
// CORRECCIÓN 1: Restaurado el método 'Register(ActionInfo)' para compatibilidad con BRD_ActionData.
// CORRECCIÓN 2: Resuelta ambigüedad entre System.Action y Lumina.Excel.Sheets.Action.

using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace MyOwnACR.Logic.Common
{
    public enum ActionCooldownType { GCD, oGCD, Item }

    public class ActionInfo
    {
        public uint Id { get; }
        public string Name { get; }
        public ActionCooldownType CooldownType { get; }
        public float Cooldown { get; }
        public int MaxCharges { get; }

        public ActionInfo(uint id, string name, ActionCooldownType type, float cooldown = 0f, int maxCharges = 0)
        {
            Id = id;
            Name = name;
            CooldownType = type;
            Cooldown = cooldown;
            MaxCharges = maxCharges;
        }

        public bool IsGCD => CooldownType == ActionCooldownType.GCD;
        public bool IsOGCD => CooldownType == ActionCooldownType.oGCD;
    }

    public static class ActionLibrary
    {
        // 1. MEMORIA MANUAL
        private static readonly Dictionary<uint, ActionInfo> ManualActions = new();
        private static readonly Dictionary<string, uint> NameToId = new(StringComparer.OrdinalIgnoreCase);

        // 2. DATA MANAGER
        private static IDataManager? _dataManager;

        public static void Initialize(IDataManager dm)
        {
            _dataManager = dm;
        }

        // --- MÉTODOS DE REGISTRO (Compatibilidad Total) ---

        // Método 1: Para cuando pasas el objeto ActionInfo entero (usado en BRD_ActionData)
        public static void Register(ActionInfo action)
        {
            if (!ManualActions.ContainsKey(action.Id))
            {
                ManualActions[action.Id] = action;
                if (!NameToId.ContainsKey(action.Name))
                {
                    NameToId[action.Name] = action.Id;
                }
            }
        }

        // Método 2: Helper rápido (usado en nuevos archivos)
        public static void RegisterAction(uint id, string name, ActionCooldownType type, float cd = 0f, int maxCharges = 0)
        {
            Register(new ActionInfo(id, name, type, cd, maxCharges));
        }

        // --- CONSULTAS ---

        public static ActionInfo? Get(uint id) => ManualActions.TryGetValue(id, out var info) ? info : null;

        public static uint GetIdByName(string name) => NameToId.TryGetValue(name, out var id) ? id : 0;

        public static uint GetActionId(string name) => GetIdByName(name);

        // --- OBTENCIÓN DE NOMBRES ---
        public static string GetName(uint id)
        {
            if (id == 0) return "None";

            // 1. Manual
            if (ManualActions.TryGetValue(id, out var info)) return info.Name;

            // 2. DataManager
            if (_dataManager != null)
            {
                // Ítems (Pociones)
                if (id > 500000)
                {
                    uint lookupId = (id > 1000000) ? id - 1000000 : id;
                    try
                    {
                        var itemRow = _dataManager.GetExcelSheet<Item>()?.GetRow(lookupId);
                        if (itemRow.HasValue)
                            return $"{itemRow.Value.Name}{(id > 1000000 ? " (HQ)" : "")}";
                    }
                    catch { return $"Item_{id}"; }
                }
                // Acciones
                else
                {
                    try
                    {
                        // FIX AMBIGÜEDAD: Usamos el nombre completo del tipo (Lumina.Excel.Sheets.Action)
                        var actionRow = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(id);
                        if (actionRow.HasValue) return actionRow.Value.Name.ToString();
                    }
                    catch { /* Fallback */ }
                }
            }

            return $"ID_{id}";
        }

        public static bool IsGCD(uint id)
        {
            // 1. Manual
            if (ManualActions.TryGetValue(id, out var info)) return info.IsGCD;

            // 2. DataManager
            if (id > 500000) return false;

            if (_dataManager != null)
            {
                try
                {
                    // FIX AMBIGÜEDAD:
                    var row = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(id);
                    if (row.HasValue)
                    {
                        // 2=Spell, 3=Weaponskill
                        return row.Value.ActionCategory.RowId == 2 || row.Value.ActionCategory.RowId == 3;
                    }
                }
                catch { }
            }
            return false;
        }

        public static int GetMaxCharges(uint id)
        {
            if (ManualActions.TryGetValue(id, out var info)) return info.MaxCharges;

            // Si quisieras leer cargas desde Excel:
            // var row = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRow(id);
            // return row.Value.MaxCharges;

            return 0;
        }
    }
}
