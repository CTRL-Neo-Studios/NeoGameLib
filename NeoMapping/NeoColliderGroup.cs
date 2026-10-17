using System.Collections.Generic;
using NeoGameLib.NeoMapping.DurableSpecifications;

namespace NeoGameLib.NeoMapping;

/// <summary>This is for merging colliders into groups. every cell that references it will try to merge into as few boxes as possible</summary>
public sealed class NeoColliderGroup
{
    public List<string> Tags { get; set; } = new();
    public List<NeoSpecEntry> Specs { get; set; } = new();
}