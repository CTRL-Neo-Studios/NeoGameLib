using NeoGameLib.NeoGO;
using NeoGameLib.NeoMapping.DurableMap;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

public sealed record LoadWorldAction(string World, bool UnloadCurrent = true) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        WorldManager manager = NeoGame.Singleton?.WorldManager;
        if (manager is null) return;

        if (UnloadCurrent && ctx.World is not null)
            manager.Unload(ctx.World.Name);

        if (manager.IsLoaded(World)) return;

        // rare case scenario if world isn't registered yet; if it wasn't, register it
        if (manager.GetWorld(World) is null)
            NeoMap.RegisterWorld(World, World);

        manager.Load(World);
    }
}