using Microsoft.Xna.Framework;

namespace NeoGameLib.Common;

// note to future self:
// the bus that ticks every registered timer. timers register/unregister themselves on start/stop/finish
// the update iterates backwards so a timer finishing (or its OnFinished callback stopping OTHER timers) can remove itself from the loop without through null index exceptions
public class NeoTimerBus : NeoBus<NeoTimer>
{
    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (i >= Items.Count) continue;
            Items[i].Tick(dt);
        }
    }
}
