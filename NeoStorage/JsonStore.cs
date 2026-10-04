using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeoGameLib.NeoStorage;

// note to future self:
// check out instructions.md if you forgot the json/file store architecture. basically tauri v2's file-as-an-object-store concept
// but json store is based off of FileStore and is a wrapper on top of FileStore functions.
// JsonStore overrides the original load by letting the base class load strings to buffer and then converting that buffer to a json object.
// store has its own abstracted "buffer" as a json object data
// Write is just the other way around, the "buffer" json object data to base class buffer and then call base class write to text file.
public class JsonStore : FileStore
{
    // note to self:
    // monogame classes and structures i.e. Vector2, Rectangle, etc. are public fields, which means that they'll be ignored by System.Text.Json. It serializes to {} as default without throwing an error.
    // Thankfully (thank you and only for this one time, microsoft), microsoft had thought of this and has an IncludeFields option that should solve this issue in their json serializer.
    // and now you get the luxury of customizing how json is serialized if you need it. yay!
    //
    // FUCK JSON SERIALIZATION. consistent pain in the ass since unity and frontend/backend bullshit and it's still biting my ass even in monogame
    private static readonly JsonSerializerOptions DefaultOptions = new() { IncludeFields = true };

    private JsonObject _data = new();

    public JsonSerializerOptions Options { get; set; } = DefaultOptions;

    public JsonStore(string filePath) : base(filePath) { }

    public override void Load()
    {
        base.Load();
        _data = string.IsNullOrWhiteSpace(Buffer) ? new JsonObject() : ((JsonObject.Parse(Buffer) as JsonObject) ?? new JsonObject());
    }

    public override void Write()
    {
        Buffer = _data.ToJsonString();
        base.Write();
    }

    public T Get<T>(string key)
    {
        return _data.TryGetPropertyValue(key, out JsonNode node) ? node.Deserialize<T>(Options) : default;
    }

    public void Set(string key, object value)
    {
        _data[key] = JsonSerializer.SerializeToNode(value, Options);
    }
}
