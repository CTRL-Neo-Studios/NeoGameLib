using NeoGameLib.NeoGO;
using NeoGameLib.NeoRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib;

// note to future self:
// library front door. inherit this instead of Game; game logic goes in the On* hooks.
// components reach managers via NeoGame.Singleton (e.g. NeoGame.Singleton.RenderBus),
// and their world via ParentObject.World.
// adding a future manager (e.g. NeoColliderBus): create it in Initialize, update it in
// Update, expose a property — components pick it up through Singleton. that's the whole contract.
public class NeoGame : Game
{
    private GraphicsDeviceManager _gdm;
    private SpriteBatch _spriteBatch;
    private NeoRenderBus _renderBus;

    public static NeoGame? Singleton { get; private set; }

    public GraphicsDeviceManager GraphicsDeviceManager => _gdm;
    public NeoRenderBus RenderBus => _renderBus;
    public NeoWorld? World { get; private set; }

    public NeoGame()
    {
        _gdm = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        if (Singleton is null) Singleton = this;
    }

    protected sealed override void Initialize()
    {
        base.Initialize();
        _renderBus = new NeoRenderBus(_gdm);
        OnInitialize();
    }

    protected sealed override void LoadContent()
    {
        base.LoadContent();
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        OnLoadContent();
    }

    protected sealed override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        OnUpdate(gameTime);
        World?.Update(gameTime);
    }

    protected sealed override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        RenderBus.Draw(_spriteBatch);
        OnDraw(_spriteBatch, gameTime);
        base.Draw(gameTime);
    }

    public NeoWorld CreateWorld(string name)
    {
        World = new NeoWorld(name);
        return World;
    }

    public Vector2 GetScreenCenterCoords()
    {
        return new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnLoadContent() { }
    protected virtual void OnUpdate(GameTime gameTime) { }
    protected virtual void OnDraw(SpriteBatch spriteBatch, GameTime gameTime) { }
}
