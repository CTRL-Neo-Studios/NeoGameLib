using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoGameLib.NeoMapping;

/// <summary>
/// Represents a project file at Data/neo.project.json read by the editor and the tools.
/// </summary>
[Serializable]
public sealed class NeoProject
{
    public const string FileName = "neo.project.json";
    public const string DataFolder = "Data";
    public const string MapsFolder = "NeoMaps";
    public const string PrefabsFolder = "NeoPrefabs";

    public int Version { get; set; } = 1;

    /// <summary>references the schema file in Data/[game].neoschema.json</summary>
    public string Schema { get; set; } = "";

    /// <summary>the MonoGame content root relative to Data/ (default ../Content)</summary>
    public string ContentRoot { get; set; } = "../Content";

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extensions { get; set; } = new();

    /// <summary>
    /// The TitleContainer path of a map/library by name i.e. <c>DataPath(MapsFolder, "forest/level1", NeoMap.Extension)</c> -> <c>Data/NeoMaps/forest/level1.neomap.json</c>
    /// </summary>
    /// <remarks>the passed-in names might contain '/'-separated subfolders; see <see cref="NeoNames.ValidatePath"/> for allowed chars in regex rules</remarks>
    public static string DataPath(string folder, string name, string extension)
    {
        NeoNames.ValidatePath(name);
        return $"{DataFolder}/{folder}/{name}{extension}";
    }
}
