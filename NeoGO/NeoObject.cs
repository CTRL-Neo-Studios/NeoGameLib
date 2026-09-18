using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoGO;

// note to future self:
// core idea is that objects represent game entities and object behaviour can be customized by the components attached to the object
// i did thought about completely replicated the game objects model but realized it'd take too much time and just opted for a slightly easier version of it, without proper scenes and etc.
// i also did think about ECS but i despise the component interactions in ECS so fuck that
// so now we land on a weird model of godot-unity hybrid of game-object-nodes
public class NeoObject
{
    private string _name = "NeoObjects";
    private Transform _transform = new();
    private List<NeoComponent> _components = new();

    public string Name
    {
        get => _name; 
        set => _name = value;
    }

    public Transform Transform
    {
        get => _transform;
        set
        {
            _transform.Position = value.Position;
            _transform.Rotation = value.Rotation;
            _transform.Scale = value.Scale;
        }
    }
    
    public List<NeoComponent> Components { get => _components; }
    public NeoWorld World { get; internal set; }
    public bool IsDestroyed { get; private set; }

    public NeoObject(string name)
    {
        this._name = name;
        this._transform = new();
        this._components = new();
    }

    public NeoObject(Transform transform)
    {
        this._name = "NeoObject";
        this.Transform = transform;
        this._components = new();
    }

    public NeoObject(string name, Transform transform)
    {
        this._name = name;
        this.Transform = transform;
        this._components = new();
    }

    #region Object Manipulation Functions
    
    #region Component Related Funcs
    public T AddComponent<T>() where T : NeoComponent, new()
    {
        return AddComponent(new T());
    }

    public T AddComponent<T>(T component) where T : NeoComponent
    {
        if (component is null || component.ParentObject is not null) throw new Exception();

        component.ParentObject = this;
        _components.Add(component);
        component.OnAwake();
        return component;
    }

    public T GetComponent<T>() where T : NeoComponent
    {
        foreach (var component in _components)
            if (component is T match) return match;
        
        return null;
    }

    public bool TryGetComponent<T>(out T component) where T : NeoComponent
    {
        component = GetComponent<T>();
        return component is not null;
    }

    public bool HasComponent<T>() where T : NeoComponent
    {
        return GetComponent<T>() is not null;
    }

    public bool RemoveComponent(NeoComponent component)
    {
        if (component is null || !_components.Remove(component))
            return false;

        component.OnDestroy(); // TODO: separate the remove component event from destroy component event in the future maybe, considering they're different events; or not since they have the same outcome...?
        component.ParentObject = null;
        return true;
    }

    public bool RemoveComponent<T>() where T : NeoComponent
    {
        T component = GetComponent<T>();
        return component is not null && RemoveComponent(component);
    }
    #endregion

    // note to future self: this marks the object to be destroyed in the next update end-tick but the components destroys immediately when calling this
    public void DestroyToQueue()
    {
        World?.Destroy(this);
    }

    public void Tick(GameTime gameTime)
    {
        if (IsDestroyed) return; // If tick is somehow ran after it's destroyed via world queue, this prevents logic in tick from running after destroy
        
        foreach (NeoComponent component in _components)
        {
            if (!component.Enabled) continue;

            if (!component.HasRanOnStart)
            {
                component.HasRanOnStart = true;
                component.OnStart();
            }

            component.OnUpdate(gameTime);
        }
    }
    
    // note to future self: this destroys the object directly via marking it as destroyed
    public void DestroyImmediate()
    {
        if (IsDestroyed) return;

        IsDestroyed = true;
        foreach (var component in _components)
        {
            component.OnDestroy();
        }
        
        _components.Clear();
    }
    #endregion
}
