using Newtonsoft.Json;
#if SPACED_OUT
using PeterHan.PLib.Options;
#endif
using TUNING;
using UnityEngine;

namespace TagnumElite
{
    namespace AdvancedElectrolyzer
    {
        public class AdvancedElectrolyzerConfig : IBuildingConfig
        {
            //Woops, typo. Can't fix this because save game compatibility.
            public const string ID = "AdvacnedElectrolyzer";

            private readonly ConduitPortInfo secondaryPort = new ConduitPortInfo(ConduitType.Gas, new CellOffset(0, 1));

            public static AEConfig Config => AdvancedElectrolyzersMod.config.advancedElectrolyzer;

            [JsonObject(MemberSerialization.OptIn)]
            public class AEConfig
            {
#if SPACED_OUT
                [Option("Water Consumption Rate", "This is litre per second")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float waterConsumptionRate { get; set; }

                // # Clean/Polluted Water #
#if SPACED_OUT
                [Option("Oxygen Production Rate", "This is gram per second")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float water2OxygenRatio { get; set; }

#if SPACED_OUT
                [Option("Hydrogen Production Rate", "This is gram per second")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float water2HydrogenRatio { get; set; }

                // # Salt Water #
#if SPACED_OUT
                [Option("Salt Water 2 Water Ratio")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float saltWater2WaterRatio { get; set; }

#if SPACED_OUT
                [Option("Salt Water 2 Salt Ratio")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float saltWater2SaltRatio { get; set; }

                // # Brine #
#if SPACED_OUT
                [Option("Brine 2 Water Ratio")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float brine2WaterRatio { get; set; }

#if SPACED_OUT
                [Option("Brine 2 Salt Ratio")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float brine2SaltRatio { get; set; }

#if SPACED_OUT
                [Option("Salt 2 Bleach Stone Ratio")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float salt2BleachStoneRatio { get; set; }

#if SPACED_OUT
                [Option("Oxygen Output Temperature")]
                [Limit(0, 10000)]
#endif
                [JsonProperty("Minimium Oxygen Temperature")]
                public float oxygenTemperature { get; set; }

#if SPACED_OUT
                [Option("Hydrogen Output Temperature")]
                [Limit(0, 10000)]
#endif
                [JsonProperty("Minimium Hydrogen Temperature")]
                public float hydrogenTemperature { get; set; }

#if SPACED_OUT
                [Option("Heat Exhaust Amount")]
                [Limit(0, 10000)]
#endif
                [JsonProperty("Exhaust Heat Amount")]
                public float heatExhaust { get; set; }

#if SPACED_OUT
                [Option("Heat Internal Amount")]
                [Limit(0, 10000)]
#endif
                [JsonProperty("Self Heat Amount")]
                public float heatSelf { get; set; }

#if SPACED_OUT
                [Option("Energy Consumption")]
                [Limit(0, 10000)]
#endif
                [JsonProperty("Energy Consumption")]
                public float energyConsumption { get; set; }

#if SPACED_OUT
                [Option("Work Speed Multiplier")]
                [Limit(0, 10)]
#endif
                [JsonProperty("Work Speed Multiplier")]
                public float workSpeedMultiplier { get; set; }

#if SPACED_OUT
                [Option("Process Brine/Saltwater")]
#endif
                [JsonProperty("Process Brine/Saltwater")]
                public bool processSaltAndBrine { get; set; }

#if SPACED_OUT
                [Option("Require Salt for construction")]
#endif
                [JsonProperty]
                public bool requireSaltForConstruction { get; set; }

#if SPACED_OUT
                [Option("Required Salt for construction")]
                [Limit(0, 10000)]
#endif
                [JsonProperty]
                public float requiredSaltForConstruction { get; set; }

                public AEConfig()
                {
                    waterConsumptionRate = 1f;
                    water2OxygenRatio = 0.888f;
                    water2HydrogenRatio = 0.111999989f;
                    saltWater2WaterRatio = 0.93f;
                    saltWater2SaltRatio = 0.07f;
                    brine2WaterRatio = 0.7f;
                    brine2SaltRatio = 0.3f;
                    salt2BleachStoneRatio = 0.61f;
                    oxygenTemperature = 293.15f;
                    hydrogenTemperature = 293.15f;
                    heatExhaust = 0f;
                    heatSelf = 4f;
                    energyConsumption = 400f;
                    workSpeedMultiplier = 1f;
                    processSaltAndBrine = false;
                    requireSaltForConstruction = false;
                    requiredSaltForConstruction = 50f;
                }
            }

            public override BuildingDef CreateBuildingDef()
            {
                float[] material_mass = new float[2] { BUILDINGS.CONSTRUCTION_MASS_KG.TIER3[0], Config.requiredSaltForConstruction };
                string[] materials = new string[2] { MATERIALS.METAL, "Salt" };

                BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(
                    id: ID,
                    width: 2,
                    height: 2,
                    anim: "advanced_electrolyzer_kanim",
                    hitpoints: BUILDINGS.HITPOINTS.TIER3,
                    construction_time: BUILDINGS.CONSTRUCTION_TIME_SECONDS.TIER3,
                    construction_mass: Config.requireSaltForConstruction ? material_mass : BUILDINGS.CONSTRUCTION_MASS_KG.TIER3,
                    construction_materials: Config.requireSaltForConstruction ? materials : MATERIALS.ALL_METALS,
                    melting_point: BUILDINGS.MELTING_POINT_KELVIN.TIER3,
                    build_location_rule: BuildLocationRule.Anywhere,
                    decor: BUILDINGS.DECOR.PENALTY.TIER2,
                    noise: NOISE_POLLUTION.NOISY.TIER2,
                    0.2f
                );
                buildingDef.RequiresPowerInput = true;
                buildingDef.PowerInputOffset = new CellOffset(1, 0);
                buildingDef.EnergyConsumptionWhenActive = Config.energyConsumption;
                buildingDef.ExhaustKilowattsWhenActive = Config.heatExhaust;
                buildingDef.SelfHeatKilowattsWhenActive = Config.heatSelf;
                buildingDef.ViewMode = OverlayModes.GasConduits.ID;
                buildingDef.MaterialCategory = MATERIALS.REFINED_METALS;
                buildingDef.AudioCategory = "HollowMetal";
                buildingDef.InputConduitType = ConduitType.Liquid;
                buildingDef.UtilityInputOffset = new CellOffset(0, 0);
                buildingDef.OutputConduitType = ConduitType.Gas;
                buildingDef.UtilityOutputOffset = new CellOffset(1, 1);
                buildingDef.PermittedRotations = PermittedRotations.FlipH;
                buildingDef.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(1, 0));
                GeneratedBuildings.RegisterWithOverlay(OverlayScreen.GasVentIDs, ID);
                return buildingDef;
            }

            public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
            {
                go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
                go.AddOrGet<Structure>();
                go.AddOrGet<LoopingSounds>();

                Storage storage = go.AddOrGet<Storage>();
                storage.capacityKg = 6f;
                storage.showInUI = true;

                ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
                conduitConsumer.conduitType = ConduitType.Liquid;
                conduitConsumer.capacityTag = GameTags.AnyWater;
                conduitConsumer.capacityKG = storage.capacityKg;
                conduitConsumer.forceAlwaysSatisfied = true;
                conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;

                AdvancedElectrolyzer electrolyzer = go.AddOrGet<AdvancedElectrolyzer>();
                electrolyzer.portInfo = secondaryPort;

                Prioritizable.AddRef(go);
            }

            public override void DoPostConfigurePreview(BuildingDef def, GameObject go)
            {
                base.DoPostConfigurePreview(def, go);
                AttachPort(go);
            }

            public override void DoPostConfigureUnderConstruction(GameObject go)
            {
                base.DoPostConfigureUnderConstruction(go);
                AttachPort(go);
            }

            public override void DoPostConfigureComplete(GameObject go)
            {
                go.AddOrGet<LogicOperationalController>();
                go.AddOrGetDef<PoweredActiveController.Def>();
            }

            private void AttachPort(GameObject go)
            {
                ConduitSecondaryOutput conduitSecondaryOutput = go.AddComponent<ConduitSecondaryOutput>();
                conduitSecondaryOutput.portInfo = secondaryPort;
            }
        }
    }
}
