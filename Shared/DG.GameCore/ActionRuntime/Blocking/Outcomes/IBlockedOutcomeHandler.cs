namespace DG.GameCore
{
public interface IBlockedOutcomeHandler
{
    void Apply(BlockedOutcomeContext context, BlockedResultDecision decision, ActionArbitrationResult result);
}
}
