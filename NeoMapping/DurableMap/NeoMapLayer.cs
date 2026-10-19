using System;
using System.Collections.Generic;

namespace NeoGameLib.NeoMapping.DurableMap;

[Serializable]
public sealed class NeoMapLayer
{
    public string Name { get; set; } = "";

    /// <summary>Legend character -> prefab name; set to null to make cell empty</summary>
    public Dictionary<string, string> Legend { get; set; } = new();

    public List<string> Rows { get; set; } = new();
}