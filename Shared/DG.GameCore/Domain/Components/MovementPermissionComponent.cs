namespace DG.GameCore
{
public struct MovementPermissionComponent
{
    public MovementPermissionComponent(bool canMove, bool canBePushed)
    {
        CanMove = canMove;
        CanBePushed = canBePushed;
    }

    public bool CanMove { get; }
    public bool CanBePushed { get; }
}
}
