using System;
using System.Linq;

namespace DG.GameCore
{
public static class LubanActionPresentationRegistry
{
    public static ActionPresentationRegistry FromDirectory(string dataDirectory)
    {
        return FromTables(LubanConfigLoader.LoadTables(dataDirectory));
    }

    public static ActionPresentationRegistry FromTables(cfg.Tables tables)
    {
        if (tables == null)
        {
            throw new ArgumentNullException(nameof(tables));
        }

        return new ActionPresentationRegistry(tables.TbActionPresentation.DataList.Select(Convert).ToArray());
    }

    private static ActionPresentationConfig Convert(cfg.gamecore.ActionPresentation row)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        if (!Enum.TryParse(row.FactType, true, out PresentationFactType factType))
        {
            factType = PresentationFactType.Unknown;
        }

        return new ActionPresentationConfig(row.ActionSpecId, factType);
    }
}
}
