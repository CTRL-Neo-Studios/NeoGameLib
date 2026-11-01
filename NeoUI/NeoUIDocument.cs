using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NeoGameLib.NeoMapping;
using NeoGameLib.NeoUI.Elements;

namespace NeoGameLib.NeoUI;

/// <summary>
/// Reads and writes a UI tree as a .neoui.xml document: one canvas per file.
/// </summary>
/// <remarks>
/// <para>Files live in Data/NeoUI/[name].neoui.xml (name may include subfolders, same rules as map names), in the namespace <see cref="Namespace"/>.
/// The grammar of each attribute value is in NeoUIValues.cs; the format is described in ui-instr.md.</para>
/// <para>Layout attributes are written per axis in one of two forms, depending on whether the axis is stretched (the anchors differ on it):
/// a point axis uses x/width (or y/height), a stretched axis uses left/right (or top/bottom) insets. Mixing both forms on one axis is a load error.</para>
/// <para>Only the built-in elements are supported. A custom NeoUIElement subclass can't be written (ToXml throws) or read (unknown element).</para>
/// </remarks>
public static class NeoUIDocument
{
    public const string Extension = ".neoui.xml";
    public const string Namespace = "urn:neogamelib:ui:1";

    private static readonly XNamespace Ns = Namespace;

    /// <summary>Loads Data/NeoUI/[name].neoui.xml; name may include subfolders, e.g. "menus/pause".</summary>
    public static NeoCanvas Load(string name)
    {
        string path = NeoProject.DataPath(NeoProject.UIFolder, name, Extension);
        using Stream stream = TitleContainer.OpenStream(path);
        return Read(XDocument.Load(stream, LoadOptions.SetLineInfo), path);
    }

    public static NeoCanvas FromXml(string xml)
    {
        return Read(XDocument.Parse(xml, LoadOptions.SetLineInfo), "<string>");
    }

