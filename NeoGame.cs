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
        InputBus.Update(); // input lifecycle runs first so everything collected will be accessible by lifecycles below
        UIBus.Update(gameTime, new Point(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height)); // ui runs before gameplay so screen-space logic reads the latest input and layout
        OnUpdate(gameTime);
        WorldManager.Update(gameTime);
        ColliderBus.Update();
        TimerBus.Update(gameTime);
    }

    protected sealed override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // world pass: one batch per enabled camera, stacked in Order. with no
        // enabled camera the pass falls back to identity, the pre-camera behavior
        if (RenderBus.HasAnyCamera)
        {
            foreach (NeoCamera camera in RenderBus.Cameras)
            {
                if (!camera.Enabled) continue;

                _spriteBatch.Begin(transformMatrix: camera.GetViewMatrix(GraphicsDevice.Viewport));
                RenderBus.Draw(_spriteBatch, camera);
                if (ReferenceEquals(camera, RenderBus.MainCamera)) OnDraw(_spriteBatch, gameTime);
                _spriteBatch.End();
            }
        }
        else
        {
            _spriteBatch.Begin();
            RenderBus.Draw(_spriteBatch);
            OnDraw(_spriteBatch, gameTime);
            _spriteBatch.End();
        }

        // screen-space pass: identity matrix, always on top of the world
        _spriteBatch.Begin();
        UIBus.Draw(_spriteBatch);
        OnDrawUI(_spriteBatch, gameTime);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    public Vector2 GetScreenCenterCoords()
    {
        return new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
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
    /// this is the screen-space equivalent of OnDraw and it runs after all canvases with an identity matrix, so anything drawn here does not take the camera into account.
    /// </summary>
    /// <remarks>See instructions.md 0.3.0 Actual Implementation section beginning for more info.</remarks>
    protected virtual void OnDrawUI(SpriteBatch spriteBatch, GameTime gameTime) { }
}

// note to future self:
// inherit from NeoGame<T> instead of NeoGame when you want a custom singleton with custom public fields you define in your base game class, i.e. public class Game1 : NeoGame<Game1> { }
// this would make Game1.Singleton and show Game1's own public fields and etc.
public class NeoGame<T> : NeoGame where T : NeoGame<T>
{
    public new static T Singleton => (T)NeoGame.Singleton;
}
