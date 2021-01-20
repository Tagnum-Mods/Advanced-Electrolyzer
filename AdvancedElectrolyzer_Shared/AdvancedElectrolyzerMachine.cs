using Klei;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;


namespace TagnumElite
{
    namespace AdvancedElectrolyzer
    {
        public class AdvancedElectrolyzerMachine : StateMachineComponent<AdvancedElectrolyzerMachine.StatesInstance>
        {
            [Conditional("DEBUG")]
            private static void debug(object obj)
            {
                Debug.Log("[Advanced Electrolyzer] " + obj);
            }

            public class StatesInstance : GameStateMachine<States, StatesInstance, AdvancedElectrolyzerMachine, object>.GameInstance
            {
                private List<Guid> statusItemEntries = new List<Guid>();

                public StatesInstance(AdvancedElectrolyzerMachine smi) : base(smi) { }

                public void AddStatusItems()
                {
                    statusItemEntries.Add(base.master.GetComponent<KSelectable>().AddStatusItem(ElementConverterInput));
                    statusItemEntries.Add(base.master.GetComponent<KSelectable>().AddStatusItem(ElementConverterOutput, GameTags.Oxygen));
                    statusItemEntries.Add(base.master.GetComponent<KSelectable>().AddStatusItem(ElementConverterOutput, GameTags.Hydrogen));
                }

                public void RemoveStatusItems()
                {
                    foreach (Guid statusItemEntry in statusItemEntries)
                    {
                        base.master.GetComponent<KSelectable>().RemoveStatusItem(statusItemEntry);
                    }
                    statusItemEntries.Clear();
                }
            }

            public class States : GameStateMachine<States, StatesInstance, AdvancedElectrolyzerMachine>
            {
                public State disabled;
                public State waiting;
                public State converting;
                public State blocked;

                public override void InitializeStates(out BaseState default_state)
                {
                    default_state = disabled;
                    debug("Initialization States");

                    //First we transition from root to disabled if not IsOperational. Then on OnStorageChange update the meter
                    root.EventTransition(GameHashes.OperationalChanged, disabled, (StatesInstance smi) => !smi.master.operational.IsOperational).EventHandler(GameHashes.OnStorageChange, delegate (StatesInstance smi)
                    {
                        debug("Update Meter");
                        smi.master.UpdateMeter();
                    });

                    //Transition from disabled to waiting if operational or IsActive
                    disabled.Enter("Disabled", delegate (StatesInstance smi)
                    {
                        debug("Disabled");
                    }).EventTransition(GameHashes.OperationalChanged, waiting, (StatesInstance smi) => smi.master.operational.IsOperational);

                    waiting.Enter("Waiting", delegate (StatesInstance smi)
                    {
                        debug("Waiting");
                        smi.master.operational.SetActive(false);
                    }).EventTransition(GameHashes.OnStorageChange, converting, (StatesInstance smi) => smi.master.HasEnoughMass());

                    //When we enter into converting status, add status items
                    converting.Enter("Ready", delegate (StatesInstance smi)
                    {
                        debug("Converting Enter");
                        smi.AddStatusItems();
                        smi.master.operational.SetActive(true);
                        // Then once we exit, remove status items
                    }).Exit("RemoveStatusItems", delegate (StatesInstance smi)
                    {
                        debug("Converting Exit");
                        smi.RemoveStatusItems();
                        // Transition into disabled if not operation or IsActive
                    })
                    .Transition(waiting, (StatesInstance smi) => !smi.master.CanConvertAtAll())
                    .Transition(blocked, (StatesInstance smi) => !smi.master.RoomForPressure)
                    //.EventTransition(GameHashes.OperationalChanged, disabled, (StatesInstance smi) => smi.master.operational != null && !smi.master.operational.IsActive)
                    .Update("ConvertMass", delegate (StatesInstance smi, float dt)
                    {
                        debug("Convert Mass");
                        smi.master.CheckStorage();
                        smi.master.ConvertMass();
                    }, UpdateRate.SIM_1000ms, load_balance: true);

                    blocked.Enter("Blocked", delegate (StatesInstance smi)
                    {
                        debug("Blocked");
                        smi.master.operational.SetActive(false);
                    }).Transition(converting, (StatesInstance smi) => smi.master.RoomForPressure);
                    debug("Initialized States");
                }
            }

