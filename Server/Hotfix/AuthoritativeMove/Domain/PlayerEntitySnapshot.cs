using DG.GameCore;

namespace Fantasy;

public readonly struct PlayerEntitySnapshot
{
    public readonly long EntityId;
    public readonly GridCoord Coord;

    public PlayerEntitySnapshot(long entityId, GridCoord coord)
    {
        EntityId = entityId;
        Coord = coord;
    }
}
