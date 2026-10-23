namespace NeoGameLib.NeoUI;

public enum NeoUISizing
{
    /// <summary>The default, like css `width: auto`: uses the Size attr on this axis when it's set (greater than 0), otherwise sizes itself to its content.</summary>
    Auto,
    /// <summary>Always uses the element's Size attr, even when it's 0.</summary>
    Fixed,
    /// <summary>Resizes to the parent's full length. If set on a NeoFlexBox, it would share the "leftover" space on the main axis (like how the flex html css class works) and stretch itself on the cross axis.</summary>
    Fill,
    /// <summary>Always resizes itself to its content size, ignoring the Size attr.</summary>
    Fit
}