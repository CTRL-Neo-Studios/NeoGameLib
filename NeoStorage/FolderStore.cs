using System;
using System.Collections.Generic;
using System.IO;

namespace NeoGameLib.NeoStorage;

// note to future self:
// check out instructions.md if you forgot the folder store architecture.
// This is built on top of tauri's file-as-a-store-object concept except it's for folders instead of single files. It's folder-as-a-store-object
// the load function enumerates the folder's top-level files into Files attr
// Instantiate wraps any file in the folder as a FileStore (or a given subclass like the JsonStore)
// TODO: currently there's no recursion or glob filtering, so you should add them if you need them in the future. Like grep/tree/ls commands or smth, idk, write what you need
// TODO: maybe for recursion, we can have a RecursiveFolderStore class instead? Since I may have just incurred some thoughts on what use cases could be recursive, idk
public class FolderStore
{
    private string _path;

    public string FolderPath => _path;
    public List<string> Files { get; private set; } = new();

    public FolderStore(string path)
    {
        _path = path;
    }

    // same as file store, loads files in directory or creates the directory if it don't exist
    public void Load()
    {
        if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);
        
        Files = new(Directory.EnumerateFiles(_path));
    }

    // gets a file in this folder as a file-store object. or a subclass of the file-store, like json-store
    public T Instantiate<T>(string fileName) where T : FileStore
    {
        return (T) Activator.CreateInstance(typeof(T), Path.Combine(_path, fileName));
    }
}
