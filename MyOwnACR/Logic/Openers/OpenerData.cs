using System.Collections.Generic;

namespace MyOwnACR.Logic.Openers
{
    public enum OpenerStepType
    {
        Action,
        Potion
    }

    public class OpenerStep
    {
        public string Name { get; set; } = "";        // Nombre visual (ej: "Gekko")
        public uint ActionId { get; set; } = 0;       // [MEJORA] Usamos ID directo en vez de string KeyName
        public float PrepullSeconds { get; set; } = 0;
        public OpenerStepType Type { get; set; } = OpenerStepType.Action;
    }

    public class OpenerProfile
    {
        public string Name { get; set; } = "Unknown Opener";
        public List<OpenerStep> Steps { get; set; } = new List<OpenerStep>();
    }
}
