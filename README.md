# NeoGameLib
A folder of MonoGame utility scripts that I made; common script utilities and all that

I'm thinking of having a system smth similar to the GameObject-on-top-of-Entity-Component-System programming model.
My own MonoGame GameObjects system to ease workloads for my future self, or smth like that.

note to past self: hey you did it after two days you finally managed to implement a rough draft of the game objects system yayyyy

I've left comments here and there so when you're confused by why a variable is named that way can just chekd the comment i left lol, good luck with those

## Install

install in bash or zsh

```bash
dotnet add package NeoGameLib
```

WARNING: install after you initialized the project with monogame

## Update

```bash
dotnet add package NeoGameLib
```

same command pulls the latest version, the package reference in your csproj gets overwritten wiht the newer version number

## Coordinates

same as MonoGame / screen space: y points DOWN.

| direction | sign |
|---|---|
| right | +x |
| left | -x |
| down | +y |
| up | -y |

- `(0, 0)` is the top-left of the world (and of the window for UI / `Input.MousePosition`)
- rotation is in radians and positive = clockwise on screen (`Transform.Right` at 0, `Down` at +π/2, `Up` at -π/2)
- positions are centers: an object's sprite (default `Anchor` = `(0.5, 0.5)`) and its `NeoBoxCollider` are both centred on `Transform.Position`. a collider `Offset` moves that center, and a sprite `Anchor` only moves the art, never a box collider (use `NeoSpriteCollider` if the box should follow the sprite)
- `Size` is the full width/height, so a slab of height `h` inside a tile of height `H` sits at `±(H - h) / 2` from the tile center. e.g. 16x8 box in a 16x16 tile: `offset.y = -4` = upper slab, `+4` = lower slab
- map cells are placed at `origin + (col * cellSize.x, row * cellSize.y)`, which is the tile's CENTER, so row 0 / col 0 is centred on `origin`
- a negative `Transform.Scale` mirrors collider offsets too (flip y and an upper slab becomes a lower one)

## Schema tool

every `dotnet build` of a game that references NeoGameLib runs the schema tool automatically and creates/updates `Data/neo.project.json` and `Data/<Game>.neoschema.json` which the map editor reads

you can run it manually (e.g. after turning it off) via:

```bash
dotnet ~/.nuget/packages/neogamelib/<version>/tools/neo-schema.dll bin/Debug/net9.0/MyGame.dll .
```

- 1st arg: your built game assembly (NeoGameLib.dll has to be in the same file tree next to it, which it does in the build output)
- 2nd arg: the game project folder, the one that gets the `Data/` folder
- on Windows the package folder is `%USERPROFILE%\.nuget\packages\neogamelib\<version>\tools\neo-schema.dll`

to turn off the automatic run or target the tool at another folder:

```bash
dotnet build -p:NeoSchemaOnBuild=false
dotnet build -p:NeoProjectDir=path/to/game
```