using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public interface IGameConfigProvider
{
    bool TryGetArchetype(int configId, out EntityArchetype archetype);
    IReadOnlyList<EntitySpawnSpec> GetWorldSpawns(string worldId);
    bool TryGetPlayerSpawnRule(string ruleId, out PlayerSpawnRule rule);
}
}
