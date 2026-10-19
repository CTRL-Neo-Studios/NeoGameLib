# Changelogs, Instructions, and Rambling

## [0.1.x] for animation: Make a spritesheet and import it, and then do smth like this

```csharp [InsideAClassInheritingNeoGameClass.cs]
protected override void OnLoadContent(ContentManager CM)
{
    CreateWorld("level1");

    Texture2D sheet = Content.Load<Texture2D>("sprites/spritesheet");

    NeoObject player = World.Instantiate("Player");
    NeoSpriteRenderer renderer = player.AddComponent(new NeoSpriteRenderer(RenderBus, sheet));
    NeoAnimator animator = player.AddComponent<NeoAnimator>();

    // sheet has two frames side by side, 32x32 each
    NeoAnimation idle = new(new() {
        new Rectangle(0,  0, 32, 32),
        new Rectangle(32, 0, 32, 32),
    }, 0.2f);  // 0.2 seconds per frame, loops by default

    animator.Play(idle);

    animator.OnAnimationFinished = () => animator.Play(idle);
    animator.Play(/* attack animation or smth*/);
}
```

instead of listing bounds one by one, walk a grid:

```csharp
// 6x4 cells of 32x32, read left-to-right top-to-bottom
NeoAnimation run = NeoAnimation.FromGrid(new Point(6, 4), new Point(32, 32), 0.1f);
// horizontal/vertical args flip the walk direction (Left = mirrored, Up = from the bottom)
// padding = gap between cells, margin = sheet top-left offset, both in px:
NeoAnimation padded = NeoAnimation.FromGrid(new Point(6, 4), new Point(32, 32), 0.1f, padding: new Point(2), margin: new Point(4));
// horizontalFirst: setting to false walks down each column first instead of across each row
// holdLastFrame: false clears the sprite when it ends instead of holding the last frame. for non-looping-animations where it needs a one-off end
NeoAnimation vanish = NeoAnimation.FromGrid(new Point(4, 6), new Point(32, 32), 0.08f, loop: false, holdLastFrame: false);
```

---

## [0.1.x] for text: add a SpriteFont asset in the mgcb editor, then smth like this

```csharp [AlsoInsideAClassInheritingNeoGameClass.cs]
protected override void OnLoadContent(ContentManager CM)
{
    SpriteFont font = Content.Load<SpriteFont>("fonts/ui");

    NeoObject label = World.Instantiate("ScoreLabel");
    NeoTextRenderer text = label.AddComponent(new NeoTextRenderer(RenderBus, font));
    text.Text = "Score: 0";
    text.Anchor = Vector2.Zero; // (0,0) = top-left of the text, default (0.5,0.5) centers it
    label.Transform.Position = new Vector2(20, 20);
}
```

texts draw after sprites

## [0.1.x] Colliders

two flavors, both register themselves on awake, collisions are computed automatically every frame after the world update


---

# Road to 0.2.0 - Worlds, Collisions, Input Gathering, Files, and Assets

## [0.2.0] Worlds
registry of ALL worlds. Load pulls registered ones into the running set (any number at once, or none), Unload kicks one out and destroys its objects. objects are built on Load, not create

Initially I was actually thinking of making the World Man.utility based instead of being a full-on registry-like-manager. But I was considering possible use cases such as worlds for UI-only and worlds for game objects only for better organization, and so I thought
hey, how about just change to the registry-manager architecture instead? So, this architecture should be able to solve the object-layers problem which is just object on different layers but instead of layers it's now different worlds. This does affect things a bit,
especially the collision where now collision is world-agnostic -- meaning that colliders in different worlds can collide with each other. This is a nuisance particularly for the use-case I've described, which means I have to change the collision bus architecture. See my updated note above for how collision works after the new world loading architecture.

In addition to that, notice that I specify "registry-manager": this is not a full-save-state-registry manager in a traditional sense that saves the "snapshot" and "state" of a world; it's more like a registry-keeper manager that keeps in track of what worlds you load and that's that. Does not keep track of the worlds' states and created objects in its registry.
For that to happen I'd actually need to write-load files and I'm NOT LOOKING TO MAKE A UNITY REPLICA. If I want Unity, I'd use unity, but this is MonoGame and fundamental architectures are different.

Here's an example usage where a world only contains UI objects and another world contains only game-related objects.

```csharp
WorldManager.CreateWorld("ui", w => { /* build hud objects */ });
WorldManager.CreateWorld("level1", w => { /* build game objects */ });
WorldManager.Load("ui"); // builds using the registered builder function and starts running in the update tick loop
WorldManager.Load("level1"); // same as above
NeoObject hud = WorldManager.GetWorld("ui").FindObjectByComponent<HudManagerOrSmthLikeThat>(); // querying across worlds
WorldManager.Unload("level1"); // destroys all objects in the world as a non-reversible action. the world is still in the registry and can be reloaded.
WorldManager.Load("level1"); // rebuilt from the builder function. please make sure you did read my architecture notes above before using this, future self
WorldManager.RenameWorld("ui", "hud"); // renames world "ui" to the new name "hud"
```

---

## [0.2.4] Input Action Maps
an action map over the keyboard. you can map keys to named actions, query key states and axis values. input maps collects inputs as the first lifecycle in the update tick, so other update-tick lifecycles are able to read the latest input states and values.

```csharp [InsideAClassInheritingNeoGameClass.cs]
NeoInputMap gameplay = new();

gameplay.AddDigital("jump", Keys.Space, Keys.W); // any key given fires the action
gameplay.AddAxis("moveX", NeoAxisRange.Full, Keys.D, Keys.A); // positive key, negative key

gameplay.Subscribe(); // registers itself on NeoGame.InputBus

gameplay.IsDown("jump");
gameplay.IsPressed("jump");
gameplay.IsReleased("jump");
gameplay.GetAxis("moveX");

// NeoAxisRange's enum options has documentations on them. Check intellisense
```

trying to query an action that wasn't mapped in the input map throws an exception. that includes typos. (You're welcome, future me)

mouse buttons bind like keys, and combine with keys on the same action:

```csharp
gameplay.AddMouseButton("fire", MouseButtons.Left); // any bound button fires the action
```

`Input` is a shorthand facade over a hidden map registered to NeoGame.InputBus: `Input.LeftMouseButton.IsPressed()` / `IsDown()` / `IsReleased()`, plus Middle/Right/XButton1/XButton2 and `Input.MousePosition` in window pixels

---
## **[0.2.0] !New in Collider Architecture!**

