using System.Collections.Generic;
using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

/// <summary>
/// Unloads worlds and loads one.
/// </summary>
/// <remarks>Unloading the trigger's own world destroys the trigger, so actions after this one in the same list don't run.</remarks>
public sealed record SwitchWorldAction(string World, List<string> Unload = null) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        WorldManager manager = NeoGame.Singleton?.WorldManager;
        if (manager is null) return;

        if (Unload is null)
        {
            if (ctx.World is not null) manager.Unload(ctx.World.Name);
        }
        else
        {
            foreach (string name in Unload) manager.Unload(name);
        }

        LoadWorldAction.Load(manager, World);
    }
}
