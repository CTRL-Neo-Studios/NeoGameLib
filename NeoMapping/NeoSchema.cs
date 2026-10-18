using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NeoGameLib.NeoMapping.DurableActions;
using NeoGameLib.NeoMapping.DurableMap;
using NeoGameLib.NeoMapping.DurablePrefabs;
using NeoGameLib.NeoMapping.DurableSpecifications;

namespace NeoGameLib.NeoMapping;

/// <summary>
/// Generates the game's data schema
/// </summary>
public static class NeoSchema
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions Indented = new(NeoRegistry.Options) { WriteIndented = true };

    /// <summary>Builds the schema for a game assembly. The assembly's spec/action types are discovered first.</summary>
    public static JsonObject Generate(Assembly gameAssembly)
    {
        NeoRegistry.Scan(gameAssembly);
        return new Builder().Build();
    }

    /// <summary>
    /// creates the Data/ folder structure (and folders + default neo.project.json if missing), then writes the schema next to the project file. Returns a short report string.
    /// </summary>
    public static string ExportProject(Assembly gameAssembly, string projectDir)
    {
        NeoRegistry.Scan(gameAssembly);

        string dataDir = Path.Combine(projectDir, NeoProject.DataFolder);
        Directory.CreateDirectory(Path.Combine(dataDir, NeoProject.MapsFolder));
        Directory.CreateDirectory(Path.Combine(dataDir, NeoProject.PrefabsFolder));

        string projectPath = Path.Combine(dataDir, NeoProject.FileName);
        NeoProject project;
        if (File.Exists(projectPath))
        {
            project = NeoJsonIO.Deserialize<NeoProject>(File.ReadAllText(projectPath), projectPath);
        }
        else
        {
            project = new NeoProject { Schema = $"{gameAssembly.GetName().Name}.neoschema.json" };
            File.WriteAllText(projectPath, NeoJsonIO.Serialize(project));
        }

        JsonObject schema = new Builder().Build();
        string schemaPath = Path.Combine(dataDir, project.Schema);
        string content = schema.ToJsonString(Indented);

        // write only on change so editors watching the file don't reload on every tool build
        if (!File.Exists(schemaPath) || File.ReadAllText(schemaPath) != content) File.WriteAllText(schemaPath, content);

        return $"{NeoRegistry.Specs.Count} specs, {NeoRegistry.Actions.Count} actions -> {schemaPath}";
    }

    private sealed class Builder
    {
        private readonly JsonObject _defs = new();

        public JsonObject Build()
        {
            // GetTypeInfo only resolves through an explicitly configured resolver
            NeoRegistry.Options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

            // collects registry entry keys
            List<string> specNames = NeoRegistry.Specs.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
            List<string> actionNames = NeoRegistry.Actions.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

            // propogating names
            foreach (string name in specNames)
                ObjectDef(NeoRegistry.Specs[name], MemberKey("spec", name));
            foreach (string name in actionNames)
                ObjectDef(NeoRegistry.Actions[name], MemberKey("action", name));

            JsonObject root = new()
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["$id"] = $"urn:neogamelib:neoschema:{Version}",
                ["title"] = "NeoGameLib data schema",
                ["x-neo-schema-version"] = Version,
                ["x-neo-specs"] = new JsonArray(specNames.Select(n => (JsonNode) n).ToArray()),
                ["x-neo-actions"] = new JsonArray(actionNames.Select(n => (JsonNode) n).ToArray()),
                ["x-neo-files"] = new JsonObject
                {
                    ["neomap"] = TypeSchema(typeof(NeoMap)),
                    ["neoprefabs"] = TypeSchema(typeof(NeoPrefabLibrary)),
                    ["project"] = TypeSchema(typeof(NeoProject)),
                },
            };
            root["$defs"] = _defs;
            return root;
        }

        /// <summary>
        /// converts type to schema (used recursively)
        /// </summary>
        /// <returns>Returns a JSON Schema of the type</returns>
        private JsonObject TypeSchema(Type type)
        {
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (underlying is not null)
            {
                return new JsonObject
                {
                    ["anyOf"] = new JsonArray(TypeSchema(underlying), new JsonObject { ["type"] = "null" }),
                };
            }

            // no nullable-reference annotations: every reference-type member accepts null at runtime,
            // so its schema must too (legend values are `string | null`, prefab lists may be absent/null, ...)
            JsonObject body = BuildTypeSchema(type);
            return type.IsValueType ? body : new JsonObject
            {
                ["anyOf"] = new JsonArray(body, new JsonObject { ["type"] = "null" }),
            };
        }

        private JsonObject BuildTypeSchema(Type type)
        {
            if (type == typeof(string)) return new JsonObject { ["type"] = "string" };
            if (type == typeof(bool)) return new JsonObject { ["type"] = "boolean" };
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
                return new JsonObject { ["type"] = "integer" };
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return new JsonObject { ["type"] = "number" };
            if (type == typeof(JsonObject) || type == typeof(JsonNode))
                return new JsonObject(); // free-form JSON, no constraints

            if (type == typeof(INeoSpec) || type == typeof(NeoSpecEntry))
                return Ref(SpecEntryDef());
            if (type == typeof(INeoAction))
                return Ref(ActionEntryDef());

            if (type.IsEnum)
            {
                JsonNamingPolicy policy = NeoRegistry.Options.PropertyNamingPolicy;
                JsonArray values = new(Enum.GetNames(type).Select(n => (JsonNode)(policy?.ConvertName(n) ?? n)).ToArray());
                return new JsonObject { ["type"] = "string", ["enum"] = values };
            }

            JsonTypeInfo info = NeoRegistry.Options.GetTypeInfo(type);
            switch (info.Kind)
            {
                case JsonTypeInfoKind.Enumerable:
                {
                    Type? element = ElementTypeOf(type);
                    return new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = element is null ? new JsonObject() : TypeSchema(element),
                    };
                }
                case JsonTypeInfoKind.Dictionary:
                {
                    Type? value = DictionaryValueOf(type);
                    return new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = value is null ? new JsonObject() : TypeSchema(value),
                    };
                }
                case JsonTypeInfoKind.Object:
                    return ObjectDef(type);
                default:
                    // an unregistered type
                    return new JsonObject();
            }
        }

        // ---- object definitions (specs, actions, DTOs, MonoGame structs) ----
        
        private JsonObject ObjectDef(Type type) => ObjectDef(type, KeyFor(type));

        private JsonObject ObjectDef(Type type, string key)
        {
            if (_defs.ContainsKey(key)) return Ref(key);

            _defs[key] = new JsonObject(); // cycle placeholder: recursive references point at this slot

            JsonObject schema = type == typeof(NeoSpecEntry) ? SpecEntryDefBody() : ObjectDefBody(type);
            _defs[key] = schema;
            return Ref(key);
        }

        private JsonObject ObjectDefBody(Type type)
        {
            JsonTypeInfo info = NeoRegistry.Options.GetTypeInfo(type);
            ConstructorInfo? ctor = SelectConstructor(type);
            Dictionary<string, ParameterInfo> paramByName = ctor?.GetParameters()
                .ToDictionary(p => p.Name!, p => p, StringComparer.OrdinalIgnoreCase);
            object? sample = MakeSample(type);

            JsonObject properties = new();
            JsonArray required = new();

            foreach (JsonPropertyInfo prop in info.Properties)
            {
                if (prop.IsExtensionData) continue;

                // skip readonly deserialized member
                string memberName = (prop.AttributeProvider as MemberInfo)?.Name ?? prop.Name;
                ParameterInfo param = null;
                paramByName?.TryGetValue(memberName, out param);
                if (prop.Set is null && param is null) continue;

                JsonObject member = TypeSchema(prop.PropertyType);
                object? def = DefaultFor(param, prop, sample);
                if (def is not null)
                    member["default"] = JsonSerializer.SerializeToNode(def, prop.PropertyType, NeoRegistry.Options);

                NeoAssetAttribute? asset = FindAsset(param, prop);
                if (asset is not null)
                    member["x-neo-asset"] = asset.AssetType.Name;

                if (param is not null && !param.HasDefaultValue)
                    required.Add(prop.Name);

                properties[prop.Name] = member;
            }

            JsonObject schema = new()
            {
                ["type"] = "object",
                ["properties"] = properties,
                // JsonExtensionData fields (NeoProject.Extensions) for future sections
                ["additionalProperties"] = info.Properties.Any(p => p.IsExtensionData),
            };
            if (required.Count > 0) schema["required"] = required;
            return schema;
        }

        private static object? DefaultFor(ParameterInfo? param, JsonPropertyInfo prop, object? sample)
        {
            if (param is not null && param.HasDefaultValue)
            {
                object? def = param.DefaultValue;
                
                if (def is null && param.ParameterType.IsValueType) def = Activator.CreateInstance(param.ParameterType); // to default if null
                if (def is not null && param.ParameterType.IsEnum) def = Enum.ToObject(param.ParameterType, def); // enums defaults to their integers
                
                return def;
            }

            return sample is null ? null : prop.Get?.Invoke(sample);
        }

        private static NeoAssetAttribute? FindAsset(ParameterInfo? param, JsonPropertyInfo prop)
        {
            if (param is not null && param.GetCustomAttribute<NeoAssetAttribute>() is NeoAssetAttribute onParam) return onParam;
            
            return (prop.AttributeProvider as ICustomAttributeProvider)?
                .GetCustomAttributes(typeof(NeoAssetAttribute), false)
                .FirstOrDefault() as NeoAssetAttribute;
        }

        private static ConstructorInfo? SelectConstructor(Type type)
        {
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            ConstructorInfo? marked = ctors.FirstOrDefault(c => c.GetCustomAttribute<JsonConstructorAttribute>() is not null);
            if (marked is not null) return marked;

            ConstructorInfo? parameterless = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
            if (parameterless is not null || type.IsValueType) return parameterless;
            return ctors.Length == 1 ? ctors[0] : null;
        }

        private static object? MakeSample(Type type)
        {
            try
            {
                if (type.IsValueType) return Activator.CreateInstance(type);
                return type.GetConstructor(Type.EmptyTypes)?.Invoke(null);
            }
            catch
            {
                return null;
            }
        }

        private static Type? ElementTypeOf(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }
            
            return null;
        }

        private static Type? DictionaryValueOf(Type type)
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                    return iface.GetGenericArguments()[1];
            }
            return null;
        }

        // spec/action entry unions: { "type": "Name", "properties": <that spec's schema> }
        // i remember i done a project before similar to this where its like user can write custom schemas and make custom widgets for them to make up a dashboard
        // that was inspired by YASB Reborn, but the dashboard also used the custom schema for a custom graph-based execution programming blueprint
        private string SpecEntryDef()
        {
            string key = "entry:spec";
            if (!_defs.ContainsKey(key)) _defs[key] = SpecEntryDefBody();
            return key;
        }

        private JsonObject SpecEntryDefBody()
        {
            JsonObject union = EntryUnion("spec", NeoRegistry.Specs);
            // map-level entries & nested specs carry an optional id for overrides
            return new JsonObject
            {
                ["allOf"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject { ["id"] = new JsonObject { ["type"] = new JsonArray("string", "null") } },
                    },
                    union),
            };
        }

        private string ActionEntryDef()
        {
            string key = "entry:action";
            if (!_defs.ContainsKey(key)) _defs[key] = EntryUnion("action", NeoRegistry.Actions);
            return key;
        }

        private JsonObject EntryUnion(string kind, IReadOnlyDictionary<string, Type> members)
        {
            JsonArray oneOf = new();
            foreach ((string name, Type type) in members.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                JsonObject memberDef = _defs[MemberKey(kind, name)] as JsonObject;
                JsonObject variant = new()
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["type"] = new JsonObject { ["const"] = name },
                        ["properties"] = Ref(MemberKey(kind, name)),
                    },
                    ["required"] = new JsonArray("type"),
                };
                if (memberDef is not null && memberDef.ContainsKey("required"))
                    variant["required"] = new JsonArray("type", "properties");
                oneOf.Add(variant);
            }

            return new JsonObject { ["oneOf"] = oneOf };
        }

        // helper funcs
        private JsonObject Ref(string defKey) => new() { ["$ref"] = $"#/$defs/{defKey}" };

        private static string MemberKey(string kind, string name) => $"{kind}:{name}";

        private static string KeyFor(Type type) => type.FullName!;
    }
}
