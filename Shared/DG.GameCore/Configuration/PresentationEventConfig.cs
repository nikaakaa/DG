using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public readonly struct ActionPresentationConfig
{
    public ActionPresentationConfig(ActionSpecId actionSpecId, PresentationFactType factType)
    {
        ActionSpecId = actionSpecId;
        FactType = factType;
    }

    public ActionSpecId ActionSpecId { get; }
    public PresentationFactType FactType { get; }
    public bool IsValid => ActionSpecId.IsValid && FactType != PresentationFactType.Unknown;
}

public sealed class ActionPresentationRegistry
{
    private readonly Dictionary<ActionSpecId, ActionPresentationConfig> byActionSpecId;

    public ActionPresentationRegistry(IEnumerable<ActionPresentationConfig> configs)
    {
        byActionSpecId = new Dictionary<ActionSpecId, ActionPresentationConfig>();
        if (configs == null)
        {
            return;
        }

        foreach (ActionPresentationConfig config in configs)
        {
            if (config.IsValid)
            {
                byActionSpecId[config.ActionSpecId] = config;
            }
        }
    }

    public static ActionPresentationRegistry Default { get; } = LubanActionPresentationRegistry.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory());

    public bool TryGet(ActionSpecId actionSpecId, out ActionPresentationConfig config)
    {
        return byActionSpecId.TryGetValue(actionSpecId, out config);
    }
}
}
