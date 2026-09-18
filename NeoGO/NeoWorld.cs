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

    public string Name { get => _name; }
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

    public List<NeoObject> FindObjectsByComponent<T>() where T : NeoComponent
    {
        List<NeoObject> list = new();
        foreach (NeoObject obj in _objects)
        {
            if (obj.HasComponent<T>()) list.Add(obj);
        }

        return list;
    }
    #endregion
}
