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

    /// <summary>'/'-separated (e.g. "terrain/slabs") editor grouping. empty string means library root.</summary>
    /// <remarks>This attr is ignored by the runtime and is NOT a part of the prefab's name. (because if it is a part of the prefab's name, moving this would break its references across the project.)</remarks>
    public string Folder { get; set; } = "";
}