            protected override void OnPrefabInit()
            {
                base.OnPrefabInit();
                Attributes attributes = gameObject.GetAttributes();
                machinerySpeedAttribute = attributes.Add(Db.Get().Attributes.MachinerySpeed);

                if (ElementConverterInput == null)
                {
                    ElementConverterInput = new StatusItem("ADVANCEDELECTROLYZERINPUT", "BUILDING", string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: false, OverlayModes.None.ID).SetResolveStringCallback(delegate (string str, object data)
                    {
                        return str.Replace("{FlowRate}", "" + Config.waterConsumptionRate * 1000 + "kg/s");
                    });
                }

                if (ElementConverterOutput == null)
                {
                    ElementConverterOutput = new StatusItem("ADVANCEDELECTROLYZEROUTPUT", "BUILDING", string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: true, OverlayModes.None.ID).SetResolveStringCallback(delegate (string str, object data)
                    {
                        Tag tag = (Tag)data;
                        str = str.Replace("{ElementType}", tag == GameTags.Oxygen ? "Oxygen" : "Hydrogen");
                        str = str.Replace("{FlowRate}", "" + (tag == GameTags.Oxygen ? Config.water2OxygenRatio : Config.water2HydrogenRatio) * 1000 + "kg/s");
                        return str;
                    });
                }
            }

            protected override void OnSpawn()
            {
                base.OnSpawn();

                oxygenOutputCell = building.GetUtilityOutputCell();
                int cell = Grid.PosToCell(transform.GetPosition());
                CellOffset rotatedOffset = building.GetRotatedOffset(portInfo.offset);
                hydrogenOutputCell = Grid.OffsetCell(cell, rotatedOffset);
                IUtilityNetworkMgr networkManager = Conduit.GetNetworkManager(portInfo.conduitType);
                hydrogenOutputItem = new FlowUtilityNetwork.NetworkItem(portInfo.conduitType, Endpoint.Source, hydrogenOutputCell, gameObject);
                networkManager.AddToNetworks(hydrogenOutputCell, hydrogenOutputItem, true);

                WaterAccumulator = Game.Instance.accumulators.Add("ElementsConsumed", this);
                OxygenAccumulator = Game.Instance.accumulators.Add("OutputElements", this);
                HydrogenAccumulator = Game.Instance.accumulators.Add("OutputElements", this);

                KBatchedAnimController batchedAnimController = GetComponent<KBatchedAnimController>();

                if (hasMeter)
                {
                    meter = new MeterController(batchedAnimController, "U2H_meter_target", "meter", Meter.Offset.Behind, Grid.SceneLayer.NoLayer, new Vector3(-0.4f, 0.5f, -0.1f), "U2H_meter_target", "U2H_meter_tank", "U2H_meter_waterbody", "U2H_meter_level");
                }
                smi.StartSM();
                UpdateMeter();
                Tutorial.Instance.oxygenGenerators.Add(gameObject);


            }

            protected override void OnCleanUp()
            {
                Tutorial.Instance.oxygenGenerators.Remove(gameObject);
                IUtilityNetworkMgr networkManager = Conduit.GetNetworkManager(portInfo.conduitType);
                networkManager.RemoveFromNetworks(hydrogenOutputCell, hydrogenOutputItem, true);

                Game.Instance.accumulators.Remove(WaterAccumulator);
                Game.Instance.accumulators.Remove(OxygenAccumulator);
                Game.Instance.accumulators.Remove(HydrogenAccumulator);

                base.OnCleanUp();
            }

