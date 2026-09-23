using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;

namespace NeoGameLib.Common;

// note to future self:
// a simple cache-load-unload-storing asset loader-unloader with the loaded path being the registry key
public class AssetManager
{
    private ContentManager _content;
    private Dictionary<string, object> _cache = new();

    public AssetManager(ContentManager content)
    {
        _content = content;
    }

    public T Load<T>(string path, bool forceLoad = false)
    {
        if (_cache.TryGetValue(path, out object cached) && !forceLoad) return (T) cached; // returns cached asset if it's been loaded before; if forceLoad is true then it forces to reload-recache the target asset

        T asset = _content.Load<T>(path);
        _cache[path] = asset;
        return asset;
    }

    public void Unload(string path)
    {
        if (_cache.Remove(path, out object asset) && asset is IDisposable disposable) disposable.Dispose(); // if removed successfully then try to dispose it as well
    }

    public void UnloadAll()
    {
        foreach (object asset in _cache.Values)
            if (asset is IDisposable disposable) disposable.Dispose();

        _cache.Clear();
    }
}
