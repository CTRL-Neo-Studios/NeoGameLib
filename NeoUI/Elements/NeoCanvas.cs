using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI.Elements;

/// <summary>
/// A standalone viewport root for UI Elements.
/// </summary>
/// <remarks>
/// <para>The canvas layout size is `viewport size / Scale`. The children's anchors, padding, and flexbox and the Fit size mode doesn't change with the resolution because UI elements use UI units instead of pixel units.</para>
/// <para>Each canvas draws in its own SpriteBatch pass with their scales determined by their Scale attr.</para>
/// </remarks>
public class NeoCanvas : NeoUIElement
{
    private int _order;
    private float _scaleFactor = 1f, _match;
    private Vector2 _referenceResolution = new(1280, 720);

    /// <summary>Lower order canvasses renders first</summary>
    public int Order
    {
        get => _order;
        set
        {
            if (_order == value) return;
            _order = value;
            Bus?.InvalidateOrder();
        }
    }

    public NeoCanvasScaleMode ScaleMode { get; set; } = NeoCanvasScaleMode.ConstantPixelSize;

    /// <summary>
    /// Viewport pixels per UI unit in ConstantPixelSize mode. Ideally it should be positive AND finite.
    /// Set to 1 by default
    /// </summary>
    public float ScaleFactor
    {
        get => _scaleFactor;
        set => _scaleFactor = (float.IsFinite(value) && value > 0) ? 
            value : throw new ArgumentOutOfRangeException(nameof(value), "the scale factor must be positive AND finite");
    }

    public Vector2 ReferenceResolution
    {
        get => _referenceResolution;
        set => _referenceResolution = (float.IsFinite(value.X) && float.IsFinite(value.Y) && value.X > 0 && value.Y > 0) ?
            value : throw new ArgumentOutOfRangeException(nameof(value), "the reference resolution must be positive AND finite");
    }

    /// <summary>
    /// For ScaleWithScreen mode: 0 follows the viewport width, 1 follows the viewport height, and values between blend the two.
    /// </summary>
    public float Match
    {
        get => _match;
        set => _match = (value >= 0 && value <= 1) ? 
            value : throw new ArgumentOutOfRangeException(nameof(value), "match must be between 0 and 1");
    }

    public bool PixelPerfect { get; set; }

    public float Scale { get; private set; } = 1f;

    /// <summary>position of the window pixel that is top-left of the viewport on this canvas</summary>
    public Vector2 ViewportOrigin { get; private set; }

    public NeoUIBus? Bus { get; private set; }
    internal long RegistrationOrder { get; set; }

    /// <summary>Converts a window-pixel point, e.g. Input.MousePosition, into this canvas's UI units.</summary>
    /// <remarks>Accounts for the letterbox bars and Scale.</remarks>
    public Vector2 ScreenToCanvas(Vector2 screen) => (screen - ViewportOrigin) / Scale;

    /// <summary>Converts a point in this canvas's UI units into window pixels.</summary>
    public Vector2 CanvasToScreen(Vector2 canvas) => canvas * Scale + ViewportOrigin;

    /// <summary>Registers this canvas for UI updates and drawing.</summary>
    /// <param name="bus">Defaults to the attached bus, then NeoGame.Singleton.UIBus. No-op if neither exists.</param>
    /// <param name="dedupe">When true, an existing registration is not added again. False allows repeated updates and draws.</param>
    public void Subscribe(NeoUIBus bus = null, bool dedupe = true)
    {
        (bus ?? Bus ?? NeoGame.Singleton?.UIBus)?.Add(this, dedupe);
    }

    /// <summary>Unregisters this canvas without destroying its elements or assets.</summary>
    /// <param name="bus">Defaults to the attached bus, then NeoGame.Singleton.UIBus.</param>
    /// <param name="dedupe">When true, removes all registrations. False removes one, keeping the canvas attached until the last is removed.</param>
    public void Unsubscribe(NeoUIBus bus = null, bool dedupe = true)
    {
        (bus ?? Bus ?? NeoGame.Singleton?.UIBus)?.Remove(this, dedupe);
    }

   internal void UpdateScale(Rectangle viewport)
    {
        ViewportOrigin = new Vector2(viewport.X, viewport.Y);
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        float scale = _scaleFactor;
        if (ScaleMode == NeoCanvasScaleMode.ScaleWithScreen)
        {
            float width = MathF.Log2(viewport.Width / _referenceResolution.X);
            float height = MathF.Log2(viewport.Height / _referenceResolution.Y);
            scale = MathF.Pow(2, MathHelper.Lerp(width, height, _match));
        }

        // log2/pow round-trips land a hair under whole numbers (1280/256 -> 4.9999995), so snap before flooring or the UI drops a whole scale step
        if (PixelPerfect) scale = MathF.Max(1, MathF.Floor(scale + 1e-4f));
        Scale = scale;
    }

    internal void BeginBatch(SpriteBatch spriteBatch, Matrix visual)
    {
        spriteBatch.Begin(
            samplerState: PixelPerfect ? SamplerState.PointClamp : null,
            transformMatrix: visual * Matrix.CreateScale(Scale, Scale, 1));
    }

    internal void AttachToBus(NeoUIBus? bus)
    {
        Bus = bus;
        InvalidateAttachment();
    }
}
