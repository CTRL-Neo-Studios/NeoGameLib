using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NeoGameLib.NeoUI;

// note to future self:
// a ContentManager that reads xnbs out of NeoGameLib.dll's embedded resources instead of the game's Content folder, so the library can ship its own default font without asking the game to copy anything.
// the xnb is built from Content/Builtin/Inter.spritefont by Content/Builtin/build.sh and is DesktopGL only, which is the only backend the package references.
// Inter is under the SIL OFL 1.1, its license ships in the package under licenses/.

/// <summary>Loads the library's built-in assets (currently the Inter font) from embedded resources.</summary>
public sealed class NeoBuiltinContent : ContentManager
{
    private const string FontResource = "NeoGameLib.Builtin.Inter.xnb";

    private static NeoBuiltinContent? _instance;
    private static SpriteFont? _font;

    private NeoBuiltinContent(IServiceProvider services) : base(services) { }

    /// <summary>
    /// The font that labels and buttons use when no font is set: Inter Regular 16px, characters 32-126 and 160-255.
    /// </summary>
    /// <remarks>Loaded on first use, so a running NeoGame (its graphics device) is required. Returns null before the game has a graphics device.</remarks>
    public static SpriteFont? DefaultFont
    {
        get
        {
            if (_font is not null) return _font;

            NeoGame? game = NeoGame.Singleton;
            if (game?.GraphicsDevice is null) return null;

            _instance = new NeoBuiltinContent(game.Services);
            _font = _instance.Load<SpriteFont>("Inter");
            return _font;
        }
    }

    /// <summary>Frees the font. NeoGame calls this on unload; the next DefaultFont access loads it again.</summary>
    internal static void Release()
    {
        _instance?.Dispose();
        _instance = null;
        _font = null;
    }

    // MonoGame asks for the asset name without ".xnb"
    protected override Stream OpenStream(string assetName)
    {
        if (assetName != "Inter") throw new ContentLoadException($"'{assetName}' is not a built-in asset");

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(FontResource)
            ?? throw new ContentLoadException($"the embedded resource '{FontResource}' is missing from NeoGameLib.dll");
    }
}
