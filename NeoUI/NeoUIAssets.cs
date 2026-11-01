namespace NeoGameLib.NeoUI;

// note to future self:
// elements keep an asset PATH (what files store) next to the live asset (what draws). the path resolves lazily on first use through NeoGame.Assets, so a canvas can be built before the game finishes initializing, or without any game at all (tools/tests): the path just stays unresolved and nothing draws.
// a path that doesn't exist in the content project throws ContentLoadException with the path in the message, same as a bad SpriteSpec texture would.
internal static class NeoUIAssets
{
    internal static T? Load<T>(string? path) where T : class
    {
        if (string.IsNullOrEmpty(path)) return null;
        return NeoGame.Singleton?.Assets?.Load<T>(path);
    }
}
