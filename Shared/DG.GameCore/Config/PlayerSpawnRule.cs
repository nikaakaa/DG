using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct PlayerSpawnRule
{
    public PlayerSpawnRule(string ruleId, int playerConfigId, GridCoord startCoord, GridCoord stepCoord, int maxAttempts)
    {
        RuleId = ruleId ?? string.Empty;
        PlayerConfigId = playerConfigId;
        StartCoord = startCoord;
        StepCoord = stepCoord;
        MaxAttempts = Math.Max(1, maxAttempts);
    }

    public string RuleId { get; }
    public int PlayerConfigId { get; }
    public GridCoord StartCoord { get; }
    public GridCoord StepCoord { get; }
    public int MaxAttempts { get; }

    public GridCoord GetCandidate(int index)
    {
        return new GridCoord(StartCoord.X + StepCoord.X * index, StartCoord.Y + StepCoord.Y * index);
    }
}
}
