namespace DG.GameCore
{
public struct DirectionComponent
{
    public DirectionComponent(Direction direction)
    {
        Direction = direction;
    }

    public Direction Direction { get; set; }
}
}
