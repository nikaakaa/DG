using DG.GameCore;

namespace Fantasy;

public interface IIntentAuthorization<in TSession>
{
    InputIntentAuthorizationResult Authorize(TSession session, InputIntent intent, out long boundEntityId);
}

public sealed class PlayerIntentAuthorization<TSession> : IIntentAuthorization<TSession>
    where TSession : class
{
    private readonly MultiplayerEntityManager<TSession> players;

    public PlayerIntentAuthorization(MultiplayerEntityManager<TSession> players)
    {
        this.players = players;
    }

    public InputIntentAuthorizationResult Authorize(TSession session, InputIntent intent, out long boundEntityId)
    {
        boundEntityId = 0;
        if (intent.SourceKind != InputSourceKind.Player)
        {
            return InputIntentAuthorizationResult.Reject(MoveErrorCode.UnauthorizedEntity, "unsupported input source");
        }

        if (!players.TryAuthorizeMove(session, intent.ActorEntityId, out boundEntityId, out MoveErrorCode errorCode, out string reason))
        {
            return InputIntentAuthorizationResult.Reject(errorCode, reason);
        }

        return InputIntentAuthorizationResult.Accept();
    }
}
