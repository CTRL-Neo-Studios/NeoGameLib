for animation: Make a spritesheet and import it, and then do smth like this

```csharp [InsideAClassInheritingNeoGameClass.cs]
protected override void OnLoadContent()
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
// horizontalFirst: false walks down each column first instead of across each row
```

for text: add a SpriteFont asset in the mgcb editor, then smth like this

```csharp [AlsoInsideAClassInheritingNeoGameClass.cs]
protected override void OnLoadContent()
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

```csharp [InsideAClassInheritingNeoGameClass.cs]
protected override void OnLoadContent()
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

react to hits by subclassing and overriding OnCollision:

```csharp
public class HurtBox : NeoBoxCollider
{
    public override void OnCollision(NeoCollision collision)
    {
        // both sides get the call with the same collision obj so you have to figure out which collision object is yourself
        NeoBoxCollider other = collision.A == this ? collision.B : collision.A;
    }
}
```
