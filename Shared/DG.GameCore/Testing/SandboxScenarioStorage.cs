using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
public static class SandboxScenarioStorage
{
    public static string ToJson(SandboxScenarioDocument document)
    {
        return JsonConvert.SerializeObject(document, Formatting.Indented);
    }

    public static void Save(string path, SandboxScenarioDocument document)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path is empty", nameof(path));
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToJson(document));
    }

    public static bool TryLoad(string path, IGameConfigProvider provider, out SandboxScenarioDocument document, out IReadOnlyList<string> errors)
    {
        document = default!;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            errors = new[] { "scenario file missing" };
            return false;
        }

        return SandboxScenarioParser.TryParse(File.ReadAllText(path), provider, out document, out errors);
    }
}

}
