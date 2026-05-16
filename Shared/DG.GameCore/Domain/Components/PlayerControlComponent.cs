namespace DG.GameCore
{
public struct PlayerControlComponent
{
    public PlayerControlComponent(long playerId)
    {
        PlayerId = playerId;
    }

    public long PlayerId { get; }
}
}
