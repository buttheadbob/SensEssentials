using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace SensEssentials;

/// <summary>
/// Checks block names against the definitions of the running world, so modded blocks count and a typo is reported.
/// Commands run in a loaded session, so the definitions are always there when this is called.
/// </summary>
public static class BlockLookup
{
    private static IEnumerable<MyCubeBlockDefinition> Blocks =>
        MyDefinitionManager.Static.GetAllDefinitions().OfType<MyCubeBlockDefinition>();

    public static bool IsKnownType(string name) => FindType(name).HasValue;

    public static bool IsKnownSubtype(string name) => Blocks.Any(b => IsSubtype(b, name));

    /// <summary>
    /// Reads a comma separated list of block types. Throws ArgumentException on anything unknown.
    /// </summary>
    public static HashSet<MyObjectBuilderType> ParseTypes(string text)
    {
        var types = new HashSet<MyObjectBuilderType>();
        foreach (var name in Split(text))
        {
            var type = FindType(name) ?? throw new ArgumentException($"Unknown block type '{name}'.");
            if (IsArmor(type))
                throw new ArgumentException($"'{name}' is armor, which conditions cannot check.");
            types.Add(type);
        }

        return types.Count > 0 ? types : throw new ArgumentException("No block type was given.");
    }

    /// <summary>
    /// Reads a comma separated list of block subtypes. Throws ArgumentException on anything unknown.
    /// </summary>
    public static HashSet<MyStringHash> ParseSubtypes(string text)
    {
        var subtypes = new HashSet<MyStringHash>();
        foreach (var name in Split(text))
        {
            var matches = Blocks.Where(b => IsSubtype(b, name)).ToList();
            if (matches.Count == 0)
                throw new ArgumentException($"Unknown block subtype '{name}'.");
            if (matches.All(b => IsArmor(b.Id.TypeId)))
                throw new ArgumentException($"'{name}' is armor, which conditions cannot check.");
            subtypes.UnionWith(matches.Select(b => b.Id.SubtypeId));
        }

        return subtypes.Count > 0 ? subtypes : throw new ArgumentException("No block subtype was given.");
    }

    private static IEnumerable<string> Split(string text) =>
        text.Split([","], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0);

    private static MyObjectBuilderType? FindType(string name)
    {
        var wanted = Utilities.NormalizeTypeName(name);
        foreach (var block in Blocks)
        {
            if (string.Equals(Utilities.NormalizeTypeName(block.Id.TypeId.ToString()), wanted, StringComparison.OrdinalIgnoreCase))
                return block.Id.TypeId;
        }

        return null;
    }

    private static bool IsSubtype(MyCubeBlockDefinition block, string name) =>
        string.Equals(block.Id.SubtypeName, name.Trim(), StringComparison.OrdinalIgnoreCase);

    // Armor has no block entity, so the fat block list that conditions read never contains it.
    private static bool IsArmor(MyObjectBuilderType type) => type == typeof(MyObjectBuilder_CubeBlock);
}
