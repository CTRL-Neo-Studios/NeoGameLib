using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace NeoGameLib;

// note to future self: this is what a consumer project's Game1 looks like now.
// no GraphicsDeviceManager, no SpriteBatch, no wiring — NeoGame owns all of it.
public class Game1 : NeoGame
{
    protected override void OnInitialize()
    {
        // TODO: Add your initialization logic here
    }

    protected override void OnLoadContent()
    {
        CreateWorld("level1");

        // TODO: use this.Content to load your game content here
    }

    protected override void OnUpdate(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: Add your update logic here
    }

    protected override void OnDraw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        // TODO: Add your drawing code here
    }
}
