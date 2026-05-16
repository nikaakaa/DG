using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
public sealed class SandboxEntityPaletteEntry
{
    public SandboxEntityPaletteEntry(EntityArchetype archetype)
    {
        Archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
        ComponentSummary = string.Join(",", archetype.Components.Select(component => component.ToString()));
        TagSummary = string.Join(",", archetype.Tags);
    }

    public EntityArchetype Archetype { get; }
    public int ConfigId => Archetype.ConfigId;
    public int ArchetypeId => Archetype.ArchetypeId;
    public int EntityTarget => Archetype.EntityTarget;
    public IReadOnlyList<ComponentKind> Components => Archetype.Components;
    public IReadOnlyList<string> Tags => Archetype.Tags;
    public int DefaultAutoMoveIntervalTicks => Archetype.DefaultAutoMoveIntervalTicks;
    public string ComponentSummary { get; }
    public string TagSummary { get; }
}

public sealed class SandboxEntityPalette
{
    private readonly IReadOnlyList<SandboxEntityPaletteEntry> entries;

    private SandboxEntityPalette(IReadOnlyList<SandboxEntityPaletteEntry> entries)
    {
        this.entries = entries;
    }

    public IReadOnlyList<SandboxEntityPaletteEntry> Entries => entries;

    public static SandboxEntityPalette FromProvider(IGameConfigProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        return new SandboxEntityPalette(provider.GetEntityArchetypes()
            .Select(archetype => new SandboxEntityPaletteEntry(archetype))
            .OrderBy(entry => entry.ConfigId)
            .ToArray());
    }

    public IReadOnlyList<SandboxEntityPaletteEntry> Search(string text, ComponentKind? componentKind = null)
    {
        string query = text ?? string.Empty;
        return entries
            .Where(entry => Matches(entry, query, componentKind))
            .ToArray();
    }

    private static bool Matches(SandboxEntityPaletteEntry entry, string query, ComponentKind? componentKind)
    {
        if (componentKind.HasValue && !entry.Components.Contains(componentKind.Value))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(entry.ConfigId.ToString(), query) ||
            Contains(entry.ArchetypeId.ToString(), query) ||
            entry.Tags.Any(tag => Contains(tag, query)) ||
            entry.Components.Any(component => Contains(component.ToString(), query));
    }

    private static bool Contains(string text, string query)
    {
        return text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

}
