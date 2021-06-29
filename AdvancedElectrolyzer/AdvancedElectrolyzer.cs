using KSerialization;

namespace TagnumElite
{
    namespace AdvancedElectrolyzer
    {
        [SerializationConfig(MemberSerialization.OptIn)]
        public class AdvancedElectrolyzer : AdvancedElectrolyzerMachine, ISecondaryOutput
        {
            public ConduitType GetSecondaryConduitType()
            {
                return portInfo.conduitType;
            }

            public CellOffset GetSecondaryConduitOffset()
            {
                return portInfo.offset;
            }
        }
    }
}
