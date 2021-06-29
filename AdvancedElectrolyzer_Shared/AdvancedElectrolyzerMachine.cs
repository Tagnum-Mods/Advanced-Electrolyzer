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
            private static void Debug(object obj)
            {
                global::Debug.Log("[Advanced Electrolyzer] " + obj);
            }

            public class StatesInstance : GameStateMachine<States, StatesInstance, AdvancedElectrolyzerMachine, object>.GameInstance
            {
                private List<Guid> statusItemEntries = new List<Guid>();

                public StatesInstance(AdvancedElectrolyzerMachine smi) : base(smi) { }

                public void AddStatusItems()
                {
                    //statusItemEntries.Add(master.GetComponent<KSelectable>().AddStatusItem(AdvElectrolyzerInputStatusItem));
                    statusItemEntries.Add(master.GetComponent<KSelectable>().AddStatusItem(AdvElectrolyzerOutputStatusItem, GameTags.Oxygen));
                    statusItemEntries.Add(master.GetComponent<KSelectable>().AddStatusItem(AdvElectrolyzerOutputStatusItem, GameTags.Hydrogen));
                }

                public void RemoveStatusItems()
                {
                    foreach (Guid statusItemEntry in statusItemEntries)
                    {
                        master.GetComponent<KSelectable>().RemoveStatusItem(statusItemEntry);
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
                    Debug("Initialization States");

                    //First we transition from root to disabled if not IsOperational. Then on OnStorageChange update the meter
                    root.EventTransition(GameHashes.OperationalChanged, disabled, (StatesInstance smi) => !smi.master.operational.IsOperational).EventHandler(GameHashes.OnStorageChange, delegate (StatesInstance smi)
                    {
                        Debug("Update Meter");
                        smi.master.UpdateMeter();
                    });

                    //Transition from disabled to waiting if operational or IsActive
                    disabled.Enter("Disabled", delegate (StatesInstance smi)
                    {
                        Debug("Disabled");
                    }).EventTransition(GameHashes.OperationalChanged, waiting, (StatesInstance smi) => smi.master.operational.IsOperational);

                    waiting.Enter("Waiting", delegate (StatesInstance smi)
                    {
                        Debug("Waiting");
                        smi.master.operational.SetActive(false);
                    }).EventTransition(GameHashes.OnStorageChange, converting, (StatesInstance smi) => smi.master.HasEnoughMass());

                    //When we enter into converting status, add status items
                    converting.Enter("Ready", delegate (StatesInstance smi)
                    {
                        Debug("Converting Enter");
                        smi.AddStatusItems();
                        smi.master.operational.SetActive(true);
                        // Then once we exit, remove status items
                    }).Exit("RemoveStatusItems", delegate (StatesInstance smi)
                    {
                        Debug("Converting Exit");
                        smi.RemoveStatusItems();
                        // Transition into disabled if not operation or IsActive
                    })
                    .Transition(waiting, (StatesInstance smi) => !smi.master.CanConvertAtAll())
                    .Transition(blocked, (StatesInstance smi) => !smi.master.RoomForPressure)
                    //.EventTransition(GameHashes.OperationalChanged, disabled, (StatesInstance smi) => smi.master.operational != null && !smi.master.operational.IsActive)
                    .Update("ConvertMass", delegate (StatesInstance smi, float dt)
                    {
                        Debug("Convert Mass");
                        smi.master.CheckStorage();
                        smi.master.ConvertMass();
                    }, UpdateRate.SIM_1000ms, load_balance: true);

                    blocked.Enter("Blocked", delegate (StatesInstance smi)
                    {
                        Debug("Blocked");
                        smi.master.operational.SetActive(false);
                    }).Transition(converting, (StatesInstance smi) => smi.master.RoomForPressure);
                    Debug("Initialized States");
                }
            }

            protected override void OnPrefabInit()
            {
                base.OnPrefabInit();
                Attributes attributes = gameObject.GetAttributes();
                machinerySpeedAttribute = attributes.Add(Db.Get().Attributes.MachinerySpeed);

                /*if (AdvElectrolyzerInputStatusItem == null)
                {
                    AdvElectrolyzerInputStatusItem = new StatusItem("ADVANCEDELECTROLYZERINPUT", "BUILDING", string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: false, OverlayModes.None.ID).SetResolveStringCallback(delegate (string str, object data)
                    {
                        str = str.Replace("{ElementType}", "Important Fluid");
                        str = str.Replace("{FlowRate}", GameUtil.GetFormattedByTag(GameTags.Water, Config.waterConsumptionRate, GameUtil.TimeSlice.PerSecond));
                        return str;
                    });
                }*/

                if (AdvElectrolyzerOutputStatusItem == null)
                {
                    AdvElectrolyzerOutputStatusItem = new StatusItem("ADVANCEDELECTROLYZEROUTPUT", "BUILDING", string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: true, OverlayModes.None.ID).SetResolveStringCallback(delegate (string str, object data)
                    {
                        Tag tag = (Tag)data;
                        str = str.Replace("{ElementType}", tag == GameTags.Oxygen ? "Oxygen" : "Hydrogen");
                        str = str.Replace("{FlowRate}", GameUtil.GetFormattedByTag(GameTags.Oxygen, tag == GameTags.Oxygen ? Config.water2OxygenRatio : Config.water2HydrogenRatio, GameUtil.TimeSlice.PerSecond));
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

            private const float EPSILON = 0.0001f;

            public Action<float> onConvertMass;

            //private static StatusItem AdvElectrolyzerInputStatusItem;

            private static StatusItem AdvElectrolyzerOutputStatusItem;

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
                    float positionPercent = Mathf.Clamp01(storage.GetMassAvailable(GameTags.AnyWater) / storage.capacityKg);
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
                var bleachStoneTag = SimHashes.BleachStone.CreateTag();
                for (int i = 0; i < storage.items.Count; i++)
                {
                    GameObject storageItem = storage.items[i];
                    if (storageItem != null)
                    {
                        if (storageItem.HasTag(GameTags.AnyWater)) continue;

                        i--;
                        storage.Remove(storageItem);
                        PrimaryElement element = storageItem.GetComponent<PrimaryElement>();
                        int disease_count = (int)(element.DiseaseCount * element.Mass);
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
                float oxygenMass = 0f;
                float hydrogenMass = 0f;
                float bleachStoneMass = 0f;

                float totalConsumedAmount = 0f;
                float waterConsumptionRate = Config.waterConsumptionRate * speedMultiplier;

                Tag dirtyWaterTag = SimHashes.DirtyWater.CreateTag();
                Tag saltWaterTag = SimHashes.SaltWater.CreateTag();
                Tag brineTag = SimHashes.Brine.CreateTag();

                bool isDirty = false;

                for (int i = 0; i < storage.items.Count; i++)
                {
                    GameObject storageItem = storage.items[i];
                    if (storageItem != null && storageItem.HasTag(GameTags.AnyWater))
                    {
                        PrimaryElement element = storageItem.GetComponent<PrimaryElement>();

                        //element.KeepZeroMassObject = true;
                        float consumedAmount = Mathf.Min(waterConsumptionRate, element.Mass);
                        float consumedPercentage = consumedAmount / element.Mass;

                        int diseaseCount = (int)(consumedPercentage * element.DiseaseCount);

                        element.Mass -= consumedAmount;
                        element.ModifyDiseaseCount(-diseaseCount, "AdvancedElectrolyzer.ConvertMass");

                        if (storageItem.HasTag(saltWaterTag))
                        {
                            // 93% H2O
                            oxygenMass += consumedAmount * Config.saltWater2WaterRatio * Config.water2OxygenRatio;
                            hydrogenMass += consumedAmount * Config.saltWater2WaterRatio * Config.water2HydrogenRatio;
                            // 7% NaCl
                            bleachStoneMass += consumedAmount * Config.saltWater2SaltRatio * Config.salt2BleachStoneRatio;
                        }
                        else if (storageItem.HasTag(brineTag))
                        {
                            // 70% H2O
                            oxygenMass += consumedAmount * Config.brine2WaterRatio * Config.water2OxygenRatio;
                            hydrogenMass += consumedAmount * Config.brine2WaterRatio * Config.water2HydrogenRatio;
                            // 30% NaCl
                            bleachStoneMass += consumedAmount * Config.brine2SaltRatio * Config.salt2BleachStoneRatio;
                        }
                        else
                        {
                            oxygenMass += consumedAmount * Config.water2OxygenRatio;
                            hydrogenMass += consumedAmount * Config.water2HydrogenRatio;
                        }

                        totalConsumedAmount += consumedAmount;

                        if (storageItem.HasTag(dirtyWaterTag) && consumedAmount > EPSILON) isDirty = true;

                        diseaseInfo = SimUtil.CalculateFinalDiseaseInfo(diseaseInfo.idx, diseaseInfo.count, element.DiseaseIdx, element.DiseaseCount);

                        waterConsumptionRate -= consumedAmount;
                        if (waterConsumptionRate <= 0f)
                        {
                            global::Debug.Assert(waterConsumptionRate <= 0f);
                            break;
                        }
                    }
                }

                float temperature = GetComponent<PrimaryElement>().Temperature;

                if (onConvertMass != null && totalConsumedAmount > EPSILON) onConvertMass(totalConsumedAmount);

                ConduitFlow gasFlowManager = Conduit.GetFlowManager(portInfo.conduitType);

                SimHashes oxygenHash = isDirty ? SimHashes.ContaminatedOxygen : SimHashes.Oxygen;
                float oxygenGenerated = gasFlowManager.AddElement(oxygenOutputCell, oxygenHash, oxygenMass * speedMultiplier, Mathf.Max(Config.oxygenTemperature, temperature), diseaseInfo.idx, diseaseInfo.count / 2);
                ReportManager.Instance.ReportValue(ReportManager.ReportType.OxygenCreated, oxygenGenerated, gameObject.GetProperName());
                Game.Instance.accumulators.Accumulate(OxygenAccumulator, oxygenGenerated);

                float hydrogenGenerated = gasFlowManager.AddElement(hydrogenOutputCell, SimHashes.Hydrogen, hydrogenMass * speedMultiplier, Mathf.Max(Config.hydrogenTemperature, temperature), diseaseInfo.idx, diseaseInfo.count / 2);
                Game.Instance.accumulators.Accumulate(HydrogenAccumulator, hydrogenGenerated);

                if (bleachStoneMass > EPSILON)
                {
                    Element bleachStone = ElementLoader.FindElementByHash(SimHashes.BleachStone);

                    Vector3 position = building.GetRotatedOffset(new CellOffset(1, 0)).ToVector3() + new Vector3(.5f, .5f, .0f) + transform.position;
                    UnityEngine.Debug.Log("[AE] transform is at " + transform.position);
                    UnityEngine.Debug.Log("[AE] exit is at " + position);

                    bleachStone.substance.SpawnResource(position, bleachStoneMass, temperature, diseaseInfo.idx, diseaseInfo.count);
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

            public AdvancedElectrolyzerConfig.AEConfig Config => AdvancedElectrolyzerConfig.Config;

            public HandleVector<int>.Handle WaterAccumulator { get; private set; }
            public HandleVector<int>.Handle OxygenAccumulator { get; private set; }
            public HandleVector<int>.Handle HydrogenAccumulator { get; private set; }
        }
    }
}