            [SerializeField]
            public bool hasMeter = true;

            [MyCmpAdd]
            private Storage storage;

            [MyCmpReq]
            private Operational operational;

            [SerializeField]
            public ConduitPortInfo portInfo;

            [MyCmpReq]
            private Building building;

            private const float EPSILON = 0.00001f;

            public Action<float> onConvertMass;

            private static StatusItem ElementConverterInput;

            private static StatusItem ElementConverterOutput;

            private MeterController meter;

            private AttributeInstance machinerySpeedAttribute;

            private int hydrogenOutputCell;
            private FlowUtilityNetwork.NetworkItem hydrogenOutputItem;
            private int oxygenOutputCell;

            private float GetSpeedMultiplier()
            {
                return machinerySpeedAttribute.GetTotalValue() * Config.workSpeedMultiplier;
            }

            public void UpdateMeter()
            {
                if (hasMeter)
                {
                    float positionPercent = Mathf.Clamp01(storage.MassStored() / storage.capacityKg);
                    meter.SetPositionPercent(positionPercent);
                }
            }

            public bool CanConvertAtAll()
            {
                bool result = false;
                List<GameObject> items = storage.items;
                for (int i = 0; i < items.Count; i++)
                {
                    GameObject item = items[i];

                    if (item.HasTag(GameTags.AnyWater) && item.GetComponent<PrimaryElement>().Mass > EPSILON)
                    {
                        result = true;
                    }
                }

                return result;
            }

            public bool HasEnoughMass()
            {
                float speedMultiplierPercentage = 1f * GetSpeedMultiplier();
                bool result = true;
                List<GameObject> items = storage.items;
                float totalMass = 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    GameObject gameObject = items[i];
                    if (gameObject != null && gameObject.HasTag(GameTags.AnyWater))
                    {
                        totalMass += gameObject.GetComponent<PrimaryElement>().Mass;
                    }
                }
                if (totalMass < Config.waterConsumptionRate * speedMultiplierPercentage)
                {
                    result = false;
                }
                return result;
            }

            private void CheckStorage()
            {
                for (int i = 0; i < storage.items.Count; i++)
                {
                    GameObject storageItem = storage.items[i];
                    if (storageItem != null)
                    {
                        if (storageItem.HasTag(GameTags.AnyWater)) continue;

                        i--;
                        storage.Remove(storageItem);
                        PrimaryElement element = storageItem.GetComponent<PrimaryElement>();
                        int disease_count = (int)((float)element.DiseaseCount * element.Mass);
                        SimMessages.AddRemoveSubstance(Grid.PosToCell(transform.GetPosition()), element.Element.id, CellEventLogger.Instance.ConduitConsumerWrongElement, element.Mass, element.Temperature, element.DiseaseIdx, disease_count);
                    }
                }
            }

