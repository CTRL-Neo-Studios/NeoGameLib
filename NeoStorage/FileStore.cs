using System.Collections.Generic;
using System.IO;

namespace NeoGameLib.NeoStorage;

// note to future self:
// check out instructions.md if you forgot the file store architecture. basically tauri v2's file-as-an-object-store concept
public class FileStore
{
    protected string Buffer = "";

    public string FilePath { get; }

    public FileStore(string filePath)
    {
        FilePath = filePath;
    }

    // reads the file or creates it if it doesn't exist
    public virtual void Load()
    {
        string dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(FilePath))
            File.WriteAllText(FilePath, "");

        Buffer = File.ReadAllText(FilePath);
    }

    // the raw buffer, line breaks as "\n"
    public string Get()
    {
        return Buffer;
    }

    // note to self:
    // splits buffer into lines and normalize \r\n to \n so there's no trailing line break
    public List<string> GetAsLines()
    {
        string normalized = Buffer.Replace("\r\n", "\n");
        List<string> lines = new(normalized.Split('\n'));
        if (normalized.EndsWith('\n')) lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    // note to future self:
    // sets the buffer. Try not to use this. I have it here just in case you need to write one line of text without wanting to just set a new line.
    // for multiline content setting, use the Set func overload that takes in the string list
    public void Set(string content)
    {
        Buffer = content;
    }

    // replaces the buffer with list of strings, with automatic trailing return line \n
    // this is the overload for multiline buffer setting
    public void Set(List<string> lines)
    {
        Buffer = string.Join('\n', lines);
    }

    // writes actual file or creates the directory + file if missing, similar logic to read func
    public virtual void Write()
    {
        string dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(FilePath, Buffer);
    }
}
