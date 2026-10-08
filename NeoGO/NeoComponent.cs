using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoGO;

// fuck encapsulationnnnnnnnnnnnnnnnnnnnnnnnn
public abstract class NeoComponent
{
    private NeoObject _parentObj;
    private bool _enabled = true;

    public NeoObject ParentObject
    {
        get => _parentObj;
        internal set => _parentObj = value;
    }

    public Transform ParentTransform
    {
        get => _parentObj.Transform;
    }

    /// <summary>
    /// A QoL Shorthand from `ParentObject.World`.
    /// </summary>
    public NeoWorld ParentWorld
    {
        get => _parentObj.World;
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool HasRanOnStart = false;

    /// <summary>
    /// Fires when AddComponent attaches this component to an object, before the object starts ticking.
    /// ParentObject/ParentTransform/ParentWorld are all not-null. components added AFTER this one are not "awaken" yet, so GetComponent can't get them.
    /// </summary>
    public virtual void OnAwake() { }

    /// <summary>
    /// Fires once, on the first tick after OnAwake and only while it's enabled; so any new component added to an object that's disabled defers OnStart until it's enabled.
    /// the whole component list is usually registered/complete by now, therefore GetComponent has everything.
    /// </summary>
    public virtual void OnStart() { }

    /// <summary>
    /// Fires every world tick after OnStart. ignored while disabled or destroyed.
    /// </summary>
    public virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>
    /// Fires once when the component is removed from its object or the object is destroyed.
    /// ParentObject/ParentTransform/ParentWorld are still attached during the call.
    /// </summary>
    public virtual void OnDestroy() { }
}