            private void ConvertMass()
            {
                SimUtil.DiseaseInfo diseaseInfo = SimUtil.DiseaseInfo.Invalid;
                diseaseInfo.idx = byte.MaxValue;
                diseaseInfo.count = 0;

                float speedMultiplier = GetSpeedMultiplier();

                // Accumulators
                float pollutedWater = 0f;
                float cleanWater = 0f;
                float saltWater = 0f;
                float brineWater = 0f;

                float totalConsumedAmount = 0f;
                float waterConsumptionRate = Config.waterConsumptionRate * speedMultiplier;
                for (int i = 0; i < storage.items.Count; i++)
                {
                    GameObject storageItem = storage.items[i];
                    if (storageItem != null && storageItem.HasTag(GameTags.AnyWater))
                    {
                        PrimaryElement element = storageItem.GetComponent<PrimaryElement>();

                        if (storageItem.HasTag(SimHashes.DirtyWater.CreateTag()))
                            pollutedWater += element.Mass;
                        else if (storageItem.HasTag(SimHashes.SaltWater.CreateTag()))
                            saltWater += element.Mass;
                        else if (storageItem.HasTag(SimHashes.Brine.CreateTag()))
                            brineWater += element.Mass;
                        else
                            cleanWater += element.Mass;

                        element.KeepZeroMassObject = true;
                        float consumedAmount = Mathf.Min(waterConsumptionRate, element.Mass);
                        float consumedPercentage = consumedAmount / element.Mass;

                        int diseaseCount = (int)(consumedPercentage * (float)element.DiseaseCount);

                        element.Mass -= consumedAmount;
                        element.ModifyDiseaseCount(-diseaseCount, "AdvancedElectrolyzer.ConvertMass");
                        totalConsumedAmount += consumedAmount;

                        diseaseInfo = SimUtil.CalculateFinalDiseaseInfo(diseaseInfo.idx, diseaseInfo.count, element.DiseaseIdx, element.DiseaseCount);

                        waterConsumptionRate -= consumedAmount;
                        if (waterConsumptionRate <= 0f)
                        {
                            Debug.Assert(waterConsumptionRate <= 0f);
                            break;
                        }
                    }
                }

                float temperature = GetComponent<PrimaryElement>().Temperature;

                if (onConvertMass != null && totalConsumedAmount > 0f)
                {
                    onConvertMass(totalConsumedAmount);
                }

                ConduitFlow gasFlowManager = Conduit.GetFlowManager(portInfo.conduitType);
                SimHashes oxygenHash = pollutedWater > 0.1f ? SimHashes.ContaminatedOxygen : SimHashes.Oxygen;
                float oxygenGenerated = gasFlowManager.AddElement(oxygenOutputCell, oxygenHash, Config.water2OxygenRatio * speedMultiplier, Mathf.Max(Config.oxygenTemperature, temperature), diseaseInfo.idx, diseaseInfo.count / 2);
                ReportManager.Instance.ReportValue(ReportManager.ReportType.OxygenCreated, oxygenGenerated, gameObject.GetProperName());
                Game.Instance.accumulators.Accumulate(OxygenAccumulator, oxygenGenerated);
                float hydrogenGenerated = gasFlowManager.AddElement(hydrogenOutputCell, SimHashes.Hydrogen, Config.water2HydrogenRatio * speedMultiplier, Mathf.Max(Config.hydrogenTemperature, temperature), diseaseInfo.idx, diseaseInfo.count / 2);
                Game.Instance.accumulators.Accumulate(HydrogenAccumulator, hydrogenGenerated);

                if (brineWater > EPSILON || saltWater > EPSILON) {
                    Element salt = ElementLoader.FindElementByHash(SimHashes.Salt);
                    Vector3 base_position = transform.GetPosition();
                    Vector3 position = new Vector3(base_position.x + 0.5f, base_position.y + 0.5f, base_position.z + 0.5f);
                    float mass = 0f;
                    mass += saltWater * Config.saltWaterRatio;
                    mass += brineWater * Config.brineRatio;
                    salt.substance.SpawnResource(position, mass, temperature, diseaseInfo.idx, diseaseInfo.count);
                }

                storage.Trigger((int)GameHashes.OnStorageChange, gameObject);
            }

            private bool RoomForPressure
            {
                get
                {
                    ConduitFlow flowManager = Conduit.GetFlowManager(portInfo.conduitType);
                    return flowManager.IsConduitEmpty(hydrogenOutputCell) && flowManager.IsConduitEmpty(oxygenOutputCell);
                }
            }

            public AdvancedElectrolyzerConfig.Config Config
            {
                get
                {
                    return AdvancedElectrolyzerConfig.config;
                }
            }

            public HandleVector<int>.Handle WaterAccumulator { get; private set; }
            public HandleVector<int>.Handle OxygenAccumulator { get; private set; }
            public HandleVector<int>.Handle HydrogenAccumulator { get; private set; }
        }
    }
}