### Interface-based collider callbacks
So, before this change, the right way of using a collider would be creating a new class that inherits the collider class used, adding that to the object, and then having it call other components' functions when a collision happens.
Too much steps and too much inheritance, and so I'm making it interface-based: now you only have to add the collider component to the object, implement the Collision interface in other components in the same object, and the collider component will
automatically call the interface functions on collision. Easier to setup, and makes it more easier to check which object has a collider as oyu can just now look for a collider component instead of looking for a component that had inherited the collider component.

### World-based collisions
since the introduction of World Man. and multiple-world-loading, object collisions are now cross-worlds, which means that objects in different worlds can collide with each other and so on -- which maybe is some cases is great if you want
worlds to contain only one type of game objects and the other, but not particularly great in the use case described in the architecture note below where UI and game objects are in two different worlds. Not good. So I had to update the collider architecture and I've decided
to make it somewhat "list-based": I could make a list of worlds in which object collisions are detected with one another, and worlds outside of that list don't share collision  
detections. So, "ui" world could be a separate list, where "enemies" and "players" worlds can be in the same collision list. This may be a bit redundant as I might have to create a separate  
list for one world. This does make Collision Bus rely on the World Man. more than intended but I'm advocating creating worlds through World Man. instead of creating world objects outside of the registry, so I guess that checks out itself.

```csharp [InsideAClassInheritingNeoGameClass.cs]
protected override void OnLoadContent(ContentManager CM)
{
    // the sprite collider's bounds automatically resizes to the sprite on the SAME object. bounds resize for animation frames as well
    NeoObject player = World.Instantiate("Player");
    player.AddComponent(new NeoSpriteRenderer(RenderBus, sheet));
    player.AddComponent<NeoSpriteCollider>();

    // the box collider is centered on the object position. the offset property shifts the collider center
    NeoObject wall = World.Instantiate("Wall");
    NeoBoxCollider wallCol = wall.AddComponent<NeoBoxCollider>();
    wallCol.Size = new Vector2(64, 32);
    wallCol.Offset = new Vector2(0, -16);
}
```

react to hits with a plain component implementing ICollision on the same object, no subclassing colliders. add/remove these components to add/remove collision behavior:

```csharp
public class HurtBox : NeoComponent, ICollision
{
    public void OnCollision(NeoBoxCollider other, NeoCollision collision)
    {
        // `other` is the collider on the object you hit
    }
}

player.AddComponent<NeoSpriteCollider>();
player.AddComponent(new HurtBox());
```

world collision groups: worlds listed together share collision detection while worlds aren't listed does not interact with the listed worlds. ungrouped worlds collide with all other ungrouped worlds

```csharp
WorldManager.GroupCollisions("ui", "ui"); // ui only collides with ui world objects
WorldManager.GroupCollisions("gameplay", "players", "enemies"); // gameplay world objects collides with players and enemies world objects
WorldManager.UngroupCollisions("ui"); // collides with everything in the ungrouped section
```

---

## [0.2.0] Built-in Movement Handler Boilerplate
one movement logic component does keys, Move(), lerp, gravity, jump and ground detection. everything optional

note to future self: you're welcome in advance. i find myself having to write the same movement code in two projects so might as well write a general purpose component that moves sprites instead.

```csharp
NeoMover mover = player.AddComponent<NeoMover>();
mover.KeyLeft = Keys.A;
mover.KeyRight = Keys.D;
mover.KeyJump = Keys.Space;
mover.GroundComponentType = typeof(GroundTag); // needs a NeoBoxCollider on the same object
// keys are intentionally options so you can just move it with the move function
// mover.Gravity = 0 to fly, Omnidirectional = false for platformer-style, UseLerp/LerpSnappiness tune smoothing
```

## [0.2.3] NeoMover Colliders Whitelist/Blacklist
colliders now stops the mover by default. when the mover overlaps a collider it is now "pushed" (moved in negative velocity) back out. also needs a NeoBoxCollider on the same object:

```csharp
mover.CollisionWhitelist.Add(typeof(TriggerTag)); // objects with these components are pass-through (everything else solid)
mover.CollisionBlacklist.Add(typeof(SolidTag)); // ONLY these objects are solid (everything else pass-through)
// both lists empty = everything is solid
// you can use the generic helpers to add/remove tags to/from the lists
```

mover.Velocity is now exposed in case if you want to write your own collision correction.
Grounded property is still determined via GroundComponentType tags

---

## [0.2.3] Timers
reusable individual countdown timers without the need to attach to specific objects. but it's ideally used in a component or an object.

```csharp
NeoTimer invuln = new(0.5f); // seconds
invuln.OnFinished = () => { /* finish func here */ };
invuln.Start(); // Start while running restarts from full

// a sisyphus timer
spawnTimer.OnFinished = () => spawnTimer.Start();

// Stop kills it (fires OnStop), Pause/Resume freeze the countdown (fire OnPause/OnResume), OnStart fires on every Start, TimeLeft is the remaining seconds
```

---

## [0.2.3] Asset Manager
cached loads and uses the path as key

```csharp
Texture2D bot = Assets.Load<Texture2D>("Sprites/bot"); // cache and load and quick reference/loading
Assets.Unload("Sprites/bot"); // removes cache
Assets.UnloadAll();
```

---

## [0.2.3] file-system i/o: File as an object, Folder as an object

This is mostly inspired by Tauri V2's Store class. Essentially, Tauri provides the Store class that provides load/write/other-object-manipulation functions that represents one .json file.

For example:
```typescript
const config: Store = new Store("config.json"); // auto loads the file in question in constructor
await config.load(); // load function that loads the file into the buffer which the class holds
await config.save(); // save function that writes the class-held buffer data into the file
await config.get<MyJsonType>("key");
await config.set<MyJsonType>("key", obj as MyJsonType);
```

I want to up their game in this library. Tauri's store stores the json data as a custom-value-typed dictionary where developers can customize string keys and correspoding value types and objects, and get-set them. If
the file isn't in the system in the first place, the class would automatically create the file.

So you'd load, get, set, and save.

In this library it should work somewhere similar to this:

```csharp [InsideAClassInheritingNeoGameClass.cs]
FileStore cfg = new FileStore(Path.Combine(Directory.GetCurrentDirectory(), "Data", "config.txt"));

cfg.Load(); // reads the file into the buffer; a missing file AND folder get created

string raw = cfg.Get(); // whole buffer, line breaks intact as \n
List<string> lines = cfg.GetAsLines(); // split into lines, no \n, \r\n normalized

cfg.Set("hello\nworld"); // replace the buffer verbatim, line breaks are on you
cfg.Set(new List<string> { "hello", "world" }); // each entry becomes one line

cfg.Write(); // pushes the buffer to disk
```

