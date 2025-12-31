using System.Collections.Generic;

namespace MyOwnACR.Openers
{
    public class OpenerStep
    {
        public string Name { get; set; } = "";
        public string KeyName { get; set; } = "";
        public uint ActionId { get; set; }
        public string Type { get; set; } = "Action";
    }

    public class OpenerProfile
    {
        public string Name { get; set; } = "";
        public List<OpenerStep> Steps { get; set; } = new List<OpenerStep>();
    }
}
