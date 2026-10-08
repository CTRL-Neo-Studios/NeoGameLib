using System;

namespace NeoGameLib.Common;

/// <summary>
/// utility base for dev-made singletons.
/// </summary>
public abstract class NeoSingleton<T> where T : NeoSingleton<T>
{
    public static T Singleton { get; private set; }

    protected NeoSingleton()
    {
        if (Singleton is not null)
            throw new Exception($"a {typeof(T).Name} singleton already exists");

        Singleton = (T)this;
    }
}
