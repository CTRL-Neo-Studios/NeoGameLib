using Microsoft.Xna.Framework;

namespace NeoGameLib.Common;

// note to future self:
// the bus that ticks every registered timer. timers register/unregister themselves on start/stop/finish.
// the update walks a snapshot (see PassSnapshot): a callback that stops, starts or restarts any timer can't make another timer tick twice or get skipped.
public class NeoTimerBus : NeoBus<NeoTimer>
{
    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        using PassSnapshot<NeoTimer> timers = new(Items);
        for (int i = 0; i < timers.Count; i++)
        {
            if (Items.Contains(timers[i]))
                timers[i].Tick(dt);
        }
    }
}
