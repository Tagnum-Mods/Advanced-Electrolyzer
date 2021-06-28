using Database;
using Harmony;
using KMod;
using Newtonsoft.Json;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static Localization;

namespace TagnumElite
{
    namespace AdvancedElectrolyzer
    {
        public static class AdvancedElectrolyzersMod
        {
            //private static JsonSerializer serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings { Formatting = Formatting.Indented });

            public static string mod_loc;

            public static Config config = new Config();

            [JsonObject(MemberSerialization.OptIn)]
            public class Config
            {
                [JsonProperty]
                public const string version = "2.0.0";
                [Option("STRINGS.ADVANCEDELECTROLYZERS.OPTIONS.ADV_ELECTROLYZER")]
                [JsonProperty]
                public AdvancedElectrolyzerConfig.AEConfig advancedElectrolyzer = new AdvancedElectrolyzerConfig.AEConfig();
            }

            public static void OnLoad()
            {
#if DEBUG
                HarmonyInstance.DEBUG = true;
#endif
                PUtil.InitLibrary(false);
                PeterHan.PLib.Options.POptions.RegisterOptions(typeof(Config));
                Config f_config = PeterHan.PLib.Options.POptions.ReadSettings<Config>();
                if (f_config == null)
                {
                    PeterHan.PLib.Options.POptions.WriteSettings(config);
                }
                else {
                    config = f_config;
                }
                /* OLD CODE, we are now using PLib
                try
                {
                    System.Reflection.Assembly assem = System.Reflection.Assembly.GetExecutingAssembly();
                    string dir = assem.Location;
                    string cbdir = assem.CodeBase.Replace("file:///", "").Replace('/', '\\');

                    if (dir != cbdir) { dir = cbdir; }
                    mod_loc = dir;

                    string config_path = Path.Combine(Path.GetDirectoryName(dir), "Config.json");
                    Debug.Log("File Path: " + config_path);
                    if (File.Exists(config_path))
                    {
                        using (StreamReader streamReader = new StreamReader(config_path))
                        {
                            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                            {
                                config = serializer.Deserialize<Config>(jsonReader);
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
                            serializer.Serialize(writer, config);
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
                */
            }

            [Conditional("DEBUG")]
            public static void Log(object obj, UnityEngine.LogType logLevel = UnityEngine.LogType.Log)
            {
                string message = "[AdvancedElectrolyzer]: " + obj;
                switch (logLevel)
                {
                    case UnityEngine.LogType.Exception:
                    case UnityEngine.LogType.Error:
                        Debug.LogError(message);
                        break;
                    case UnityEngine.LogType.Assert:
                    case UnityEngine.LogType.Warning:
                        Debug.LogWarning(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
        internal class GeneratedBuildings_LoadGeneratedBuildings_Patch
        {
            public static void Prefix()
            {
                ModUtil.AddBuildingToPlanScreen("Oxygen", AdvancedElectrolyzerConfig.ID);
            }
        }

        [HarmonyPatch(typeof(Db), "Initialize")]
        public static class Db_Initialize_Patch
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

        [HarmonyPatch(typeof(Localization), "Initialize")]
        public class Localization_Initialize_Patch
        {
            public static void Postfix() => Translate(typeof(AdvancedElectrolyzersStrings.STRINGS));

            public static void Translate(Type root)
            {
                // Basic intended way to register strings, keeps namespace
                RegisterForTranslation(root);

                // Load user created translation files
                LoadStrings();

                // Register strings without namespace
                // because we already loaded user transltions, custom languages will overwrite these
                LocString.CreateLocStringKeys(root, null);

                // Creates template for users to edit
                GenerateStringsTemplate(root, Path.Combine(Manager.GetDirectory(), "strings_templates"));
            }

            private static void LoadStrings()
            {
                string path = Path.Combine(AdvancedElectrolyzersMod.mod_loc, "translations", GetLocale()?.Code + ".po");
                if (File.Exists(path))
                    OverloadStrings(LoadStringsFile(path, false));
            }
        }
    }
}
