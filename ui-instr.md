# UI: Canvases, Elements, Layout, and Scaling

everything about the screen-space UI.

note to future self: every code block in this file was compiled against the actual library, and every layout number in here came out of a real layout pass, not out of my head. if you change the UI code, recheck the blocks. the old docs used `Transform` on UI elements for months and it never compiled once. that's how I found out.

---

## [0.4.4] it behaves like html/css now

if you know web dev, the defaults should already be what you expect

so this just works, no `Size`/`WidthMode`/`Color` needed:

```csharp
NeoFlexBox row = new() { Align = NeoFlexAlign.Center, Gap = 2 }; // transparent, wraps its children, sits at the top-left
row.Children.Add(new NeoImage { Texture = Content.Load<Texture2D>("Sprites/Inventory/gem") }); // as big as the texture
row.Children.Add(new NeoLabel { Text = "X0", Font = font });
```

to upgrade old UI code: put `Anchor = new(0.5f), Pivot = new(0.5f)` back on anything that relied on being centered, `Color = Color.White` on panels that relied on the white default, and `Enabled = false` on top of `Visible = false` is no longer needed.

(object initializers like `new NeoLabel { Text = "X0" }` vs setting properties after `new()` make no difference at all: an initializer IS "construct, then set each property". my HUD broke because of the old defaults above, not because of how I wrote it.)

---

## The tree

```
NeoGame.UIBus
 └─ NeoCanvas            (a root, one per HUD/menu/layer, never a child)
     └─ NeoPanel         (a div: optional background Color, has Padding)
         ├─ NeoLabel     (text)
         ├─ NeoButton    (clickable text)
         ├─ NeoImage     (texture)
         └─ NeoFlexBox   (panel that lays its children out in a row/column)
             └─ ...
```

- every element is a `NeoUIElement`. UI elements are NOT `NeoObject`s and have nothing to do with worlds, components or cameras
- canvases are drawn AFTER the world, so they ignore the camera entirely
- `NeoGame.UIBus` updates every canvas before `OnUpdate`/the worlds tick, and draws them after the world pass

```csharp [InsideAClassInheritingNeoGameClass.cs]
SpriteFont font = Content.Load<SpriteFont>("fonts/ui");

NeoCanvas canvas = new();
NeoPanel panel = new()
{
    ScreenTransform = { Position = new Vector2(10, 10), Size = new Vector2(200, 100) }, // 10,10 from the canvas's top-left
    Color = Color.Gray
};
NeoLabel label = new() { Text = "HP 100", Font = font, Color = Color.White }; // Font is borrowed, never disposed by the UI

panel.Children.Add(label);
canvas.Children.Add(panel);
UIBus.Add(canvas); // or canvas.Subscribe();
```

- `UIBus.Add(canvas)` / `canvas.Subscribe()` register a canvas, `UIBus.Remove(canvas)` / `canvas.Unsubscribe()` take it off again without destroying anything
- a canvas can only be on one bus. `canvas.Order` sorts canvases: lower draws first, ties draw in add order
- `Children` is a normal collection (`Add`, `Insert`, `Remove`, `Clear`, indexer). children draw in list order, so later = on top
- an element has one parent. remove it before adding it somewhere else, otherwise it throws. a canvas can't be a child, and an element can't contain itself
- adding/removing elements, canvases, whatever from inside `OnUpdate` or `OnClick` is safe (same deal as the 0.3.2 lifecycle iteration stuff in `instructions.md`)

---

## ScreenTransform: where an element is

all layout lives on `element.ScreenTransform`. it's get-only: set it with the nested initializer, or set its members directly.

```csharp
NeoPanel badge = new()
{
    ScreenTransform =
    {
        Size = new Vector2(64, 64),
        Anchor = new Vector2(1, 0), Pivot = new Vector2(1, 0), // top-right corner
        Position = new Vector2(-8, 8),
        WidthMode = NeoUISizing.Fixed, HeightMode = NeoUISizing.Fixed, // optional here, Auto uses Size anyway once it's set
        Rotation = MathHelper.ToRadians(15), Scale = new Vector2(1.2f) // visual only
    }
};

badge.ScreenTransform.Position = new Vector2(-16, 8); // changing it later works the same
Vector2 topLeft = badge.ScreenTransform.LayoutPosition; // computed, read-only
```

