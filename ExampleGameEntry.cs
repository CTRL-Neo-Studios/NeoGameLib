using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace NeoGameLib;

internal class ExampleGameEntry : NeoGame
{
    protected override void OnInitialize()
    {
        // TODO: Add your initialization logic here
    }

    protected override void OnLoadContent()
    {
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