And this would be a base class that could be inherited into something like `JsonStore` or `TomlStore` or `YamlStore`.

Maybe for `JsonStore`, which should inherit FileStore class and use buffer as json string instead, and have its own separate buffer json object data so it can write to json string into text buffer then into the file.

```csharp
JsonStore save = new JsonStore(Path.Combine(Directory.GetCurrentDirectory(), "Data", "save.json"));
save.Load();

save.Set("playerName", "Neo");
save.Set("gold", 420);

string name = save.Get<string>("playerName"); // "Neo"
int gold = save.Get<int>("gold"); // 420
int nope = save.Get<int>("missing"); // gets 0 by default. For any other type, gets that type's default value. or should throw an exception instead

save.Write();
```

custom classes/structs should work too, i.e. MonoGame's classes/structs. which should be a must if you think about it

```csharp
WorldPlayersData wpd = save.Get<WorldPlayersData>("wpd"); // missing key would return null
save.Set("wpd", wpd);
```

As for folders: (temporary thought, NEEDS TO BE REVISED)

```csharp
FolderStore data = new FolderStore(Path.Combine(Directory.GetCurrentDirectory(), "Data"));
data.Load(); // lists every top-level file into file list buffer

FileStore txt = data.Instantiate<FileStore>("config.txt");
JsonStore json = data.Instantiate<JsonStore>("save.json");
```

## [0.2.5] Quality of Life Improvements

`ParentObject.World` -> `ParentWorld`

`Game.Singleton.RenderBus` -> `RenderBus`, etc., as shorthands in the NeoComponent class

and other stuff i forgot to record here

---

# Road to 0.3.0 - Screen Space and World Space

> Funnily enough, this took a night to plan but took an actual day and a half for me to research on whatever the fuck I've planned and how to implement them.
> Fuck me...
> 
> You're welcome as always, future me

So, when I was thinking about the idea of the camera, I would think of the camera as a game object and use shader matrix operations to manipulate the render batch to simulate what the camera actually sees of the world.

Except for one thing: UI.

Currently all of my used implementations of UI components are as game objects that exist in the world. That means, when the camera moves, the UI moves as well, but it's not supposed to move.

So two things:
- Camera needs to render the worlds and UI separately, but the matrix manipulations must be on the world instead of on the UI.
  - This means that previous ideas that UI and game objects are just two separate game object worlds is fucked.
- I need UI to function, but also interop with the world as well. UI taps into both the rendering master lifecycle, but also the update master lifecycle since UI has their own separate logic as well.
  - This would mean I have to create an additional system, like an additional bus for canvas rendering and logic handling instead of using existing world-related APIs.
  - This would also mean that I have to make my own declarative UI syntax, which luckily I do have some experience in since I am a full-stack web developer + flutter user as well.

## Actual Implementation

Well we all know that plannings and thoughts are different from what's actually done during the engineering and implementation phase.

World-space and screen-space means separating the old one-rendering-pass into two.
1. The world-space pass: world-space pass renders the worlds to the viewport under the influence of the camera's transform. The resulting render patch would have its transform matrix modified according to the camera transform.
   - The transform coordinate system remains the same. x+ is viewport right, x- is viewport left, y+ is viewport down, y- is viewport up.
   - Multiple cameras rendering to viewport. Similar to the neo-sprite rendering order, cameras have rendering orders as well, and they all render to the same viewport overlaying on top of each other. This leaves space for layered UI rendering or layered-world-space rendering of cameras, where one camera could render sprites, other renders special effects, or have multiple cameras displaced and color-graded on the output to create crazy schizophrenic effects.
   - No cameras rendering to viewport. This should fallback to the original rendering, where (0,0) is just viewport's top-left corner.
2. The screen-space pass: screen-space pass renders the UI stuff. It uses viewport coords, which does not have relations to world coords in any way.
   - Before 0.3.0, transform was bound to viewport coords. Now, only UI stuff are bound to viewport coords in the screen-space, and transform represents coordinates in the world-space instead. It's not bound by viewport-space anymore.
   - Yes, multiple cameras exist, but ideally, screen-space is one pass, so no matter how many cameras there are, there should be only one UI pass. A camera ONLY renders world-space with a certain offset. The UI is added after the world-space pass rendering.

### Cameras
Cameras should be a separate component attached to game objects to indicate that they're cameras. And on attach/create, the NeoCamera's OnAwake() call registers itself to the render bus. Detroying the component unregisters itself. (And, yes I know this means that cameras can be registered outside the normal world-object-component registration flow)
- Cameras use the Render Bus, same as the render bus for sprite rendering. So, this means that cameras also has the Order attribute and should render from lowest-to-highest order as well.
- They also have a Zoom parameter that controls, well, its zoom.

#### Actual Rendering

In order for cameras to render out the world, we need matrices. Cameras, specifically, has something called a "View Matrix."

A View Matrix is basically a 4x4 math matrix that transforms vertex coords from world-space into camera-space. This is the google definition, but my interpretation is, **basically it describes everything in the world relative to the camera.**

There is, or there should, be 4 steps into building the view matrix:
1. translate object positions in the world relative to the camera.
   - camera moves right, world moves left
   - i.e. an object at (100, 50) and a camera at (130, 50); the object is at (-30, 0) relative to the camera since object - camera = (100,50) - (130,50) = (-30,0).
2. translate object rotations in the world relative to the camera.
   - camera rotates clockwise, world rotates counterclockwise
   - i.e. an object that is at 60 deg rot and camera at 30 deg rot clockwise. To the camera, the object is rotated 30-deg clockwise.
3. scale the world by the camera's zoom.
   - camera zooms in, world scales up
   - i.e. an object at (2,2) scale viewed through a 1.5x zoom camera appears (3,3): object scale x camera zoom = 2 x 1.5. note the object's own scale stays on the object; the view matrix only carries the zoom.
4. makes sure the camera's "position" is landed at the viewport center.
   - imagine everything above describes the objects, but renders it at the (0,0) position of the viewport, which is the top-left corner of your window. So this step adds half of the viewport's height & width and makes it that everything described above is at the center of the viewport.
   - i.e. a 800x600 window. this would add (400, 300) to the final matrix that recenters everything described above back to the center.

So basically, everything comes down to this equation:
```
screen = Transform(center) x Scale(zoom) x Rotate(-cameraRot) x Transform(-cameraPos) applied to world coordinates
```

