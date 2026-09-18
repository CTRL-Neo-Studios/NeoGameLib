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

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool HasRanOnStart = false;

    public virtual void OnAwake() { }
    public virtual void OnStart() { }
    public virtual void OnUpdate(GameTime gameTime) { }
    public virtual void OnDestroy() { }
}
