using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public interface IGameConfigProvider
{
    bool TryGetArchetype(int configId, out EntityArchetype archetype);
    IReadOnlyList<EntityArchetype> GetEntityArchetypes();
    IReadOnlyList<EntitySpawnSpec> GetWorldSpawns(string worldId);
    bool TryGetPlayerSpawnRule(string ruleId, out PlayerSpawnRule rule);
    bool TryGetPortConnector(int configId, out PortConnectorConfig config);
    bool TryGetPushOnEnter(int configId, out PushOnEnterConfig config);
}
}