```
d = world - cameraPos;
d' = rotate d by -cameraRot;
p = d' x zoom;
screen = p + center;
```


---

## [0.3.0] World-Space Camera
a world-space camera component. attach one to an object, select it explicitly, and the whole world pass renders through it. UI does not interact with the view mat

```csharp [InsideAClassInheritingNeoGameClass.cs]
NeoObject camObj = World.Instantiate("cam", new Transform(new Vector2(0, 0)));
NeoCamera cam = camObj.AddComponent(new NeoCamera(RenderBus)); // self-registers on awake
cam.Zoom = 2f;
cam.CullingMask = -1; // bitmask of sprite/text Layers this camera draws, default = everything

// every ENABLED camera renders the world according to the arranged Order.
// cameras unregisters themselves on destroy, so loading/unloading a world moves its cameras.
// MainCamera (the first enabled camera) drives WorldToScreen/ScreenToWorld and the OnDraw hook.
// no enabled camera at all defaults the world pass back to the identity pass, a.k.a the pre-camera viewport rendering behavior
Vector2 onScreen = RenderBus.WorldToScreen(new Vector2(100, 50));
```

## [0.3.0] Screen-Space UI
screen-space canvases, drawn AFTER the world so they ignore the camera entirely. screen-space contains UI elements using UI units and normalized anchors/pivots. by default 1 UI unit = 1 window pixel, see the 0.3.1 UI scaling section for resolution scaling.

```csharp [InsideAClassInheritingNeoGameClass.cs]
NeoCanvas canvas = new();
NeoPanel panel = new()
{
    Transform = { Position = new Vector2(10, 10), Size = new Vector2(200, 100), Anchor = Vector2.Zero, Pivot = Vector2.Zero },
    Color = Color.Gray
};
NeoLabel label = new() { Text = "HP 100", Color = Color.White }; // Font is borrowed, never disposed by the UI
panel.Children.Add(label);
canvas.Children.Add(panel);
UIBus.Add(canvas); // NeoGame.UIBus updates before gameplay and draws its contents after the world

// top-left = parent top-left + parent size * Anchor + Position - own size * Pivot.
// Anchor and Pivot are centered (0.5, 0.5) by default since 0.3.1
```

buttons are clickable labels that read the Input facade

```csharp
NeoButton play = new() { Text = "Play", Font = font, Transform = { Anchor = Vector2.Zero, Pivot = Vector2.Zero } }; // sizes itself to the text
play.OnClick = () => { /* start game */ };
panel.Children.Add(play);
```

for implementing custom elements: you should subclass NeoUIElement and override OnUpdate() and OnDraw() functions. OnDrawUI() on your game draws AFTER all canvases and screen-space. override MeasureContent() to tell Fit how big your content is.

---

## [0.3.1] Negative rendering scales for UI & Sprites

Negative `Transform.Scale` now mirrors an object around its position. `Scale = new(-1, 1)` faces a right-facing sprite left; `(1, -1)` flips it upside down.

Before 0.3.1, a negative scale value on any axis would make the sprite vanish, and `NeoSpriteCollider` stops running collision logic of it as well:
- rendering: SpriteBatch builds the sprite rendering quad from `size * scale`, so a negative scale flips the quad's winding and the default `RasterizerState.CullCounterClockwise` culls it. Making both of the axes negative would make the thing flip the winding twice, which is why `(-1, -1)` still works somehow.
- bounds: `NeoSpriteRenderer.Bounds` came out with a negative width/height, and the logic for handling that is that "a negative rectangle never intersects anything".

The fix:
- the render bus makes the scale absolute and converts the negative/positive meaning to `SpriteEffects.FlipHorizontally` / `FlipVertically` instead. SpriteEffects only mirrors the pixels inside the same box and that the sprite origin mirrors (`size - origin`) to put the box on the other side of the position as well. both the sprites and texts go through the same render process with this sprite effect.
- `NeoSpriteRenderer.Bounds` now maintains an absolute positive rectangle by deriving it from the min/max of the two transformed frame corners. `NeoBoxCollider` uses this absolute scale values for size.

```csharp
// make sprite face cursor example
Vector2 scale = Transform.Scale;
scale.X = cursorX > Transform.Position.X ? MathF.Abs(scale.X) : -MathF.Abs(scale.X);
Transform.Scale = scale;
```

---

## [0.3.1] Window Sizing
you can now control window modes, i.e. Windowed, Fullscreen, BorderlessFullscreen

```csharp
NeoGame.Singleton.SetWindowMode(NeoWindowMode.Windowed);             // bordered window, restores the last windowed size
NeoGame.Singleton.SetWindowMode(NeoWindowMode.Fullscreen);           // exclusive fullscreen, the display changes mode
NeoGame.Singleton.SetWindowMode(NeoWindowMode.BorderlessFullscreen); // borderless window that covers the display, no mode change
NeoWindowMode current = NeoGame.Singleton.WindowMode;
```

call SetWindowMode after the game initializes.

macOS has no borderless fullscreen, so BorderlessFullscreen becomes Fullscreen there, and WindowMode reports Fullscreen.

---

## [0.3.1] Render Features

NeoSpriteRenderFeature is a NeoComponent that requires a base NeoSpriteRenderer to work. this mainly emerged as a need for me to make a sprite glow with an emissive outline when my mouse is hovered over it, but I don't want to make another set of spritesheet that has all the sprites have a drawn outline.

So, NeoSpriteRenderFeature is born, and should attach to the render bus by default.

a render feature draws extra layers in the same pass as its sprite in this order: OnDrawBehind -> Sprite -> OnDrawFront, with the camera culling, the camera matrix, negative-value flips and animation frames all apply on top of the feature.

Note: You should attach it to a NeoObject **AFTER** you've attached the NeoSpriteRenderer to the object.

```csharp
public class MyFeature : NeoSpriteRenderFeature
{
    public override void OnDrawBehind(SpriteBatch spriteBatch, in NeoSpriteDrawInfo info)
    {
        info.Draw(spriteBatch, someTexture, Color.Black, new Vector2(4, 4)); // e.g. a drop shadow
    }
} // thank you minecraft for being an inspiration of many things, including this library, you were great and I enjoyed modding more than ever
```

### Examples

`NeoOutlineFeature` is a built-in render feature for glowing sprite outlines: it draws a silhouette of the sprite 8 times behind it and expands by Thickness attr.

