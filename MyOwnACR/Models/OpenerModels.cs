using System.Collections.Generic;
using MyOwnACR.JobConfigs; // Asumo que KeyBind está aquí o en Root

namespace MyOwnACR.Models // <--- CAMBIO AQUÍ
{
    public class OpenerStep
    {
        public string Name { get; set; } = string.Empty;
        public string KeyName { get; set; } = string.Empty; // Nombre de la variable en Config (ej: "DragonKick")
        public uint ActionId { get; set; }
        public string Type { get; set; } = "Action"; // "Action", "Potion", "Sprint"
    }

    public class OpenerProfile
    {
        public string Name { get; set; } = string.Empty;
        public List<OpenerStep> Steps { get; set; } = new();
    }
}
