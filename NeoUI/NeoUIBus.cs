using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI;

public sealed class NeoUIBus : IDisposable // I googled this: "c# best practice to clear resources that are temporary in a class" and `IDisposable` came up.
{
    private readonly List<NeoCanvas> _canvases = new();
    private Texture2D? _whitePixel;
    private long _registrationOrder;
    private bool _orderDirty, _traversing, _disposed;
    private int _next, _end;

    public int Count => _canvases.Count;
    public bool Contains(NeoCanvas canvas) => canvas != null && ReferenceEquals(canvas.Bus, this);

    /// <summary>Adds a canvas; dedupe=false permits multiple visits per update/draw. A canvas can belong to only one bus.</summary>
    public void Add(NeoCanvas canvas, bool dedupe = true) // this function used to be filled with a lot of if xx is null statements which was NOT readable at all. so I googled "is there a cleaner way to write if xxx is null throw exception in C#". i love googling
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        
        bool attached = ReferenceEquals(canvas.Bus, this);
        if (attached && dedupe) return;
        if (canvas.Bus != null && !attached)
            throw new InvalidOperationException("please remove the canvas from its current bus before re-parenting it");
        
        if (!attached)
        {
            canvas.RegistrationOrder = _registrationOrder++;
            canvas.AttachToBus(this);
        }
        _canvases.Add(canvas);
        _orderDirty = true;
    }

    /// <summary>Removes all registrations by default, or one with dedupe=false. Remaining registrations retain ownership.</summary>
    public void Remove(NeoCanvas canvas, bool dedupe = true)
    {
        if (!Contains(canvas)) return;
        
        for (int index = 0; index < _canvases.Count;)
        {
            if (!ReferenceEquals(_canvases[index], canvas))
            {
                index++;
                continue;
            }
            
            _canvases.RemoveAt(index);
            if (_traversing)
            {
                if (index < _next) _next--;
                if (index < _end) _end--;
            }
            
            if (!dedupe) break;
        }

        // Partial removals cannot detach a canvas that still has registrations.
        for (int i = 0; i < _canvases.Count; i++)
            if (ReferenceEquals(_canvases[i], canvas)) return;
        canvas.AttachToBus(null);
    }

    public void Clear()
    {
        for (int i = 0; i < _canvases.Count; i++) _canvases[i].AttachToBus(null);
        _canvases.Clear();
        _next = _end = 0;
    }

    public void Update(GameTime gameTime, Point viewportSize)
    {
        ArgumentNullException.ThrowIfNull(gameTime);
        if (viewportSize.X < 0 || viewportSize.Y < 0) throw new ArgumentOutOfRangeException(nameof(viewportSize));
        Visit(gameTime, null, viewportSize);
    }

    /// <summary>
    /// Draws in an already-begun, identity-transform SpriteBatch render pass using submission order.
    /// </summary>
    /// <remarks>the caller should own Begin/End batch calls. (ideally you shouldn't be using this in any other place)</remarks>
    public void Draw(SpriteBatch spriteBatch)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
        Visit(null, spriteBatch, new Point(viewport.Width, viewport.Height));
    }

    private void Visit(GameTime? gameTime, SpriteBatch? spriteBatch, Point viewportSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_traversing) throw new InvalidOperationException("UI Update and Draw cannot be called recursively.");
        
        if (_orderDirty)
        {
            _canvases.Sort(static (a, b) => // re-order the order stack if order is changed
            {
                int order = a.Order.CompareTo(b.Order);
                return order != 0 ? order : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
            });
            _orderDirty = false;
        }
        
        _traversing = true;
        _next = 0;
        _end = _canvases.Count;
        try
        {
            while (_next < _end)
            {
                NeoCanvas canvas = _canvases[_next++];
                canvas.Visit(this, canvas, gameTime, spriteBatch, viewportSize);
            }
        }
        finally
        {
            _traversing = false;
        }
    }

    internal void InvalidateOrder() => _orderDirty = true;

    internal Texture2D GetWhitePixel(GraphicsDevice graphicsDevice)
    {
        if (_whitePixel != null && !ReferenceEquals(_whitePixel.GraphicsDevice, graphicsDevice))
        {
            _whitePixel.Dispose();
            _whitePixel = null;
        }
        if (_whitePixel == null)
        {
            _whitePixel = new Texture2D(graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });
        }
        return _whitePixel;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _whitePixel?.Dispose();
        _whitePixel = null;
        _disposed = true;
    }
}
