using static STRINGS.UI;

namespace TagnumElite.AdvancedElectrolyzer
{
    class AdvancedElectrolyzersStrings
    {
        // Special thanks to Akiart -> https://forums.kleientertainment.com/forums/topic/123339-guide-for-creating-translatable-mods/?tab=comments
        public class STRINGS
        {
            public class BUILDINGS
            {
                public class PREFABS
                {
                    public class ADVACNEDELECTROLYZER
                    {
                        public static LocString NAME = FormatAsLink("Advanced Electrolyzer", AdvancedElectrolyzerConfig.ID);
                        public static LocString DESC = "Water goes in one end. life sustaining oxygen comes out the other.";
                        public static LocString EFFECT = $"Converts {FormatAsLink("Water", "WATER")} to {FormatAsLink("Oxygen", "OXYGEN")} " +
                            $"and {FormatAsLink("Hydrogen", "HYDROGEN")}. Also converts {FormatAsLink("Polluted Water", "DIRTYWATER")} to " +
                            $"{FormatAsLink("Polluted Oxygen", "CONTAMINATEDOXYGEN")} and {FormatAsLink("Hydrogen", "HYDROGEN")}." +
                            $"Through ElectroQuarters off dangerous areas and prevents gases" +
                            $" from seeping into the colony while closed, while allowing {FormatAsLink("Light", "LIGHT")}" +
                            $" and {FormatAsLink("Decor", "DECOR")} to pass through.\n\nDuplicants passing through will open the door for a short while, letting gases and liquids to pass through.";
                    }
                    /*
                    public class HighTempElectrolyzerMachine
                    {
                        public static LocString NAME = FormatAsLink("Hight Temperature Electrolyzer", HighTempElectrolyzerConfig.ID);
                        public static LocString DESC = "Steam goes in one end. Life sustaining oxygen comes out the other.";
                        public static LocString EFFECT = $"Converts {FormatAsLink("Steam", "STEAM")} to {FormatAsLink("Oxygen", "OXYGEN")} " +
                            $"and {FormatAsLink("Hydrogen", "HYDROGEN")}";
                    }*/
                }
            }

            public class BUILDING
            {
                public class STATUSITEMS
                {
                    public class ADVANCEDELECTROLYZERINPUT
                    {
                        public static LocString NAME = "Using {ElementTypes}: {FlowRate}";

                        public static LocString TOOLTIP = "This building is using {ElementTypes} from storage at a rate of " + FormatAsNegativeRate("{FlowRate}");
                    }

                    public class ADVANCEDELECTROLYZEROUTPUT
                    {
                        public static LocString NAME = "Emitting {ElementTypes}: {FlowRate}";

                        public static LocString TOOLTIP = "This building is releasing {ElementTypes} at a rate of " + FormatAsPositiveRate("{FlowRate}");
                    }
                }
            }
        }
    }
}
