using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

public class NeoUIElement
{
    private int _attachmentVersion, _traversalVersion;
    private bool _traversing, _hasSlot;
    private Vector2 _slotPosition, _slotSize;

    public NeoUIElement()
    {
        Children = new NeoUIChildren(this);
    }

    public NeoUIChildren Children { get; }
    public NeoUIElement? Parent { get; private set; }

    /// <summary>
    /// Position, Size, Anchor, Pivot, sizing modes, visual Rotation/Scale,  layout box, etc.
    /// </summary>
    public ScreenTransform ScreenTransform { get; } = new();

    /// <summary>Disables updates for this subtree, independent from the Visible attr.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Hides this subtree, independent from the Enabled attr.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>This is a matrix specifically for scale & rotate attr application visuals.</summary>
    public Matrix VisualMatrix { get; private set; } = Matrix.Identity;

    public virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>draws before children INSIDE the CALLER'S active screen-space batch</summary>
    protected virtual void OnDraw(SpriteBatch spriteBatch) { }

    /// <summary>The size of this element's own content, e.g. measured text. Fit uses the larger of this and the children.</summary>
    protected virtual Vector2 MeasureContent() => Vector2.Zero;

    /// <summary>Returns the smallest visible children size.</summary>
    private protected virtual Vector2 MeasureChildren()
    {
        Vector2 fit = Vector2.Zero;
        foreach (NeoUIElement child in Children)
        {
            if (!child.Visible) continue;
            
            ScreenTransform t = child.ScreenTransform;
            fit = Vector2.Max(fit, new Vector2(
                FitAxis(t.Anchor.X, t.Pivot.X, t.Position.X, t.DesiredSize.X),
                FitAxis(t.Anchor.Y, t.Pivot.Y, t.Position.Y, t.DesiredSize.Y)
            ));
        }
        return fit;
    }

    // note to self:
    // the smallest parent length that keeps the child inside [0, length] on one axis.
    // child left = anchor * length + position - pivot * size, child right = left + size.
    // right <= length and left >= 0, solved for length. a child that sticks out past
    // the parent's left/top edge can't be fixed by growing, so that case adds nothing
    private static float FitAxis(float anchor, float pivot, float position, float size)
    {
        float need = 0;
        if (anchor < 1) need = MathF.Max(need, (position + (1 - pivot) * size) / (1 - anchor));
        if (anchor > 0) need = MathF.Max(need, (pivot * size - position) / anchor);
        return need;
    }

    // bottom-up: children first, so a Fit parent can read their DesiredSize.
    // a Fill axis measures as its Size, then gets stretched when it is placed
    private void Measure()
    {
        foreach (NeoUIElement child in Children)
            if (child.Visible) child.Measure();

        Vector2 fit = Vector2.Max(MeasureContent(), MeasureChildren()) + ContentPadding.Total;
        ScreenTransform.DesiredSize = new Vector2(
            ScreenTransform.WidthMode == NeoUISizing.Fit ? fit.X : ScreenTransform.Size.X,
            ScreenTransform.HeightMode == NeoUISizing.Fit ? fit.Y : ScreenTransform.Size.Y);
    }

    /// <summary>The space between this element's edge and its children. NeoPanel returns its Padding.</summary>
    private protected virtual NeoPadding ContentPadding => default;

    /// <summary>The top-left of the padded box that children are laid out in.</summary>
    private protected Vector2 InnerPosition => ScreenTransform.LayoutPosition + ContentPadding.TopLeft;

    /// <summary>The size of the padded box that children are laid out in.</summary>
    private protected Vector2 InnerSize => Vector2.Max(Vector2.Zero, ScreenTransform.LayoutSize - ContentPadding.Total);

    /// <summary>The canvas at the root of this element's tree, or null when the element is not under a canvas.</summary>
    public NeoCanvas? Canvas
    {
        get
        {
            NeoUIElement root = this;
            while (root.Parent != null) root = root.Parent;
            return root as NeoCanvas;
        }
    }

