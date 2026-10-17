using System;
using System.Collections.Generic;
using NeoGameLib.NeoMapping.DurableSpecifications;

namespace NeoGameLib.NeoMapping.DurablePrefabs;

[Serializable]
public sealed class NeoPrefab
{
    public List<string> Tags { get; set; } = new();
    public List<NeoSpecEntry> Specs { get; set; } = new();

    /// <summary>Name of the collider group that this prefab's cells merges into.</summary>
    public string Collider { get; set; } = "";
}