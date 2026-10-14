using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoUI.Elements;

/// <summary>
/// A panel that automatically orders its children in a row or a column. like the css flex-styled divs
/// And now you're gonna ask me "how to center a panel like a div"
/// </summary>
public class NeoFlexBox : NeoPanel
{
    public NeoFlexDirection Direction { get; set; } = NeoFlexDirection.Row;

    /// <summary>gap in pixels between children on the main axis.</summary>
    public float Gap { get; set; }

    /// <summary>Where the children go on the main axis.</summary>
    public NeoFlexAlign Justify { get; set; } = NeoFlexAlign.Start;

    /// <summary>Where each child goes on the cross axis.</summary>
    public NeoFlexAlign Align { get; set; } = NeoFlexAlign.Start;

    private bool Row => Direction == NeoFlexDirection.Row;
    private float Main(Vector2 v) => Row ? v.X : v.Y;
    private float Cross(Vector2 v) => Row ? v.Y : v.X;
    private Vector2 Join(float main, float cross) => Row ? new Vector2(main, cross) : new Vector2(cross, main);
    private bool FillsMain(NeoUIElement e) => (Row ? e.ScreenTransform.WidthMode : e.ScreenTransform.HeightMode) == NeoUISizing.Fill;
    private bool FillsCross(NeoUIElement e) => (Row ? e.ScreenTransform.HeightMode : e.ScreenTransform.WidthMode) == NeoUISizing.Fill;

    private protected override Vector2 MeasureChildren()
    {
        float main = 0, cross = 0;
        int count = 0;
        foreach (NeoUIElement child in Children)
        {
            if (!child.Visible) continue;
            main += Main(child.ScreenTransform.DesiredSize);
            cross = MathHelper.Max(cross, Cross(child.ScreenTransform.DesiredSize));
            count++;
        }

        if (count > 1) main += Gap * (count - 1);
        return Join(main, cross);
    }

    private protected override void ArrangeChildren()
    {
        int count = 0, fills = 0;
        float used = 0;
        foreach (NeoUIElement child in Children)
        {
            if (!child.Visible) continue;
            
            count++;
            
            if (FillsMain(child)) fills++;
            else used += Main(child.ScreenTransform.DesiredSize);
        }
        if (count == 0) return;

        float innerMain = Main(InnerSize), innerCross = Cross(InnerSize);
        float leftover = MathHelper.Max(0, innerMain - used - Gap * (count - 1));
        float fillShare = fills > 0 ? leftover / fills : 0;
        if (fills > 0) leftover = 0;

        float gap = Gap, cursor = 0;
        switch (Justify)
        {
            case NeoFlexAlign.Center: cursor = leftover / 2; break;
            case NeoFlexAlign.End: cursor = leftover; break;
            case NeoFlexAlign.SpaceBetween when count > 1: gap += leftover / (count - 1); break;
        }

        Vector2 origin = InnerPosition;
        foreach (NeoUIElement child in Children)
        {
            if (!child.Visible) continue;

            float main = FillsMain(child) ? fillShare : Main(child.ScreenTransform.DesiredSize);
            bool stretch = FillsCross(child) || Align is NeoFlexAlign.Stretch or NeoFlexAlign.SpaceBetween;
            float cross = stretch ? innerCross : Cross(child.ScreenTransform.DesiredSize);
            float crossOffset = Align switch
            {
                NeoFlexAlign.Center => (innerCross - cross) / 2,
                NeoFlexAlign.End => innerCross - cross,
                _ => 0
            };

            Place(child, origin + Join(cursor, crossOffset), Join(main, cross));
            cursor += main + gap;
        }
    }
}
