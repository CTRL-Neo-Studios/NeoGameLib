using System;

namespace NeoGameLib.NeoMapping;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NeoNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}