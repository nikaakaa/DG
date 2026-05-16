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

    public IReadOnlyList<EntityArchetype> GetEntityArchetypes()
    {
        return tables.TbEntityArchetype.DataList
            .Select(ConvertArchetype)
            .OrderBy(archetype => archetype.ConfigId)
            .ToArray();
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

    public bool TryGetPortConnector(int configId, out PortConnectorConfig config)
    {
        cfg.gamecore.PortConnectorConfig row = tables.TbPortConnectorConfig.GetOrDefault(configId);
        if (row == null)
        {
            config = default;
            return false;
        }

        config = new PortConnectorConfig(row.ConfigId, (DirectionMask)row.Ports);
        return true;
    }

    public bool TryGetPushOnEnter(int configId, out PushOnEnterConfig config)
    {
        cfg.gamecore.PushOnEnterConfig row = tables.TbPushOnEnterConfig.GetOrDefault(configId);
        if (row == null)
        {
            config = default;
            return false;
        }

        if (string.IsNullOrWhiteSpace(row.OutputSpecId) || tables.TbActionSpec.GetOrDefault(row.OutputSpecId) == null)
        {
            throw new InvalidOperationException("PushOnEnter output action spec missing: " + configId + " -> " + row.OutputSpecId);
        }

        config = new PushOnEnterConfig(row.ConfigId, row.OutputSpecId, row.OutputCostTicks);
        return true;
    }

    public bool TryGetEffectSpec(EffectSpecId effectSpecId, out EffectSpec spec)
    {
        cfg.gamecore.EffectSpec row = tables.TbEffectSpec.GetOrDefault(effectSpecId.ToString());
        if (row == null)
        {
            spec = null!;
            return false;
        }

        spec = ConvertEffectSpec(row);
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

    private static EffectSpec ConvertEffectSpec(cfg.gamecore.EffectSpec row)
    {
        return new EffectSpec(
            row.EffectId,
            (EffectKind)(int)row.Kind,
            (EffectTargetBinding)(int)row.TargetBinding,
            (EffectDurationPolicy)(int)row.DurationPolicy,
            (EffectStackPolicy)(int)row.StackPolicy,
            (EffectRemovePolicy)(int)row.RemovePolicy,
            row.DurationTicks,
            row.AutoMoveIntervalTicks,
            (DirectionMask)row.PortMask,
            row.CanMove,
            row.CanBePushed,
            ParseTag(row.Tag),
            row.CueId,
            row.StatPayloadId);
    }

    private static WorldTag ParseTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return WorldTag.None;
        }

        if (!Enum.TryParse(value, out WorldTag tag))
        {
            throw new InvalidOperationException("Unknown effect tag: " + value);
        }

        return tag;
    }
}
}
