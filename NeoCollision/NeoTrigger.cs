using System.Collections.Generic;
using NeoGameLib.NeoGO;
using NeoGameLib.NeoMapping;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoMapping.DurableActions;
using NeoGameLib.NeoMapping.DurableActions.Context;

namespace NeoGameLib.NeoCollision;

/// <summary>
/// A trigger collider. collider that dont block movement and fires its action lists when a matching object enters, stays, or exits its bounds.
/// </summary>
/// <remarks>the actions run during the collision pass</remarks>
public class NeoTrigger : NeoBoxCollider
{
    private readonly HashSet<NeoBoxCollider> _inside = new();
    private int _running; // reentrancy guard, dont delete, thank you

    /// <summary>If Tag is set to a value, only objects with the value fires the trigger. Otherwise it'll fire the trigger with anything</summary>
    public string Tag { get; set; }

    public List<INeoAction> OnEnter { get; } = new();
    public List<INeoAction> OnStay { get; } = new();
    public List<INeoAction> OnExit { get; } = new();

    public NeoTrigger()
    {
        IsTrigger = true;
    }

    public bool IsInside(NeoBoxCollider collider) => _inside.Contains(collider);

    internal override void OnPairEnter(NeoBoxCollider other, NeoCollision col)
    {
        if (_running > 0 || !Accepts(other)) return;
        _inside.Add(other);
        Run(OnEnter, other);
    }

    internal override void OnPairStay(NeoBoxCollider other, NeoCollision col)
    {
        if (_running > 0 || !_inside.Contains(other)) return;
        Run(OnStay, other);
    }

    internal override void OnPairExit(NeoBoxCollider other)
    {
        if (_running > 0 || !_inside.Remove(other)) return;
        Run(OnExit, other);
    }

    public override void OnDestroy()
    {
        _inside.Clear();
        base.OnDestroy();
    }

    private bool Accepts(NeoBoxCollider other)
    {
        if (Tag is null) return true;
        NeoObject otherObj = other.ParentObject;
        return otherObj is not null && otherObj.Tags.Contains(Tag);
    }

    private void Run(List<INeoAction> actions, NeoBoxCollider other)
    {
        _running++;
        NeoActionContext ctx = new(ParentObject, other.ParentObject);
        for (int i = 0; i < actions.Count; i++)
        {
            if (!IsAttached) break; // an action may've destroyed this trigger's object during iteration of the list so it's important to do mid-iteration attach testing
            actions[i].Invoke(ctx);
        }
        _running--;
    }
}
