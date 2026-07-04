# Battle City

Battle City is a very fun and addicting online game. You start off by either building a city or applying for a job in a city built by someone else. Your goal is to defend your city while attacking and destroying enemy cities. All of this is done in real-time from the comfort of your tank!

The best thing about Battle City is that it has been released under the GPLv3 open source license. This license gives ownership of the game to the community and ensures Battle City will stay free and open source forever!

License: GPLv3  
Credits: Deceth

Download the latest release from the [official website](http://battlecity.org)

## C# / MonoGame Rewrite

This repository is being rewritten in **C# with MonoGame**. Progress is tracked in [docs/REWRITE-PROGRESS.md](docs/REWRITE-PROGRESS.md).

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet tool restore
dotnet restore src/BattleCity.sln
dotnet build src/BattleCity.sln
./tools/ContentBuild.ps1   # first run or after asset changes
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
```

The original C++ source (Visual Studio 2010, DirectDraw) is preserved in [`legacy/`](legacy/).

## Resources

* [Rewrite progress checklist](docs/REWRITE-PROGRESS.md)
* [How to Setup your Development Environment (legacy C++)](https://github.com/Deceth/Battle-City/wiki/How-to-Setup-your-Development-Environment)
* [Game Design](https://github.com/Deceth/Battle-City/wiki#game-design)