    /// <summary>Writes the canvas as the canonical document: fixed attribute order, defaults left out.</summary>
    public static string ToXml(NeoCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        XDocument document = new(new XDeclaration("1.0", "utf-8", null), WriteElement(canvas));
        using StringWriter writer = new();
        using (XmlWriter xml = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, IndentChars = "  ", OmitXmlDeclaration = true, NewLineChars = "\n" }))
            document.Save(xml);

        return writer.ToString() + "\n";
    }

    private static NeoCanvas Read(XDocument document, string source)
    {
        try
        {
            XElement root = document.Root ?? throw new UIFormatException("the document is empty");
            if (root.Name != Ns + "Canvas")
                throw UIFormatException.At(root, $"the root element must be <Canvas xmlns=\"{Namespace}\">, found <{root.Name}>");

            return (NeoCanvas)ReadElement(root);
        }
        catch (UIFormatException ex)
        {
            throw ex.WithSource(source);
        }
        catch (XmlException ex)
        {
            throw new UIFormatException($"{source}: line {ex.LineNumber}, column {ex.LinePosition}: {ex.Message}", ex);
        }
    }

    // ---- reading ----

    private static NeoUIElement ReadElement(XElement node)
    {
        if (node.Name.Namespace != Ns)
            throw UIFormatException.At(node, $"element <{node.Name.LocalName}> must be in the namespace {Namespace}");

        NeoUIElement element = node.Name.LocalName switch
        {
            "Canvas" => new NeoCanvas(),
            "Panel" => new NeoPanel(),
            "FlexBox" => new NeoFlexBox(),
            "Label" => new NeoLabel(),
            "Button" => new NeoButton(),
            "Image" => new NeoImage(),
            _ => throw UIFormatException.At(node, $"unknown element <{node.Name.LocalName}>, the elements are Canvas, Panel, FlexBox, Label, Button and Image"),
        };

        Dictionary<string, XAttribute> attributes = node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.Namespace != XNamespace.Xmlns && a.Name.NamespaceName != "http://www.w3.org/2001/XMLSchema-instance")
            .ToDictionary(a => a.Name.LocalName);

        // a canvas always covers the viewport, so layout attributes on it would silently do nothing
        if (element is NeoCanvas && attributes.Keys.FirstOrDefault(LayoutNames.Contains) is { } layoutName)
            throw UIFormatException.At(attributes[layoutName], $"'{layoutName}' does nothing on a <Canvas>: a canvas always covers the whole viewport. scale it with scaleMode/scaleFactor/referenceResolution instead");

        if (element is not NeoCanvas) ReadLayout(element, node, attributes);
        ReadProperties(element, attributes);

        if (attributes.Count > 0)
        {
            XAttribute unknown = attributes.Values.First();
            throw UIFormatException.At(unknown, $"<{node.Name.LocalName}> has no attribute '{unknown.Name.LocalName}'");
        }

        foreach (XElement child in node.Elements())
        {
            if (child.Name == Ns + "Button.onClick" && element is NeoButton button)
            {
                foreach (XElement actionNode in child.Elements()) button.OnClickActions.Add(NeoUIActionXml.Read(actionNode));
                continue;
            }

            if (element is NeoCanvas && child.Name == Ns + "Canvas")
                throw UIFormatException.At(child, "a canvas can't be a child of another element");

            try
            {
                element.Children.Add(ReadElement(child));
            }
            catch (InvalidOperationException ex)
            {
                throw UIFormatException.At(child, ex.Message);
            }
        }

        return element;
    }

    // attribute name -> how to read it into the element. consumed attributes are removed from the dictionary, so what's left is unknown.
    private static void ReadProperties(NeoUIElement element, Dictionary<string, XAttribute> attributes)
    {
        foreach (AttributeBinding binding in Bindings(element))
        {
            if (!attributes.Remove(binding.Name, out XAttribute attribute)) continue;

            try
            {
                binding.Set(attribute.Value);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
            {
                throw UIFormatException.At(attribute, $"{binding.Name}=\"{attribute.Value}\": {ex.Message}");
            }
        }
    }

    private static void ReadLayout(NeoUIElement element, XElement node, Dictionary<string, XAttribute> attributes)
    {
        ScreenTransform t = element.ScreenTransform;

        T Take<T>(string name, Func<string, T> parse, T fallback)
        {
            if (!attributes.Remove(name, out XAttribute attribute)) return fallback;
            try
            {
                return parse(attribute.Value);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
            {
                throw UIFormatException.At(attribute, $"{name}=\"{attribute.Value}\": {ex.Message}");
            }
        }

        bool Has(string name) => attributes.ContainsKey(name);

        // anchor: the shorthand (preset / point / range) or the explicit anchorMin/anchorMax, never both
        if (Has("anchor") && (Has("anchorMin") || Has("anchorMax")))
            throw UIFormatException.At(node, "write either anchor or anchorMin/anchorMax, not both");

        // the setters reject values outside 0-1 too, but Take only reports errors thrown while parsing, so the range is checked there: the message then points at the attribute
        if (Has("anchor"))
        {
            (Vector2 min, Vector2 max) = Take("anchor", s => NeoUIValues.CheckNormalized(NeoUIValues.ParseAnchor(s)), (t.AnchorMin, t.AnchorMax));
            t.AnchorMin = min;
            t.AnchorMax = max;
        }
        else
        {
            t.AnchorMin = Take("anchorMin", s => NeoUIValues.CheckNormalized(NeoUIValues.ParseVector(s)), t.AnchorMin);
            t.AnchorMax = Take("anchorMax", s => NeoUIValues.CheckNormalized(NeoUIValues.ParseVector(s)), t.AnchorMax);
        }

        t.Pivot = Take("pivot", s => NeoUIValues.CheckNormalized(NeoUIValues.ParseVector(s)), t.Pivot);

        // the per-axis forms are applied after the pivot, because insets depend on it
        ReadAxis(node, attributes, t.StretchesX, "x", "width", "left", "right",
            () => (t.Position.X, t.Size.X), (p, s) => { t.Position = new Vector2(p, t.Position.Y); t.Size = new Vector2(s, t.Size.Y); },
            v => t.InsetLeft = v, v => t.InsetRight = v);
        ReadAxis(node, attributes, t.StretchesY, "y", "height", "top", "bottom",
            () => (t.Position.Y, t.Size.Y), (p, s) => { t.Position = new Vector2(t.Position.X, p); t.Size = new Vector2(t.Size.X, s); },
            v => t.InsetTop = v, v => t.InsetBottom = v);

        t.WidthMode = Take("widthMode", s => (NeoUISizing)NeoUIValues.ParseEnum(typeof(NeoUISizing), s), t.WidthMode);
        t.HeightMode = Take("heightMode", s => (NeoUISizing)NeoUIValues.ParseEnum(typeof(NeoUISizing), s), t.HeightMode);
        t.Rotation = Take("rotation", s => DegreesToRadians(NeoUIValues.ParseFloat(s)), t.Rotation);
        t.Scale = Take("scale", NeoUIValues.ParseVector, t.Scale);
    }

    private static void ReadAxis(XElement node, Dictionary<string, XAttribute> attributes, bool stretched, string pos, string size, string start, string end,
        Func<(float Position, float Size)> get, Action<float, float> set, Action<float> setStart, Action<float> setEnd)
    {
        string wrong1 = stretched ? pos : start, wrong2 = stretched ? size : end;
        foreach (string name in new[] { wrong1, wrong2 })
        {
            if (attributes.TryGetValue(name, out XAttribute bad))
                throw UIFormatException.At(bad, stretched
                    ? $"this axis is stretched (the anchors differ), so write {start}/{end} instead of '{name}'"
                    : $"this axis is a point (the anchors are equal), so write {pos}/{size} instead of '{name}'");
        }

        float Parse(string name, string text, XAttribute a)
        {
            try { return NeoUIValues.ParseFloat(text); }
            catch (FormatException ex) { throw UIFormatException.At(a, $"{name}=\"{text}\": {ex.Message}"); }
        }

        if (stretched)
        {
            if (attributes.Remove(start, out XAttribute s)) setStart(Parse(start, s.Value, s));
            if (attributes.Remove(end, out XAttribute e)) setEnd(Parse(end, e.Value, e));
            return;
        }

        (float p, float sz) = get();
        if (attributes.Remove(pos, out XAttribute px)) p = Parse(pos, px.Value, px);
        if (attributes.Remove(size, out XAttribute sx)) sz = Parse(size, sx.Value, sx);
        if (sz < 0) throw UIFormatException.At((XObject)sx ?? node, $"{size} can't be negative on a point axis");
        set(p, sz);
    }

    // ---- the property table: which attributes each element type has, how they're read and written, and (through Describe) what the schema says about them ----
    // Type is one of: string, bool, int, float, vector2, color, padding, rect, anchor, enum. the grammar of each is in NeoUIValues.cs

    internal sealed record AttributeBinding(string Name, string Type, Func<string?> Get, string? Default, Action<string> Set, Type? EnumType = null, Type? AssetType = null);

    private static IEnumerable<AttributeBinding> Bindings(NeoUIElement element)
    {
        yield return Text("id", () => element.Id, v => element.Id = v, "");
        yield return Bool("visible", () => element.Visible, v => element.Visible = v, true);
        yield return Bool("enabled", () => element.Enabled, v => element.Enabled = v, true);

        switch (element)
        {
            case NeoCanvas c:
                yield return Enum("scaleMode", () => c.ScaleMode, v => c.ScaleMode = v, NeoCanvasScaleMode.ConstantPixelSize);
                yield return Float("scaleFactor", () => c.ScaleFactor, v => c.ScaleFactor = v, 1f);
                yield return Vec("referenceResolution", () => c.ReferenceResolution, v => c.ReferenceResolution = v, new Vector2(1280, 720));
                yield return Float("match", () => c.Match, v => c.Match = v, 0f);
                yield return Bool("pixelPerfect", () => c.PixelPerfect, v => c.PixelPerfect = v, false);
                yield return Int("order", () => c.Order, v => c.Order = v, 0);
                break;

            case NeoPanel p:
                if (p is NeoFlexBox f)
                {
                    yield return Enum("direction", () => f.Direction, v => f.Direction = v, NeoFlexDirection.Row);
                    yield return Float("gap", () => f.Gap, v => f.Gap = v, 0f);
                    yield return Enum("justify", () => f.Justify, v => f.Justify = v, NeoFlexAlign.Start);
                    yield return Enum("align", () => f.Align, v => f.Align = v, NeoFlexAlign.Start);
                }

                yield return ColorBinding("color", () => p.Color, v => p.Color = v, Color.Transparent);
                yield return new AttributeBinding("padding", "padding", () => NeoUIValues.PaddingText(p.Padding), "0", v => p.Padding = NeoUIValues.ParsePadding(v));
                break;

            case NeoLabel l:
                yield return Text("text", () => l.Text, v => l.Text = v, "");
                yield return Text("fontPath", () => l.FontPath, v => l.FontPath = v, "", typeof(SpriteFont));
                yield return ColorBinding("color", () => l.Color, v => l.Color = v, Color.White);
                break;

            case NeoButton b:
                yield return Text("text", () => b.Text, v => b.Text = v, "");
                yield return Text("fontPath", () => b.FontPath, v => b.FontPath = v, "", typeof(SpriteFont));
                yield return ColorBinding("color", () => b.Color, v => b.Color = v, Color.White);
                yield return ColorBinding("hoverColor", () => b.HoverColor, v => b.HoverColor = v, Color.LightGray);
                yield return ColorBinding("pressedColor", () => b.PressedColor, v => b.PressedColor = v, Color.Gray);
                break;

            case NeoImage i:
                yield return Text("texturePath", () => i.TexturePath, v => i.TexturePath = v, "", typeof(Texture2D));
                yield return ColorBinding("color", () => i.Color, v => i.Color = v, Color.White);
                yield return new AttributeBinding("sourceRectangle", "rect", () => i.SourceRectangle is { } r ? NeoUIValues.RectText(r) : null, null, v => i.SourceRectangle = NeoUIValues.ParseRect(v));
                break;
        }
    }

    private static AttributeBinding Text(string name, Func<string?> get, Action<string> set, string def, Type? asset = null) => new(name, "string", get, def, set, AssetType: asset);
    private static AttributeBinding Bool(string name, Func<bool> get, Action<bool> set, bool def) => new(name, "bool", () => NeoUIValues.BoolText(get()), NeoUIValues.BoolText(def), v => set(NeoUIValues.ParseBool(v)));
    private static AttributeBinding Int(string name, Func<int> get, Action<int> set, int def) => new(name, "int", () => get().ToString(System.Globalization.CultureInfo.InvariantCulture), def.ToString(System.Globalization.CultureInfo.InvariantCulture),
        v => set(int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int i) ? i : throw new FormatException($"'{v}' is not a whole number")));
    private static AttributeBinding Float(string name, Func<float> get, Action<float> set, float def) => new(name, "float", () => NeoUIValues.Float(get()), NeoUIValues.Float(def), v => set(NeoUIValues.ParseFloat(v)));
    private static AttributeBinding Vec(string name, Func<Vector2> get, Action<Vector2> set, Vector2 def) => new(name, "vector2", () => NeoUIValues.Vector(get()), NeoUIValues.Vector(def), v => set(NeoUIValues.ParseVector(v)));
    private static AttributeBinding ColorBinding(string name, Func<Color> get, Action<Color> set, Color def) => new(name, "color", () => NeoUIValues.ColorText(get()), NeoUIValues.ColorText(def), v => set(NeoUIValues.ParseColor(v)));
    private static AttributeBinding Enum<T>(string name, Func<T> get, Action<T> set, T def) where T : struct, System.Enum =>
        new(name, "enum", () => NeoUIValues.EnumText(get()), NeoUIValues.EnumText(def), v => set((T)NeoUIValues.ParseEnum(typeof(T), v)), EnumType: typeof(T));

    // ---- description for the schema outputs ----

    internal sealed record UIAttributeInfo(string Name, string Type, string? Default, string[]? EnumValues, string? Asset, string? EnumName = null);

    internal sealed record UIElementInfo(string Name, List<UIAttributeInfo> Attributes);

    internal static string[] EnumValues(Type enumType) => System.Enum.GetNames(enumType).Select(NeoUIValues.Camel).ToArray();

    private static UIAttributeInfo Info(AttributeBinding b) => new(b.Name, b.Type, b.Default, b.EnumType is null ? null : EnumValues(b.EnumType), b.AssetType?.Name, b.EnumType?.Name);

    /// <summary>The built-in elements and their own attributes (id/visible/enabled included, layout attributes not), in document order. Defaults come from fresh instances.</summary>
    internal static List<UIElementInfo> Describe()
    {
        (string Name, NeoUIElement Sample)[] elements =
        {
            ("Canvas", new NeoCanvas()), ("Panel", new NeoPanel()), ("FlexBox", new NeoFlexBox()),
            ("Label", new NeoLabel()), ("Button", new NeoButton()), ("Image", new NeoImage()),
        };
        return elements.Select(e => new UIElementInfo(e.Name, Bindings(e.Sample).Select(Info).ToList())).ToList();
    }

    /// <summary>The layout attributes every element accepts. x/width and y/height are for point axes, left/right and top/bottom for stretched axes.</summary>
    internal static List<UIAttributeInfo> DescribeLayout()
    {
        ScreenTransform d = new();
        string[] sizing = EnumValues(typeof(NeoUISizing));
        return new List<UIAttributeInfo>
        {
            new("anchor", "anchor", NeoUIValues.AnchorText(d.AnchorMin, d.AnchorMax), null, null),
            new("anchorMin", "vector2", NeoUIValues.Vector(d.AnchorMin), null, null),
            new("anchorMax", "vector2", NeoUIValues.Vector(d.AnchorMax), null, null),
            new("pivot", "vector2", NeoUIValues.Vector(d.Pivot), null, null),
            new("x", "float", "0", null, null), new("y", "float", "0", null, null),
            new("width", "float", "0", null, null), new("height", "float", "0", null, null),
            new("left", "float", "0", null, null), new("right", "float", "0", null, null),
            new("top", "float", "0", null, null), new("bottom", "float", "0", null, null),
            new("widthMode", "enum", NeoUIValues.EnumText(d.WidthMode), sizing, null, nameof(NeoUISizing)),
            new("heightMode", "enum", NeoUIValues.EnumText(d.HeightMode), sizing, null, nameof(NeoUISizing)),
            new("rotation", "float", "0", null, null), // degrees, clockwise (y points down)
            new("scale", "vector2", NeoUIValues.Vector(d.Scale), null, null),
        };
    }

    // ---- writing ----

    private static XElement WriteElement(NeoUIElement element)
    {
        // exact types: a subclass of a built-in would silently lose its extra state, so it counts as a custom element
        string name = element.GetType() switch
        {
            Type t when t == typeof(NeoCanvas) => "Canvas",
            Type t when t == typeof(NeoPanel) => "Panel",
            Type t when t == typeof(NeoFlexBox) => "FlexBox",
            Type t when t == typeof(NeoLabel) => "Label",
            Type t when t == typeof(NeoButton) => "Button",
            Type t when t == typeof(NeoImage) => "Image",
            _ => throw new NotSupportedException($"{element.GetType().Name} is a custom element, only the built-in elements can be written to a .neoui.xml"),
        };

        XElement node = new(Ns + name);
        if (element is NeoCanvas) node.Add(new XAttribute("xmlns", Namespace));

        if (element is not NeoCanvas) WriteLayout(element, node);

        foreach (AttributeBinding binding in Bindings(element))
        {
            string? value = binding.Get();
            if (value is null || value == binding.Default) continue;
            node.SetAttributeValue(binding.Name, value);
        }

        if (element is NeoButton { OnClickActions.Count: > 0 } button)
        {
            XElement actions = new(Ns + "Button.onClick");
            foreach (var action in button.OnClickActions) actions.Add(NeoUIActionXml.Write(action, Ns));
            node.Add(actions);
        }

        foreach (NeoUIElement child in element.Children) node.Add(WriteElement(child));
        return node;
    }

    private static void WriteLayout(NeoUIElement element, XElement node)
    {
        ScreenTransform t = element.ScreenTransform;
        ScreenTransform defaults = new();

        if (t.AnchorMin != defaults.AnchorMin || t.AnchorMax != defaults.AnchorMax)
            node.SetAttributeValue("anchor", NeoUIValues.AnchorText(t.AnchorMin, t.AnchorMax));
        if (t.Pivot != defaults.Pivot) node.SetAttributeValue("pivot", NeoUIValues.Vector(t.Pivot));

        if (t.StretchesX)
        {
            if (t.InsetLeft != 0) node.SetAttributeValue("left", NeoUIValues.Float(t.InsetLeft));
            if (t.InsetRight != 0) node.SetAttributeValue("right", NeoUIValues.Float(t.InsetRight));
        }
        else
        {
            if (t.Position.X != 0) node.SetAttributeValue("x", NeoUIValues.Float(t.Position.X));
            if (t.Size.X != 0) node.SetAttributeValue("width", NeoUIValues.Float(t.Size.X));
        }

        if (t.StretchesY)
        {
            if (t.InsetTop != 0) node.SetAttributeValue("top", NeoUIValues.Float(t.InsetTop));
            if (t.InsetBottom != 0) node.SetAttributeValue("bottom", NeoUIValues.Float(t.InsetBottom));
        }
        else
        {
            if (t.Position.Y != 0) node.SetAttributeValue("y", NeoUIValues.Float(t.Position.Y));
            if (t.Size.Y != 0) node.SetAttributeValue("height", NeoUIValues.Float(t.Size.Y));
        }

        if (t.WidthMode != defaults.WidthMode) node.SetAttributeValue("widthMode", NeoUIValues.EnumText(t.WidthMode));
        if (t.HeightMode != defaults.HeightMode) node.SetAttributeValue("heightMode", NeoUIValues.EnumText(t.HeightMode));
        if (t.Rotation != 0) node.SetAttributeValue("rotation", NeoUIValues.Float(RadiansToDegrees(t.Rotation)));
        if (t.Scale != Vector2.One) node.SetAttributeValue("scale", NeoUIValues.Vector(t.Scale));
    }

    private static readonly HashSet<string> LayoutNames = DescribeLayout().Select(a => a.Name).ToHashSet();

    // files store degrees (readable by hand, what Unity's inspector shows); the runtime stores radians. written rounded to 4 decimals so 15 stays 15 through any number of round trips
    private static float DegreesToRadians(float degrees) => (float)(degrees * Math.PI / 180.0);

    private static float RadiansToDegrees(float radians) => (float)Math.Round(radians * 180.0 / Math.PI, 4);
}
