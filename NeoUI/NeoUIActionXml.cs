using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using NeoGameLib.NeoMapping;
using NeoGameLib.NeoMapping.DurableActions;

namespace NeoGameLib.NeoUI;

// note to future self:
// actions in a .neoui.xml. there is ONE loading path for actions: the xml element is turned into the same JsonObject a map file would hold, and NeoRegistry.CreateAction builds it. so defaults, required constructor params and unknown-key errors behave exactly like in maps.
// the shape rules (the same as a XAML-ish object element):
//   <SwitchWorldAction world="level1">                the element name is the action's registry name, scalar properties are attributes
//     <SwitchWorldAction.unload>                      lists and nested records are property elements named "<owner element>.<property>"
//       <item>menu</item>                             list items are <item> (scalars hold their text; records use attributes + item.<property> elements)
//     </SwitchWorldAction.unload>
//   </SwitchWorldAction>
// scalar = string, bool, integers, floats, enums (camelCase) and Vector2 ("x y"). a list of INeoAction holds action elements directly.
internal static class NeoUIActionXml
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---- XML -> action ----

    internal static INeoAction Read(XElement element)
    {
        string name = element.Name.LocalName;
        JsonObject properties = ReadObject(element, ResolveAction(element), name);
        try
        {
            return NeoRegistry.CreateAction(name, properties);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException)
        {
            throw UIFormatException.At(element, $"action '{name}': {ex.Message}");
        }
    }

    private static Type ResolveAction(XElement element)
    {
        string name = element.Name.LocalName;
        return NeoRegistry.Actions.TryGetValue(name, out Type type)
            ? type
            : throw UIFormatException.At(element, $"unknown action '{name}', is the assembly that defines it the game or NeoGameLib? the known actions are: {string.Join(", ", NeoRegistry.Actions.Keys.Order())}");
    }

    private static JsonObject ReadObject(XElement element, Type type, string ownerName)
    {
        Dictionary<string, JsonPropertyInfo> props = NeoRegistry.Options.GetTypeInfo(type).Properties.ToDictionary(p => p.Name);
        JsonObject result = new();

        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration) continue;
            if (!props.TryGetValue(attribute.Name.LocalName, out JsonPropertyInfo prop))
                throw UIFormatException.At(attribute, $"'{ownerName}' has no property '{attribute.Name.LocalName}'");
            if (!IsScalar(prop.PropertyType))
                throw UIFormatException.At(attribute, $"'{prop.Name}' holds a list or an object, write it as a property element <{ownerName}.{prop.Name}>");

            result[prop.Name] = ScalarNode(prop.PropertyType, attribute.Value, attribute);
        }

        foreach (XElement child in element.Elements())
        {
            string prefix = ownerName + ".";
            string childName = child.Name.LocalName;
            if (!childName.StartsWith(prefix, StringComparison.Ordinal))
                throw UIFormatException.At(child, $"expected a property element named '{prefix}<property>' inside '{ownerName}', found '{childName}'");

            string propName = childName[prefix.Length..];
            if (!props.TryGetValue(propName, out JsonPropertyInfo prop))
                throw UIFormatException.At(child, $"'{ownerName}' has no property '{propName}'");

            result[prop.Name] = ReadComposite(prop.PropertyType, child);
        }

        return result;
    }

    private static JsonNode ReadComposite(Type type, XElement element)
    {
        if (IsList(type, out Type itemType))
        {
            JsonArray array = new();
            foreach (XElement item in element.Elements())
            {
                if (itemType == typeof(INeoAction))
                {
                    // one pass: names and properties are checked here (with this item's line), the outer action's CreateAction builds the whole tree
                    array.Add(new JsonObject { ["type"] = item.Name.LocalName, ["properties"] = ReadObject(item, ResolveAction(item), item.Name.LocalName) });
                    continue;
                }

                if (item.Name.LocalName != "item")
                    throw UIFormatException.At(item, $"list items are written <item>, found '{item.Name.LocalName}'");

                array.Add(IsScalar(itemType) ? ScalarNode(itemType, item.Value, item) : ReadObject(item, itemType, "item"));
            }
            return array;
        }

        if (IsScalar(type)) throw UIFormatException.At(element, "scalar properties are attributes, not property elements");
        return ReadObject(element, type, element.Name.LocalName);
    }

    private static JsonNode ScalarNode(Type type, string text, XObject at)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (t == typeof(string)) return JsonValue.Create(text);
            if (t == typeof(bool)) return JsonValue.Create(NeoUIValues.ParseBool(text));
            if (t.IsEnum) return JsonValue.Create(NeoUIValues.Camel(Enum.GetName(t, NeoUIValues.ParseEnum(t, text))!));
            if (t == typeof(Vector2))
            {
                Vector2 v = NeoUIValues.ParseVector(text);
                return new JsonObject { ["x"] = v.X, ["y"] = v.Y };
            }
            if (IsIntegral(t)) return JsonValue.Create(long.Parse(text.Trim(), NumberStyles.Integer, Inv));
            return JsonValue.Create(double.Parse(text.Trim(), NumberStyles.Float, Inv));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw UIFormatException.At(at, ex is FormatException && ex.Message.StartsWith('\'') ? ex.Message : $"'{text}' is not a valid {t.Name}");
        }
    }

    // ---- action -> XML ----

    internal static XElement Write(INeoAction action, XNamespace ns)
    {
        Type type = action.GetType();
        string name = NeoRegistry.NameOf(type);

        JsonObject properties = (JsonObject)JsonSerializer.SerializeToNode(action, type, NeoRegistry.Options)!;
        XElement element = new(ns + name);
        WriteObject(element, ns, type, properties, name);
        return element;
    }

    private static void WriteObject(XElement element, XNamespace ns, Type type, JsonObject values, string ownerName)
    {
        Dictionary<string, JsonPropertyInfo> props = NeoRegistry.Options.GetTypeInfo(type).Properties.ToDictionary(p => p.Name);
        foreach ((string key, JsonNode node) in values)
        {
            if (node is null) continue; // null = the constructor default
            JsonPropertyInfo prop = props[key];

            if (IsScalar(prop.PropertyType)) element.SetAttributeValue(key, ScalarText(prop.PropertyType, node));
            else element.Add(WriteComposite(ns, $"{ownerName}.{key}", prop.PropertyType, node));
        }
    }

    private static XElement WriteComposite(XNamespace ns, string elementName, Type type, JsonNode node)
    {
        XElement element = new(ns + elementName);

        if (IsList(type, out Type itemType))
        {
            foreach (JsonNode item in (JsonArray)node)
            {
                if (itemType == typeof(INeoAction))
                {
                    JsonObject entry = (JsonObject)item;
                    string actionName = (string)entry["type"]!;
                    XElement child = new(ns + actionName);
                    WriteObject(child, ns, NeoRegistry.Actions[actionName], (JsonObject)entry["properties"]!, actionName);
                    element.Add(child);
                }
                else if (IsScalar(itemType)) element.Add(new XElement(ns + "item", ScalarText(itemType, item)));
                else
                {
                    XElement child = new(ns + "item");
                    WriteObject(child, ns, itemType, (JsonObject)item, "item");
                    element.Add(child);
                }
            }
            return element;
        }

        WriteObject(element, ns, type, (JsonObject)node, elementName);
        return element;
    }

    private static string ScalarText(Type type, JsonNode node)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(Vector2)) return NeoUIValues.Vector(new Vector2(node["x"]!.GetValue<float>(), node["y"]!.GetValue<float>()));
        if (t == typeof(string) || t.IsEnum) return node.GetValue<string>();
        return node.ToJsonString(); // bools and numbers: already invariant ("true", "12.5")
    }

    // ---- type shapes ----

    private static bool IsIntegral(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
                                              t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte);

    private static bool IsScalar(Type type)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(string) || t == typeof(bool) || t.IsEnum || t == typeof(Vector2) || IsIntegral(t) ||
               t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    private static bool IsList(Type type, out Type itemType)
    {
        itemType = null;
        if (type.IsArray)
        {
            itemType = type.GetElementType();
            return true;
        }

        if (!type.IsGenericType) return false;
        Type def = type.GetGenericTypeDefinition();
        if (def != typeof(List<>) && def != typeof(IList<>) && def != typeof(IReadOnlyList<>) && def != typeof(IEnumerable<>)) return false;

        itemType = type.GetGenericArguments()[0];
        return true;
    }
}
