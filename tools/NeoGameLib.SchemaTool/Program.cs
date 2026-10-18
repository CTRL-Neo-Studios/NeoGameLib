using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

// usage: dotnet neo-schema.dll <gameAssemblyPath> <projectDir>
//
// loads the game's build output in its own AssemblyLoadContext and calls the game's own copy of NeoGameLib (NeoSchema.ExportProject) through it.
// googled "how to make a tool for C# library that can be executed via cli" and found https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create
// that was weirdly helpful for a microsoft documentation site

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: neo-schema <game-assembly> <project-dir>");
    return 2;
}

string gamePath = Path.GetFullPath(args[0]);
string projectDir = Path.GetFullPath(args[1]);

GameLoadContext context = new(gamePath);
try
{
    Assembly game = context.LoadFromAssemblyPath(gamePath);
    Assembly lib = context.LoadFromAssemblyPath(FindNextTo(gamePath, "NeoGameLib.dll"));

    MethodInfo export = lib
        .GetType("NeoGameLib.NeoMapping.NeoSchema", throwOnError: true)!
        .GetMethod("ExportProject", new[] { typeof(Assembly), typeof(string) })!; // i hope these assertions don't break... it's running on hopes and dreams and a lot of stackoverflow
    
    Console.WriteLine(export.Invoke(null, new object[] { game, projectDir }));
    return 0;
}
catch (Exception ex)
{
    while (ex is TargetInvocationException { InnerException: not null } tie)
        ex = tie.InnerException;
    Console.Error.WriteLine($"neo-schema failed: {ex}");
    return 1;
}
finally
{
    context.Unload();
}

static string FindNextTo(string assemblyPath, string fileName)
{
    string path = Path.Combine(Path.GetDirectoryName(assemblyPath)!, fileName);
    if (!File.Exists(path))
        throw new FileNotFoundException($"{fileName} not found next to the game assembly", path);
    return path;
}

sealed class GameLoadContext : AssemblyLoadContext
{
    private readonly string _dir;
    private readonly AssemblyDependencyResolver _resolver;

    public GameLoadContext(string gamePath) : base("game-schema", isCollectible: true)
    {
        _dir = Path.GetDirectoryName(gamePath)!;
        _resolver = new AssemblyDependencyResolver(gamePath);
    }

    protected override Assembly Load(AssemblyName name)
    {
        string? path = _resolver.ResolveAssemblyToPath(name);
        if (path is not null) return LoadFromAssemblyPath(path);

        string direct = Path.Combine(_dir, name.Name + ".dll");
        if (File.Exists(direct)) return LoadFromAssemblyPath(direct);

        return null; // framework assemblies fall back to the default context
    }
}
