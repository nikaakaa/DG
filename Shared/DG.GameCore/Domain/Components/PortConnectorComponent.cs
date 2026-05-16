namespace DG.GameCore
{
public struct PortConnectorComponent
{
    public PortConnectorComponent(DirectionMask localPorts)
    {
        LocalPorts = localPorts;
    }

    public DirectionMask LocalPorts { get; }
}
}
