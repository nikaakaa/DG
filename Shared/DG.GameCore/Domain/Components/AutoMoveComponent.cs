using System;

namespace DG.GameCore
{
public struct AutoMoveComponent
{
    public AutoMoveComponent(int intervalTicks)
    {
        IntervalTicks = Math.Max(1, intervalTicks);
        LastMoveTick = 0;
    }

    public int IntervalTicks { get; }
    public long LastMoveTick { get; set; }
}
}
