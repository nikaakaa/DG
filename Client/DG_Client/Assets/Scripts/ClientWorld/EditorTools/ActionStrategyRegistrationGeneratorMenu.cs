using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DG.GameCore;
using UnityEditor;
using UnityEngine;

public static class ActionStrategyRegistrationGeneratorMenu
{
    private const string GeneratedPath = "Shared/DG.GameCore/ActionRuntime/Generated/GeneratedActionStrategyRegistration.cs";

    [MenuItem("DG/Action/Generate Strategy Registration")]
    public static void Generate()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string outputPath = Path.Combine(root, GeneratedPath);
        IReadOnlyList<ActionStrategyRegistrationDescriptor> descriptors = DiscoverDescriptors();
        string source = ActionStrategyRegistrationGenerator.GenerateSource("DG.GameCore", "GeneratedActionStrategyRegistration", descriptors);
        File.WriteAllText(outputPath, source);
        AssetDatabase.Refresh();
    }

    private static IReadOnlyList<ActionStrategyRegistrationDescriptor> DiscoverDescriptors()
    {
        var descriptors = new List<ActionStrategyRegistrationDescriptor>();
        Type strategyType = typeof(IActionStrategy);
        Type attributeType = typeof(ActionStrategyAttribute);
        Type[] types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(type => type != null).Cast<Type>();
                }
            })
            .Where(type => type != null && !type.IsAbstract && strategyType.IsAssignableFrom(type))
            .ToArray()!;

        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            var attribute = (ActionStrategyAttribute)Attribute.GetCustomAttribute(type, attributeType);
            if (attribute == null)
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException("Action strategy missing parameterless constructor: " + type.FullName);
            }

            descriptors.Add(new ActionStrategyRegistrationDescriptor(attribute.StrategyKey, type.FullName ?? type.Name));
        }

        return descriptors;
    }
}
