using Klei;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;


namespace TagnumElite_AdvancedElectrolyzer
{

    [SerializationConfig(MemberSerialization.OptIn)]
    public class AdvancedElectrolyzer : StateMachineComponent<AdvancedElectrolyzer.StatesInstance>, ISecondaryOutput
    {
        [Conditional("DEBUG")]
        private static void debug(object obj)
        {
            Debug.Log("[Advanced Electrolyzer] " + obj);
        }

        public class StatesInstance : GameStateMachine<States, StatesInstance, AdvancedElectrolyzer, object>.GameInstance
        {
            private List<Guid> statusItemEntries = new List<Guid>();

            public StatesInstance(AdvancedElectrolyzer smi) : base(smi) { }

            public void AddStatusItems()
            {
                Guid item = base.master.GetComponent<KSelectable>().AddStatusItem(ElementConverterInput, new object());
                statusItemEntries.Add(item);
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

        public class States : GameStateMachine<States, StatesInstance, AdvancedElectrolyzer>
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
                disabled.Enter("Disabled", delegate (StatesInstance smi) {
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
                    smi.master.ConvertMass();
                }, UpdateRate.SIM_1000ms, load_balance: true);

                blocked.Enter("Blocked", delegate (StatesInstance smi)
                {
                    debug("Blocked");
                    smi.master.operational.SetActive(false);
                }).ToggleStatusItem(Db.Get().BuildingStatusItems.GasVentObstructed).Transition(converting, (StatesInstance smi) => smi.master.RoomForPressure);
                debug("Initialized States");
            }
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Attributes attributes = base.gameObject.GetAttributes();
            machinerySpeedAttribute = attributes.Add(Db.Get().Attributes.MachinerySpeed);

            if (ElementConverterInput == null)
            {
                ElementConverterInput = new StatusItem("ElementConverterInput", "BUILDING", string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: true, OverlayModes.None.ID).SetResolveStringCallback(delegate (string str, object data)
                {
                    str = str.Replace("{ElementTypes}", "Test eLemnet");
                    str = str.Replace("{FlowRate}", "Test Flow Rate");
                    return str;
                });
            }

            if (ElementConverterOutput == null)
            {
                ElementConverterOutput = new StatusItem("ElementConverterOutput", "BUILDING", string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: true, OverlayModes.None.ID).SetResolveStringCallback(delegate (string str, object data)
                {
                    str = str.Replace("{ElementTypes}", "Test eLemnet");
                    str = str.Replace("{FlowRate}", "Test Flow Rate");
                    return str;
                });
            }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            this.oxygenOutputCell = this.building.GetUtilityOutputCell();
            int cell = Grid.PosToCell(base.transform.GetPosition());
            CellOffset rotatedOffset = this.building.GetRotatedOffset(this.portInfo.offset);
            this.hydrogenOutputCell = Grid.OffsetCell(cell, rotatedOffset);
            IUtilityNetworkMgr networkManager = Conduit.GetNetworkManager(this.portInfo.conduitType);
            this.hydrogenOutputItem = new FlowUtilityNetwork.NetworkItem(this.portInfo.conduitType, Endpoint.Source, this.hydrogenOutputCell, base.gameObject);
            networkManager.AddToNetworks(this.hydrogenOutputCell, this.hydrogenOutputItem, true);

            waterAccumulator = Game.Instance.accumulators.Add("ElementsConsumed", this);
            oxygenAccumulator = Game.Instance.accumulators.Add("OutputElements", this);
            hydrogenAccumulator = Game.Instance.accumulators.Add("OutputElements", this);

            KBatchedAnimController batchedAnimController = GetComponent<KBatchedAnimController>();

            if (hasMeter)
            {
                meter = new MeterController(batchedAnimController, "U2H_meter_target", "meter", Meter.Offset.Behind, Grid.SceneLayer.NoLayer, new Vector3(-0.4f, 0.5f, -0.1f), "U2H_meter_target", "U2H_meter_tank", "U2H_meter_waterbody", "U2H_meter_level");
            }
            base.smi.StartSM();
            UpdateMeter();
            Tutorial.Instance.oxygenGenerators.Add(base.gameObject);
        }