```csharp
crystal.AddComponent(new NeoSpriteRenderer(RenderBus, sheet));
NeoOutlineFeature outline = crystal.AddComponent(new NeoOutlineFeature { Color = Color.Gold, Thickness = 2f });

// in a component's OnUpdate: glow only while hovered
outline.Enabled = renderer.IsCursorOnSprite();
```

---

## [0.3.2] UI sizing and FlexBox

every element picks its size per axis with `Transform.WidthMode` and `Transform.HeightMode`:
- `Fixed` (default): use `Size`.
- `Fill`: attempts to resize to parent's full length.
- `Fit`: wraps the content; as in measured text for labels/buttons or a box that holds any children.

labels and buttons start as `Fit` on both axes. this replaces the old `AutoSize` flag. to give a button a fixed size, set the mode back to `Fixed` and set `Size`.

```csharp
// a panel that wraps its children. no flexbox
NeoPanel card = new()
{
    Transform = { WidthMode = NeoUISizing.Fit, HeightMode = NeoUISizing.Fit }, Color = Color.Gray,
    Padding = new NeoPadding(16, 8) // 16 left and right, 8 top and bottom
};
card.Children.Add(new NeoLabel { Text = "HP 100", Font = font });
```

panels (and flexboxes, since they inherit panels) have per-side `Padding`. the children anchors to the padded inner box where `Fill` takes the inner size and `Fit` adds the padding around the children.

```csharp
panel.Padding = 12; // same on all four sides
panel.Padding = new NeoPadding(16, 8); // horizontal, vertical
panel.Padding = new NeoPadding(4, 8, 4, 16); // left, top, right, bottom
panel.Padding = new NeoPadding(8) { Bottom = 24 }; // 8 everywhere except the bottom
```

negative or non-finite padding throws. (Ideally I should change this in the future but for now this works) If the padding is bigger than the panel then it'll squeeze the inner box to 0.

`NeoFlexBox` is a `NeoPanel` that orders its children in a row or a column. it controls the children's positions.

```csharp
NeoFlexBox menu = new()
{
    Direction = NeoFlexDirection.Column,
    Gap = 8, Padding = 12,
    Justify = NeoFlexAlign.Center,
    Align = NeoFlexAlign.Stretch,
    Transform = { WidthMode = NeoUISizing.Fit, HeightMode = NeoUISizing.Fit },
    Color = Color.DarkSlateGray
};
menu.Children.Add(new NeoButton { Text = "Play", Font = font });
menu.Children.Add(new NeoButton { Text = "Quit", Font = font });
```

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

// TODO: wrapping onto a second line, min/max sizes.

---

## [0.3.2] UI scaling

every canvas picks how the viewport pixels turn into UI units. the viewport is the game's draw box: the letterboxed area when `ViewportAspect` is set, else the whole window. the canvas layout size is `viewport / Scale`, so everything inside it (anchors, padding, flexbox, Fit) works in UI units and doesn't care about the resolution.

