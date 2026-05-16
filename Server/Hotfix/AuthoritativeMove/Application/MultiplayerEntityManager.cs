using DG.GameCore;

namespace Fantasy;

public sealed class MultiplayerEntityManager<TSession> where TSession : class
{
    private readonly GameWorld World;
    private readonly IGameConfigProvider ConfigProvider;
    private readonly string PlayerSpawnRuleId;
    private readonly Dictionary<TSession, long> SessionEntities = new();
    private readonly Dictionary<long, TSession> EntitySessions = new();
    private long nextEntityId = 1;

    public MultiplayerEntityManager(GameWorld world) : this(world, LubanGameConfigProvider.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory()))
    {
    }

    public MultiplayerEntityManager(GameWorld world, IGameConfigProvider configProvider, string playerSpawnRuleId = DefaultWorldConfig.DefaultPlayerSpawnRuleId)
    {
        World = world;
        ConfigProvider = configProvider;
        PlayerSpawnRuleId = playerSpawnRuleId;
    }

    public int OnlineCount => SessionEntities.Count;

    public bool Join(TSession session, out PlayerEntitySnapshot snapshot)
    {
        TryJoin(session, out snapshot, out bool created, out _);
        return created;
    }

    public bool TryJoin(TSession session, out PlayerEntitySnapshot snapshot, out bool created, out string reason)
    {
        if (SessionEntities.TryGetValue(session, out long existingEntityId))
        {
            if (TryGetPlayerCoord(existingEntityId, out GridCoord existingCoord))
            {
                snapshot = new PlayerEntitySnapshot(existingEntityId, existingCoord);
                created = false;
                reason = string.Empty;
                return true;
            }

            SessionEntities.Remove(session);
            EntitySessions.Remove(existingEntityId);
        }

        long entityId = AllocateEntityId();
        if (!TryAllocateSpawnCoord(entityId, out GridCoord coord))
        {
            snapshot = default;
            created = false;
            reason = "no spawn coord";
            return false;
        }

        SessionEntities.Add(session, entityId);
        EntitySessions.Add(entityId, session);
        World.AddEntity(new EntitySpawnSpec(entityId, GetPlayerConfigId(), coord, Direction.None, entityId, 1));
        snapshot = new PlayerEntitySnapshot(entityId, coord);
        created = true;
        reason = string.Empty;
        return true;
    }

    public bool TryGetEntity(TSession session, out long entityId)
    {
        return SessionEntities.TryGetValue(session, out entityId);
    }

    public bool IsOwner(TSession session, long entityId)
    {
        return SessionEntities.TryGetValue(session, out long boundEntityId) && boundEntityId == entityId;
    }

    public bool TryAuthorizeMove(TSession session, long entityId, out long boundEntityId, out MoveErrorCode errorCode, out string reason)
    {
        if (!SessionEntities.TryGetValue(session, out boundEntityId))
        {
            errorCode = MoveErrorCode.NotJoined;
            reason = "session not joined";
            return false;
        }

        if (boundEntityId != entityId)
        {
            errorCode = MoveErrorCode.UnauthorizedEntity;
            reason = "unauthorized entity";
            return false;
        }

        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }

    public void RemoveSession(TSession session)
    {
        if (!SessionEntities.Remove(session, out long entityId))
        {
            return;
        }

        EntitySessions.Remove(entityId);
        World.RemoveEntity(entityId);
    }

    public IReadOnlyList<TSession> EnumerateAvailable(Func<TSession, bool> isAvailable)
    {
        var result = new List<TSession>();
        List<TSession>? removeSessions = null;
        foreach (TSession session in SessionEntities.Keys)
        {
            if (isAvailable(session))
            {
                result.Add(session);
                continue;
            }

            removeSessions ??= new List<TSession>();
            removeSessions.Add(session);
        }

        if (removeSessions == null)
        {
            return result;
        }

        foreach (TSession session in removeSessions)
        {
            RemoveSession(session);
        }

        return result;
    }

    public IReadOnlyList<PlayerEntitySnapshot> EnumerateSnapshots()
    {
        var result = new List<PlayerEntitySnapshot>();
        foreach (long entityId in EntitySessions.Keys.OrderBy(id => id))
        {
            if (TryGetPlayerCoord(entityId, out GridCoord coord))
            {
                result.Add(new PlayerEntitySnapshot(entityId, coord));
            }
        }

        return result;
    }

    private long AllocateEntityId()
    {
        while (EntitySessions.ContainsKey(nextEntityId) || World.TryGetEntity(nextEntityId, out _))
        {
            nextEntityId++;
        }

        return nextEntityId++;
    }

    private bool TryAllocateSpawnCoord(long entityId, out GridCoord coord)
    {
        if (!ConfigProvider.TryGetPlayerSpawnRule(PlayerSpawnRuleId, out PlayerSpawnRule rule))
        {
            coord = default;
            return false;
        }

        int startIndex = Math.Max(0, (int)entityId - 1);
        for (int offset = 0; offset < rule.MaxAttempts; offset++)
        {
            GridCoord candidate = rule.GetCandidate(startIndex + offset);
            if (World.CanEnterNewEntity(candidate))
            {
                coord = candidate;
                return true;
            }
        }

        coord = default;
        return false;
    }

    private int GetPlayerConfigId()
    {
        return ConfigProvider.TryGetPlayerSpawnRule(PlayerSpawnRuleId, out PlayerSpawnRule rule) ? rule.PlayerConfigId : DefaultWorldConfig.PlayerConfigId;
    }

    private bool TryGetPlayerCoord(long entityId, out GridCoord coord)
    {
        if (World.TryGetEntity(entityId, out GameEntity entity) &&
            World.HasComponent<PlayerControlComponent>(entity) &&
            World.TryGetComponent(entity, out PositionComponent position))
        {
            coord = position.Coord;
            return true;
        }

        coord = default;
        return false;
    }
}