it inherits the world `Transform`, so `Position`, `Rotation`, `Scale` and the Up/Right helpers are the same members you use on game objects, plus:

| member | what it does |
|---|---|
| `Size` | the box size in UI units. with the default `Auto` mode an axis left at 0 sizes to the content instead. non-negative |
| `Anchor` | the point on the PARENT's inner box this element hangs from, 0-1 per axis. default (0, 0) = top-left |
| `Pivot` | the point on THIS box that sits on the anchor, 0-1 per axis. default (0, 0) = top-left |
| `Position` | offset from the anchor, in UI units. with the defaults that's just "offset from the parent's top-left", like css `top`/`left` |
| `WidthMode` / `HeightMode` | `Auto` (default), `Fixed`, `Fill` or `Fit`, see Sizing |
| `Rotation` / `Scale` | visual only, see below |
| `LayoutPosition` / `LayoutSize` | computed top-left and size from the last update/draw, in canvas units. read-only |
| `DesiredSize` | what the element asked for during measuring. read-only |

the placement formula:

```
top-left = parent inner top-left + parent inner size * Anchor + Position - own size * Pivot
```

with the defaults (both 0) an element sits at the parent's top-left plus `Position`. to center something, set both to 0.5: a 200x100 panel on an 800x600 canvas lands at (300, 250). (1, 0) for both = top-right corner, (0.5, 1) = bottom-center, and so on.

- "inner box" = the parent's box minus its `Padding` (panels and flexboxes only)
- anchors and pivots outside 0-1 throw, and so do negative sizes
- `NeoFlexBox` children and canvases ignore `Anchor`, `Pivot` and `Position`: the flexbox places its children itself, and a canvas always covers the whole viewport

### Rotation and Scale

they work like a CSS transform: the element and its children turn/scale around the Pivot when drawn.

- layout, Fit and NeoFlexBox use the untransformed box, so a rotated button doesn't push its neighbours around
- hit tests (`ContainsScreenPoint`, buttons) follow the drawn shape
- a rotated or scaled element costs one extra SpriteBatch pass for its subtree
- the canvas's own Rotation/Scale do nothing, use the canvas scale settings instead

---

## Sizing: Auto, Fixed, Fill, Fit

every element picks its size per axis with `ScreenTransform.WidthMode` and `ScreenTransform.HeightMode`:

- `Auto` (default): like css `width: auto`. use `Size` on that axis when it's set (above 0), otherwise wrap the content: the measured text for labels/buttons, the texture for images, or the smallest box that holds all visible children, plus `Padding`
- `Fixed`: always use `Size`, even when it's 0
- `Fill`: take the parent's full inner length. inside a `NeoFlexBox` it shares the leftover space on the main axis instead (see FlexBox)
- `Fit`: always wrap the content, ignoring `Size`

so most of the time you don't touch the modes at all: leave `Size` alone to wrap, set it to pin the size, and it works per axis (`Size = new(100, 0)` = 100 wide, as tall as the content).

```csharp
// a panel that wraps its children. no flexbox, no modes needed
NeoPanel card = new()
{
    Color = Color.Gray,
    Padding = new NeoPadding(16, 8) // 16 left and right, 8 top and bottom
};
card.Children.Add(new NeoLabel { Text = "HP 100", Font = font });
```

a 100x20 label in that card comes out as a 132x36 card with the label at (16, 8) inside it.

the gotchas:

- an element with nothing to measure (a panel with no children and no `Size`, a label with no `Font`, an image with no `Texture`) is 0x0 and draws nothing
- a wrapping parent around a `Fill` child wraps the child's content (like css), then the child stretches to that. with an empty `Fill` child there's nothing to wrap, so give the parent a `Size`
- on a canvas, `Fill` = the whole canvas (minus nothing, canvases have no padding)

### Padding

panels (and flexboxes, since they inherit panels) have per-side `Padding`. the children anchor to the padded inner box, `Fill` takes the inner size and `Fit` adds the padding around the children.

```csharp
panel.Padding = 12; // same on all four sides
panel.Padding = new NeoPadding(16, 8); // horizontal, vertical
panel.Padding = new NeoPadding(4, 8, 4, 16); // left, top, right, bottom
panel.Padding = new NeoPadding(8) { Bottom = 24 }; // 8 everywhere except the bottom
```

