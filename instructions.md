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
