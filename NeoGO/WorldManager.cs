using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NeoGameLib.NeoGO;

// note to future self:
// the keeper of ALL worlds, unity-build-settings flavored. every world gets
// registered here first (CreateWorld), then Load pulls a registered world into the
// running set, Unload kicks it back out. any number of worlds can be loaded at once
// (ui world + game world ticking side by side), or none at all, nothing stops you
//
// important: objects are built (the build callback runs) on LOAD, not on create,
// because unload destroys every object in the world and reload reruns the build.
// a manually created world without a build callback reloads empty, so give it one
// if you plan to unload and come back
public class WorldManager
{
    private Dictionary<string, NeoWorld> _worlds = new();
    private Dictionary<string, Action<NeoWorld>> _builders = new();
    private List<NeoWorld> _loaded = new();
    private Dictionary<string, int> _worldToGroup = new(); // world name -> collision group id
    private Dictionary<string, int> _groupIds = new();     // collision group name -> id
    private int _nextGroupId = 0;

    // the currently loaded worlds, in load order, all of them get ticked every frame
    public List<NeoWorld> LoadedWorlds => _loaded;

    // note to self AND future self:
    // creates and REGISTERS a world and binds a function builder that allows you to put stuff inside to initialize the world.
    // Please note that it is highly suggested to use the function builder instead of storing the created world instance and then instantiating objects as the function builders runs on each load, and that worlds DO NOT store their created objects when registered.
    // this is not a full-save-state-registry manager in a traditional sense that saves the "snapshot" and "state" of a world; it's more like a registry-keeper manager that keeps in track of what worlds you load and that's that. Does not keep track of the worlds' states and created objects in its registry.
    // YOU HAVE BEEN WARNED
    public NeoWorld CreateWorld(string name, Action<NeoWorld> build = null)
    {
        if (_worlds.ContainsKey(name))
            throw new Exception($"world '{name}' already exists, unload it first, dumbass");

        NeoWorld world = new NeoWorld(name);
        _worlds[name] = world;
        _builders[name] = build;
        return world;
    }

    // note to future self:
    // registers the already created world and creates the builder function corresponding to that world as well.
    // it is HIGHLY UN-RECOMMENDED to use this function since if you register a world and instantiate objects without using the function builder, it will be empty if you unload and load it using the world manager.
    // YOU HAVE BEEN WANRED
    public NeoWorld CreateWorld(NeoWorld world, Action<NeoWorld> build = null)
    {
        if (_worlds.ContainsKey(world.Name))
            throw new Exception($"world '{world.Name}' already exists. unregister the existing one to load this one");

        _worlds[world.Name] = world;
        _builders[world.Name] = build;
        return world;
    }

    // note to self:
    // loads a REGISTERED world: runs its builder function which is also registered on creation (unless already loaded) and includes it in the update tick loop.
    public NeoWorld Load(string name)
    {
        if (!_worlds.TryGetValue(name, out NeoWorld world))
            throw new Exception($"world '{name}' is null or isn't registered");

        if (_loaded.Contains(world)) return world;

        _builders[name]?.Invoke(world);
        _loaded.Add(world);
        return world;
    }

    // note to self:
    // unloading a loaded world removes its updates in the tick loop and destroys every object in it. the world stays registered so you can load it again for later and it'll rebuild the scene from scratch
    public bool Unload(string name)
    {
        if (!_worlds.TryGetValue(name, out NeoWorld world)) return false;
        if (!_loaded.Remove(world)) return false;

        world.Clear();
        return true;
    }

    // note to self:
    // unloads everything but all registered worlds stay registered
    public void UnloadAll()
    {
        for (int i = _loaded.Count - 1; i >= 0; i--)
            _loaded[i].Clear();

        _loaded.Clear();
    }

    public NeoWorld GetWorld(string name)
    {
        return _worlds.TryGetValue(name, out NeoWorld world) ? world : null;
    }

    public bool TryGetWorld(string name, out NeoWorld world)
    {
        return _worlds.TryGetValue(name, out world);
    }

    public bool IsLoaded(string name)
    {
        return _worlds.TryGetValue(name, out NeoWorld world) && _loaded.Contains(world);
    }

    public void RenameWorld(string name, string newName)
    {
        if (!_worlds.TryGetValue(name, out NeoWorld world))
            throw new Exception($"world '{name}' isn't registered");
        if (_worlds.ContainsKey(newName))
            throw new Exception($"world '{newName}' already exists");

        _worlds.Remove(name);
        _worlds[newName] = world;

        _builders.Remove(name, out Action<NeoWorld> builder);
        if (builder is not null) _builders[newName] = builder;

        if (_worldToGroup.Remove(name, out int groupId))
            _worldToGroup[newName] = groupId;

        world.Name = newName;
    }

    // note to self:
    // see architecture explanation in instructions.md
    public void GroupCollisions(string groupName, params string[] worldNames)
    {
        if (!_groupIds.TryGetValue(groupName, out int id))
            _groupIds[groupName] = id = _nextGroupId++;

        foreach (string name in worldNames)
            _worldToGroup[name] = id;
    }

    public void UngroupCollisions(string worldName)
    {
        _worldToGroup.Remove(worldName);
    }

    // note to self:
    // the collider bus calls this once per collider per frame to cache the group where `-1` == ungrouped
    internal int GetCollisionGroupId(string worldName)
    {
        return worldName is not null && _worldToGroup.TryGetValue(worldName, out int id) ? id : -1;
    }

    // NeoGame calls this every frame so that every loaded world gets ticked
    public void Update(GameTime gameTime)
    {
        // components can Load/Unload worlds mid-tick so using an index loop here instead of foreach
        for (int i = 0; i < _loaded.Count; i++)
            _loaded[i].Update(gameTime);
    }
}
