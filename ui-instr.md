# UI: Canvases, Elements, Layout, and Scaling

everything about the screen-space UI.

note to future self: every code block in this file was compiled against the actual library, and every layout number in here came out of a real layout pass, not out of my head. if you change the UI code, recheck the blocks. the old docs used `Transform` on UI elements for months and it never compiled once. that's how I found out.

---

## The tree

```
NeoGame.UIBus
 └─ NeoCanvas            (a root, one per HUD/menu/layer, never a child)
     └─ NeoPanel         (colored box, has Padding)
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
    ScreenTransform = { Position = new Vector2(10, 10), Size = new Vector2(200, 100), Anchor = Vector2.Zero, Pivot = Vector2.Zero },
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
        WidthMode = NeoUISizing.Fixed, HeightMode = NeoUISizing.Fixed,
        Rotation = MathHelper.ToRadians(15), Scale = new Vector2(1.2f) // visual only
    }
};

badge.ScreenTransform.Position = new Vector2(-16, 8); // changing it later works the same
Vector2 topLeft = badge.ScreenTransform.LayoutPosition; // computed, read-only
```

it inherits the world `Transform`, so `Position`, `Rotation`, `Scale` and the Up/Right helpers are the same members you use on game objects, plus:

| member | what it does |
|---|---|
| `Size` | the box size in UI units, used when the mode is `Fixed`. non-negative |
| `Anchor` | the point on the PARENT's inner box this element hangs from, 0-1 per axis. default (0.5, 0.5) = center |
| `Pivot` | the point on THIS box that sits on the anchor, 0-1 per axis. default (0.5, 0.5) = center |
| `Position` | offset from the anchor, in UI units |
| `WidthMode` / `HeightMode` | `Fixed`, `Fill` or `Fit`, see Sizing |
| `Rotation` / `Scale` | visual only, see below |
| `LayoutPosition` / `LayoutSize` | computed top-left and size from the last update/draw, in canvas units. read-only |
| `DesiredSize` | what the element asked for during measuring. read-only |

the placement formula:

```
top-left = parent inner top-left + parent inner size * Anchor + Position - own size * Pivot
```

so with the defaults (both 0.5) an element sits dead center: a 200x100 panel on an 800x600 canvas lands at (300, 250). for top-left placement like a normal person, set both `Anchor` and `Pivot` to `Vector2.Zero`.

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

## Sizing: Fixed, Fill, Fit

every element picks its size per axis with `ScreenTransform.WidthMode` and `ScreenTransform.HeightMode`:

- `Fixed` (default): use `Size`
- `Fill`: take the parent's full inner length. inside a `NeoFlexBox` it shares the leftover space on the main axis instead (see FlexBox)
- `Fit`: wrap the content: the measured text for labels/buttons, or the smallest box that holds all visible children, plus `Padding`

labels and buttons start as `Fit` on both axes. this replaces the old `AutoSize` flag. to give a button a fixed size, set the mode back to `Fixed` and set `Size`.

```csharp
// a panel that wraps its children. no flexbox
NeoPanel card = new()
{
    ScreenTransform = { WidthMode = NeoUISizing.Fit, HeightMode = NeoUISizing.Fit },
    Color = Color.Gray,
    Padding = new NeoPadding(16, 8) // 16 left and right, 8 top and bottom
};
card.Children.Add(new NeoLabel { Text = "HP 100", Font = font });
```

a 100x20 label in that card comes out as a 132x36 card with the label at (16, 8) inside it.

the gotchas:

- `Fit` has nothing to measure on a plain `NeoPanel` with no children, or on a `NeoImage` (images don't know their texture size, see NeoImage), so those come out 0x0 and draw nothing
- `Fit` around a `Fill` child is circular: the parent asks the child how big it is, and the child answers "as big as you". the child's `Size` is what gets measured, which is (0, 0) by default, so both collapse to 0x0. give the `Fill` child a `Size` to act as its minimum, or don't nest them like that
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
    ScreenTransform = { WidthMode = NeoUISizing.Fit, HeightMode = NeoUISizing.Fit },
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
| `SpaceBetween` | first at the start, last at the end, leftover split into the gaps | **same as `Stretch`** |
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
- a `Fit` flexbox wraps its children + gaps + padding: two 20-tall children with `Gap = 8` and `Padding = 12` make a 72-tall column
- hidden children (`Visible = false`) take no space
- children never wrap onto a second line. too many children just overflow the box, and nothing clips them

---

## Elements

### NeoPanel

a solid colored box behind its children.

| property | default | |
|---|---|---|
| `Color` | `Color.White` | `Color.Transparent` (alpha 0) skips drawing, handy for invisible layout boxes |
| `Padding` | 0 | see Padding |

it's a flat rectangle: no border, no rounded corners, no texture, no 9-slice. for a textured background put a `NeoImage` in it as the first child with `Fill` on both axes.

### NeoLabel

| property | default | |
|---|---|---|
| `Font` | null | a `SpriteFont`. **no font = draws nothing and measures 0x0** |
| `Text` | `""` | |
| `Color` | `Color.White` | |

- `Fit` on both axes by default, sized to `Font.MeasureString(Text)`
- draws at its native size from the top-left of its box: no wrapping, no scaling, no clipping, no alignment inside a bigger box. if you set the label to `Fixed`/`Fill`, the box gets bigger but the text stays in the top-left corner
- `\n` in `Text` makes multiple lines, since that's what `SpriteFont` does

### NeoButton

a clickable label. it reads the `Input` facade, so it needs a running `NeoGame`.

```csharp
NeoButton play = new() { Text = "Play", Font = font, ScreenTransform = { Anchor = Vector2.Zero, Pivot = Vector2.Zero } }; // sizes itself to the text
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
- unlike the label, the text is centered in the button's box, so a `Fixed` button works the way you'd expect
- mouse only. there's no keyboard/gamepad navigation or focus
- **`Visible = false` does NOT stop it from being clicked.** hidden elements still update. use `Enabled = false` (or both), see Enabled vs Visible

### NeoImage

| property | default | |
|---|---|---|
| `Texture` | null | borrowed, never disposed by the UI |
| `SourceRectangle` | null | part of the texture to draw, null = whole texture. negative values throw |
| `Color` | `Color.White` | tint |

- the texture is stretched to the element's box
- **an image has no size of its own.** `Fit` doesn't look at the texture, so a default `NeoImage` is 0x0 and draws nothing. give it a `Size` (e.g. `new Vector2(texture.Width, texture.Height)`) or `Fill`
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

    protected override Vector2 MeasureContent() => new(100, 8); // what Fit wraps
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
| `OnUpdate` (this + children) | skipped | **still runs** |
| drawing (this + children) | still draws | skipped |
| takes layout space | yes | no |
| button clicks | no | **yes** |

for "this menu is closed", set both, or just `Unsubscribe` the canvas.

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
- a window resize, a window mode change or a new `ViewportAspect` gets picked up on the next update
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
- automatic image sizing from the texture
