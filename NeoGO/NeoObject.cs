using System;
using System.Collections.Generic;
using NeoGameLib.Common;
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
    /// <summary>Free-form string case-sensitive labels as tags (e.g. "Player", "Enemy"). Used by triggers and map files (finally! string tags instead of component-as-tags)</summary>
    public HashSet<string> Tags { get; } = new();
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
        component._detaching = false;
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

    public bool HasComponent(Type type)
    {
        if (type is null) return false;
        
        foreach (var component in _components)
            if (type.IsAssignableFrom(component.GetType())) return true;
        
        return false;
    }

    public bool RemoveComponent(NeoComponent component)
    {
        if (component is null || component._detaching || !_components.Remove(component))
            return false;

        component._detaching = true;
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

    /// <summary>
    /// This function is invoked when the world instantiates this object, right after World is assigned and the object is in the world's object list.
    /// This is the place to add your components instead of in the constructor.
    /// </summary>
    public virtual void OnInstantiate() { }

    // note to future self: this marks the object to be destroyed in the next update end-tick but the components destroys immediately when calling this
    public void DestroyToQueue()
    {
        World?.Destroy(this);
    }

    public void Tick(GameTime gameTime)
    {
        if (IsDestroyed) return; // If tick is somehow ran after it's destroyed via world queue, this prevents logic in tick from running after destroy

        // note to future self:
        // walks a snapshot (see PassSnapshot): a component that removes itself or a sibling no longer makes the next one skip its update, and one added during the tick starts next tick.
        // IsAttached is checked per component so a removed or destroyed one is never called again this tick
        using PassSnapshot<NeoComponent> components = new(_components);
        for (int i = 0; i < components.Count; i++)
        {
            if (IsDestroyed) return;
            NeoComponent component = components[i];
            if (!component.IsAttached || !component.Enabled) continue;

            if (!component.HasRanOnStart)
            {
                component.HasRanOnStart = true;
                component.OnStart();
                if (!component.IsAttached || !component.Enabled) continue;
            }

            component.OnUpdate(gameTime);
        }
    }

    // note to future self: this destroys the object directly via marking it as destroyed
    public void DestroyImmediate()
    {
        if (IsDestroyed) return;

        IsDestroyed = true;

        // backwards over a snapshot: OnDestroy is user code and can remove siblings. the `_detaching` flag makes sure each component gets exactly one OnDestroy even if a sibling removed it first
        using PassSnapshot<NeoComponent> components = new(_components);
        for (int i = components.Count - 1; i >= 0; i--)
        {
            NeoComponent component = components[i];
            if (component._detaching) continue;
            component._detaching = true;
            component.OnDestroy();
        }

        _components.Clear();
    }
    #endregion
}
