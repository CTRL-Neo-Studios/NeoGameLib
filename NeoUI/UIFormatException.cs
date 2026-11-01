using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NeoGameLib.NeoUI;

/// <summary>A .neoui.xml that doesn't match the library. The message names the file (when known), the line and the column.</summary>
public sealed class UIFormatException : Exception
{
    internal UIFormatException(string message, Exception? inner = null) : base(message, inner) { }

    // line info is only there when the document was loaded with LoadOptions.SetLineInfo, which NeoUIDocument always does
    internal static UIFormatException At(XObject node, string message)
    {
        IXmlLineInfo info = node;
        string where = info.HasLineInfo() ? $"line {info.LineNumber}, column {info.LinePosition}" : "unknown position";
        return new UIFormatException($"{where}: {message}");
    }

    internal UIFormatException WithSource(string source) => new($"{source}: {Message}", this);
}
