using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoMapping.DurableSpecifications;

namespace NeoGameLib.NeoMapping.DurableMap;

[Serializable]
public sealed class NeoMapObject
{
    public string Name { get; set; } = "";
    public string Prefab { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<NeoSpecEntry> Specs { get; set; } = new();
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public Vector2 Scale { get; set; } = Vector2.One;
    public Dictionary<string, JsonObject> Overrides { get; set; } = new();
}