using System.Collections.Generic;
using MyOwnACR.GameData.Jobs.Samurai;
using MyOwnACR.GameData.Common;

namespace MyOwnACR.Logic.Openers.Jobs
{
    public static class SAM_Openers
    {
        public static OpenerProfile Standard_Lv100 => new OpenerProfile
        {
            Name = "SAM Standard Lv100",
            Steps = new List<OpenerStep>
            {
                // Pre-Pull (-14s Meikyo, -5s True North)
                new() { Name = "Meikyo Shisui", ActionId = SAM_IDs.MeikyoShisui, PrepullSeconds = 14f },
                new() { Name = "True North",    ActionId = SAM_IDs.TrueNorth,    PrepullSeconds = 5f },
                
                // Pull
                new() { Name = "Gekko",         ActionId = SAM_IDs.Gekko },
                
                // [CAMBIO] ActionId = 0 para usar la poción configurada en el Dashboard
                new() { Name = "Potion",        ActionId = 0,                    Type = OpenerStepType.Potion },

                new() { Name = "Kasha",         ActionId = SAM_IDs.Kasha },
                new() { Name = "Ikishoten",     ActionId = SAM_IDs.Ikishoten },

                new() { Name = "Yukikaze",      ActionId = SAM_IDs.Yukikaze },

                new() { Name = "Tendo Setsugekka",        ActionId = SAM_IDs.TendoSetsugekka },
                new() { Name = "Hissatsu Senei",          ActionId = SAM_IDs.HissatsuSenei },

                new() { Name = "Tendo Kaeshi Setsugekka", ActionId = SAM_IDs.TendoKaeshiSetsugekka },
                new() { Name = "Meikyo Shisui",           ActionId = SAM_IDs.MeikyoShisui },

                new() { Name = "Gekko",                   ActionId = SAM_IDs.Gekko },
                new() { Name = "Zanshin",                 ActionId = SAM_IDs.Zanshin },

                new() { Name = "Higanbana",               ActionId = SAM_IDs.Higanbana },

                new() { Name = "Ogi Namikiri",            ActionId = SAM_IDs.OgiNamikiri },
                new() { Name = "Shoha",                   ActionId = SAM_IDs.Shoha },

                new() { Name = "Kaeshi Namikiri",         ActionId = SAM_IDs.KaeshiNamikiri },

                new() { Name = "Kasha",             ActionId = SAM_IDs.Kasha },
                new() { Name = "Hissatsu Shinten",  ActionId = SAM_IDs.HissatsuShinten },

                new() { Name = "Gekko",             ActionId = SAM_IDs.Gekko },
                new() { Name = "Hissatsu Gyoten",   ActionId = SAM_IDs.HissatsuGyoten },

                new() { Name = "Gyofu",             ActionId = SAM_IDs.Gyofu },

                new() { Name = "Yukikaze",          ActionId = SAM_IDs.Yukikaze },
                new() { Name = "Hissatsu Shinten",  ActionId = SAM_IDs.HissatsuShinten },

                new() { Name = "Tendo Setsugekka",        ActionId = SAM_IDs.TendoSetsugekka },
                new() { Name = "Tendo Kaeshi Setsugekka", ActionId = SAM_IDs.TendoKaeshiSetsugekka }
            }
        };
    }
}
