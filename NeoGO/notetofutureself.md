if you somehow forget what you've written, congrats you're a failure

ok so

basically you create a new world, then "instantiate" a new object in the world instance, then you can add whatever components to that neo object

each neo object has a transform and components can't exist without being attached to a neo object, similar to monobehaviour in unity

if you want smth like a Player object or a Entity object, make new class inhert `NeoObject` and in the on awake you attach some of your own components or write your own logic in the `Tick` update loop smhw, but for maintenance's sake please do custom logic in a component instead i don't wanna fix shit code in the future for myself thank you very much

example

```csharp
NeoWorld level1 = new NeoWorld("level1");
NeoObject mario = level1.Instantiate("Mario");

MarioMovementComponent marioMove = mario.AddComponent<MarioMovementComponent>(new MarioMovementComponent());

marioMove.Move(/* vector velocity stuff idk */);
```

Also in addition to this architecture: I did think about using the architecture stated in Lecture 0 but I think it's somewhat limited, as in each "entity" must have a sprite and that one custom functionality can only be on one entity at a time; i guess this reminded me of godot which I didn't like (i hated their node-based architecture but im still learning them, in pain) and probably it's not as extensible as godot was even if it is similar to that architecture

so i wrote a system that's more comfortable to my mental model instead

the rendering is still a nuisance though, like, im fucking amazed by how consistently annoying rendering is as an issue across all working game engines and even in software engineering/web development as well