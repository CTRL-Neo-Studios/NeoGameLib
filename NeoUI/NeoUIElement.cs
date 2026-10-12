using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI;

public class NeoUIElement
{
    private Vector2 _position, _size, _anchor, _pivot;
    private int _attachmentVersion, _traversalVersion;
    private bool _traversing;

    public NeoUIElement()
    {
        Children = new NeoUIChildren(this);
    }

    public NeoUIChildren Children { get; }
    public NeoUIElement? Parent { get; private set; }

    /// <summary>Disables updates for this subtree, independent from the Visible attr.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Hides this subtree, independent from the Enabled attr.</summary>
    public bool Visible { get; set; } = true;

    // yay encapsulation
    public Vector2 Position
    {
        get => _position;
        set { ValidateVector(value, false); _position = value; }
    }

    public Vector2 Size
    {
        get => _size;
        set { ValidateVector(value, true); _size = value; }
    }

    public Vector2 Anchor
    {
        get => _anchor;
        set { ValidateNormalized(value); _anchor = value; }
    }

    public Vector2 Pivot
    {
        get => _pivot;
        set { ValidateNormalized(value); _pivot = value; }
    }

    /// <summary>the viewport-local pixel position from the latest update or draw</summary>
    public Vector2 LayoutPosition { get; private set; }
    public Vector2 LayoutSize { get; private set; }

    public virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>draws before children INSIDE the CALLER'S active screen-space batch</summary>
    protected virtual void OnDraw(SpriteBatch spriteBatch) { }

    protected virtual Vector2 MeasureSize() => Size;

    internal void AttachTo(NeoUIElement? parent)
    {
        Parent = parent;
        InvalidateAttachment();
    }

    internal void InvalidateAttachment() => _attachmentVersion++;

    internal void Visit(NeoUIBus bus, NeoCanvas root, GameTime? gameTime, SpriteBatch? spriteBatch, Point viewportSize)
    {
        bool drawing = spriteBatch != null;
        if (drawing ? !Visible : !Enabled) return;
        if (_traversing) throw new InvalidOperationException("A UI subtree cannot be traversed recursively.");

        _traversing = true;
        _traversalVersion = _attachmentVersion;
        Children.BeginTraversal();
        try
        {
            if (!CanVisit(bus, root, drawing)) return;
            LayoutSize = ReferenceEquals(this, root) ? viewportSize.ToVector2() : MeasureSize();
            LayoutPosition = ReferenceEquals(this, root) ? Vector2.Zero : Parent.LayoutPosition + Parent.LayoutSize * Anchor + Position - LayoutSize * Pivot;

            if (drawing) OnDraw(spriteBatch);
            else OnUpdate(gameTime);

            while (CanVisit(bus, root, drawing) && Children.TryNext(out NeoUIElement child))
                child.Visit(bus, root, gameTime, spriteBatch, viewportSize);
        }
        finally
        {
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

    private static void ValidateVector(Vector2 value, bool nonnegative)
    {
        if (
            !float.IsFinite(value.X) || 
            !float.IsFinite(value.Y) || 
            (nonnegative && (value.X < 0 || value.Y < 0))
        )
            throw new ArgumentOutOfRangeException(nameof(value), "UI coordinates must be finite and non-negative sizes");
    }

    private static void ValidateNormalized(Vector2 value)
    {
        ValidateVector(value, true);
        if (value.X > 1 || value.Y > 1) throw new ArgumentOutOfRangeException(nameof(value), "anchors and pivots must be between 0 and 1");
    }
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