negative or non-finite padding throws. (Ideally I should change this in the future but for now this works) If the padding is bigger than the panel then it'll squeeze the inner box to 0.

only `NeoPanel` and `NeoFlexBox` have `Padding`. labels, buttons and images don't, so wrap them in a panel if you need space around them.

---

## FlexBox

`NeoFlexBox` is a `NeoPanel` that orders its children in a row or a column. it controls the children's positions, so their `Anchor`/`Pivot`/`Position` do nothing.

```csharp
NeoFlexBox menu = new()
{
    Direction = NeoFlexDirection.Column,
    Gap = 8, Padding = 12,
    Justify = NeoFlexAlign.Center,
    Align = NeoFlexAlign.Stretch,
    // no Size: it wraps its buttons + gap + padding
    Color = Color.DarkSlateGray
};
menu.Children.Add(new NeoButton { Text = "Play", Font = font });
menu.Children.Add(new NeoButton { Text = "Quit", Font = font });
```

| property | default | what it does |
|---|---|---|
| `Direction` | `Row` | `Row` = left to right, `Column` = top to bottom |
| `Gap` | 0 | UI units between children on the main axis |
| `Justify` | `Start` | where the children go on the main axis |
| `Align` | `Start` | where each child goes on the cross axis |
| `Padding`, `Color` | | same as `NeoPanel` |

`Justify` and `Align` both take a `NeoFlexAlign`, but not every value means something on both axes. measured on a 200-wide row:

| value | as `Justify` (main axis) | as `Align` (cross axis) |
|---|---|---|
| `Start` | packed at the start | top (row) / left (column) |
| `Center` | packed in the middle | centered |
| `End` | packed at the end | bottom (row) / right (column) |
| `SpaceBetween` | first at the start, last at the end, leftover split into the gaps | same as `Start` (css has no cross-axis space-between either) |
| `Stretch` | **same as `Start`**, children keep their size. use `Fill` children to grow along the main axis | every child = the full inner cross size |

When `Direction = Column`:

```
 Align = Start            Align = Center           Align = End              Align = Stretch
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│[Play]            │     │      [Play]      │     │            [Play]│     │[      Play      ]│
│[Settings     ]   │     │  [Settings     ] │     │   [Settings     ]│     │[    Settings    ]│
│[Quit  ]          │     │     [Quit  ]     │     │          [Quit  ]│     │[      Quit      ]│
└──────────────────┘     └──────────────────┘     └──────────────────┘     └──────────────────┘
  own widths, left         own widths, middle       own widths, right        all = inner width
```

When `Direction = Row`:

```
 Align = Start                          Align = Stretch
┌────────────────────────────┐         ┌────────────────────────────┐
│[A ]┌─────┐[C]              │         │┌──┐┌─────┐┌─┐              │
│    │  B  │                 │         ││A ││  B  ││C│              │
│    └─────┘                 │         ││  ││     ││ │              │
│                            │         │└──┘└─────┘└─┘              │
└────────────────────────────┘         └────────────────────────────┘
  own heights, top-aligned               all = inner height
```

Padding & Box:

```
┌────────────────────────┐
│ ← Padding              │
│  ┌──────────────────┐  │
│  │ child (stretched)│  │   stretched width = box width − Padding.Left − Padding.Right
│  └──────────────────┘  │
│                 Gap ↕  │
│  ┌──────────────────┐  │ <- Box
│  │ child (stretched)│  │
│  └──────────────────┘  │
└────────────────────────┘
```

Growing Children along the axes w/ `Fill`:

```
 widths: [Fixed 40] [Fill] [Fill]   in a 200-wide row, Gap = 0
┌──────────────────────────────────┐
│[ 40 ][     80     ][     80     ]│
└──────────────────────────────────┘
```

(I asked chat gippity to help me draw in ascii how the general concept (so not just limited to this library, applies to other UI lib flex, though details may vary) of Align, Direction, Flexbox should work.)

