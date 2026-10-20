using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoMapping.DurablePrefabs;
using NeoGameLib.NeoMapping.DurableSpecifications;
using NeoGameLib.NeoMapping.Grouping;

namespace NeoGameLib.NeoMapping.DurableMap;

/// <summary>
/// A data-driven scene.
/// <para></para>
/// NeoMap is loaded from Data/NeoMaps/[name].neomap.json, and loads NeoPrefabs from Data/NeoMaps/[name].neomap.json.
/// It's a grid map with layers, tiles, prefab tiles, and tile specifications. Specifications are packets of data that can be mapped to load custom components.
/// There's already default primitives, but if you have custom components you want to read-from/write-to specifications, you can make custom specs as well, in the schema that describes a spec shape.
///
/// For the sake of performance, the final map tries to merge all tiles into a full tileset, so instead of instantiating every tile as an object, you get one big tile that runs logic/renders in one pass.
/// </summary>
[Serializable]
public sealed class NeoMap
{
    public const string Extension = ".neomap.json";

    [JsonPropertyName("cellSize")] public Point CellSize { get; set; } = new(32, 32);
    [JsonPropertyName("origin")] public Vector2 Origin { get; set; }

    /// <summary>Prefab library names resolved from Data/NeoPrefabs/[name].neoprefabs.json; names may include subfolders, e.g. "terrain/common".</summary>
    [JsonPropertyName("prefabs")] public List<string> Libraries { get; set; } = new();

    [JsonPropertyName("layers")] public List<NeoMapLayer> Layers { get; set; } = new();
    [JsonPropertyName("objects")] public List<NeoMapObject> Objects { get; set; } = new();

    // build-time resolution of libraries, filled on first Build
    [JsonIgnore] private Dictionary<string, NeoPrefab> _prefabs;
    [JsonIgnore] private Dictionary<string, NeoColliderGroup> _colliders;

    public static NeoMap Load(string name) => Load(name, null);

    /// <summary>Loads Data/NeoMaps/[name].neomap.json via file stream; name may include subfolders, e.g. "forest/level1".</summary>
    /// <remarks>Since the final map might be too big for json to parse, a streamed read is much safer</remarks>
    public static NeoMap Load(string name, Func<string, NeoPrefabLibrary> loader)
    {
        string path = NeoProject.DataPath(NeoProject.MapsFolder, name, Extension);
        using Stream stream = TitleContainer.OpenStream(path);
        NeoMap map = NeoJsonIO.Deserialize<NeoMap>(new StreamReader(stream).ReadToEnd(), path);
        map.Resolve(loader ?? DefaultLibraryLoader);
        return map;
    }

    /// <summary>Parses JSON string to map</summary>
    public static NeoMap FromJson(string json)
    {
        NeoMap map = NeoJsonIO.Deserialize<NeoMap>(json, "<string>");
        map.Resolve(DefaultLibraryLoader);
        return map;
    }

    public string ToJson() => NeoJsonIO.Serialize(this);

    /// <summary>Registers a world whose build callback loads a map file. mapName may include subfolders ("forest/level1"); the world name is separate and can stay short.</summary>
    public static NeoWorld RegisterWorld(string worldName, string mapName)
    {
        WorldManager manager = NeoGame.Singleton?.WorldManager ?? throw new InvalidOperationException("RegisterWorld requires a running NeoGame instance with an initialized World Manager");
        return manager.CreateWorld(worldName, w => Load(mapName).Build(w));
    }

    public Vector2 CellToWorld(int row, int col) => new(Origin.X + col * CellSize.X, Origin.Y + row * CellSize.Y);

    /// <summary>Instantiates every layer cell/merged collider/object into the world. Returns the created objects in their corresponding build order.</summary>
    public List<NeoObject> Build(NeoWorld world)
    {
        if (_prefabs is null || _colliders is null) Resolve(DefaultLibraryLoader);
        if (CellSize.X <= 0 || CellSize.Y <= 0) throw new InvalidOperationException("cellSize must be positive");

        List<NeoObject> created = new();

        foreach (NeoMapLayer layer in Layers) BuildLayer(world, layer, created);
        BuildColliders(world, created);
        BuildObjects(world, created);
        return created;
    }

    private void Resolve(Func<string, NeoPrefabLibrary> loader)
    {
        Dictionary<string, NeoPrefab> prefabs = new();
        Dictionary<string, NeoColliderGroup> colliders = new();
        foreach (string name in Libraries)
        {
            NeoPrefabLibrary library = loader(name);
            MergeInto(prefabs, library.Prefabs, $"library '{name}'");
            MergeInto(colliders, library.Colliders, $"library '{name}'");
        }
        _prefabs = prefabs;
        _colliders = colliders;
    }