```csharp
// the default of ScaleFactor is 1 UI unit = ScaleFactor viewport pixels (1 by default), same as before 0.3.1
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

`Match` blends in log space like unity does, so 0.5 treats a 2x-wider viewport the same as a 2x-taller one. with `ViewportAspect` set to the reference ratio (e.g. 16:9 for 1280x720) the viewport always has that shape, so every `Match` value gives the same exact scale. a window resize, a window mode change or a new `ViewportAspect` gets picked up on the next update.

each canvas draws in its own SpriteBatch pass scaled by `Scale`, so text, panels and images all scale with no per-element code. `OnDrawUI` still draws afterwards in plain viewport pixels.

mouse: the mouse is in window pixels, the layout is in UI units. buttons convert already. custom elements do the same:

```csharp
bool over = ContainsScreenPoint(Input.MousePosition); // inside this element as drawn: letterbox, canvas scale, Rotation/Scale all handled
Vector2 local = ScreenToLocal(Input.MousePosition); // window pixels -> this element's untransformed layout space
Vector2 back = canvas.CanvasToScreen(element.Transform.LayoutPosition); // UI units -> window pixels
```

heads up: SpriteFont is a bitmap, so text scaled way up gets blurry. load a bigger font for big scale ranges.

---

## [0.3.2] Letterbox viewport

the game viewport doesn't have to match the window anymore. set an aspect ratio and the game draws into the largest box with that ratio that fits the window, centered, with bars around it:

```csharp
ViewportAspect = 16f / 9f; // null (default) = fill the whole window
LetterboxColor = Color.Black; // the bars
ClearColor = Color.CornflowerBlue; // the viewport background, replaces the hardcoded clear
Rectangle box = ViewportBounds; // window-pixel rectangle the game draws into
```

cameras, sprites, canvases, OnDraw and OnDrawUI all work inside that box, so a canvas with `ReferenceResolution = 1280x720` in a 16:9 viewport scales exactly with no `Match` compromise. `Input.MousePosition` stays in window pixels; `RenderBus.ScreenToWorld`, `NeoCanvas.ScreenToCanvas` and `ContainsScreenPoint` remove the bar offset.

the viewport is re-applied every update and draw, so window resizes and window mode switches keep the bars right. this is NOT a virtual resolution: everything still renders at native pixels, nothing is resampled.

---

## [0.3.2] ScreenTransform

UI layout moved from the element onto `element.Transform`, a `ScreenTransform`. it inherits the world `Transform`, so `Position`, `Rotation`, `Scale` and the Up/Right helpers are the same members you use on game objects, plus:

```csharp
NeoPanel badge = new()
{
    Transform =
    {
        Size = new Vector2(64, 64),
        Anchor = new Vector2(1, 0), Pivot = new Vector2(1, 0), // top-right corner
        Position = new Vector2(-8, 8),
        WidthMode = NeoUISizing.Fixed, HeightMode = NeoUISizing.Fixed,
        Rotation = MathHelper.ToRadians(15), Scale = new Vector2(1.2f) // visual only
    }
};
Vector2 topLeft = badge.Transform.LayoutPosition; // computed, read-only: LayoutPosition, LayoutSize, DesiredSize
```

- `Transform` is get-only: one transform per element, set it with the nested `Transform = { ... }` initializer.
- Anchor and Pivot default to (0.5, 0.5), centered. NeoFlexBox children and canvases ignore them.
- Rotation (radians) and Scale work like a CSS transform: the element and its children turn/scale around the Pivot when drawn. layout, Fit and NeoFlexBox use the untransformed box, so a rotated button doesn't push its neighbours around. hit tests (`ContainsScreenPoint`, buttons) follow the drawn shape.
- a rotated or scaled element costs one extra SpriteBatch pass for its subtree. the canvas's own Rotation/Scale do nothing, use the canvas scale settings instead.


---

## [0.3.2] Much Fucking Better Lifecycle Iterations & During-Iteration Modification Handling.

you can now load/unload worlds, destroy objects, add/remove components, start/stop timers and add/remove cameras or sprites now safely from inside ANY callback: `OnUpdate`, `OnStart`, `OnCollision`, `OnDestroy`, timer callbacks, render features, `OnDraw`.

This liberates the need to defer a scene switch anymore.

```csharp
public class Portal : NeoComponent, ICollision
{
    public void OnCollision(NeoBoxCollider other, NeoCollision collision)
    {
        WorldManager.Unload("level1"); // fine mid-collision now
        WorldManager.Load("level2");
    }
}
```

### `PassSnapshot<T>` Disposable Struct Helper

A wrapper that helps with iterating loops, with functions that helps process removal-during-iteration or addition-during-iteration.

#### The problem

The problem we have before that is existent in EACH LIFECYCLE of NeoGameLib is that a list can be mutated during iteration.

For example: while iterating through a game object list, a component from a game object could:

1. kill a game object in that list;
2. add a new game object ot that list;
3. kill every game object in that list.

What results from that example:

1. a "Collection was modified" runtime exception that explodes the game;
2. skipped items -- an item at index 3 was removed while the loop is still iterating on index 3, and index 4 skips to index 3 -- causing the original index 4-but-now-3-object skipped, game explodes;
3. An object was destroyed already but its update function was still called in the next tick because indexes are stale, game explodes

There are easy micro fixes to prevent some of the issues, i.e. a reverse-for-loop can solve the removal-during-iteration but not the addition issue; `list.ToArray()` generates fucking garbage every fucking frame which will slow down the game tick loop a lot; snapshotting into a list which shares the same GC problem; locking/deferring modifications to a queue which requires more mental power for me to implement and it's way too inconvenient to keep track of every single callback.

#### What this struct does

So this struct was created to address all of the above. Mechanically, it does 4 things:

1. Copies the list at the start of the pass into a shared array, which prevents modification of the list since user code modifies the real list, not the copied list. Everything the user code modifies to the real list is then snapshotted in the next frame and then ran.
2. Uses the Shared Array Pool instead of creating a new list instance every time. The `ArrayPool<T>.Shared` gives a pre-existing array so there's no new-list-creating and no GC required for creating a list.
3. Clears its own pool for stale references (i.e. dead objects, etc.) before returning it back
4. Each call gets its own snapshot: for example, if an enemy update func needs to iterate through another list i.e. collisions list or its "children", the inner loop rents its own array. Simpler words: nesting works.

#### Caveats

Some caveats while using this:

- You must check `IsAttached` or its equivalent in the loop iteration. Since the snapshot won't know that an item was destroyed after it's copied.
- "Removed" means "detached from array", but not null in the snapshot. You'll decide whether that reference is still alive.
- Count is readonly. (duh)
- If the list is empty, it'll still rent a 0-length array. But this is much better than creating a list on each update tick so a very efficient compromise.

### Usage of the Helper Struct

This is the basic shape of using it:

```csharp
using PassSnapshot<Enemy> pass = new PassSnapshot<Enemy>(enemies);
for (int i = 0; i < pass.Count; i++)
{
    Enemy enemy = pass[i];
    if (!enemy.IsAttached) continue; // it may've been destroyed earlier in this same pass
    enemy.Update();
}
```

This shape should handle the removal/addition-during-iteration issues by default.

This is the nested version:

```csharp
using PassSnapshot<Enemy> pass = new PassSnapshot<Enemy>(enemies);
for (int i = 0; i < pass.Count; i++)
{
    Enemy enemy = pass[i];
    if (!enemy.IsAttached) continue;

    using PassSnapshot<Bullet> children = new PassSnapshot<Bullet>(enemy.Children);
    for (int j = 0; j < children.Count; j++)
    {
        Bullet bullet = children[j];
        if (!bullet.IsAttached) continue;

        bullet.Update(); // can remove from enemy.Children, spawn more, whatever
    }
}
```

**BAD EXAMPLES: Don't use it like this!**

```csharp
// no IsAttached check which leads to calling an Update() on a dead enemy.
using PassSnapshot<Enemy> pass = new PassSnapshot<Enemy>(enemies);
for (int i = 0; i < pass.Count; i++)
    pass[i].Update();

// forgetting using / Dispose, which causes the array never returns to the pool.
PassSnapshot<Enemy> pass = new PassSnapshot<Enemy>(enemies); // leaked
for (int i = 0; i < pass.Count; i++) { ... }

// BAD: caching the struct and reusing it across frames -- it's a one-shot copy and it expires very quickly

