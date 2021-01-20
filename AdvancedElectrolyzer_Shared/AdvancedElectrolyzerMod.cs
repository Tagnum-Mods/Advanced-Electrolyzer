using Database;
using Harmony;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace TagnumElite
{
    namespace AdvancedElectrolyzer
    {
        public static class Mod_OnLoad
        {
            private static JsonSerializer serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings { Formatting = Formatting.Indented });

            public static void OnLoad()
            {
#if DEBUG
                HarmonyInstance.DEBUG = true;
#endif
                try
                {
                    System.Reflection.Assembly assem = System.Reflection.Assembly.GetExecutingAssembly();
                    string dir = assem.Location;
                    string cbdir = assem.CodeBase.Replace("file:///", "").Replace('/', '\\');

                    if (dir != cbdir) { dir = cbdir; }

                    string config_path = Path.Combine(Path.GetDirectoryName(dir), "Config.json");
                    Debug.Log("File Path: " + config_path);
                    if (File.Exists(config_path))
                    {
                        using (StreamReader streamReader = new StreamReader(config_path))
                        {
                            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                            {
                                AdvancedElectrolyzerConfig.config = serializer.Deserialize<AdvancedElectrolyzerConfig.Config>(jsonReader);
                                jsonReader.Close();
                            }
                            streamReader.Close();
                        }
                    }
                    else
                    {
                        using (StreamWriter writer = File.CreateText(config_path))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            serializer.Serialize(writer, AdvancedElectrolyzerConfig.config);
                        }
                    }
                }
                catch (NotSupportedException)
                {
                    Debug.Log(" === Unable to find code dir! ===");
                }
                catch (Exception e)
                {
                    Debug.Log(" === Unable to load config === " + e);
                }
            }
        }

        [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
        internal class AdvancedElectrolyzerMod
        {
            public static void Prefix()
            {
                AddBuilding(AdvancedElectrolyzerConfig.ID,
                name: "Advanced Electrolyzer",
                desc: "Water goes in one end. life sustaining oxygen comes out the other.",
                effect: string.Format("Converts {0} to {1} and {2}. Also converts {3} to {4} and {2}.",
                    STRINGS.UI.FormatAsLink("Water", "WATER"),
                    STRINGS.UI.FormatAsLink("Oxygen", "OXYGEN"),
                    STRINGS.UI.FormatAsLink("Hydrogen", "HYDROGEN"),
                    STRINGS.UI.FormatAsLink("Polluted Water", "DIRTYWATER"),
                    STRINGS.UI.FormatAsLink("Polluted Oxygen", "CONTAMINATEDOXYGEN")));

                string status_prefix = "STRINGS.BUILDING.STATUSITEMS.{0}.{1}";
                Strings.Add(string.Format(status_prefix, "ADVANCEDELECTROLYZERINPUT", "NAME"), "Using Water: {FlowRate}");
                Strings.Add(string.Format(status_prefix, "ADVANCEDELECTROLYZERINPUT", "TOOLTIP"), "This building is using Water from storage at a rate of " + STRINGS.UI.FormatAsNegativeRate("{FlowRate}"));
                Strings.Add(string.Format(status_prefix, "ADVANCEDELECTROLYZEROUTPUT", "NAME"), "Producing {ElementType}: {FlowRate}");
                Strings.Add(string.Format(status_prefix, "ADVANCEDELECTROLYZEROUTPUT", "TOOLTIP"), "This building is producing {ElementType} at a rate of " + STRINGS.UI.FormatAsPositiveRate("{FlowRate}"));
            }

            private static void AddBuilding(string id, string name, string desc, string effect)
            {
                string prefix = "STRINGS.BUILDINGS.PREFABS." + id.ToUpper();
                Strings.Add(prefix + ".NAME", name);
                Strings.Add(prefix + ".DESC", desc);
                Strings.Add(prefix + ".EFFECT", effect);
                ModUtil.AddBuildingToPlanScreen("Oxygen", id);
            }
        }
        [HarmonyPatch(typeof(Db), "Initialize")]
        public static class InitAdvacnedElectrolyzerMod
        {
            public static void Postfix()
            {
                AddBuildingToTechnology("ImprovedOxygen", AdvancedElectrolyzerConfig.ID);
            }

            /* Shamelessly stolen from SanchozzDeponianin: https://github.com/SanchozzDeponianin/ONIMods/blob/134293b28abf0ec96cf307eb0f8f372eff4e9940/src/lib/Utils.cs#L137-L165 */
            public static void AddBuildingToTechnology(string tech, string buildingId)
            {
                var tech_grouping = Traverse.Create(typeof(Techs))?.Field("TECH_GROUPING")?.GetValue<Dictionary<string, string[]>>();
                if (tech_grouping != null)
                {
                    if (tech_grouping.ContainsKey(tech))
                    {
                        List<string> techList = new List<string>(tech_grouping[tech]) { buildingId };
                        tech_grouping[tech] = techList.ToArray();
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find '{tech}' tech in TECH_GROUPING.");
                    }
                }
                else
                {
                    var targetTech = Db.Get().Techs.TryGet(tech);
                    if (targetTech != null)
                    {
                        Traverse.Create(targetTech)?.Field("unlockedItemIDs")?.GetValue<List<string>>()?.Add(buildingId);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find '{tech}' tech.");
                    }
                }
            }
        }
    }
}
