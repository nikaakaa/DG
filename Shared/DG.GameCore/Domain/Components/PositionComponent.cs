namespace DG.GameCore
{
public struct PositionComponent
{
    public PositionComponent(GridCoord coord)
    {
        Coord = coord;
    }

    public GridCoord Coord { get; set; }
}
}
