using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct PrimitiveRunnerContext
{
    public PrimitiveRunnerContext(GameWorld world, ActionRequest request, ActionSpec spec, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
        : this(world, request, spec, proposals, moveRequests, actionResults, reasons, serverTick, null, null)
    {
    }

    public PrimitiveRunnerContext(GameWorld world, ActionRequest request, ActionSpec spec, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick, IGameConfigProvider? configProvider)
        : this(world, request, spec, proposals, moveRequests, actionResults, reasons, serverTick, configProvider, null)
    {
    }

    public PrimitiveRunnerContext(GameWorld world, ActionRequest request, ActionSpec spec, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick, IGameConfigProvider? configProvider, IEnumerable<TargetFilterSpec>? targetFilters)
    {
        World = world;
        Request = request;
        Spec = spec;
        Proposals = proposals;
        MoveRequests = moveRequests;
        ActionResults = actionResults;
        Reasons = reasons;
        ServerTick = serverTick;
        ConfigProvider = configProvider;
        TargetFilters = targetFilters == null ? new[] { TargetFilterSpec.None } : targetFilters.ToArray();
    }

    public GameWorld World { get; }
    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public List<CommitProposal> Proposals { get; }
    public List<ActionRequest> MoveRequests { get; }
    public Dictionary<long, MoveResult> ActionResults { get; }
    public List<string> Reasons { get; }
    public long ServerTick { get; }
    public IGameConfigProvider? ConfigProvider { get; }
    public IReadOnlyList<TargetFilterSpec> TargetFilters { get; }
}
}