    private static void MergeInto<T>(Dictionary<string, T> target, Dictionary<string, T> source, string sourceName)
    {
        if (source is null) return;
        foreach ((string key, T value) in source)
        {
            if (!target.TryAdd(key, value)) throw new InvalidOperationException($"duplicate prefab/collider '{key}' (again in {sourceName})");
        }
    }


    private static NeoPrefabLibrary DefaultLibraryLoader(string name) => NeoPrefabLibrary.Load(name);

    private void BuildLayer(NeoWorld world, NeoMapLayer layer, List<NeoObject> created)
    {
        Dictionary<char, string> legend = new();
        foreach ((string key, string prefab) in layer.Legend)
        {
            if (key.Length != 1) throw new InvalidOperationException($"layer '{layer.Name}': legend key '{key}' must be exactly one character");
            legend[key[0]] = prefab;
        }

        for (int row = 0; row < layer.Rows.Count; row++)
        {
            string line = layer.Rows[row];
            for (int col = 0; col < line.Length; col++)
            {
                if (!legend.TryGetValue(line[col], out string prefabName) || prefabName is null) continue;
                if (!_prefabs.TryGetValue(prefabName, out NeoPrefab def)) throw new InvalidOperationException($"layer '{layer.Name}' cell {row},{col} references missing prefab '{prefabName}'");

                NeoObject obj = world.Instantiate($"{layer.Name}-{row}-{col}", new Transform(CellToWorld(row, col)));
                ApplyTags(obj, def.Tags);
                ApplyEntries(obj, def.Specs, null);
                created.Add(obj);
            }
        }
    }

