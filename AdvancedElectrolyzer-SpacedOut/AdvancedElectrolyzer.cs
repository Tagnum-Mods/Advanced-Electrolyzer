using KSerialization;


namespace TagnumElite
{
    namespace AdvancedElectrolyzer
    {
        [SerializationConfig(MemberSerialization.OptIn)]
        public class AdvancedElectrolyzer : AdvancedElectrolyzerMachine, ISecondaryOutput
        {
            public bool HasSecondaryConduitType(ConduitType type)
            {
                return portInfo.conduitType == type;
            }

            public CellOffset GetSecondaryConduitOffset(ConduitType type)
            {
                return portInfo.offset;
            }
        }
    }
}
