using System;
using System.Text.Json.Nodes;

namespace NeoGameLib.NeoMapping.DurableSpecifications;

/// <summary>One specification entry in each prefab/collider group/object. set `Id` for spec overrides.</summary>
[Serializable]
public sealed class NeoSpecEntry
{
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
    public JsonObject Properties { get; set; } = new();
}