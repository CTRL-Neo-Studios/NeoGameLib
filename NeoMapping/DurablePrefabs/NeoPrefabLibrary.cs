using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoMapping.DurableMap;
using NeoGameLib.NeoMapping.Grouping;

namespace NeoGameLib.NeoMapping.DurablePrefabs;

[Serializable]
public sealed class NeoPrefabLibrary
{
    public const string Extension = ".neoprefabs.json";

    public Dictionary<string, NeoPrefab> Prefabs { get; set; } = new();
    public Dictionary<string, NeoColliderGroup> Colliders { get; set; } = new();

    /// <summary>Loads Data/NeoPrefabs/[name].neoprefabs.json. name might include subfolders i.e. "terrain/common"</summary>
    public static NeoPrefabLibrary Load(string name)
    {
        string path = NeoProject.DataPath(NeoProject.PrefabsFolder, name, Extension);
        using Stream stream = TitleContainer.OpenStream(path);
        return Validated(NeoJsonIO.Deserialize<NeoPrefabLibrary>(new StreamReader(stream).ReadToEnd(), path), path);
    }

    public static NeoPrefabLibrary FromJson(string json) => Validated(NeoJsonIO.Deserialize<NeoPrefabLibrary>(json, "<string>"), "<string>");

    // the prefab/collider keys and prefab folders that fulfills NeoNames' key regex rules; checks on load
    private static NeoPrefabLibrary Validated(NeoPrefabLibrary library, string source)
    {
        try
        {
            foreach ((string name, NeoPrefab prefab) in library.Prefabs ?? new())
            {
                NeoNames.ValidateKey(name, "prefab name");
                if (!string.IsNullOrEmpty(prefab?.Folder))
                    foreach (string segment in prefab.Folder.Split('/'))
                        NeoNames.ValidateKey(segment, $"folder segment of prefab '{name}'");
            }
            foreach (string name in (library.Colliders ?? new()).Keys)
                NeoNames.ValidateKey(name, "collider group name");
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException($"{source}: {ex.Message}", ex);
        }
        return library;
    }

    public string ToJson() => NeoJsonIO.Serialize(this);
}