using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct PortConnectorConfig
{
    public PortConnectorConfig(int configId, DirectionMask localPorts)
    {
        ConfigId = configId;
        LocalPorts = localPorts;
    }

    public int ConfigId { get; }
    public DirectionMask LocalPorts { get; }
}
}
