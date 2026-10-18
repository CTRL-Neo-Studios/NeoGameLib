using NeoGameLib.NeoGO;
using NeoGameLib.NeoMapping.DurableMap;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

/// <summary>Loads a world alongside the ones already running. Never unloads anything; use SwitchWorldAction for that.</summary>
public sealed record LoadWorldAction(string World) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        WorldManager manager = NeoGame.Singleton?.WorldManager;
        if (manager is not null) Load(manager, World);
    }

    // a world that isn't registered yet is registered from the map file with the same name
    internal static void Load(WorldManager manager, string world)
    {
        if (manager.IsLoaded(world)) return;
        if (manager.GetWorld(world) is null) NeoMap.RegisterWorld(world, world);
        manager.Load(world);
    }
}
