using NeoGameLib.NeoGO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoRendering;

// note to self:
// This is a world-space camera component which attaches to a NeoObject and reads its Transform each frame, so moving/rotating the object moves/rotates the camera. Zoom is a positive (hopefully positive only. anything other than the positive value inverts the view mat) multiplier on top of that.
// cameras registers themselves to the render bus on awake (like sprites/colliders) and unregisters on destroy, so a loaded world brings its cameras along and unloading it takes them away.
// every ENABLED camera renders the world stacked in Order (like NeoSprites), while a world with no cameras enabled forces the world pass fall back to identity pass (the pre-camera behavior).
// CullingMask determines which sprite/text Layers this camera draws. MainCamera is the first enabled camera of the scene.
public class NeoCamera : NeoComponent
{
    private NeoRenderBus _bus;
    private float _zoom = 1f;
    private int _order;
    private int _cullingMask = -1;

    public float Zoom
    {
        get => _zoom;
        // reject non-positive / non-finite zooms; a <= 0 or NaN/Inf zoom inverts the view matrix
        set
        {
            if (value > 0f && float.IsFinite(value)) _zoom = value;
        }
    }

    // lower orders render first
    public int Order
    {
        get => _order;
        set
        {
            if (_order == value) return;
            _order = value;
            _bus.InvalidateCameraOrder();
        }
    }

    // bitmask that is used to compare against sprite/text Layer attributes. default `-1` draws everything
    public int CullingMask
    {
        get => _cullingMask;
        set => _cullingMask = value;
    }

    internal long RegOrder { get; set; }

    public NeoCamera(NeoRenderBus bus)
    {
        _bus = bus;
    }

    public override void OnAwake()
    {
        _bus.RegisterCamera(this);
    }

    public override void OnDestroy()
    {
        _bus.UnregisterCamera(this);
    }

    // converts world to viewport-local screen matrix (origin is at the viewport center with position/rotation/zoom applied).
    // viewport-local: coordinates relative to the viewport's top-left, so it can be passed straight into SpriteBatch.Begin.
    public Matrix GetViewMatrix(Viewport viewport)
    {
        Transform t = ParentTransform;

        // Check out instructions.md # Road to 0.3.0 Cameras for what this is!
        return Matrix.CreateTranslation(-t.Position.X, -t.Position.Y, 0f) // step 1
             * Matrix.CreateRotationZ(-t.Rotation) // step 2
             * Matrix.CreateScale(_zoom) // step 3
             * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f); // step 4
    }

    // viewport-local screen coords; the bus adds the viewport offset to get window pixel coords.
    /// <summary>
    /// World to Screen Coordinates
    /// </summary>
    /// <returns>Screen coords corresponding to the world coords</returns>
    /// <remarks>this is the viewport-local screen coords. the bus adds the viweport offset to get window pixel coords</remarks>
    public Vector2 WorldToScreen(Vector2 world, Viewport viewport)
    {
        return Vector2.Transform(world, GetViewMatrix(viewport));
    }

    /// <summary>
    /// Screen to World Coordinates
    /// </summary>
    /// <returns>World coords corresponding to the screen coords</returns>
    public Vector2 ScreenToWorld(Vector2 screen, Viewport viewport)
    {
        return Vector2.Transform(screen, Matrix.Invert(GetViewMatrix(viewport)));
    }
}
