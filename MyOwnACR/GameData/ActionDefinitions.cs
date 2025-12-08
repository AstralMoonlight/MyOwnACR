// Archivo: GameData/ActionDefinitions.cs
// Descripción: Definiciones base para el sistema de metadatos de habilidades.
// CAMBIO: Renombrado ActionType -> ActionCooldownType para evitar conflictos.

using System.Collections.Generic;

namespace MyOwnACR.GameData
{
    /// <summary>
    /// Clasificación del tipo de cooldown de la habilidad.
    /// </summary>
    public enum ActionCooldownType
    {
        GCD,    // Habilidad afectada por el Global Cooldown (Recast ~2.5s base)
        oGCD    // Habilidad fuera del GCD (Ability, Instant)
    }

    /// <summary>
    /// Metadatos de una habilidad.
    /// </summary>
    public class ActionInfo
    {
        public uint Id { get; }
        public string Name { get; }
        public ActionCooldownType CooldownType { get; }
        public float Cooldown { get; } // Recast base en segundos (ej. 60s, 120s)

        public ActionInfo(uint id, string name, ActionCooldownType type, float cooldown = 0f)
        {
            Id = id;
            Name = name;
            CooldownType = type;
            Cooldown = cooldown;
        }

        public bool IsGCD => CooldownType == ActionCooldownType.GCD;
        public bool IsOGCD => CooldownType == ActionCooldownType.oGCD;
    }

    /// <summary>
    /// Repositorio central para buscar metadatos de acciones.
    /// </summary>
    public static class ActionLibrary
    {
        private static readonly Dictionary<uint, ActionInfo> _actions = new();

        public static void Register(ActionInfo action)
        {
            if (!_actions.ContainsKey(action.Id))
            {
                _actions[action.Id] = action;
            }
        }

        public static ActionInfo? Get(uint id)
        {
            return _actions.TryGetValue(id, out var info) ? info : null;
        }

        public static bool IsGCD(uint id)
        {
            var info = Get(id);
            return info != null && info.IsGCD;
        }
    }
}