// BAD: relying on ordering to matter across mutations.
// Items removed mid-pass are still in the snapshot, though ideally the loop would just skip them.
// Don't assume "the list looks like X" after the pass. Only the real list does.
```

Rule of thumb:

```csharp
using PassSnapshot<T> pass = new PassSnapshot<T>(list);
for (int i = 0; i < pass.Count; i++)
{
    T item = pass[i];
    if (!item.IsAlive) continue;
    item.DoTheThing();
}
```

---

# Road to 0.4.0 - NeoMap, Data-Driven Scenes, Animation State Machines

This is a huge upgrade from my previous GridMapperComponent.cs which acted more as a makeshift rather than a scalable solution.

The issues with the previous implementation: 
- relied heavily on hardcoded logic for each tiles
- each tile that has a collider is an individual collider; collider calculations aren't merged so that makes for inefficient collider passes
- I can't move this from project to project forever. Most of the logic had to be rewritten if I wanted to add something to the generation mechanics, and even the generation logic was patched up with glue.
- Editing files by hand were slow. Especially when maps get SUPER LARGE.

So I wanted the new "grid mapper" to do these things:
1. I can move files to other places and have them work without needing to implement the same function-mapping shit
2. maps have a format that is extensible, and that these files actually dictates what the tiles does, instead of me hardcoding tile behaviour
3. colliders are grouped for pass efficiency
4. a map editor (WIP, check out [NeoMap Editor made using Nuxt v4 + Tauri v2](https://github.com/CTRL-Neo-Studios/neomap-editor))

How do you make custom tile behaviour and store that on a file? Obviously I couldn't store actual code since that poses an actual cybersecurity issue, and also I have no clue how to do that. I did have some clue, however, in making CLIs with C# & Typescript.

What would be the representation of a custom function in a file? An "id" that corresponds to a "function" that holds the same "id" in the code. That way, I only need to copy the standalone function when moving maps to other projects. Having data map to actual logic and having that data structure be extensible enough to allow mutations in the data structure is hard;
but luckily i found json-schema.org and things had worked out. In fact, I've worked on something similar a year before, where I wanted to make a graphical programming tool that runs nodes-as-functions through the software I'm making across platforms, while having the blocks be self-correcting-according-to-signature and store custom logic. I hardcoded a schema of nodes with different functions and mapped them to corresponding runtime functions in the software.

So let's call these little packets of data that contains references to actual logic in the code as "specifications", or "specs" for short. Then I thought about prefab tiles, tiles that has specs already assigned on them, as well as a registry that contains mapping from specs to actual runtime code parts; and, with that, a layered map that holds tiles and prefabs. Sets of prefabs.

Now we're storing everything in JSON. These maps includes layers of stuff on a grid, including:
- prefabs
- merged colliders
- free-placed objects
- tags
- collider triggers

which all run data-driven actions. the file describes WHAT is where; the code dictates what the WHAT does.

This solves per-tile hardcoding and per-map code, since you can just copy and paste the schema, types, prefabs, and maps from one object to another.

## [0.4.0] File layout

everything is stored in the `Data/` folder in the project root. the NuGet package's build targets creates the folders and generate the schema on every build and copies the maps + prefab libraries next to the build output to let the game read them in runtime.

```
Data/
  neo.project.json -> editor/tool entry point that holds schema name, content root. I forsee this file having potential of being a brief representation of references of the project's persisted file data-to-code stuff, like fingerprints of a project, so this is extensible.
  <Game>.neoschema.json -> generated JSON Schema, ONE per game. the reason I didn't go for multi-schema is because... there's no need. One schema holds shapes of specs and multiple schemas would mean a nightmare in finding them, generating them, reading them, doing all sorts of logic with more than one schema file... augh
  NeoMaps/level1.neomap.json -> levels
  NeoPrefabs/common.neoprefabs.json -> prefab & collider libraries
```

the schema regenerates on every build and ideally it's editor-only. the game's job is to validate the maps against the code.

## [0.4.0] Specs: authoring data

In a better way of putting this (trying to straighten your mind in 2am is hard): a "spec" is a small authoring-data-type that adds components to an object when building a map. they're discovered automatically by the game in that any public non-abstract `INeoSpec` type in the game assembly (or an assembly it references) is accounted for, w/o registration calls. the name of the spec-implemented class is the resulting typing name, or you can use the attribute `[NeoName("Door")]` for a more stable one in case of class name changes; duplicate names will throw an error (You've been warned).
the spec's constructor params ARE its schema

```csharp [InsideYourGame.cs]
public sealed record DoorSpec([property: NeoAsset(typeof(Texture2D))] string Texture, string Level = "level1") : INeoSpec
{
    public void Apply(NeoObject target)
    {
        target.AddComponent(new NeoSpriteRenderer(NeoGame.Singleton.RenderBus, NeoGame.Singleton.Assets.Load<Texture2D>(Texture)));
        target.AddComponent(new Door { Level = Level });
    }
}
```

the library ships `SpriteSpec`, `TextSpec`, `BoxColliderSpec` (Size required, Offset/IsTrigger optional), `SpriteColliderSpec`, `CameraSpec` and `TriggerSpec` (below). prefer records over classes-with-setters: property-initializer defaults on a plain class don't survive into the schema.

- missing properties fall back to constructor defaults; unknown properties THROW (typos fail loudly); on net9, params without a default are required. on net8 they quietly become null, so if you care, target net9.
- unknown members in files throw too, matching the strict schema. MonoGame structs serialize as `{ "x": .., "y": .. }`.

## [0.4.0] Prefab libraries

```json
{
  "prefabs": {
    "floor": { "specs": [ { "type": "SpriteSpec", "properties": { "texture": "Sprites/floor", "depth": 1 } } ] },
    "wall":  { "tags": [ "solid" ], "collider": "walls",
               "specs": [ { "type": "SpriteSpec", "properties": { "texture": "Sprites/wall" } } ] },
    "door":  { "tags": [ "door" ], "specs": [ { "type": "TriggerSpec", "properties": {
                 "size": { "x": 32, "y": 32 }, "tag": "Player",
                 "onEnter": [ { "type": "SwitchWorldAction", "properties": { "world": "level2" } } ] } } ] }
  },
  "colliders": {
    "walls": { "tags": [ "solid" ] }
  }
}
```

- a prefab is tags + a spec list + an optional `collider` group name. specs apply in order; an entry's `id` names it for per-object overrides.
- a prefab whose cells reference a collider group get merged into as few rectangles as possible, one `NeoBoxCollider` each (auto-added), and the group's specs land on each merged collider object -- that's where tags like `solid` go. group cells merge across layers. a group used by a prefab but missing from `colliders` throws.
- the library file itself: `{ "prefabs": { ... }, "colliders": { ... } }`.

## [0.4.0] Maps

```json
{
  "cellSize": { "x": 32, "y": 32 },
  "prefabs": [ "common" ],
  "layers": [
    { "name": "ground", "legend": { ".": "floor", "#": "wall" }, "rows": [ "###", "#.#", "###" ] }
  ],
  "objects": [
    { "name": "spawn", "tags": [ "spawn" ] },
    { "name": "Door-A", "prefab": "door", "position": { "x": 96, "y": 96 },
      "overrides": { "TriggerSpec": { "onEnter": [ { "type": "SwitchWorldAction", "properties": { "world": "level3" } } ] } } }
  ]
}
```

- layers stack in array order; a legend character maps to a prefab, `null` to empty, keys must be one character, rows may be ragged.
- an object inherits its prefab's tags + specs, adds its own on top (empty prefab ref = pure tag marker, e.g. spawnpoints). `overrides` shallow-merge top-level properties into entries keyed by their `id` (or spec name without one) -- to change a nested action, replace the whole list as above. `null` resets the entry to spec defaults, unknown keys throw.
- `SwitchWorldAction` unloads the trigger's own world before loading the target, which destroys the trigger, so any actions after it in the same list don't run. put it last.
- runtime: `WorldManager.CreateWorld("level1", w => NeoMap.Load("level1").Build(w));`, or `NeoMap.RegisterWorld("level1", "level1")` for the same thing in one call, which is what `LoadWorldAction`/`SwitchWorldAction` use when the world isn't registered yet. building never mutates the map, so one `NeoMap` can build any number of worlds. `NeoMap.FromJson` / `ToJson` exist for editors.

## [0.4.0] Tags

`NeoObject.Tags` is a set of strings, set from data (prefab/object/collider-group `tags`) or code. triggers filter by them, and `World.FindObjectByTag("spawn")` / `FindObjectsByTag` query them. tags replace component-marker classes (`GroundTag` style): if you still use the mover's `GroundComponentType`, it still works, but tags are the intended way forward.

## [0.4.0] Triggers and actions

every collider has enter/stay/exit events now: the collision bus diffs this frame's overlapping pairs against last frame's and fires `OnCollisionEnter` (both sides), then `OnCollision` every frame, then `OnCollisionExit` on whichever side still exists -- so a player destroyed while inside a trigger still gets the exit delivered to the trigger. `OnCollisionEnter`/`Exit` are default interface methods, so existing `ICollision` components compile unchanged.

`NeoTrigger` is a `NeoBoxCollider` with `IsTrigger = true` (the mover ignores triggers: no push-out, no grounding) that fires action lists when a matching object enters, stays in, or exits its bounds. the tag filter is `null` = anything.

an action is the same idea as a spec, but for behaviour: `INeoAction` types, discovered automatically, stored in map files as `{ "type": ..., "properties": ... }` lists. built-ins:

- `LoadWorldAction { world }` -- loads a world next to the running ones, never unloads anything. registers from the same-named map if needed
- `SwitchWorldAction { world, unload = null }` -- unloads `unload` (null = the trigger's own world), then loads `world`. list `world` itself in `unload` to restart a level
- `UnloadWorldAction { world = null }` -- null = the trigger's own world
- `DestroyAction { target = "self" | "other" }` -- queues the destroy for end of tick
- `SetEnabledAction { component, enabled = true, target = "self" }` -- matches a component by class name

game-defined actions: `public sealed record SaveGameAction(string Slot) : INeoAction { public void Invoke(NeoActionContext ctx) { ... } }`. `ctx.Self` is the trigger's object, `ctx.Other` the overlapping one. actions run during the collision pass, so loading worlds / destroying objects mid-callback is safe (same guarantee as any collision callback).

a door is now data, not code: sprite + `TriggerSpec` + unload/load actions. actions compose into lists, and `OnStay` exists for things like damage-over-time zones.

## [0.4.0] Schema for editors

`<Game>.neoschema.json` is a JSON Schema that follows the latest json-schema.org specifications. this specification applies to every spec and action under `$defs` (`spec:SpriteSpec`, `action:LoadWorldAction`) and the full `NeoMap`, `NeoPrefabLibrary` and `NeoProject` file shapes with `required`, `default`, enums-as-strings and `additionalProperties: false` (typos fail in the editor).
Ideally, the map editor software loads the file, validates the json schema, and renders the properties panel from the definitions.

## [0.4.0] What data still can't do

While specs/actions holds data that describes behaviour and allows reconstruction of that behaviour type to use the functions that exists in code, they can't hold actual behaviour. For example, if a want a tile that when "stepped on it and it'll shake the camera" is still a component somebody has to write in their monogame project alongside with a spec that attaches to it. the only way past that is an embedded scripting language, which I DO NOT INTEND IMPLEMENTING.

I find it interesting that no matter whatever the fuck library it is that I'm writing, there always comes to a point where I need to implement my custom programming language that is runnable across devices. I hope there's a library that allows me to make a programming langauge, define its syntax, and does a one-command-build that gives me compiler with cross-platform abilities, and can be run through that library's runner with correct given configurations.

COme to think of it... I might make it sometime. Or just use typescript lmao

also, a batched per-layer tilemap renderer needs to be implemented in the future for better rendering performance since rn each tile is one neosprite pass.

## [0.4.0] State Machines

originally I wanted to evolve the animator into a unity-style animator state machine, but then it occurred to me that enemy AI needs state machines too. So  I'm making the the state machine be standalone.

```csharp
public class SlimeBrain : NeoComponent
{
    public NeoStateMachine<SlimeBrain> Fsm;
    public int Hp = 3;
    public bool JustAttacked; // set by Chase when it bites, cleared next tick
    public float DistanceToPlayer() => /* ... */ 0;

