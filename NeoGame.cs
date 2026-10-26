using System;
using NeoGameLib.Common;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoInput;
using NeoGameLib.NeoRendering;
using NeoGameLib.NeoUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib;

// note to future self:
// to setup: inherit this instead of the `Game` class; this `NeoGame` class automatically sets up the Singleton, update and initialize hooks, and creates the managers.
// 
// steps for adding a future manager (e.g. NeoAudioBus or smth):
// 1. create it in Initialize()
// 2. update it in Update()
//
// TODO: Need NeoAudioBus/NeoAnimationBus in the future
public class NeoGame : Game
{
    private GraphicsDeviceManager _gdm;
    private SpriteBatch _spriteBatch;
    private NeoRenderBus _renderBus;
    private NeoColliderBus _colliderBus;
    private NeoTimerBus _timerBus;
    private NeoInputBus _inputBus;
    private NeoUIBus _uiBus;
    private WorldManager _worldManager;
    private AssetManager _assetManager;
    private Point _windowedSize;
    private float? _viewportAspect;

    /// <summary>
    /// The width / height ratio the game viewport keeps, e.g. 16f / 9f. Null (as default) fills the whole window.
    /// </summary>
    /// <remarks>
    /// <para>The viewport is basically the largest rectangle with this ratio that can fit the window center. The rest of the window renders LetterboxColor.</para>
    /// <para>Cameras, sprites, UI canvases and OnDraw/OnDrawUI all work inside the viewport. Input.MousePosition stays in window pixels</para>
    /// </remarks>
    public float? ViewportAspect
    {
        get => _viewportAspect;
        set => _viewportAspect = value is null || (float.IsFinite(value.Value) && value.Value > 0)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "the viewport aspect must be positive and finite");
    }

    /// <summary>The color of the bars outside the viewport when ViewportAspect is set.</summary>
    public Color LetterboxColor { get; set; } = Color.Black;

    /// <summary>
    /// The sampler state of the world pass (sprites, OnDraw). Null keeps MonoGame's default (LinearClamp, smooths scaled pixel art). Use SamplerState.PointClamp for crisp pixel art under camera zoom.
    /// </summary>
    /// <remarks>UI canvases pick their own with PixelPerfect, OnDrawUI keeps the default.</remarks>
    public SamplerState? WorldSamplerState { get; set; }

    /// <summary>The color the viewport is cleared to every frame.</summary>
    public Color ClearColor { get; set; } = Color.CornflowerBlue;

    public static NeoGame? Singleton { get; private set; }

    public GraphicsDeviceManager GraphicsDeviceManager => _gdm;
    public NeoRenderBus RenderBus => _renderBus;
    public NeoColliderBus ColliderBus => _colliderBus;
    public NeoTimerBus TimerBus => _timerBus;
    public NeoInputBus InputBus => _inputBus;
    public NeoUIBus UIBus => _uiBus;
    public WorldManager WorldManager => _worldManager;
    public AssetManager Assets => _assetManager;
    /// <summary>
    /// Shorthand for GraphicsDeviceManager.GraphicsDevice.Viewport.
    /// </summary>
    public Viewport GraphicsViewport => _gdm.GraphicsDevice.Viewport;

    public NeoGame()
    {
        _gdm = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        if (Singleton is null) Singleton = this;
    }

    protected sealed override void Initialize()
    {
        // note to self:
        // monoGame runs LoadContent() from the TAIL of base.Initialize(), not after it, so the buses MUST be created before base.Initialize() or every OnLoadContent that adds a renderer/collider/timer NREs on a null bus. it's also why OnLoadContent fires before OnInitialize since LoadContent happens inside base.Initialize()
        _renderBus = new NeoRenderBus(_gdm);
        _worldManager = new WorldManager();
        _colliderBus = new NeoColliderBus(_worldManager);
        _timerBus = new NeoTimerBus();
        _inputBus = new NeoInputBus();
        _uiBus = new NeoUIBus();
        _assetManager = new AssetManager(Content);

        base.Initialize();
        OnInitialize();
    }

    protected sealed override void LoadContent()
    {
        base.LoadContent();
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        OnLoadContent(Content);
    }

    protected sealed override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        Input.EnsureRegistered(); // input maps must exist before the bus refreshes in order for the buttons to get the state of the same frame
        InputBus.Update(); // input lifecycle runs first so everything collected will be accessible by lifecycles below
        ApplyViewport();
        UIBus.Update(gameTime, GraphicsDevice.Viewport.Bounds); // ui runs before gameplay so screen-space logic reads the latest input and layout
        OnUpdate(gameTime);
        WorldManager.Update(gameTime);
        ColliderBus.Update();
        TimerBus.Update(gameTime);
    }

    protected sealed override void Draw(GameTime gameTime)
    {
        // viewport clearing pass: whole window clears with letterbox color, then the viewport area clears with viewport clear color.
        GraphicsDevice.Viewport = new Viewport(0, 0, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        GraphicsDevice.Clear(LetterboxColor);
        ApplyViewport();
        if (_viewportAspect is null)
        {
            GraphicsDevice.Clear(ClearColor);
        }
        else
        {
            // ponytail: MonoGame's Clear ignores the viewport on some backends, so the viewport is filled with a quad
            _spriteBatch.Begin();
            _spriteBatch.Draw(_uiBus.GetWhitePixel(GraphicsDevice), Vector2.Zero, null, ClearColor, 0, Vector2.Zero,
                new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), SpriteEffects.None, 0);
            _spriteBatch.End();
        }

        // world pass: one batch per enabled camera, stacked in Order. with no enabled camera the pass falls back to identity, the pre-camera behavior
        if (RenderBus.HasAnyCamera)
        {
            // snapshot (see PassSnapshot): OnDraw and render features can destroy cameras or change their Order -- which re-sorts the live list
            NeoCamera main = RenderBus.MainCamera;
            using PassSnapshot<NeoCamera> cameras = new(RenderBus.Cameras);
            for (int i = 0; i < cameras.Count; i++)
            {
                NeoCamera camera = cameras[i];
                if (!camera.IsAttached || !camera.Enabled) continue;

                _spriteBatch.Begin(samplerState: WorldSamplerState, transformMatrix: camera.GetViewMatrix(GraphicsDevice.Viewport));
                RenderBus.Draw(_spriteBatch, camera);
                if (ReferenceEquals(camera, main)) OnDraw(_spriteBatch, gameTime);
                _spriteBatch.End();
            }
        }
        else
        {
            _spriteBatch.Begin(samplerState: WorldSamplerState);
            RenderBus.Draw(_spriteBatch);
            OnDraw(_spriteBatch, gameTime);
            _spriteBatch.End();
        }

        // screen-space pass: the UI bus begins and ends one batch per canvas (scaled by the canvas), only then OnDrawUI gets its own identity batch which allows it to be always on top of the world and the canvases
        UIBus.Draw(_spriteBatch);
        _spriteBatch.Begin();
        OnDrawUI(_spriteBatch, gameTime);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    public Vector2 GetScreenCenterCoords()
    {
        return new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
    }

    /// <summary>The window-pixel rectangle that the game draws into, as in the letterboxed ViewportAspect area.</summary>
    public Rectangle ViewportBounds
    {
        get
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (_viewportAspect is not float aspect || width <= 0 || height <= 0) return new Rectangle(0, 0, width, height);

            // widest box that fits: full width when the window is taller than the aspect, full height otherwise
            int w = width, h = (int)MathF.Round(width / aspect);
            if (h > height)
            {
                h = height;
                w = (int)MathF.Round(height * aspect);
            }
            return new Rectangle((width - w) / 2, (height - h) / 2, w, h);
        }
    }

    // sets on every update and draw due to MonoGame resetting the viewport to the full back buffer when the window resizes
    private void ApplyViewport() => GraphicsDevice.Viewport = new Viewport(ViewportBounds);

    /// <summary>
    /// Sets the viewport aspect from a width and height, e.g. SetViewportAspect(256, 232) for an NES screen. Same as setting ViewportAspect = width / height.
    /// </summary>
    public void SetViewportAspect(float width, float height)
    {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(width <= 0 || !float.IsFinite(width) ? nameof(width) : nameof(height), "the viewport width and height must be positive and finite");
        ViewportAspect = width / height;
    }

    /// <summary>
    /// Makes viewport fill to window size again.
    /// </summary>
    public void ResetViewportAspect()
    {
        ViewportAspect = null;
    }

    /// <summary>
    /// Sets the window's size in pixels. Works from the constructor, OnInitialize, or any time after.
    /// </summary>
    public void SetWindowSize(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "the window width must be positive");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "the window height must be positive");

        _windowedSize = new Point(width, height);
        if (WindowMode != NeoWindowMode.Windowed) return;

        _gdm.PreferredBackBufferWidth = width;
        _gdm.PreferredBackBufferHeight = height;
        // before Initialize the device doesn't exist yet and picks the preferred size up when it's created
        if (_gdm.GraphicsDevice is not null) _gdm.ApplyChanges();
    }

    /// <summary>The mode from the last SetWindowMode call. The game starts in Windowed by default.</summary>
    public NeoWindowMode WindowMode { get; private set; } = NeoWindowMode.Windowed;

    /// <summary>
    /// Sets the game window mode. Use it AFTER the game initializes.
    /// </summary>
    /// <remarks>
    /// <para>macOS has no borderless fullscreen, so BorderlessFullscreen becomes Fullscreen. WindowMode fetches to Fullscreen on mac as well.</para>
    /// </remarks>
    public void SetWindowMode(NeoWindowMode mode)
    {
        if (mode == NeoWindowMode.BorderlessFullscreen && OperatingSystem.IsMacOS())
            mode = NeoWindowMode.Fullscreen;

        if (mode == WindowMode) return;

        // keep the windowed size so Windowed can restore it
        if (WindowMode == NeoWindowMode.Windowed)
            _windowedSize = Window.ClientBounds.Size;

        DisplayMode display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        switch (mode)
        {
            case NeoWindowMode.Windowed:
                _gdm.IsFullScreen = false;
                Window.IsBorderless = false;
                if (_windowedSize != Point.Zero)
                {
                    _gdm.PreferredBackBufferWidth = _windowedSize.X;
                    _gdm.PreferredBackBufferHeight = _windowedSize.Y;
                }
                _gdm.ApplyChanges();
                break;

            case NeoWindowMode.Fullscreen:
            case NeoWindowMode.BorderlessFullscreen:
                // HardwareModeSwitch true = exclusive mode change, false = borderless desktop fullscreen
                _gdm.HardwareModeSwitch = mode == NeoWindowMode.Fullscreen;
                _gdm.PreferredBackBufferWidth = display.Width;
                _gdm.PreferredBackBufferHeight = display.Height;
                _gdm.IsFullScreen = true;
                _gdm.ApplyChanges();
                break;
        }

        WindowMode = mode;
    }

    protected override void UnloadContent()
    {
        _uiBus?.Dispose();
        base.UnloadContent();
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnLoadContent(ContentManager CM) { }
    protected virtual void OnUpdate(GameTime gameTime) { }
    protected virtual void OnDraw(SpriteBatch spriteBatch, GameTime gameTime) { }

    /// <summary>
    /// this is the screen-space equivalent of OnDraw and it runs after all canvases in window pixels with an identity matrix, so it ignores the camera and the canvas scale.
    /// </summary>
    /// <remarks>See ui-instr.md "OnDrawUI" for more info.</remarks>
    protected virtual void OnDrawUI(SpriteBatch spriteBatch, GameTime gameTime) { }
}

public class NeoGame<T> : NeoGame where T : NeoGame<T>
{
    public new static T Singleton => (T)NeoGame.Singleton;
}
