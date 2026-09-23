using NeoGameLib.Common;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoRendering;
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
    private WorldManager _worldManager;
    private AssetManager _assetManager;

    public static NeoGame? Singleton { get; private set; }

    public GraphicsDeviceManager GraphicsDeviceManager => _gdm;
    public NeoRenderBus RenderBus => _renderBus;
    public NeoColliderBus ColliderBus => _colliderBus;
    public NeoTimerBus TimerBus => _timerBus;
    public WorldManager WorldManager => _worldManager;
    public AssetManager Assets => _assetManager;

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
        OnUpdate(gameTime);
        WorldManager.Update(gameTime);
        ColliderBus.Update();
        TimerBus.Update(gameTime);
    }

    protected sealed override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();
        RenderBus.Draw(_spriteBatch);
        OnDraw(_spriteBatch, gameTime);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    public Vector2 GetScreenCenterCoords()
    {
        return new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnLoadContent(ContentManager CM) { }
    protected virtual void OnUpdate(GameTime gameTime) { }
    protected virtual void OnDraw(SpriteBatch spriteBatch, GameTime gameTime) { }
}