    /// <summary>Converts a window-pixel point, e.g. Input.MousePosition, into this element's untransformed layout space.</summary>
    /// <remarks>Goes through the letterbox viewport, the canvas scale, and every Rotation/Scale on the way down. Uses the layout from the latest update or draw.</remarks>
    public Vector2 ScreenToLocal(Vector2 screen)
    {
        Vector2 canvas = Canvas?.ScreenToCanvas(screen) ?? screen;
        return Vector2.Transform(canvas, Matrix.Invert(VisualMatrix));
    }

    /// <summary>True when the window-pixel point is inside this element's box, as drawn (rotated and scaled).</summary>
    /// <remarks>Uses the layout and transforms from the latest update or draw, so a change shows up on the next pass. An element drawn at zero scale contains nothing.</remarks>
    public bool ContainsScreenPoint(Vector2 screen)
    {
        // a zero scale anywhere up the tree flattens the box to nothing and the matrix can't be inverted
        if (MathF.Abs(VisualMatrix.Determinant()) < 1e-12f) return false;
        Vector2 p = ScreenToLocal(screen);
        Vector2 min = ScreenTransform.LayoutPosition, max = min + ScreenTransform.LayoutSize;
        return p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y;
    }

    /// <summary>Places children before they are visited. Containers like NeoFlexBox override this.</summary>
    private protected virtual void ArrangeChildren() { }

    /// <summary>Gives a child its layout rectangle for this pass. Its Anchor, Pivot and Position are ignored.</summary>
    private protected static void Place(NeoUIElement child, Vector2 position, Vector2 size)
    {
        child._slotPosition = position;
        child._slotSize = size;
        child._hasSlot = true;
    }

    internal void AttachTo(NeoUIElement? parent)
    {
        Parent = parent;
        _hasSlot = false;
        InvalidateAttachment();
    }

    internal void InvalidateAttachment() => _attachmentVersion++;

    // note to self:
    // css-style visual transform: scale then rotate around the pivot point of the laid-out box.
    // row vectors (monogame), so the matrix reads left to right: move the pivot to the origin,
    // scale, rotate, move back. children multiply the parent's matrix on the right, so they
    // turn and scale with the parent
    private Matrix LocalVisualMatrix()
    {
        ScreenTransform t = ScreenTransform;
        Vector3 pivot = new(t.LayoutPosition + t.LayoutSize * t.Pivot, 0);
        return Matrix.CreateTranslation(-pivot) *
               Matrix.CreateScale(t.Scale.X, t.Scale.Y, 1) *
               Matrix.CreateRotationZ(t.Rotation) *
               Matrix.CreateTranslation(pivot);
    }

    internal void Visit(NeoUIBus bus, NeoCanvas root, GameTime? gameTime, SpriteBatch? spriteBatch, Point viewportSize)
    {
        bool drawing = spriteBatch != null;
        if (drawing ? !Visible : !Enabled) return;
        if (_traversing) throw new InvalidOperationException("A UI subtree cannot be traversed recursively.");

        _traversing = true;
        _traversalVersion = _attachmentVersion;
        Children.BeginTraversal();
        bool rebatched = false;
        Matrix restore = Matrix.Identity;
        try
        {
            if (!CanVisit(bus, root, drawing)) return;
            ScreenTransform t = ScreenTransform;
            if (ReferenceEquals(this, root))
            {
                t.LayoutSize = viewportSize.ToVector2() / root.Scale;
                t.LayoutPosition = Vector2.Zero;
                VisualMatrix = Matrix.Identity; // the canvas is the root, its own Rotation and Scale do nothing
                Measure();
            }
            else
            {
                if (_hasSlot)
                {
                    t.LayoutSize = _slotSize;
                    t.LayoutPosition = _slotPosition;
                }
                else
                {
                    Vector2 parentSize = Parent.InnerSize;
                    t.LayoutSize = new Vector2(
                        t.WidthMode == NeoUISizing.Fill ? parentSize.X : t.DesiredSize.X,
                        t.HeightMode == NeoUISizing.Fill ? parentSize.Y : t.DesiredSize.Y);
                    t.LayoutPosition = Parent.InnerPosition + parentSize * t.Anchor + t.Position - t.LayoutSize * t.Pivot;
                }

                VisualMatrix = t.HasVisualTransform ? LocalVisualMatrix() * Parent.VisualMatrix : Parent.VisualMatrix;

                // a rotated or scaled subtree needs its own batch matrix. the parent's matrix is captured
                // now so the finally block restores it even when a callback detaches this element
                if (drawing && t.HasVisualTransform)
                {
                    restore = Parent.VisualMatrix;
                    spriteBatch.End();
                    root.BeginBatch(spriteBatch, VisualMatrix);
                    rebatched = true;
                }
            }
            ArrangeChildren();

            if (drawing) OnDraw(spriteBatch);
            else OnUpdate(gameTime);

            while (CanVisit(bus, root, drawing) && Children.TryNext(out NeoUIElement child))
                child.Visit(bus, root, gameTime, spriteBatch, viewportSize);
        }
        finally
        {
            if (rebatched)
            {
                spriteBatch.End();
                root.BeginBatch(spriteBatch, restore);
            }
            Children.EndTraversal();
            _traversing = false;
        }
    }

