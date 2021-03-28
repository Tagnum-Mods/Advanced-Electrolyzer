using Newtonsoft.Json;
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

            public static AEConfig Config => AdvancedElectrolyzerMod.config.advancedElectrolyzer;

            public class AEConfig
            {
                // This is litre per second
                [JsonProperty]
                public float waterConsumptionRate = 1f;

                // # Clean/Polluted Water #
                // This is gram per second
                [JsonProperty]
                public float water2OxygenRatio = 0.888f;
                // This is gram per second
                [JsonProperty]
                public float water2HydrogenRatio = 0.111999989f;
                // # Salt Water #
                [JsonProperty]
                public float saltWater2WaterRatio = 0.93f;
                [JsonProperty]
                public float saltWater2SaltRatio = 0.07f;
                // # Brine #
                [JsonProperty]
                public float brine2WaterRatio = 0.7f;
                [JsonProperty]
                public float brine2SaltRatio = 0.3f;

                [JsonProperty]
                public float salt2BleachStoneRatio = 0.61f;

                [JsonProperty("Minimium Oxygen Temperature")]
                public float oxygenTemperature = 293.15f;
                [JsonProperty("Minimium Hydrogen Temperature")]
                public float hydrogenTemperature = 293.15f;
                [JsonProperty("Exhaust Heat Amount")]
                public float heatExhaust = 0f;
                [JsonProperty("Self Heat Amount")]
                public float heatSelf = 4f;

                [JsonProperty("Energy Consumption")]
                public float energyConsumption = 400f;
                [JsonProperty("Work Speed Multiplier")]
                public float workSpeedMultiplier = 1f;

                [JsonProperty("Process Brine/Saltwater")]
                public bool processSaltAndBrine = false;
                [JsonProperty]
                public bool requireSaltForConstruction = false;
                [JsonProperty]
                public float requiredSaltForConstruction = 50f;
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
