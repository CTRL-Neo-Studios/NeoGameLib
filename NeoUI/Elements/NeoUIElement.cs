using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

// I need to practice how to write comments.

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

    public ScreenTransform ScreenTransform { get; } = new();

    /// <summary>Disables updates for this subtree but keeps drawing it. Independent of Visible.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Hides this subtree, like css `display: none`: no drawing, no updates (so no clicks), no layout space.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Matrix applying this element's rotation/scale, for drawing and hit-testing.</summary>
    public Matrix VisualMatrix { get; private set; } = Matrix.Identity;

    public virtual void OnUpdate(GameTime gameTime) { }

    protected virtual void OnDraw(SpriteBatch spriteBatch) { }

    /// <summary>This element's own content size, e.g. measured text. Fit takes the larger of this and the children.</summary>
    protected virtual Vector2 MeasureContent() => Vector2.Zero;

    /// <summary>Gets smallest box that holds all visible children.</summary>
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

    // Gets smallest parent length that keeps the child inbetween 0 & length on one axis.
    private static float FitAxis(float anchor, float pivot, float position, float size)
    {
        float need = 0;
        if (anchor < 1) need = MathF.Max(need, (position + (1 - pivot) * size) / (1 - anchor));
        if (anchor > 0) need = MathF.Max(need, (pivot * size - position) / anchor);
        return need;
    }

    // Bottom-up: children is iterated first so that a parent with Fit sizing mode can account for their DesiredSize.
    // Fill measures into Size attr and is stretched on placement.
    private void Measure()
    {
        foreach (NeoUIElement child in Children)
            if (child.Visible) child.Measure();

        Vector2 fit = Vector2.Max(MeasureContent(), MeasureChildren()) + ContentPadding.Total;
        ScreenTransform.DesiredSize = new Vector2(
            DesiredAxis(ScreenTransform.WidthMode, ScreenTransform.Size.X, fit.X),
            DesiredAxis(ScreenTransform.HeightMode, ScreenTransform.Size.Y, fit.Y)
        );
    }

    // Auto = Size when set, content otherwise (css `width: auto`).
    // Fill measures the same way: the parent stretches it on placement, but a Fit parent still needs its content size (css does the same)
    private static float DesiredAxis(NeoUISizing mode, float size, float fit) => mode switch
    {
        NeoUISizing.Fit => fit,
        NeoUISizing.Fixed => size,
        _ => size > 0 ? size : fit,
    };

    private protected virtual NeoPadding ContentPadding => default;

    /// <summary>Top-left of the padded box children are laid out in.</summary>
    private protected Vector2 InnerPosition => ScreenTransform.LayoutPosition + ContentPadding.TopLeft;

    /// <summary>Size of the padded box children are laid out in.</summary>
    private protected Vector2 InnerSize => Vector2.Max(Vector2.Zero, ScreenTransform.LayoutSize - ContentPadding.Total);

    public NeoCanvas? Canvas
    {
        get
        {
            NeoUIElement root = this;
            while (root.Parent != null) root = root.Parent;
            return root as NeoCanvas;
        }
    }

    /// <summary>Converts a window-pixel point (e.g. Input.MousePosition) to this element's layout space.</summary>
    public Vector2 ScreenToLocal(Vector2 screen)
    {
        return Vector2.Transform(Canvas?.ScreenToCanvas(screen) ?? screen, Matrix.Invert(VisualMatrix));
    }

    /// <summary>Returns true when the window-pixel point is inside this element's DRAWN box</summary>
    public bool ContainsScreenPoint(Vector2 screen)
    {
        // Zeros scale up the tree flattens the box
        if (MathF.Abs(VisualMatrix.Determinant()) < 1e-12f) return false;
        
        Vector2 p = ScreenToLocal(screen);
        Vector2 min = ScreenTransform.LayoutPosition, max = min + ScreenTransform.LayoutSize;
        return p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y;
    }

    /// <summary>Places children before they are visited -- mainly for containers like NeoFlexBox to override.</summary>
    private protected virtual void ArrangeChildren() { }

    /// <summary>Gives a child its layout rect for this pass</summary>
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

    // Scale & rotate around the pivot of the box.
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
        if (!Visible || (!drawing && !Enabled)) return;
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
                VisualMatrix = Matrix.Identity; // canvas is root; its own Rotation/Scale do nothing
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
                        t.HeightMode == NeoUISizing.Fill ? parentSize.Y : t.DesiredSize.Y
                    );
                    t.LayoutPosition = Parent.InnerPosition + parentSize * t.Anchor + t.Position - t.LayoutSize * t.Pivot;
                }

                VisualMatrix = t.HasVisualTransform ? LocalVisualMatrix() * Parent.VisualMatrix : Parent.VisualMatrix;

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
            {
                child.Visit(bus, root, gameTime, spriteBatch, viewportSize);
            }
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
                !element.Visible || (!drawing && !element.Enabled)) return false;
            if (ReferenceEquals(element, root)) return true;
        }
        return false;
    }
}

/// <summary>Children in render order.</summary>
/// <remarks>Remove before reparenting.</remarks>
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
