using System;

namespace DG.GameCore
{
public struct PushOnEnterComponent
{
    public PushOnEnterComponent(ActionSpecId outputSpecId, int outputCostTicks = 1)
    {
        OutputSpecId = outputSpecId;
        OutputCostTicks = Math.Max(1, outputCostTicks);
    }

    public ActionSpecId OutputSpecId { get; }
    public int OutputCostTicks { get; }
}
}
