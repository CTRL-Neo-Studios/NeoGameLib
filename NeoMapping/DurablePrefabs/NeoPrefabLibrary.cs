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

    public static NeoPrefabLibrary Load(string name)
    {
        string path = $"{NeoProject.DataFolder}/{NeoProject.PrefabsFolder}/{name}{Extension}";
        using Stream stream = TitleContainer.OpenStream(path);
        return NeoJsonIO.Deserialize<NeoPrefabLibrary>(new StreamReader(stream).ReadToEnd(), path);
    }

    public static NeoPrefabLibrary FromJson(string json) => NeoJsonIO.Deserialize<NeoPrefabLibrary>(json, "<string>");

    public string ToJson() => NeoJsonIO.Serialize(this);
}