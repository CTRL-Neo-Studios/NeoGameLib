# UI: Canvases, Elements, Layout, and Scaling

everything about the screen-space UI.

note to future self: every code block in this file was compiled against the actual library, and every layout number in here came out of a real layout pass, not out of my head. if you change the UI code, recheck the blocks. the old docs used `Transform` on UI elements for months and it never compiled once. that's how I found out.

---

## [0.5.0] it behaves like Unity's uGUI now (BREAKING)

0.4.4~5 tried to make the defaults like html/css. in 0.5.0 the layout model is uGUI's `RectTransform` instead.

So now:
- `Anchor` and `Pivot` default to **(0.5, 0.5)**. a new element is now centered in its parent like a new UI object in Unity.
- an anchor is a **range**: `AnchorMin` and `AnchorMax`. when they're equal on an axis the element hangs from that point; when they differ the element **stretches** between them (see "Anchors: points and stretch")
- `Anchor` still exists as a shorthand that sets both
- **y points DOWN**, unlike Unity: anchor `(0, 0)` is the top-left, `(1, 1)` the bottom-right. "top" is 0 and "bottom" is 1
- `Size` may be negative on a stretched axis, because there it's a delta on the anchor span, not a length
- `Fill` outside a flexbox is **deprecated**: it works the same as a stretch anchor with zero insets. inside a flexbox `Fill` is the normal way to grow and is NOT deprecated
- `NeoCanvas.Layout(viewportSize)` lays out a tree without a running game

upgrading from 0.4.x: nothing moves for elements that only used `Anchor`/`Pivot`/`Position`/`Size`; they lay out exactly as before. anything that assumed the (0, 0) defaults from the 0.4.4 docs has to set `Anchor = Vector2.Zero, Pivot = Vector2.Zero` itself.

## [0.4.4] it behaves like html/css now

(sizing and visibility parts still apply; the anchor/pivot default claim was wrong, see 0.5.0 above.)

so this just works, no `Size`/`WidthMode`/`Color` needed:

```csharp
NeoFlexBox row = new() { Align = NeoFlexAlign.Center, Gap = 2 }; // transparent, wraps its children
row.Children.Add(new NeoImage { Texture = Content.Load<Texture2D>("Sprites/Inventory/gem") }); // as big as the texture
row.Children.Add(new NeoLabel { Text = "X0", Font = font });
```

`Color = Color.White` on panels that relied on the old white default, and `Enabled = false` on top of `Visible = false` is no longer needed.

(object initializers like `new NeoLabel { Text = "X0" }` vs setting properties after `new()` make no difference at all: an initializer IS "construct, then set each property".)

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
| `Size` | on a point axis the box length (with the default `Auto` mode an axis left at 0 sizes to the content instead). on a stretched axis a delta added to the anchor span, so it can be negative |
| `AnchorMin` / `AnchorMax` | the corners of the range on the PARENT's inner box that this element hangs from, 0-1 per axis. default (0.5, 0.5) for both = center |
| `Anchor` | shorthand: sets both. reading gives `AnchorMin` |
| `Pivot` | the point on THIS box that sits on the anchor, 0-1 per axis. default (0.5, 0.5) = center |
| `Position` | offset from the anchor (on a stretched axis from the point on the anchor span that `Pivot` selects), in UI units |
| `InsetLeft` / `InsetRight` / `InsetTop` / `InsetBottom` | the box edges as distances from the anchor edges, positive pointing inwards. another view of `Position` + `Size`: setting `InsetLeft = 16, InsetRight = 16` on a horizontally stretched element leaves it 16 from both sides |
| `StretchesX` / `StretchesY` | true when the anchors differ on that axis. read-only |
| `WidthMode` / `HeightMode` | `Auto` (default), `Fixed`, `Fill` (deprecated outside flexboxes) or `Fit`, see Sizing |
| `Rotation` / `Scale` | visual only, see below |
| `LayoutPosition` / `LayoutSize` | computed top-left and size from the last update/draw/`Layout` call, in canvas units. read-only |
| `DesiredSize` | what the element asked for during measuring. read-only |