- `Fill` on the main axis splits the leftover space evenly between all `Fill` children, and then `Justify` has no leftover left to move things around with
- `Fill` on the cross axis stretches that one child, whatever `Align` says
- a flexbox with no `Size` wraps its children + gaps + padding: two 20-tall children with `Gap = 8` and `Padding = 12` make a 72-tall column
- hidden children (`Visible = false`) take no space
- children never wrap onto a second line. too many children just overflow the box, and nothing clips them

---

## Elements

### NeoPanel

a box behind its children, like a `div`.

| property | default | |
|---|---|---|
| `Color` | `Color.Transparent` | the background. transparent = draws nothing, so a panel without a `Color` is a pure layout box |
| `Padding` | 0 | see Padding |

it's a flat rectangle: no border, no rounded corners, no texture, no 9-slice. for a textured background put a `NeoImage` in it as the first child with `Fill` on both axes.

### NeoLabel

| property | default | |
|---|---|---|
| `Font` | null | a `SpriteFont`. **no font = draws nothing and measures 0x0** |
| `Text` | `""` | |
| `Color` | `Color.White` | |

- sized to `Font.MeasureString(Text)` by default (`Auto` with no `Size`)
- draws at its native size from the top-left of its box: no wrapping, no scaling, no clipping, no alignment inside a bigger box. if you give the label a bigger `Size` or `Fill`, the box gets bigger but the text stays in the top-left corner
- `\n` in `Text` makes multiple lines, since that's what `SpriteFont` does

### NeoButton

a clickable label. it reads the `Input` facade, so it needs a running `NeoGame`.

```csharp
NeoButton play = new() { Text = "Play", Font = font }; // sizes itself to the text
play.OnClick = () => { /* start game */ };
panel.Children.Add(play);
```

| property | default | |
|---|---|---|
| `Font`, `Text` | | same as `NeoLabel` |
| `Color` | `Color.White` | the text color when idle |
| `HoverColor` | `Color.LightGray` | text color while the mouse is over it |
| `PressedColor` | `Color.Gray` | text color while hovered and the left button is held |
| `OnClick` | null | fires when the left button is pressed AND released over the button |
| `Hovered` | | read-only, true while the mouse is over the drawn box |

- it's text only: no background, no border. put it in a `NeoPanel` for that
- unlike the label, the text is centered in the button's box, so a button with a `Size` works the way you'd expect
- mouse only. there's no keyboard/gamepad navigation or focus
- a hidden (`Visible = false`) or disabled (`Enabled = false`) button can't be clicked

### NeoImage

| property | default | |
|---|---|---|
| `Texture` | null | borrowed, never disposed by the UI |
| `SourceRectangle` | null | part of the texture to draw, null = whole texture. negative values throw |
| `Color` | `Color.White` | tint |

- sized to its texture by default, like an `<img>` without width/height: the `SourceRectangle` size if set, else the whole texture
- set `Size` to scale it (the texture is stretched to the box), or one axis of it: `Size = new(36, 0)` = 36 wide, texture height
- no aspect-ratio keeping, no 9-slice

### custom elements

subclass `NeoUIElement` and override what you need:

```csharp
public class HealthBar : NeoUIElement
{
    public float Fraction = 1f;
    public Texture2D Pixel;

    public override void OnUpdate(GameTime gameTime)
    {
        bool over = ContainsScreenPoint(Input.MousePosition); // inside this element as drawn: letterbox, canvas scale, Rotation/Scale all handled
        Vector2 local = ScreenToLocal(Input.MousePosition); // window pixels -> this element's untransformed layout space
    }

    protected override void OnDraw(SpriteBatch spriteBatch)
    {
        Vector2 size = ScreenTransform.LayoutSize * new Vector2(Fraction, 1);
        spriteBatch.Draw(Pixel, ScreenTransform.LayoutPosition, null, Color.Red, 0, Vector2.Zero, size, SpriteEffects.None, 0);
    }

    protected override Vector2 MeasureContent() => new(100, 8); // what Auto/Fit wrap when Size isn't set
}
```

- `OnUpdate` is `public`, `OnDraw` and `MeasureContent` are `protected`
- draw at `ScreenTransform.LayoutPosition` / `LayoutSize`. the batch is already scaled by the canvas and rotated by any transformed ancestors, so draw in UI units and don't Begin/End it yourself
- `Canvas` gives you the root canvas, `Parent` the parent element
- layout (Fill/Fit/flex) and padding are internal to the library: a custom element can't add `Padding` or arrange its own children like a flexbox does. nest a `NeoPanel`/`NeoFlexBox` inside it instead

