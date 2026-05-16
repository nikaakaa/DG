namespace DG.GameCore
{
public struct TagSetComponent
{
    public TagSetComponent(WorldTag tags)
    {
        Tags = tags;
    }

    public WorldTag Tags { get; set; }

    public bool Has(WorldTag tag)
    {
        return tag != WorldTag.None && (Tags & tag) == tag;
    }

    public TagSetComponent Add(WorldTag tag)
    {
        return new TagSetComponent(Tags | tag);
    }

    public TagSetComponent Remove(WorldTag tag)
    {
        return new TagSetComponent(Tags & ~tag);
    }
}
}
