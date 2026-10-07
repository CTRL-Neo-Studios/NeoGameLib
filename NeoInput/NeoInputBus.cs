using NeoGameLib.Common;
using Microsoft.Xna.Framework.Input;

namespace NeoGameLib.NeoInput;

/// <summary>
/// the lifecycle bus that runs the input collection lifecycle.
/// </summary>
/// <remarks>
/// <para>The bus stores a list of input maps and on every update tick loop, refreshes the keyboard state of the current tick in those maps as well as from the last tick</para>
/// <para>NeoGame's Update() lifecycle calls this before it calls other update-tick lifecycles, so this has the latest data where every other component can us</para>
/// <para>In addition to that, thanks to this being a separate lifecycle bus, NeoInputMaps has the same autonomity as NeoTimer. Create and forget, use when you want. Though it is a bit more costly to forget a created input map </para>
/// </remarks>
public class NeoInputBus : NeoBus<NeoInputMap>
{
    private KeyboardState _previous;

    public void Update()
    {
        Update(Keyboard.GetState());
    }

    public void Update(KeyboardState current)
    {
        for (int i = 0; i < Items.Count; i++)
            Items[i].Refresh(_previous, current); // refreshing all registered input maps

        _previous = current;
    }

    /// <summary>
    /// called by the input maps' to sub to input updates.
    /// </summary>
    /// <param name="map">the input map calling this func</param>
    /// <param name="dedupe">`dedupe = false` allows duplicate registrations of the same map but ideally this shouldn't be `false`, and no duplicate maps should register.</param>
    internal void Register(NeoInputMap map, bool dedupe)
    {
        if (dedupe)
        {
            Add(map); // Add already skips existing entries
            return;
        }

        Items.Add(map);
    }

    /// <summary>
    /// called by input maps to unsub from input updates.
    /// </summary>
    /// <param name="map">the input map calling this func</param>
    /// <param name="dedupe">`dedupe = true` removes all duplicate registrations of the same map. ideally this should be `true` by default</param>
    internal void Unregister(NeoInputMap map, bool dedupe)
    {
        if (dedupe)
        {
            while (Items.Remove(map)) { }
            return;
        }

        Items.Remove(map); // removes the first registration
    }
}