    public override void OnStart()
    {
        Fsm = new(this);
        Patrol patrol = Fsm.Add(new Patrol());
        Chase chase = Fsm.Add(new Chase());
        Dead dead = Fsm.Add(new Dead());

        Fsm.AddTransition(patrol, chase, () => DistanceToPlayer() < 96);
        Fsm.AddTransition(chase, patrol, () => DistanceToPlayer() > 160);
        Fsm.AddAnyTransition(dead, () => Hp <= 0);
        Fsm.Start(patrol);
    }

    public override void OnUpdate(GameTime gameTime) => Fsm.Update(gameTime);
}

public class Chase : NeoState<SlimeBrain>
{
    public override void OnUpdate(GameTime gameTime) { /* Owner.ParentTransform ... move towards player */ }
}
```

### Animator state machines

`NeoAnimator` still plays exactly one clip. `NeoAnimatorController` owns a `NeoStateMachine<NeoAnimator>` whose `NeoAnimationState`s play their clip on enter. `NeoAnimator.IsFinished` (true once a non-looping clip is past its last frame) replaces the `OnAnimationFinished = () => Play(idle)` dance:

```csharp
NeoObject slime = World.Instantiate("Slime");
slime.AddComponent(new NeoSpriteRenderer(RenderBus, sheet));
NeoAnimator animator = slime.AddComponent<NeoAnimator>();
SlimeBrain brain = slime.AddComponent<SlimeBrain>();
NeoAnimatorController anim = slime.AddComponent<NeoAnimatorController>(); // after the brain, so it reads this tick's state

NeoAnimationState idle = anim.AddState(idleClip);
NeoAnimationState hop = anim.AddState(hopClip);
NeoAnimationState bite = anim.AddState(biteClip); // loop: false

anim.Machine.AddTransition(idle, hop, () => brain.Fsm.Current is Chase);
anim.Machine.AddTransition(hop, idle, () => brain.Fsm.Current is Patrol);
anim.Machine.AddAnyTransition(bite, () => brain.JustAttacked);
anim.Machine.AddTransition(bite, hop, () => animator.IsFinished);
anim.Machine.Start(idle);
```

the AI and the animation stay two separate machines and the animation just reads the brain

// TODO: hierarchical machines (a state can own its own `NeoStateMachine` if ever needed), blend trees (sprites don't blend), and data-authored machines in neoprefabs (conditions are lambdas, so that would need the parameter system back; maybe later on top of the same core).

---

