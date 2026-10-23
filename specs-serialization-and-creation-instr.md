# Specs: Serialization, Creation, and Custom Stuff

If you're here reading this, you're either gonna be making your own specs or actions, how their data turns into JSON and back, what the schema/editor sees, and the dont-shoot-yourself-in-the-foot parts.

note to future self: if you're here because a map somehow erred on load, skip to [Ah I've went ahead and shot myself in the foot](#footguns). it's probably, possibly, potentially, and HOPEFULLY one of those. If it's not... pick your poison bruh. .50 cal or .338 Lapua

---

## What a spec is, again

a spec is a packet of authoring data that adds components to an object when a map builds. one spec type exists in three environments/forms:

1. **code**: a C# type implementing `INeoSpec`. its constructor params are its data, its `Apply` is its behaviour
2. **schema**: `Data/<Game>.neoschema.json`, regenerated from the code on every build, under `$defs` as `spec:<Name>`. the editor reads this
3. **files**: prefab libraries and maps, as `{ "type": "<Name>", "properties": { ... } }`

the code is the source of truth. the schema is derived from it and the files have to match the schema. change the code and you change the other two.

---

## Making one

```csharp [InsideYourGame.cs]
public sealed record DoorSpec(
    [property: NeoAsset(typeof(Texture2D))] string Texture,
    string Level = "level1"
) : INeoSpec
{
    public void Apply(NeoObject target)
    {
        target.AddComponent(new NeoSpriteRenderer(NeoGame.Singleton.RenderBus, NeoGame.Singleton.Assets.Load<Texture2D>(Texture)));
        target.AddComponent(new Door { Level = Level });
    }
}
```

```json
{ "type": "DoorSpec", "properties": { "texture": "Sprites/door", "level": "forest/level2" } }
```

- `Apply` runs once per object while the map builds, so `NeoGame.Singleton`, its buses and `Assets` are all up by then. add components, set tags, whatever, that's the whole job
- a spec doesn't stay on the object after `Apply`. the components you add are what stays, so put runtime state on the component, not the spec
- one spec instance gets made per object per build, so it's fine to read its fields in `Apply` without worrying about sharing

no registration call. build the game and it's in the schema, the editor and the map loader.

---

## Names and discovery

- the name in files = the C# class name. `DoorSpec` is `"type": "DoorSpec"`
- if you want to pin the name of a type after serialization, put `[NeoName("Door")]` on the type. This allows you the convenience to rename the class later without breaking every file that uses it. do this for anything that already lives in maps
- two specs with the same name throw on scan, with both full type names in the message. to solve this, one of them needs a `[NeoName]`, or just change class names. latter is better for the long-run.
- a spec has to be `public`, non-abstract and non-generic. otherwise it's skipped on scan

**where specs have to exist:** in the **game project itself** (the entry assembly) or in NeoGameLib. specs in a separate class library that the game references are NOT picked up, neither by the runtime nor by the schema tool (fuck the old documentations). if you really need one there, keep it in the game project for now.

---

## The constructor is the schema

positional records are the intended shape, since the params give you names, types, defaults and required-ness in one line:

| C# param | schema | file |
|---|---|---|
| `string Texture` (no default) | `required` | must be present (net9, see below) |
| `int Slots = 4` | `"default": 4` | optional, missing = 4 |
| `Vector2 Offset = default` | `"default": { "x": 0, "y": 0 }` | optional |
| `NeoColor Tint = null` | `anyOf [NeoColor, null]` | optional, missing = null |

- property names are camelCase in files: `Texture` -> `"texture"`, `OnEnter` -> `"onEnter"`
- **renaming a param renames its JSON key**, and every file that used the old key throws on load. `[NeoName]` can't help here because it only covers the type name. rename params only together with the data, or not at all
- **required only works on net9.** with net9 a param without a default must be in the file, and if it isn't the load throws. on net8 a missing param quietly becomes `null`/`0`. the schema marks it required either way, so the editor complains, but the game won't. target net9 if you care

classes with setters work too:

```csharp
public sealed class SpeedSpec : INeoSpec
{
    public float Speed { get; set; } = 200f; // default 200 shows up in the schema
    public int Hp = 3; // public fields count too
    public int Computed { get; } = 9; // get-only: not in files, not in the schema
    public void Apply(NeoObject target) { /* ... */ }
}
```

the defaults survive into the schema as long as there's a public parameterless constructor (the schema tool builds one sample instance and reads it). but a class can't mark anything required, so records are still better.

---

## How each type serializes

| C# type | in files | schema / editor |
|---|---|---|
| `string`, `bool`, `int`/`long`/`byte`..., `float`/`double` | JSON string / bool / number | `string` / `boolean` / `integer` / `number` |
| `string` with `[NeoAsset(typeof(Texture2D))]` | content path string, e.g. `"Sprites/door"` | string + `x-neo-asset: "Texture2D"`, the editor shows an asset picker |
| enum | camelCase string: `EnemyKind.BigSlime` -> `"bigSlime"` | `enum` of the camelCase names |
| `Vector2`, `Point` | `{ "x": .., "y": .. }` | object def |
| `NeoColor` | `{ "r", "g", "b", "a" }`, 0-255, `a` defaults to 255 | object def, r/g/b required |
| `TileRect` | `{ "x", "y", "width", "height" }` | object def |
| your own record/class | nested object | its own `$defs` entry, keyed by full type name |
| `List<T>` / `T[]` | array | `array` of T |
| `Dictionary<string, T>` | object with any keys | `additionalProperties` of T |
| `List<INeoAction>` | list of `{ "type", "properties" }` action entries | action entry union, the editor shows the action list UI |
| `INeoSpec` / `List<INeoSpec>` | spec entries, same shape | spec entry union |
| `JsonObject` / `JsonNode` | anything | no constraints (`{}`), the editor can't help you |
| any reference type / `T?` | the value or `null` | `anyOf [T, null]` |

a few notes on that table:

- `[NeoAsset]` works on the param or on the property (`[property: NeoAsset(...)]` on a record). both end up in the schema
- enums are read case-insensitively (`"BigSlime"` loads too), but integers are rejected (`"kind": 1` throws). the editor writes camelCase, so stick to that
- nested records follow the same rules all the way down: camelCase, constructor defaults, unknown keys throw

### custom stuff, examples

nested data:

```csharp
public enum EnemyKind { Slime, BigSlime }
public sealed record Drop(string Item, float Chance = 1f);

public sealed record EnemySpec(
    [property: NeoAsset(typeof(Texture2D))] string Texture,
    EnemyKind Kind = EnemyKind.Slime,
    List<Drop> Drops = null,
    Dictionary<string, int> Stats = null,
    List<INeoAction> OnDeath = null, // actions inside your own spec, same as TriggerSpec does it
    NeoColor Tint = null
) : INeoSpec
{
    public void Apply(NeoObject target) { /* ... */ }
}
```

```json
{ "type": "EnemySpec", "properties": {
    "texture": "Sprites/slime",
    "kind": "bigSlime",
    "drops": [ { "item": "gem", "chance": 0.5 } ],
    "stats": { "hp": 3 },
    "onDeath": [ { "type": "DestroyAction", "properties": { "target": "self" } } ],
    "tint": { "r": 120, "g": 255, "b": 120 }
} }
```

to run those actions yourself: `foreach (INeoAction a in OnDeath) a.Invoke(new NeoActionContext(self, other));`. game-defined actions follow every rule in this doc too, they're just `INeoAction` instead of `INeoSpec`, see [Actions](#actions).

custom JSON shape with a converter:

```csharp
public sealed class HhmmConverter : JsonConverter<TimeSpan> { /* Read/Write "12:30" */ }
public sealed record OpeningHoursSpec([property: JsonConverter(typeof(HhmmConverter))] TimeSpan Opens) : INeoSpec { ... }
```

this loads fine in the game, but the schema can't see inside a converter, so the property shows up as `{}` (unknown) in the editor. it's fine for stuff you hand-edit, annoying for anything you'd want a nice field for. prefer plain records.

---

## Footguns

**MonoGame `Color` and `Rectangle` as spec params.** they serialize, but badly:
- `Color` writes `r/g/b/a` PLUS `packedValue`, and a file that leaves out `a` loads as alpha 0, which is fully transparent. that's an invisible sprite and a fun hour of debugging. use `NeoColor` (`.ToColor()` in `Apply`)
- `Rectangle` exposes `x/y/width/height` AND `location`/`size` in the schema, so the editor shows the same thing twice. use `TileRect`
- `Vector2` and `Point` are fine

**interfaces and abstract types** (other than `INeoSpec`/`INeoAction`) can't be deserialized, and the map throws `NotSupportedException`. use a concrete record, or a list of spec/action entries if you want polymorphism.

**unknown keys throw**, in specs and in every nested record. typos fail loudly on purpose. the flip side is that removing a param from a spec makes every file still carrying that key fail. remove the key from the data too.

**get-only properties** without a matching constructor param are skipped: they're not in the schema and can't be set from files.

**specs in another assembly** aren't discovered, see [Names and discovery](#names-and-discovery).

**errors at build time** name the object and the spec: `'Door-A' (spec 'TriggerSpec'): ...`. a `JsonException` inside means the file doesn't match the code (wrong type, unknown key, missing required). `unknown spec 'X'` means the type isn't discovered or the name is wrong.

---

## Specs in files

in a prefab library or in a map object's `specs`:

```json
{ "type": "SpriteSpec", "id": "body", "properties": { "texture": "Sprites/slime" } }
```

- `type`: the spec name. `properties`: the spec's data; missing or `{}` = all defaults
- `id` (optional, free text) names the entry so map objects can override it. without an `id`, overrides key on the spec name
- map object `overrides` shallow-merge top-level properties into the prefab's entries: `"overrides": { "body": { "color": { "r": 255, "g": 0, "b": 0 } } }`. nested values (lists, records) get replaced whole, not merged. `null` resets the entry to spec defaults, and an override key that matches no entry throws
- specs apply in list order: prefab specs first, then the object's own

writing from code (editors, tools): `NeoMap.ToJson()` / `NeoPrefabLibrary.ToJson()` write every entry as `{ "type", "properties" }` with every property, nulls included.

---

## Built-in specs

| spec | properties |
|---|---|
| `SpriteSpec` | `texture` (Texture2D, required), `depth = 0`, `order = 0`, `layer = 1`, `anchor` (Vector2?), `source` (TileRect?), `color` (NeoColor?, 0.4.3) |
| `TextSpec` | `font` (SpriteFont, required), `text = ""`, `depth = 0`, `order = 0`, `layer = 1`, `anchor` (Vector2?) |
| `BoxColliderSpec` | `size` (required), `offset = (0,0)`, `isTrigger = false`. inside a collider group it's the per-cell shape (see 0.4.2 Collider group shapes in `instructions.md`) |
| `SpriteColliderSpec` | none |
| `CameraSpec` | `zoom = 1`, `order = 0`, `cullingMask = -1` |
| `TriggerSpec` | `size` (required), `tag` (null = anything), `onEnter` / `onStay` / `onExit` action lists, `offset = (0,0)` |

heads up: `order` on `SpriteSpec`/`TextSpec` currently does nothing. the render bus overwrites `Order` with its own registration counter when the renderer registers, so draw order is add order. use `depth` until that's fixed.

### [0.4.3] `SpriteSpec.color`

texture tinting.

```json
{ "type": "SpriteSpec", "properties": { "texture": "Sprites/wall", "color": { "r": 255, "g": 128, "b": 0, "a": 200 } } }
```

- 0-255 per channel, `a` is optional and defaults to 255 (opaque)
- straight alpha, like CSS `rgba()`: `a: 128` = half transparent. the conversion to MonoGame's premultiplied blending is done for you
- leave it out (or `null`) for an untinted sprite: the renderer keeps its default white

---

## Actions

an action is the same as a spec, but for behaviour/functions. an action DOES something when it fires (load a level, destroy the coin, open the shop).

| | spec | action |
|---|---|---|
| interface | `INeoSpec` | `INeoAction` |
| runs | once, while the map builds | every time it fires (mostly from a trigger) |
| method | `Apply(NeoObject target)` | `Invoke(NeoActionContext ctx)` |
| schema | `spec:<Name>` | `action:<Name>`, listed in `x-neo-actions` |
| in files | an object's `specs` list | an action list property, like `TriggerSpec`'s `onEnter` |

### the built-ins

| action | properties | what it does |
|---|---|---|
| `LoadWorldAction` | `world` (required) | loads a world next to the running ones, never unloads anything. a world that isn't registered yet gets registered from the map with the same name |
| `SwitchWorldAction` | `world` (required), `unload` (null) | unloads `unload` (null = the trigger's own world, `[]` = nothing), then loads `world` like `LoadWorldAction`. list `world` itself in `unload` to restart a level |
| `UnloadWorldAction` | `world` (null) | null = the trigger's own world |
| `DestroyAction` | `target = "self"` | queues `self` or `other` for destruction at the end of the world tick |
| `SetEnabledAction` | `component` (required), `enabled = true`, `target = "self"` | sets `Enabled` on the first component on the target whose class name matches |

- `target` is `"self"` (the object with the trigger) or `"other"` (whatever walked into it)
- `SetEnabledAction` matches the EXACT runtime class name, base classes don't count: `"NeoBoxCollider"` won't find a `NeoTrigger`, even though a trigger is a box collider. only the first match is changed
- `SwitchWorldAction` that unloads its own world destroys the trigger with it, so actions after it in the same list never run. put it last
- world names are just strings, so a typo'd world isn't caught when the map loads. it's caught when the action fires, mid-game

```json
{ "type": "TriggerSpec", "properties": {
    "size": { "x": 32, "y": 32 }, "tag": "Player",
    "onEnter": [
      { "type": "DestroyAction", "properties": { "target": "self" } },
      { "type": "SwitchWorldAction", "properties": { "world": "forest/level2" } }
    ]
} }
```

### making one

```csharp [InsideYourGame.cs]
[NeoName("Heal")]
public sealed record HealAction(int Amount = 1, NeoActionTarget Target = NeoActionTarget.Other) : INeoAction
{
    public void Invoke(NeoActionContext ctx)
    {
        NeoObject target = Target == NeoActionTarget.Self ? ctx.Self : ctx.Other;
        if (target is null) return;
        if (target.TryGetComponent(out Health health)) health.Current += Amount;
    }
}
```

```json
{ "type": "Heal", "properties": { "amount": 3 } }
```

`NeoActionContext` is all you get:

| member | what it is |
|---|---|
| `ctx.Self` | the object the action belongs to. for a trigger: the trigger's object |
| `ctx.Other` | the other object. for a trigger: whatever overlapped it. can be `null` when you invoke actions yourself, so null-check it like the built-ins do |
| `ctx.World` | `Self`'s world |

everything else goes through `NeoGame.Singleton` (`WorldManager`, buses, `Assets`), same as in a spec's `Apply`.

reuse `NeoActionTarget` for your own self/other params so the editor shows the same dropdown as the built-ins.

### how triggers fire them

`TriggerSpec` adds a `NeoTrigger`, which runs `onEnter` once when something enters, `onStay` every frame it stays, `onExit` once when it leaves. all of this happens inside the collision pass, after the worlds tick, so loading worlds and destroying objects from an action is safe.

the stuff that fucked me (all of it checked by actually running a trigger):

- **no `tag` = fires on EVERY collider**: walls, merged collider groups, other triggers, everything. two untagged triggers that overlap fire on each other. set a `tag` unless you really mean anything
- **it's per collider, not per object**: an object with two colliders inside the trigger enters twice
- **the tag is only checked on enter**: an object that gets the tag while it's already inside doesn't fire anything until it leaves and comes back in
- **an action that destroys the trigger's own object stops the list**: the actions after it don't run
- **an action that throws crashes the game**: the exception goes straight out of the collision pass, nothing catches it. if an action can fail, handle it inside `Invoke`
- **one action instance per trigger, reused on every fire**: the map deserializes the action once when it builds, and the same instance runs every time. a field you change in `Invoke` is still changed next time. keep actions stateless and put state on a component

### rules that differ from specs

- there's no `id`, and map object `overrides` can't reach into a single action: override the whole list property (`"onEnter": [ ... ]`) like the 0.4.0 Maps example in `instructions.md` does
- name clashes are per kind: a spec and an action can share a name, two actions can't

---

## Checklist for a new spec

1. `public sealed record XSpec(...) : INeoSpec` in the game project
2. data in constructor params, defaults on everything that's optional
3. `[NeoAsset(typeof(...))]` on content paths, `[NeoName("X")]` if files already use it
4. `NeoColor` / `TileRect` / your own records instead of MonoGame `Color` / `Rectangle`
5. build, then check `spec:XSpec` in the schema and open it in the editor

same checklist for an action: `INeoAction` instead of `INeoSpec`, `Invoke` instead of `Apply`, and check `action:XAction` in the schema.
