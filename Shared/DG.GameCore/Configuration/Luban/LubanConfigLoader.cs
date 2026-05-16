using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DG.GameCore
{
public static class LubanConfigLoader
{
    public static cfg.Tables LoadTables(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Config data directory is empty.", nameof(dataDirectory));
        }

        return new cfg.Tables(name => LoadArray(dataDirectory, name));
    }

    private static JArray LoadArray(string dataDirectory, string name)
    {
        string path = Path.Combine(dataDirectory, name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Luban config table not found.", path);
        }

        return JArray.Parse(File.ReadAllText(path));
    }
}
}