the placement formula, per axis (`parent` is the parent's inner box):

```
from   = parent.min + parent.length * AnchorMin
to     = parent.min + parent.length * AnchorMax
length = (AnchorMin == AnchorMax) ? desired length : max(0, to - from + Size)
min    = lerp(from, to, Pivot) + Position - length * Pivot
```

with the defaults a 200x100 panel on an 800x600 canvas lands at (300, 250). `Anchor = (1, 0), Pivot = (1, 0)` = top-right corner, `(0.5, 1)` for both = bottom-center, and so on.

- "inner box" = the parent's box minus its `Padding` (panels and flexboxes only)
- anchors and pivots outside 0-1 throw. sizes may be negative (they only mean something on a stretched axis), the final box never is
- `NeoFlexBox` children and canvases ignore `AnchorMin`/`AnchorMax`, `Pivot` and `Position`: the flexbox places its children itself, and a canvas always covers the whole viewport

### Anchors: points and stretch

anchoring to a side of the screen is just anchoring to the parent: a direct child of the canvas is anchored to the viewport (the letterbox bars are outside it). the usual presets, with y pointing down:

| preset | `AnchorMin` | `AnchorMax` |
|---|---|---|
| top-left / top / top-right | (0, 0) / (0.5, 0) / (1, 0) | same as min |
| left / center / right | (0, 0.5) / (0.5, 0.5) / (1, 0.5) | same as min |
| bottom-left / bottom / bottom-right | (0, 1) / (0.5, 1) / (1, 1) | same as min |
| stretch-top / stretch-middle / stretch-bottom | (0, y) | (1, y) with y = 0 / 0.5 / 1 |
| stretch-left / stretch-center / stretch-right | (x, 0) | (x, 1) with x = 0 / 0.5 / 1 |
| stretch | (0, 0) | (1, 1) |

```csharp
// an HP bar that is always 16 from both sides and 24 tall, 8 below the top
NeoPanel bar = new()
{
    Color = Color.DarkRed,
    ScreenTransform =
    {
        AnchorMin = new Vector2(0, 0), AnchorMax = new Vector2(1, 0), // stretched on x, a point on y
        Pivot = new Vector2(0.5f, 0),
        Position = new Vector2(0, 8), // 8 below the top edge
        Size = new Vector2(-32, 24), // 32 less than the span on x (16 per side), 24 tall on y
    }
};

// the same horizontal box through the insets, on a left-pivoted element
NeoPanel bar2 = new()
{
    ScreenTransform = { AnchorMin = new Vector2(0, 0), AnchorMax = new Vector2(1, 0), Pivot = new Vector2(0, 0), InsetLeft = 16, InsetRight = 16 }
};
```

- on a stretched axis `Size` is a delta: `Size.X = -32` means "32 less than the anchor span", so 16 off each side. the `Inset*` properties are the readable way to set it
- on a stretched axis the sizing mode is ignored: the anchors decide the length. an `Auto`/`Fit` parent still wraps a stretched child by its content size
- a stretched axis never goes below 0 length, even with huge negative `Size`

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
- `Fill`: **deprecated outside a flexbox**, use stretch anchors (`AnchorMin = (0,0)`, `AnchorMax = (1,1)`) instead. takes the parent's full inner length. inside a `NeoFlexBox` it shares the leftover space on the main axis instead (see FlexBox) and stays supported there
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

`NeoFlexBox` is a `NeoPanel` that orders its children in a row or a column. it controls the children's positions, so their `AnchorMin`/`AnchorMax`/`Pivot`/`Position` do nothing.

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
| `Font` | null | a `SpriteFont`. **no font = the built-in Inter font** (Regular, 16px, characters 32-126 and 160-255). assigning one in code clears `FontPath` |
| `FontPath` | null | the content path of a font (`"fonts/ui"`), loaded through `NeoGame.Assets` the first time it's needed. this is what UI files store |
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
| `Font`, `FontPath`, `Text` | | same as `NeoLabel` |
| `Color` | `Color.White` | the text color when idle |
| `HoverColor` | `Color.LightGray` | text color while the mouse is over it |
| `PressedColor` | `Color.Gray` | text color while hovered and the left button is held |
| `OnClick` | null | fires when the left button is pressed AND released over the button |
| `OnClickActions` | empty | `INeoAction`s that run in order after `OnClick`. this is what UI files store. the context has no `Self`/`Other` object, so `DestroyAction` and `SetEnabledAction` do nothing from here, and `SwitchWorldAction` needs an explicit `unload` list. `ctx.Element` is the button |
| `Hovered` | | read-only, true while the mouse is over the drawn box |

- it's text only: no background, no border. put it in a `NeoPanel` for that
- unlike the label, the text is centered in the button's box, so a button with a `Size` works the way you'd expect
- mouse only. there's no keyboard/gamepad navigation or focus
- a hidden (`Visible = false`) or disabled (`Enabled = false`) button can't be clicked

### NeoImage

| property | default | |
|---|---|---|
| `Texture` | null | borrowed, never disposed by the UI. assigning one in code clears `TexturePath` |
| `TexturePath` | null | the content path of a texture (`"Sprites/gem"`), loaded through `NeoGame.Assets` the first time it's needed. this is what UI files store |
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
- panel borders, rounded corners, 9-slice, textured panels (use a stretched `NeoImage` child)
- button backgrounds (wrap it in a panel), keyboard/gamepad focus and navigation
- text input fields, sliders, checkboxes, scroll views
- characters outside the built-in font's ranges (CJK, emoji): give the label/button a `FontPath` that has them
- custom elements in `.neoui.xml` files: only the built-in elements can be read and written

---

## [0.5.0] UI documents (`.neoui.xml`)

a canvas can now FINALLY live in a file as in one canvas per file, so a UI can be built in the editor (or by hand) and loaded with one call:

```csharp
NeoCanvas menu = NeoUIDocument.Load("menus/pause"); // Data/NeoUI/menus/pause.neoui.xml, same naming rules as maps
menu.Subscribe();
menu.FindById<NeoLabel>("score").Text = "0"; // ids are how code reaches into a loaded tree

string xml = NeoUIDocument.ToXml(menu); // the canonical form, see below
NeoCanvas copy = NeoUIDocument.FromXml(xml); // from a string, e.g. in tools
```

```xml
<Canvas xmlns="urn:neogamelib:ui:1" scaleMode="scaleWithScreen" referenceResolution="1280 720" match="0.5">
  <Panel id="hud" anchor="stretch-top" pivot="0.5 0" left="16" right="16" y="8" height="40" color="#00000080" padding="8 4">
    <Label id="hp" anchor="left" x="4" text="HP 100" fontPath="fonts/ui" />
  </Panel>
  <FlexBox id="menu" direction="column" gap="6" align="center">
    <Button id="play" text="Play">
      <Button.onClick>
        <SwitchWorldAction world="level1">
          <SwitchWorldAction.unload>
            <item>menu</item>
          </SwitchWorldAction.unload>
        </SwitchWorldAction>
      </Button.onClick>
    </Button>
  </FlexBox>
</Canvas>
```

- the elements are `Canvas` (the root, once), `Panel`, `FlexBox`, `Label`, `Button` and `Image`, all in the namespace `urn:neogamelib:ui:1`. children are nested elements, in draw order
- **attributes are the element's properties**, camelCase. anything that isn't at its default is written; leaving an attribute out means the default. unknown attributes, elements and enum values are errors that name the line and column
- **layout attributes**, per axis, depend on whether that axis is stretched (the anchors differ on it):

  | axis | point axis (anchors equal) | stretched axis (anchors differ) |
  |---|---|---|
  | horizontal | `x`, `width` | `left`, `right` |
  | vertical | `y`, `height` | `top`, `bottom` |

  writing the wrong form for an axis is a load error. `left`/`right`/`top`/`bottom` are the `Inset*` properties
- `anchor` takes a preset (`top-left`, `top`, `top-right`, `left`, `center`, `right`, `bottom-left`, `bottom`, `bottom-right`, `stretch-top`, `stretch-middle`, `stretch-bottom`, `stretch-left`, `stretch-center`, `stretch-right`, `stretch`), a point `"x y"`, or a range `"minX minY maxX maxY"`. or write `anchorMin` / `anchorMax` separately, but not together with `anchor`
- values: vectors `"x y"` (one number = both), colors `#RRGGBB` or `#RRGGBBAA` (raw RGBA as MonoGame's `Color` stores it, which is premultiplied, unlike `SpriteSpec.color`), padding `"all"` / `"horizontal vertical"` / `"left top right bottom"`, rectangles `"x y width height"`, enums in camelCase. numbers always use `.` as the decimal separator, whatever the machine's language is
- `rotation` is in **degrees** in files (clockwise, since y points down), like Unity's inspector. `ScreenTransform.Rotation` in code stays radians; the loader and writer convert, and the writer rounds to 4 decimals so `15` stays `15`
- assets are paths (`fontPath`, `texturePath`), resolved through `NeoGame.Assets` when first used. a path that isn't in your content project throws then, with the path in the message
- click actions go in a `<Button.onClick>` element, each action as an element named by its registry name. an action's plain values are attributes; lists and nested records are property elements named `Action.property`, with `<item>` entries. they are built by the same code as the actions in map files, so defaults and required parameters behave the same
- the root has to be `<Canvas>`, and it takes no layout attributes (`anchor`, `x`, `rotation`, ...): a canvas always covers the viewport, so those are load errors. scale it with `scaleMode`/`scaleFactor`/`referenceResolution`
- custom `NeoUIElement` subclasses aren't supported, and that includes subclasses of the built-ins (a `MyPanel : NeoPanel` would lose its extra state as a `<Panel>`): writing one throws, reading an unknown element is an error

### schema and checking in your IDE

every build of a game that references NeoGameLib writes two things next to `neo.project.json`:

- `Data/<Game>.neoui.xsd`: an XML schema for the documents. it includes **your game's own actions**, so the IDE completes and checks them too. point a document at it with `xsi:schemaLocation="urn:neogamelib:ui:1 <Game>.neoui.xsd"` (relative to the file; the path depends on how deep the file is in `Data/NeoUI/`), or associate `*.neoui.xml` with it in your IDE (Rider: Settings > Languages & Frameworks > Schemas and DTDs; VS Code with the Red Hat XML extension: `xml.fileAssociations`)
- an `x-neo-ui` section in `Data/<Game>.neoschema.json` for the editor: every element's attributes with types, defaults (read from the code, so they can't drift) and `x-neo-asset` markers, the layout attributes, and the anchor presets

what the XSD can't check, the loader does: that `width` isn't mixed with `left`/`right` (it depends on the anchors' values), that an anchor stays between 0 and 1, and the values inside an action's properties.

### for tools: layout without a game

`NeoCanvas.Layout(viewportSize)` computes every element's `ScreenTransform.LayoutPosition` / `LayoutSize` (in canvas units) for a viewport size in window pixels, with no `NeoGame`, bus or `SpriteBatch`. text needs a font to measure; without a running game the built-in font isn't loaded, so give labels and buttons a `Font` yourself. `MeasureString` only reads glyph metrics, so a `SpriteFont` built with a null texture measures exactly like the real one (`checks/NeoGameLib.Checks/BuiltinFont.cs` reads Inter's metrics out of the embedded xnb that way).

`fixtures/ui-layout/*.json` holds documents with their expected rectangles at five viewport sizes, generated by `dotnet run --project checks/NeoGameLib.Checks -- --update-fixtures`; the `text` case measures with the built-in Inter. the editor's layout code has to reproduce them. a plain run of the checks fails when a committed fixture no longer matches the library, so a layout change always shows up as a reviewed diff. `x-neo-ui.builtinFont.sha256` is the hash of the embedded Inter.xnb, so a tool that ships its own copy can tell when it's stale.
