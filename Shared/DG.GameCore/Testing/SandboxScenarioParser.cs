using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DG.GameCore
{
public static class SandboxScenarioParser
{
    public static bool TryParse(string json, IGameConfigProvider provider, out SandboxScenarioDocument document, out IReadOnlyList<string> errors)
    {
        document = default!;
        var errorList = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors = new[] { "json is empty" };
            return false;
        }

        try
        {
            JToken token = JToken.Parse(json);
            CollectForbiddenEntityDefinitions(token, errorList);
            document = token.ToObject<SandboxScenarioDocument>() ?? new SandboxScenarioDocument();
        }
        catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException)
        {
            errors = new[] { ex.Message };
            return false;
        }

        errorList.AddRange(SandboxScenarioValidator.Validate(document, provider));
        errors = errorList;
        return errorList.Count == 0;
    }

    private static void CollectForbiddenEntityDefinitions(JToken token, List<string> errors)
    {
        if (token is JObject obj)
        {
            foreach (JProperty property in obj.Properties())
            {
                string name = property.Name;
                if (string.Equals(name, "components", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "componentKinds", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "archetype", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "archetypeId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "tags", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("scenario must not define entity archetype data: " + name);
                }

                CollectForbiddenEntityDefinitions(property.Value, errors);
            }

            return;
        }

        if (token is JArray array)
        {
            foreach (JToken item in array)
            {
                CollectForbiddenEntityDefinitions(item, errors);
            }
        }
    }
}

}