    private bool CanVisit(NeoUIBus bus, NeoCanvas root, bool drawing)
    {
        if (!ReferenceEquals(root.Bus, bus)) return false;
        for (NeoUIElement element = this; element != null; element = element.Parent)
        {
            if (!element._traversing || element._attachmentVersion != element._traversalVersion ||
                (drawing ? !element.Visible : !element.Enabled)) return false;
            if (ReferenceEquals(element, root)) return true;
        }
        return false;
    }
}

/// <summary>How an element picks its layout size on one axis.</summary>
public enum NeoUISizing
{
    /// <summary>Use Size.</summary>
    Fixed,

    /// <summary>Take the parent's full length. Inside a NeoFlexBox: share the leftover space on the main axis, stretch on the cross axis.</summary>
    Fill,

    /// <summary>Shrink or grow to the content: measured text, or the box that holds the visible children.</summary>
    Fit
}

/// <summary>Owned children in render order. do remove a child before adding it to a different parent.</summary>
public sealed class NeoUIChildren : Collection<NeoUIElement>
{
    private readonly NeoUIElement _owner;
    private bool _traversing;
    private int _next, _end;

    internal NeoUIChildren(NeoUIElement owner) => _owner = owner;

    protected override void InsertItem(int index, NeoUIElement item)
    {
        ValidateChild(item);
        base.InsertItem(index, item);
        item.AttachTo(_owner);
        
        if (!_traversing) return;
        
        if (index < _next) _next++;
        if (index < _end) _end++;
    }

    protected override void SetItem(int index, NeoUIElement item)
    {
        if (ReferenceEquals(this[index], item)) return;
        ValidateChild(item);
        NeoUIElement old = this[index];
        base.SetItem(index, item);
        old.AttachTo(null);
        item.AttachTo(_owner);
    }

    protected override void RemoveItem(int index)
    {
        NeoUIElement old = this[index];
        base.RemoveItem(index);
        old.AttachTo(null);
        
        if (!_traversing) return;
        
        if (index < _next) _next--;
        if (index < _end) _end--;
    }

    protected override void ClearItems()
    {
        for (int i = 0; i < Count; i++) this[i].AttachTo(null);
        base.ClearItems();
        _next = _end = 0;
    }

    private void ValidateChild(NeoUIElement item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is NeoCanvas) throw new InvalidOperationException("Canvases are viewport roots, not children.");
        if (item.Parent != null) throw new InvalidOperationException("Remove the element from its current parent before reparenting it.");
        
        for (NeoUIElement ancestor = _owner; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(item, ancestor))
                throw new InvalidOperationException("A UI element cannot contain itself or an ancestor.");
        }
    }

    internal void BeginTraversal()
    {
        _traversing = true;
        _next = 0;
        _end = Count;
    }

    internal bool TryNext(out NeoUIElement child)
    {
        if (_next < _end)
        {
            child = this[_next++];
            return true;
        }
        
        child = null;
        return false;
    }

    internal void EndTraversal() => _traversing = false;
}
