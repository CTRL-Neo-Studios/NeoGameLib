[0.1.x] for animation: Make a spritesheet and import it, and then do smth like this

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

[0.1.x] for text: add a SpriteFont asset in the mgcb editor, then smth like this

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

---

# Road to 0.2.0 - Worlds, Collisions, Input Gathering, Files, and Assets

[0.2.0] for worlds: registry of ALL worlds. Load pulls registered ones into the running set (any number at once, or none), Unload kicks one out and destroys its objects. objects are built on Load, not create

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

[0.2.4] for inputs: an action map over the keyboard. you can map keys to named actions, query key states and axis values. input maps collects inputs as the first lifecycle in the update tick, so other update-tick lifecycles are able to read the latest input states and values.

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

---

[0.1.x] for colliders: two flavors, both register themselves on awake, collisions are computed automatically every frame after the world update

**[0.2.0] !New in Collider Architecture!**

1. Interface-based collider callbacks

So, before this change, the right way of using a collider would be creating a new class that inherits the collider class used, adding that to the object, and then having it call other components' functions when a collision happens.
Too much steps and too much inheritance, and so I'm making it interface-based: now you only have to add the collider component to the object, implement the Collision interface in other components in the same object, and the collider component will
automatically call the interface functions on collision. Easier to setup, and makes it more easier to check which object has a collider as oyu can just now look for a collider component instead of looking for a component that had inherited the collider component.

2. World-based collisions

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

[0.2.0] for movement: one movement logic component does keys, Move(), lerp, gravity, jump and ground detection. everything optional

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

[0.2.3] colliders now stops the mover by default. when the mover overlaps a collider it is now "pushed" (moved in negative velocity) back out. also needs a NeoBoxCollider on the same object:

```csharp
mover.CollisionWhitelist.Add(typeof(TriggerTag)); // objects with these components are pass-through (everything else solid)
mover.CollisionBlacklist.Add(typeof(SolidTag)); // ONLY these objects are solid (everything else pass-through)
// both lists empty = everything is solid
// you can use the generic helpers to add/remove tags to/from the lists
```

mover.Velocity is now exposed in case if you want to write your own collision correction.
Grounded property is still determined via GroundComponentType tags

---

for timers: reusable individual countdown timers without the need to attach to specific objects. but it's ideally used in a component or an object.

```csharp
NeoTimer invuln = new(0.5f); // seconds
invuln.OnFinished = () => { /* finish func here */ };
invuln.Start(); // Start while running restarts from full

// a sisyphus timer
spawnTimer.OnFinished = () => spawnTimer.Start();

// Stop kills it (fires OnStop), Pause/Resume freeze the countdown (fire OnPause/OnResume), OnStart fires on every Start, TimeLeft is the remaining seconds
```

---

for assets: cached loads and uses the path as key

```csharp
Texture2D bot = Assets.Load<Texture2D>("Sprites/bot"); // cache and load and quick reference/loading
Assets.Unload("Sprites/bot"); // removes cache
Assets.UnloadAll();
```

---

[0.2.3] file-system i/o: File as an object, Folder as an object

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

[0.3.0] for cameras: a world-space camera component. attach one to an object, select it explicitly, and the whole world pass renders through it. UI never sees this matrix

```csharp [InsideAClassInheritingNeoGameClass.cs]
NeoObject camObj = World.Instantiate("cam", new Transform(new Vector2(0, 0)));
NeoCamera cam = camObj.AddComponent(new NeoCamera(RenderBus)); // self-registers on awake
cam.Zoom = 2f;
cam.CullingMask = -1; // bitmask of sprite/text Layers this camera draws, default = everything

// every ENABLED camera renders the world stacked in Order (ties = registration order).
// cameras unregister themselves on destroy, so loading/unloading a world moves its cameras.
// MainCamera (first enabled) drives WorldToScreen/ScreenToWorld and the OnDraw hook.
// no enabled camera at all -> the world pass falls back to identity, the pre-camera behavior
Vector2 onScreen = RenderBus.WorldToScreen(new Vector2(100, 50));
```

[0.3.0] for ui: screen-space canvases, drawn after the world with an identity matrix so they ignore the camera entirely. retained elements with pixel sizes and normalized anchors/pivots

```csharp [InsideAClassInheritingNeoGameClass.cs]
NeoCanvas canvas = new();
NeoPanel panel = new()
{
    Position = new Vector2(10, 10), Size = new Vector2(200, 100),
    Anchor = Vector2.Zero, Pivot = Vector2.Zero, Color = Color.Gray
};
NeoLabel label = new() { Text = "HP 100", Color = Color.White }; // Font is borrowed, never disposed by the UI
panel.Children.Add(label);
canvas.Children.Add(panel);
UIBus.Add(canvas); // NeoGame.UIBus, updated before gameplay, drawn after the world

// top-left = parent top-left + parent size * Anchor + Position - own size * Pivot.
// children nest and paint parent-first. Enabled kills subtree updates, Visible kills subtree draws
```

custom elements subclass NeoUIElement and override OnUpdate(GameTime) + OnDraw(SpriteBatch). OnDrawUI(SpriteBatch, GameTime) on your game draws after all canvases, also screen-space

