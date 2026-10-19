using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoCollision;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoMapping.DurableActions;

namespace NeoGameLib.NeoMapping.DurableSpecifications.Primitives;

/// <summary>
/// A trigger area that fires actions on enter/stay/exit. The tag filters which objects set it off (null = anything); the actions are full INeoAction entries, so map files can drive level loading, destruction and game-defined behaviour from a trigger with zero per-map code.
/// </summary>
/// <remarks>It's taken me 4 days to get to this point and I really fucking hope this is worth it... I haven't even made the corresponding NeoMap Editor Standalone Software for this.</remarks>
public sealed record TriggerSpec(
    Vector2 Size,
    string Tag = null,
    List<INeoAction> OnEnter = null,
    List<INeoAction> OnStay = null,
    List<INeoAction> OnExit = null,
    Vector2 Offset = default
) : INeoSpec
{
    public void Apply(NeoObject target)
    {
        NeoTrigger trigger = target.AddComponent(new NeoTrigger { Size = Size, Offset = Offset });
        trigger.Tag = Tag;
        if (OnEnter is not null) trigger.OnEnter.AddRange(OnEnter);
        if (OnStay is not null) trigger.OnStay.AddRange(OnStay);
        if (OnExit is not null) trigger.OnExit.AddRange(OnExit);
    }
}