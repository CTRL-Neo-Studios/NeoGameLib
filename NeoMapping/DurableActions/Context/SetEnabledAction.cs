using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableActions.Context;

public sealed record SetEnabledAction(string Component, bool Enabled = true, NeoActionTarget Target = NeoActionTarget.Self) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        NeoObject target = Target == NeoActionTarget.Self ? ctx.Self : ctx.Other;
        if (target is null) return;

        foreach (NeoComponent component in target.Components)
        {
            if (component.GetType().Name == Component)
            {
                component.Enabled = Enabled;
                break;
            }
        }
    }
}