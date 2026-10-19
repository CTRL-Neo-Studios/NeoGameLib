using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

/// <summary>Self = the object that has the action's trigger; Other = the overlapping object.</summary>
public readonly record struct NeoActionContext(NeoObject Self, NeoObject Other)
{
    public NeoWorld World => Self?.World;
}