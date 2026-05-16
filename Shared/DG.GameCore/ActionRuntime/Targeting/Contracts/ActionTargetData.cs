namespace DG.GameCore
{
public readonly struct ActionTargetData
{
    public ActionTargetData(ActionTargetDataKind kind, long targetEntityId, GridCoord targetCoord, long bodyId, GridCoord hitCell, int hitOrder, Direction direction, string queryId)
    {
        Kind = kind;
        TargetEntityId = targetEntityId;
        TargetCoord = targetCoord;
        BodyId = bodyId;
        HitCell = hitCell;
        HitOrder = hitOrder;
        Direction = direction;
        QueryId = queryId ?? string.Empty;
    }

    public ActionTargetDataKind Kind { get; }
    public long TargetEntityId { get; }
    public GridCoord TargetCoord { get; }
    public long BodyId { get; }
    public GridCoord HitCell { get; }
    public int HitOrder { get; }
    public Direction Direction { get; }
    public string QueryId { get; }

    public static ActionTargetData Self(long entityId, GridCoord coord, Direction direction)
    {
        return new ActionTargetData(ActionTargetDataKind.Self, entityId, coord, entityId, coord, 0, direction, "self");
    }

    public static ActionTargetData Cell(string queryId, GridCoord coord, Direction direction, int hitOrder)
    {
        return new ActionTargetData(ActionTargetDataKind.Cell, 0, coord, 0, coord, hitOrder, direction, queryId);
    }

    public static ActionTargetData Entity(string queryId, long entityId, GridCoord coord, Direction direction, int hitOrder)
    {
        return new ActionTargetData(ActionTargetDataKind.Entity, entityId, coord, entityId, coord, hitOrder, direction, queryId);
    }
}
}
