using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoMapping.DurableActions;
using NeoGameLib.NeoMapping.DurableSpecifications;

namespace NeoGameLib.NeoMapping;

/// <summary>
/// The registry finds spec and action types by scanning assemblies and converts the `{ "type": ..., "properties": ... }` json entries in map files into instances.
/// Anything public and non-abstract that's implemented INeoSpec or INeoAction interfaces is registered automatically upon assembly scan. Names default to the type name.
/// </summary>
/// <remarks>
/// This spawned because originally you'd run the game in order to generate the map, schema, project files; but since when you run the game and the game needs the map, it becomes a paradox.
/// So this had to be done during build-time. I needed to find a way that finds types and generated files during build-time, and have an external tool-script to collect them during build-time.
/// And so I went and googled "how to make tooling in C# that gets all built types" which led me to https://learn.microsoft.com/en-us/dotnet/standard/assembly/ , and searching "scanning types from C# assembly" gave this https://www.code4it.dev/blog/assembly-type-methodinfo-parameterinfo/
/// </remarks>
public static class NeoRegistry
{
    private static readonly Dictionary<string, Type> _specs = new();
    private static readonly Dictionary<string, Type> _actions = new();
    private static readonly Dictionary<Type, string> _names = new();
    private static readonly HashSet<Assembly> _scanned = new();
    private static bool _defaultsDone;

    public static JsonSerializerOptions Options { get; } = new()
    {
        IncludeFields = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new TypedEntryConverter<INeoSpec>(),
            new TypedEntryConverter<INeoAction>(),
        },
#if NET9_0_OR_GREATER
        RespectRequiredConstructorParameters = true,
#endif
    };

    static NeoRegistry()
    {
        EnsureDefaults();
    }

    public static IReadOnlyDictionary<string, Type> Specs => _specs;
    public static IReadOnlyDictionary<string, Type> Actions => _actions;

    public static string NameOf(Type type) => _names.TryGetValue(type, out string name) ? name : type.Name;

    public static Type Resolve(string name, Type kind)
    {
        Dictionary<string, Type> map = kind == typeof(INeoSpec) ? _specs : _actions;
        
        if (!map.TryGetValue(name, out Type type))
        {
            throw new InvalidOperationException($"unknown {NameOf(kind)} '{name}' — is the assembly that defines it referenced by the game?");
        }
        
        return type;
    }

    public static INeoSpec CreateSpec(string name, JsonNode properties)
    {
        return (INeoSpec)JsonSerializer.Deserialize(properties ?? new JsonObject(), Resolve(name, typeof(INeoSpec)), Options)!;
    }

    public static INeoAction CreateAction(string name, JsonNode properties)
    {
        return (INeoAction)JsonSerializer.Deserialize(properties ?? new JsonObject(), Resolve(name, typeof(INeoAction)), Options)!;
    }

    /// <summary>
    /// Adds every scanned spec/action type from the assembly
    /// </summary>
    public static void Scan(Assembly assembly)
    {
        if (assembly is null || !_scanned.Add(assembly)) return;

        Type[] types;
        try { types = assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        foreach (Type type in types)
        {
            if (type is null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
            if (typeof(INeoSpec).IsAssignableFrom(type)) Add(type, _specs, "spec");
            if (typeof(INeoAction).IsAssignableFrom(type)) Add(type, _actions, "action");
        }
    }

    private static void Add(Type type, Dictionary<string, Type> map, string kind)
    {
        string name = type.GetCustomAttribute<NeoNameAttribute>()?.Name ?? type.Name;
        if (map.TryGetValue(name, out Type existing) && existing != type)
            throw new InvalidOperationException($"{kind} name '{name}' is used by both {existing.FullName} and {type.FullName}; give one of them a [NeoName]");

        map[name] = type;
        _names[type] = name;
    }

    private static void EnsureDefaults()
    {
        if (_defaultsDone) return;
        _defaultsDone = true;

        Scan(typeof(INeoSpec).Assembly);

        Assembly entry = Assembly.GetEntryAssembly();
        if (entry is not null && entry != typeof(INeoSpec).Assembly) Scan(entry);
    }

    // an "entries" wrapper for map/prefab lists
    private static object CreateFromEntry(JsonObject entry, Type kind)
    {
        string type = (string)entry["type"] ?? throw new InvalidOperationException($"a {NameOf(kind)} entry is missing its 'type'");
        JsonNode? props = entry["properties"];
        return kind == typeof(INeoSpec) ? CreateSpec(type, props) : CreateAction(type, props);
    }

    private sealed class TypedEntryConverter<T> : JsonConverter<T> where T : class
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            JsonObject entry = JsonSerializer.Deserialize<JsonObject>(ref reader, options)!;
            return (T)NeoRegistry.CreateFromEntry(entry, typeof(T));
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", NeoRegistry.NameOf(value.GetType()));
            writer.WritePropertyName("properties");
            JsonSerializer.Serialize(writer, (object)value, options);
            writer.WriteEndObject();
        }
    }
}