using System;

namespace NeoGameLib.NeoMapping;

/// <summary>
/// Marks a spec/action property or constructor parameter as a content asset reference for the editor to reference assets
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NeoAssetAttribute(Type assetType) : Attribute
{
    public Type AssetType { get; } = assetType;
}