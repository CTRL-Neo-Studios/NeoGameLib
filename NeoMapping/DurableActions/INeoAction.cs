using NeoGameLib.NeoMapping.DurableActions.Context;

namespace NeoGameLib.NeoMapping.DurableActions;

/// <summary>
/// A behaviour invoked by data (mostly by NeoTrigger on enter/stay/exit).
/// Action types are discovered automatically
/// </summary>
public interface INeoAction
{
    void Invoke(NeoActionContext ctx);
}