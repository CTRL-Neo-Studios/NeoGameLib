namespace NeoGameLib.NeoUI.Elements;

/// <summary>Where children places/aligns themselves on an axis.</summary>
public enum NeoFlexAlign
{
    Start,
    Center,
    End,
    /// <summary>tailwind css `justify-between` class</summary>
    SpaceBetween,
    /// <summary>every child tries to fill themselves equally in the space.</summary>
    Stretch
}