using NeoGameLib.NeoGO;
using NeoGameLib.NeoUI.Elements;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

/// <summary>Self = the object that has the action's trigger; Other = the overlapping object. Element = the UI element that fired the action (a clicked NeoButton), null for actions fired by triggers.</summary>
/// <remarks>Actions fired from UI have no Self/Other, so DestroyAction and SetEnabledAction do nothing there, and SwitchWorldAction needs an explicit Unload list because there is no world to infer.</remarks>
public readonly record struct NeoActionContext(NeoObject Self, NeoObject Other, NeoUIElement Element = null)
{
    public NeoWorld World => Self?.World;
}