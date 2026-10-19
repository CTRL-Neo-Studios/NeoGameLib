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