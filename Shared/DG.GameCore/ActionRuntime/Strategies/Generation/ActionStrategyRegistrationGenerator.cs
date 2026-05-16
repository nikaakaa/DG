using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ActionStrategyRegistrationDescriptor
{
    public ActionStrategyRegistrationDescriptor(ActionPrimitive primitive, string strategyKey, string typeName)
    {
        Primitive = primitive;
        StrategyKey = string.IsNullOrWhiteSpace(strategyKey) ? primitive.ToString() : strategyKey;
        TypeName = string.IsNullOrWhiteSpace(typeName) ? throw new ArgumentException("Strategy type name is empty.", nameof(typeName)) : typeName.Replace('+', '.');
    }

    public ActionPrimitive Primitive { get; }
    public string StrategyKey { get; }
    public string TypeName { get; }
}

public static class ActionStrategyRegistrationGenerator
{
    public static string GenerateSource(string namespaceName, string className, IEnumerable<ActionStrategyRegistrationDescriptor> descriptors)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace is empty.", nameof(namespaceName));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("Class name is empty.", nameof(className));
        }

        List<ActionStrategyRegistrationDescriptor> ordered = Validate(descriptors).OrderBy(item => item.StrategyKey, StringComparer.Ordinal).ToList();
        var lines = new List<string>
        {
            "namespace " + namespaceName,
            "{",
            "public static partial class " + className,
            "{",
            "    public static ActionStrategyRegistry CreateDefault()",
            "    {",
            "        var registry = new ActionStrategyRegistry();"
        };

        for (int i = 0; i < ordered.Count; i++)
        {
            lines.Add("        registry.Register(new " + ordered[i].TypeName + "());");
        }

        lines.Add("        return registry;");
        lines.Add("    }");
        lines.Add("}");
        lines.Add("}");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public static IReadOnlyList<ActionStrategyRegistrationDescriptor> Validate(IEnumerable<ActionStrategyRegistrationDescriptor> descriptors)
    {
        if (descriptors == null)
        {
            throw new ArgumentNullException(nameof(descriptors));
        }

        var result = descriptors.ToList();
        if (result.Count == 0)
        {
            throw new InvalidOperationException("No action strategies found.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < result.Count; i++)
        {
            ActionStrategyRegistrationDescriptor descriptor = result[i];
            if (string.IsNullOrWhiteSpace(descriptor.TypeName))
            {
                throw new InvalidOperationException("Action strategy type is empty.");
            }

            if (!keys.Add(descriptor.StrategyKey))
            {
                throw new InvalidOperationException("Duplicate action strategy key: " + descriptor.StrategyKey);
            }
        }

        return result;
    }
}
}
