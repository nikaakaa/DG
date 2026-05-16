using System.Collections;
using System.Collections.Generic;

namespace Luban
{
public abstract class BeanBase
{
    public abstract int GetTypeId();
}

public static class StringUtil
{
    public static string CollectionToString(IEnumerable collection)
    {
        if (collection == null)
        {
            return "null";
        }

        var values = new List<string>();
        foreach (object value in collection)
        {
            values.Add(value?.ToString() ?? "null");
        }

        return "[" + string.Join(",", values) + "]";
    }
}
}
