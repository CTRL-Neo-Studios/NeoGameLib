for animation: Make a spritesheet and import it, and then do smth like this

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

for text: add a SpriteFont asset in the mgcb editor, then smth like this

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

for colliders: two flavors, both register themselves on awake, collisions are computed automatically every frame after the world update

**!New in Collider Architecture!**

1. Interface-based collider callbacks

So, before this change, the right way of using a collider would be creating a new class that inherits the collider class used, adding that to the object, and then having it call other components' functions when a collision happens.
Too much steps and too much inheritance, and so I'm making it interface-based: now you only have to add the collider component to the object, implement the Collision interface in other components in the same object, and the collider component will
automatically call the interface functions on collision. Easier to setup, and makes it more easier to check which object has a collider as oyu can just now look for a collider component instead of looking for a component that had inherited the collider component.

2. World-based collisions
3. 
since the introduction of World Man. and multiple-world-loading, object collisions are now cross-worlds, which means that objects in different worlds can collide with each other and so on -- which maybe is some cases is great if you want
worlds to contain only one type of game objects and the other, but not particularly great in the use case described in the architecture note below where UI and game objects are in two different worlds. Not good. So I had to update the collider architecture and I've decided
to make it somewhat "list-based":

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
        // `other` is the collider on the object you hit, no A==this dance
    }
}

// object setup: the collider plus any number of ICollision components
player.AddComponent<NeoSpriteCollider>();
player.AddComponent(new HurtBox());
```

layer gate for keeping worlds' collisions separate (ui world vs game world):

```csharp
uiCollider.CollisionLayer = 0;               uiCollider.CollisionMask = ~(1 << 1); // ui accepts everything but game
gameCollider.CollisionLayer = 1;             gameCollider.CollisionMask = ~(1 << 0); // game accepts everything but ui
// enemies and players in different worlds but sharing a layer still collide, that's the point
```

for timers: reusable individual countdown timers without the need to attach to specific objects. but it's ideally used in a component or an object.

```csharp
NeoTimer invuln = new(0.5f); // seconds
invuln.OnFinished = () => { /* finish func here */ };
invuln.Start(); // Start while running restarts from full

// a sisyphus timer
spawnTimer.OnFinished = () => spawnTimer.Start();

// Stop kills it (fires OnStop), Pause/Resume freeze the countdown (fire OnPause/OnResume), OnStart fires on every Start, TimeLeft is the remaining seconds
```

for movement: one movement logic component does keys, Move(), lerp, gravity, jump and ground detection. everything optional

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

for worlds: registry of ALL worlds. Load pulls registered ones into the running set (any number at once, or none), Unload kicks one out and destroys its objects. objects are built on Load, not create

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

for assets: cached loads and uses the path as key

```csharp
Texture2D bot = Assets.Load<Texture2D>("Sprites/bot"); // cache and load and quick reference/loading
Assets.Unload("Sprites/bot"); // removes cache
Assets.UnloadAll();
```