    /// <summary>
    /// merge cell colliders.
    /// </summary>
    /// <remarks>
    /// basically, merges the cells with the same collider group into one group, using as few amount of rectangles as possible.
    /// this runs a greedy-algorithm to merge cells: it goes throught the longest horizontal run of solid cells first, then populates downwards if the following matches.
    /// then the collider groups merge across different layers.
    /// </remarks>
    private void BuildColliders(NeoWorld world, List<NeoObject> created)
    {
        if (_colliders.Count == 0) return;

        int rows = 0, cols = 0;
        foreach (NeoMapLayer layer in Layers)
        {
            rows = Math.Max(rows, layer.Rows.Count);
            foreach (string line in layer.Rows) cols = Math.Max(cols, line.Length);
        }
        if (rows == 0 || cols == 0) return;

        Dictionary<string, bool[,]> masks = new();
        foreach (NeoMapLayer layer in Layers)
        {
            for (int row = 0; row < layer.Rows.Count; row++)
            {
                string line = layer.Rows[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (!layer.Legend.TryGetValue(line[col].ToString(), out string prefabName) || prefabName is null) continue;
                    if (!_prefabs.TryGetValue(prefabName, out NeoPrefab def)) continue;
                    if (string.IsNullOrEmpty(def.Collider)) continue;

                    if (!masks.TryGetValue(def.Collider, out bool[,] mask)) masks[def.Collider] = mask = new bool[rows, cols];
                    mask[row, col] = true;
                }
            }
        }
        
        /*
         * hey-o there future me. Just in case if you don't understand this, I'm gonna make a simple explanation and a diagram.
         * The algorithm starts with the first row, and expands right until it hits a boundary.
         * Once it reaches a boundary or a pre-merged collider group, it'll start propogating down with the same row-width until it hits a boundary/pre-merged collider group.
         * Once it's done, it'll repeat the process, starting with the first cell in the second row, then third, then fourth...
         *
         * Image this as the map, where each [] is a cell. The propogation starts at the "*"
         *   [*][][][][]
         * [][ ][][][][]
         * [][ ][][][][]
         * [][ ][][]
         *
         * Then it propogates right:
         *   [*][*][*][*][*]
         * [][ ][ ][ ][ ][ ]
         * [][ ][ ][ ][ ][ ]
         * [][ ][ ][ ]
         *
         * Then propogates downwards until it hits a boundary:
         *   [*][*][*][*][*]
         * [][*][*][*][*][*]
         * [][*][*][*][*][*]
         * [][ ][ ][ ]
         *
         * And it starts at the first-cell of the next row:
         *    [*][*][*][*][*]
         * [&][*][*][*][*][*]
         * [ ][*][*][*][*][*]
         * [ ][ ][ ][ ]
         *
         * Then down:
         *    [*][*][*][*][*]
         * [&][*][*][*][*][*]
         * [&][*][*][*][*][*]
         * [&][ ][ ][ ]
         *
         * Then starting at the next available cell of the next row:
         *    [*][*][*][*][*]
         * [&][*][*][*][*][*]
         * [&][*][*][*][*][*]
         * [&][^][ ][ ]
         *
         * ...then you get the idea. It's efficient enough for now.
         */
        
        // TODO: improve efficiency of propogating collider groups ONLY when needed.

        foreach ((string groupName, bool[,] mask) in masks)
        {
            if (!_colliders.TryGetValue(groupName, out NeoColliderGroup group)) throw new InvalidOperationException($"collider group '{groupName}' is used by a prefab but isn't defined in any prefab library");

            bool[,] covered = new bool[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (covered[row, col] || !mask[row, col]) continue;

                    int width = 1;
                    while (
                        col + width < cols && 
                        !covered[row, col + width] && 
                        mask[row, col + width]
                    )
                    {
                        width++;
                    }

                    int height = 1;
                    while (
                        row + height < rows && 
                        RunIsFree(mask, covered, row + height, col, width)
                    )
                    {
                        height++;
                    }

                    for (int r = row; r < row + height; r++)
                        for (int c = col; c < col + width; c++)
                            covered[r, c] = true;

                    Vector2 center = (CellToWorld(row, col) + CellToWorld(row + height - 1, col + width - 1)) / 2f;
                    NeoObject solid = world.Instantiate($"{groupName}-{row}-{col}", new Transform(center));
                    ApplyTags(solid, group.Tags);
                    solid.AddComponent(new NeoBoxCollider { Size = new Vector2(width * CellSize.X, height * CellSize.Y) });
                    ApplyEntries(solid, group.Specs, null);
                    created.Add(solid);
                }
            }
        }
    }

    private static bool RunIsFree(bool[,] mask, bool[,] covered, int row, int col, int width)
    {
        for (int c = col; c < col + width; c++)
            if (covered[row, c] || !mask[row, c]) return false;
        return true;
    }

    private void BuildObjects(NeoWorld world, List<NeoObject> created)
    {
        foreach (NeoMapObject obj in Objects)
        {
            NeoObject spawned = world.Instantiate(
                string.IsNullOrEmpty(obj.Name) ? "MapObject" : obj.Name,
                new Transform(obj.Position + Origin, obj.Rotation) { Scale = obj.Scale }
            );

            NeoPrefab prefab = null;
            if (!string.IsNullOrEmpty(obj.Prefab) && !_prefabs.TryGetValue(obj.Prefab, out prefab))
                throw new InvalidOperationException($"object '{obj.Name}' references missing prefab '{obj.Prefab}'");

            if (prefab is not null) ApplyTags(spawned, prefab.Tags);
            ApplyTags(spawned, obj.Tags);

            // the prefab's specs are overridable base entries, the object's own specs are additions
            if (prefab is not null) ApplyEntries(spawned, prefab.Specs, obj.Overrides);
            ApplyEntries(spawned, obj.Specs, null);

            created.Add(spawned);
        }
    }

    // overrides merge extra properties into entries with their id as key (or just uses spec name when there's no id) and applies that to every matching entry.
    // a null override clears the entry's properties back to the spec defaults. unknown override keys gives errors
    private static void ApplyEntries(NeoObject target, List<NeoSpecEntry> entries, Dictionary<string, JsonObject> overrides)
    {
        if (entries is null) return;

        HashSet<string> consumed = new();
        foreach (NeoSpecEntry entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Type)) throw new InvalidOperationException($"a spec entry on '{target.Name}' is missing its 'type'");

            string key = string.IsNullOrEmpty(entry.Id) ? entry.Type : entry.Id;
            JsonObject props = entry.Properties ?? new();
            if (overrides is not null && overrides.ContainsKey(key))
            {
                consumed.Add(key);
                props = (overrides[key] is JsonObject over) ? Merge(props, over) : new JsonObject();
            }

            try
            {
                NeoRegistry.CreateSpec(entry.Type, props).Apply(target);
            }
            catch (Exception ex) when (ex is InvalidOperationException or JsonException)
            {
                throw new InvalidOperationException($"'{target.Name}' (spec '{entry.Type}'): {ex.Message}", ex);
            }
        }

        if (overrides is not null)
        {
            foreach (string key in overrides.Keys)
            {
                if (!consumed.Contains(key)) throw new InvalidOperationException($"'{target.Name}': override '{key}' matches no spec entry");
            }
        }
    }

    private static JsonObject Merge(JsonObject baseProps, JsonObject over)
    {
        JsonObject merged = (JsonObject) baseProps.DeepClone();
        foreach ((string key, JsonNode value) in over) merged[key] = value?.DeepClone();
        
        return merged;
    }

    private static void ApplyTags(NeoObject target, List<string> tags)
    {
        if (tags is null) return;
        foreach (string tag in tags) target.Tags.Add(tag);
    }
}

internal static class NeoJsonIO
{
    public static T Deserialize<T>(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, NeoRegistry.Options) ?? throw new InvalidDataException($"empty or null document in {source}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"invalid JSON in {source}: {ex.Message}", ex);
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, NeoRegistry.Options);
}
