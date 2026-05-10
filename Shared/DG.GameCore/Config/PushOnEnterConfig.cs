using System;

namespace DG.GameCore
{
public readonly struct PushOnEnterConfig
{
    public PushOnEnterConfig(int configId, ActionSpecId outputSpecId, int outputCostTicks)
    {
        if (!outputSpecId.IsValid)
        {
            throw new ArgumentException("PushOnEnter output action spec is required.", nameof(outputSpecId));
        }

        if (outputCostTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputCostTicks), "PushOnEnter output cost must be positive.");
        }

        ConfigId = configId;
        OutputSpecId = outputSpecId;
        OutputCostTicks = outputCostTicks;
    }

    public int ConfigId { get; }
    public ActionSpecId OutputSpecId { get; }
    public int OutputCostTicks { get; }
}
}
