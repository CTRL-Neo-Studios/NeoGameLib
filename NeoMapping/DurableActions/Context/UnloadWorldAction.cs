using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

public sealed record UnloadWorldAction(string World = null) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        WorldManager manager = NeoGame.Singleton?.WorldManager;
        if (manager is null) return;
        manager.Unload(World ?? ctx.World?.Name);
    }
}