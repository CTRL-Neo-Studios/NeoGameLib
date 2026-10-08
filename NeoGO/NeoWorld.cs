using System;
using System.Collections.Generic;
using NeoGameLib.NeoRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace NeoGameLib.NeoGO;

// note to future self:
// TODO: add ability to load or unload worlds later, like that of in unity
public class NeoWorld
{
    private string _name;
    private List<NeoObject> _objects = new();
    private List<NeoObject> _objectDestroyQueue = new();

    // note: internal set so renames go through WorldMan's RenameWorld function and updates the registry key. Don't manually update this otherwise the registry key in WorldMan would desync
    public string Name
    {
        get => _name;
        internal set => _name = value;
    }
    public List<NeoObject> Objects { get => _objects; }

    public NeoWorld(string name)
    {
        this._name = name;
        this._objects = new();
        this._objectDestroyQueue = new();
    }

    public NeoObject Instantiate(string name, Transform transform)
    {
        NeoObject obj = new(name, transform) { World = this };
        _objects.Add(obj);
        return obj;
    }

    public T Instantiate<T>(T obj, Transform transform) where T : NeoObject
    {
        obj.World = this;
        obj.Transform = transform;
        _objects.Add(obj);
        return obj;
    }

    public T Instantiate<T>(T obj, Vector2 position) where T : NeoObject
    {
        obj.World = this;
        obj.Transform.Position = position;
        _objects.Add(obj);
        return obj;
    }

    // note to self:
    // no-transform/pos overload keeps whatever transform the object already has
    // so the overload WITH a transform explicitly overwrites placement instead
    // also fuck you for coding these types of miniscule yet fucked up bugs i took like an hour trying to find out why my objects aren't scaling to 2
    public T Instantiate<T>(T obj) where T : NeoObject
    {
        obj.World = this;
        _objects.Add(obj);
        return obj;
    }
    
    public void Destroy(NeoObject obj)
    {
        if (obj is null || obj.IsDestroyed || obj.World != this)
            return;
        _objectDestroyQueue.Add(obj);
    }

    public void Update(GameTime gameTime)
    {
        for (int i = 0; i < _objects.Count; i++)
            _objects[i].Tick(gameTime);

        if (_objectDestroyQueue.Count <= 0) return;
        
        foreach (NeoObject obj in _objectDestroyQueue)
        {
            _objects.Remove(obj);
            obj.DestroyImmediate();
        }
        _objectDestroyQueue.Clear();
    }
    
    // note to future self:
    // destroys all objects immediately and therefore invokes OnDestroy() on all components and unregisters themselves from the event buses.
    public void Clear()
    {
        foreach (NeoObject obj in _objects)
            obj.DestroyImmediate();

        _objects.Clear();
        _objectDestroyQueue.Clear();
    }

    #region THe Find/Query Functions
    // note to future self here:
    // hey you might be wondering why im not using Nullable<T> since in unity we do nullable types almost EVERYWHERE (bad practice ik)
    // turns out we're outdated old furniture and that in .NET 9, the version that MonoGame uses, they've all opted out Nullable<T> class and added the T? question mark notation instead
    // so that was smth new to learn; and also Nullable<T> are used for native types now instead of classes and etc. but ms's .NET docs seems to heavily persuade you use the question mark notation
    // anyways, just if you're somehow wondering in the future, peace out
    public NeoObject? FindObjectByHashCode(int hashCode)
    {
        foreach (NeoObject obj in _objects)
        {
            if (hashCode == obj.GetHashCode()) return obj;
        }

        return null;
    }

    // returns false (and a null obj) when nothing matches
    public bool TryFindObjectByHashCode(int hashCode, out NeoObject? obj)
    {
        obj = FindObjectByHashCode(hashCode);
        return obj is not null;
    }

    // note to self:
    // the query `T` type here refers to the Component type, not object type, so if you somehow forget this, PLS READ THIS COMMENT FROM MYSELF thank you
    // also this function returns the first object of component T's occurrence, for a list of objects, use the function below this one
    public NeoObject? FindObjectByComponent<T>() where T : NeoComponent
    {
        foreach (NeoObject obj in _objects)
        {
            if (obj.HasComponent<T>()) return obj;
        }

        return null;
    }

    // returns false (and a null obj) when no object has the component
    public bool TryFindObjectByComponent<T>(out NeoObject? obj) where T : NeoComponent
    {
        obj = FindObjectByComponent<T>();
        return obj is not null;
    }

    public List<NeoObject> FindObjectsByComponent<T>() where T : NeoComponent
    {
        List<NeoObject> list = new();
        foreach (NeoObject obj in _objects)
        {
            if (obj.HasComponent<T>()) list.Add(obj);
        }

        return list;
    }

    // returns false (and an empty list) when no object has the component
    public bool TryFindObjectsByComponent<T>(out List<NeoObject> objects) where T : NeoComponent
    {
        objects = FindObjectsByComponent<T>();
        return objects.Count > 0;
    }

    /// <summary>
    /// A Shorthand for `FindObjectByComponent<T>().GetComponent<T>()`.
    /// </summary>
    /// <typeparam name="T">The component type you're querying for</typeparam>
    /// <returns>The component. Ideally.</returns>
    public T FindComponent<T>() where T : NeoComponent
    {
        foreach (NeoObject obj in _objects)
            if (obj.TryGetComponent<T>(out T component)) return component;

        return null;
    }

    // returns false (and a null component) when nothing matches
    public bool TryFindComponent<T>(out T? component) where T : NeoComponent
    {
        component = FindComponent<T>();
        return component is not null;
    }

    public List<T> FindComponents<T>() where T : NeoComponent
    {
        List<T> list = new();
        foreach (NeoObject obj in _objects)
            if (obj.TryGetComponent<T>(out T component)) list.Add(component);

        return list;
    }

    // returns false (and an empty list) when nothing matches
    public bool TryFindComponents<T>(out List<T> components) where T : NeoComponent
    {
        components = FindComponents<T>();
        return components.Count > 0;
    }
    #endregion
}
