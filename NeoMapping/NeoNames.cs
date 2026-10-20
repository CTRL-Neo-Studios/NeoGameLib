using System;
using System.Text.RegularExpressions;

namespace NeoGameLib.NeoMapping;

// note to future self:
// rules of the two kinds of name conventions that data files use. the editor mirrors these regexes exactly (see the neomap-editor parity notes) which means that if you change one here, change it in neomap-editor too.
//
// - path names: affects maps and prefab libraries. they ARE file paths ("forest/level1" -> Data/NeoMaps/forest/level1.neomap.json), so they have to survive every OS file-path naming regex conventions: ascii only, no characters windows rejects, no trailing dot/space (windows silently strips them for some goddamn reason, fuck you microsoft), no reserved device names (CON.neomap.json is unopenable on windows, again fuck you microsoft for having an outdated FM AL).
// - key names: prefabs and collider groups. plain dictionary keys matched exactly, never touch the disk, so almost anything goes. '/' is reserved because prefab folders display as "terrain/slabs/<name>"; leading/trailing whitespace is banned because "wall " vs "wall" is an invisible typo.
public static class NeoNames
{
    // thank you https://regex-generator.olafneumann.org
    // '/'-separated segment regex: starts and ends with a letter/digit/_/-, may contain spaces and dots in between
    private static readonly Regex PathSegment = new(@"^[A-Za-z0-9_-]([A-Za-z0-9 ._-]*[A-Za-z0-9_-])?$", RegexOptions.CultureInvariant);

    // windows device names regex
    private static readonly Regex ReservedSegment = new(@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Throws unless <paramref name="name"/> is a valid map/library name ('/'-separated segments of ASCII letters, digits, '_', '-'; spaces and dots are allowed)</summary>
    public static void ValidatePath(string name, string kind = "name")
    {
        string error = PathError(name);
        if (error is not null) throw new ArgumentException($"invalid {kind} '{name}': {error}");
    }

    /// <summary>Throws unless <paramref name="name"/> is a valid prefab/collider-group key (not empty, no '/', no control chars, no leading/trailing whitespaces)</summary>
    public static void ValidateKey(string name, string kind = "name")
    {
        string error = KeyError(name);
        if (error is not null) throw new InvalidOperationException($"invalid {kind} '{name}': {error}");
    }

    /// <summary>returns the reason why a map/library name is invalid, or null/empty-string when it's valid</summary>
    public static string PathError(string name)
    {
        if (string.IsNullOrEmpty(name)) return "it's empty";
        foreach (string segment in name.Split('/'))
        {
            if (segment.Length == 0) return "empty folder segment (leading, trailing or double '/')";
            if (!PathSegment.IsMatch(segment)) return $"'{segment}' must use only A-Z a-z 0-9 _ - (spaces and dots allowed inside, not at either end)";
            if (ReservedSegment.IsMatch(segment)) return $"'{segment}' is a reserved device name on Windows (repeat it with me: fuck you microsoft)";
        }
        return null;
    }

    /// <summary>returns the reason why a prefab/collider-group name is invalid, or null/empty-string when it's valid</summary>
    public static string KeyError(string name)
    {
        if (string.IsNullOrEmpty(name)) return "it's empty";
        if (name.Contains('/')) return "'/' is reserved for prefab folders, put the folder in the prefab's 'folder' instead";
        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1])) return "leading or trailing whitespace";
        
        foreach (char c in name)
        {
            if (char.IsControl(c)) return "contains a control character";
        }
        
        return null;
    }
}
