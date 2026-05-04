using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class LubanGameConfigProvider : IGameConfigProvider
{
    private readonly cfg.Tables tables;

    public LubanGameConfigProvider(cfg.Tables tables)
    {
        this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    public static LubanGameConfigProvider FromDirectory(string dataDirectory)
    {
        return new LubanGameConfigProvider(LubanConfigLoader.LoadTables(dataDirectory));
    }

    public bool TryGetArchetype(int configId, out EntityArchetype archetype)
    {
        cfg.gamecore.EntityArchetype row = tables.TbEntityArchetype.GetOrDefault(configId);
        if (row == null)
        {
            archetype = default!;
            return false;
        }

        archetype = ConvertArchetype(row);
        return true;
    }

    public IReadOnlyList<EntitySpawnSpec> GetWorldSpawns(string worldId)
    {
        if (string.IsNullOrEmpty(worldId))
        {
            return Array.Empty<EntitySpawnSpec>();
        }

        return tables.TbWorldSpawn.DataList
            .Where(row => row.WorldId == worldId)
            .Select(ConvertSpawn)
            .ToArray();
    }

    public bool TryGetPlayerSpawnRule(string ruleId, out PlayerSpawnRule rule)
    {
        cfg.gamecore.PlayerSpawnRule row = tables.TbPlayerSpawnRule.GetOrDefault(ruleId);
        if (row == null)
        {
            rule = default;
            return false;
        }

        rule = ConvertPlayerSpawnRule(row);
        return true;
    }

    private static EntityArchetype ConvertArchetype(cfg.gamecore.EntityArchetype row)
    {
        return new EntityArchetype(
            row.ConfigId,
            row.ArchetypeId,
            (int)row.EntityTarget,
            row.Components.Select(component => (ComponentKind)(int)component).ToArray(),
            row.Tags.ToArray(),
            row.DefaultAutoMoveIntervalTicks);
    }

    private static EntitySpawnSpec ConvertSpawn(cfg.gamecore.WorldSpawn row)
    {
        return new EntitySpawnSpec(
            row.EntityId,
            row.ConfigId,
            new GridCoord(row.X, row.Y),
            (Direction)(int)row.Direction,
            0,
            row.AutoMoveIntervalTicks);
    }

    private static PlayerSpawnRule ConvertPlayerSpawnRule(cfg.gamecore.PlayerSpawnRule row)
    {
        return new PlayerSpawnRule(
            row.RuleId,
            row.PlayerConfigId,
            new GridCoord(row.StartX, row.StartY),
            new GridCoord(row.StepX, row.StepY),
            row.MaxAttempts);
    }
}
}
