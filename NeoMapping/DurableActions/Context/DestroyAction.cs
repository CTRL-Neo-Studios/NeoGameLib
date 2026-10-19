namespace NeoGameLib.NeoMapping.DurableActions.Context;

public sealed record DestroyAction(NeoActionTarget Target = NeoActionTarget.Self) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        (Target == NeoActionTarget.Self ? ctx.Self : ctx.Other)?.DestroyToQueue();
    }
}