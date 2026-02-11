using System.Collections.Generic;

namespace MyOwnACR.Models
{
    /// <summary>
    /// Representa un paso individual dentro de una secuencia de apertura.
    /// </summary>
    public class OpenerStep
    {
        // Nombre descriptivo (ej: "Dragon Kick")
        public string Name { get; set; } = string.Empty;

        // Nombre clave para buscar el ID en ActionLibrary o Config (ej: "DragonKick")
        public string KeyName { get; set; } = string.Empty;

        // ID numérico de la acción (se resuelve al cargar el perfil)
        public uint ActionId { get; set; }

        // Tipo de paso: "Action", "Potion", "Sprint", "Wait"
        public string Type { get; set; } = "Action";

        // Tiempo relativo al inicio (0 = Start, -2 = 2s antes del pull)
        public int Prepull { get; set; } = 0;

        // [OPCIONAL] Ayuda al RotationManager a saber si debe esperar GCD o weavear
        public bool IsGCD { get; set; } = false;
    }

    /// <summary>
    /// Contenedor de la secuencia completa (El archivo JSON se deserializa en esto).
    /// </summary>
    public class OpenerProfile
    {
        // Nombre del perfil (ej: "SAM - Standard Level 100")
        public string Name { get; set; } = string.Empty;

        // Job al que pertenece (ej: "SAM", "MNK", "BRD")
        public string Job { get; set; } = "";

        // Lista ordenada de pasos a ejecutar
        public List<OpenerStep> Steps { get; set; } = new();
    }
}