        protected override void OnCleanUp()
        {
            Tutorial.Instance.oxygenGenerators.Remove(base.gameObject);
            IUtilityNetworkMgr networkManager = Conduit.GetNetworkManager(this.portInfo.conduitType);
            networkManager.RemoveFromNetworks(this.hydrogenOutputCell, this.hydrogenOutputItem, true);

            Game.Instance.accumulators.Remove(waterAccumulator);
            Game.Instance.accumulators.Remove(oxygenAccumulator);
            Game.Instance.accumulators.Remove(hydrogenAccumulator);

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
            return machinerySpeedAttribute.GetTotalValue();
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

                if (item.HasTag(GameTags.AnyWater) && item.GetComponent<PrimaryElement>().Mass > 0f)
                {
                    result = true;
                }
            }

            return result;
        }

        public bool HasEnoughMass()
        {
            float speedMultiplier = GetSpeedMultiplier();
            float speedMultiplierPercentage = 1f * speedMultiplier;
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

        private void ConvertMass()
        {
            float speedMultiplier = GetSpeedMultiplier();
            float speedMultiplierPercentage = 1f * speedMultiplier;
            float totalConsumptionAmount = 1f;
            float waterConsumptionRate = Config.waterConsumptionRate * speedMultiplierPercentage * totalConsumptionAmount;
            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject storageItem = storage.items[i];
                if (storageItem != null && storageItem.HasTag(GameTags.AnyWater))
                {
                    PrimaryElement element = storageItem.GetComponent<PrimaryElement>();
                    float elementConsumptionAmount = Mathf.Min(waterConsumptionRate, element.Mass);
                    totalConsumptionAmount += elementConsumptionAmount / waterConsumptionRate;
                }
            }

            SimUtil.DiseaseInfo diseaseInfo = SimUtil.DiseaseInfo.Invalid;
            diseaseInfo.idx = byte.MaxValue;
            diseaseInfo.count = 0;

            bool pollutedWater = false;
            float totalConsumedAmount = 0f;

            waterConsumptionRate = Config.waterConsumptionRate * speedMultiplierPercentage * totalConsumptionAmount;
            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject storageItem = storage.items[i];
                if (storageItem != null && storageItem.HasTag(GameTags.AnyWater))
                {
                    if (storageItem.HasTag(SimHashes.DirtyWater.CreateTag())) {
                        pollutedWater = true;
                    }
                    PrimaryElement element = storageItem.GetComponent<PrimaryElement>();
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
            SimHashes oxygenHash = pollutedWater ? SimHashes.ContaminatedOxygen : SimHashes.Oxygen;
            float oxygenGenerated = gasFlowManager.AddElement(oxygenOutputCell, oxygenHash, Config.water2OxygenRatio, Mathf.Max(Config.oxygenTemperature, temperature), diseaseInfo.idx, diseaseInfo.count/2);
            ReportManager.Instance.ReportValue(ReportManager.ReportType.OxygenCreated, oxygenGenerated, base.gameObject.GetProperName());
            Game.Instance.accumulators.Accumulate(oxygenAccumulator, oxygenGenerated);
            float hydrogenGenerated = gasFlowManager.AddElement(hydrogenOutputCell, SimHashes.Hydrogen, Config.water2HydrogenRatio, Mathf.Max(Config.hydrogenTemperature, temperature), diseaseInfo.idx, diseaseInfo.count / 2);
            Game.Instance.accumulators.Accumulate(hydrogenAccumulator, hydrogenGenerated);
            storage.Trigger((int)GameHashes.OnStorageChange, base.gameObject);
        }

        private bool RoomForPressure
        {
            get
            {
                ConduitFlow flowManager = Conduit.GetFlowManager(portInfo.conduitType);
                return flowManager.IsConduitEmpty(hydrogenOutputCell) && flowManager.IsConduitEmpty(oxygenOutputCell);
            }
        }

        public CellOffset GetSecondaryConduitOffset()
        {
            return this.portInfo.offset;
        }

        public ConduitType GetSecondaryConduitType()
        {
            return this.portInfo.conduitType;
        }

        public AdvancedElectrolyzerConfig.Config Config
        {
            get
            {
                return AdvancedElectrolyzerConfig.config;
            }
        }

        public HandleVector<int>.Handle waterAccumulator { get; private set; }
        public HandleVector<int>.Handle oxygenAccumulator { get; private set; }
        public HandleVector<int>.Handle hydrogenAccumulator { get; private set; }
    }

}