---

## Enabled vs Visible

| | `Enabled = false` | `Visible = false` |
|---|---|---|
| `OnUpdate` (this + children) | skipped | skipped |
| drawing (this + children) | still draws | skipped |
| takes layout space | yes | no |
| button clicks | no | no |

`Visible = false` is css `display: none`. `Enabled = false` is a greyed-out-but-still-there element (it does NOT change colors for you though).

---

## Scaling

every canvas picks how the viewport pixels turn into UI units. the viewport is the game's draw box: the letterboxed area when `ViewportAspect` is set (see the 0.3.2 Letterbox viewport section in `instructions.md`), else the whole window. the canvas layout size is `viewport / Scale`, so everything inside it (anchors, padding, flexbox, Fit) works in UI units and doesn't care about the resolution.

```csharp
// the default of ScaleFactor is 1 UI unit = ScaleFactor viewport pixels (1 by default)
NeoCanvas debug = new() { ScaleFactor = 2f };

// design at 1280x720 & grow/shrink with the viewport
NeoCanvas hud = new()
{
    ScaleMode = NeoCanvasScaleMode.ScaleWithScreen,
    ReferenceResolution = new Vector2(1280, 720),
    Match = 1f, // 0 = follow viewport width, 1 = follow viewport height, between = blend
    PixelPerfect = false // true = whole-number scale (at least 1) + point sampling, for pixel-art UI
};
float scale = hud.Scale; // viewport pixels per UI unit, updated every pass
```

- `ConstantPixelSize` (default) uses `ScaleFactor`. `ScaleWithScreen` ignores `ScaleFactor` and uses `ReferenceResolution` + `Match`
- that hud at 1920x1080 gets `Scale = 1.5` and a 1280x720 layout, so you design once at 1280x720
- `Match` blends in log space like unity does, so 0.5 treats a 2x-wider viewport the same as a 2x-taller one. with `ViewportAspect` set to the reference ratio (e.g. 16:9 for 1280x720) the viewport always has that shape, so every `Match` value gives the same exact scale
- a window resize (`SetWindowSize`, the user dragging the border, a window mode change) or a new `ViewportAspect`/`SetViewportAspect` gets picked up on the next update
- `PixelPerfect` takes the largest whole number that fits: a 256x232 reference in a 1280x1160 viewport is scale 5, in 1279x1160 it's 4
- `ScaleFactor` must be positive, `ReferenceResolution` positive, `Match` between 0 and 1, otherwise they throw

each canvas draws in its own SpriteBatch pass scaled by `Scale`, so text, panels and images all scale with no per-element code.

heads up: SpriteFont is a bitmap, so text scaled way up gets blurry. load a bigger font for big scale ranges.

### mouse and coordinates

the mouse is in window pixels, the layout is in UI units. buttons convert already, custom elements use `ContainsScreenPoint` / `ScreenToLocal` (see custom elements). to go the other way:

```csharp
Vector2 inCanvas = canvas.ScreenToCanvas(Input.MousePosition); // window pixels -> UI units (letterbox bars + Scale removed)
Vector2 back = canvas.CanvasToScreen(panel.ScreenTransform.LayoutPosition); // UI units -> window pixels
```

### OnDrawUI

`OnDrawUI(spriteBatch, gameTime)` on your game draws after all canvases, in its own identity batch: no camera, no canvas scale. its (0, 0) is the top-left of the viewport (so inside the letterbox bars), in viewport pixels.

---

## What's NOT there (yet)

stuff people (me) went looking for and didn't find. if you need one of these, it's code, not a setting:

- clipping / overflow hidden: children can draw outside their parent, and nothing scrolls
- text wrapping, text alignment inside a label, rich text
- min/max sizes, aspect-ratio boxes
- flex wrapping onto a second line, `rtl`/`btt` directions
- panel borders, rounded corners, 9-slice, textured panels (use a `Fill` `NeoImage` child)
- button backgrounds (wrap it in a panel), keyboard/gamepad focus and navigation
- text input fields, sliders, checkboxes, scroll views
