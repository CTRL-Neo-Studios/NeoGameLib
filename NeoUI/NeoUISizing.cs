namespace NeoGameLib.NeoUI;

public enum NeoUISizing
{
    /// <summary>Uses the element's Size attr instead.</summary>
    Fixed,
    /// <summary>Resizes to the parent's full length. If set on a NeoFlexBox, it would share the "leftover" space on the main axis (like how the flex html css class works) and stretch itself on the cross axis.</summary>
    Fill,
    /// <summary>Resizes itself to content size.</summary>
    Fit
}