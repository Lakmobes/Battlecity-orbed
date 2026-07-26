# Battle City

Battle City is a very fun and addicting online game. You start off by either building a city or applying for a job in a city built by someone else. Your goal is to defend your city while attacking and destroying enemy cities. All of this is done in real-time from the comfort of your tank!

The best thing about Battle City is that it has been released under the GPLv3 open source license. This license gives ownership of the game to the community and ensures Battle City will stay free and open source forever!

License: GPLv3  
Credits: Deceth

Original site / downloads: [battlecity.org](http://battlecity.org)

## C# / MonoGame Rewrite (active)

This repository’s playable path is a **C# + MonoGame** rewrite. The original C++ / DirectDraw code remains in [`legacy/`](legacy/) for reference and data.

**Start here if you are new to this codebase:**

| Doc | Why read it |
|-----|-------------|
| [docs/PROJECT-STATUS.md](docs/PROJECT-STATUS.md) | Current checkpoint, architecture, next tasks |
| [docs/LEGACY-DELTAS.md](docs/LEGACY-DELTAS.md) | What’s intentionally different from the C++ game |
| [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) | How to build, test, and continue work |
| [docs/REWRITE-PROGRESS.md](docs/REWRITE-PROGRESS.md) | Phase-by-phase checklist (0–30 done) |
| [docs/HOSTING.md](docs/HOSTING.md) | Hosting for friends (LAN / Tailscale) |

### Requirements

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet tool restore
dotnet restore src/BattleCity.sln
dotnet build src/BattleCity.sln
./tools/ContentBuild.ps1   # first run or after asset changes
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
```

- **Play Offline** — Buenos Aires sandbox  
- **Play Online** — start a server first (see below), then login → Meeting Room  

### Host / play with friends (no Visual Studio)

```powershell
./tools/Publish-Release.ps1
```

Share `dist/BattleCity-win-x64.zip`:

1. Host: `Server/BattleCity.Server.Host.exe` → **Start** → **Copy Invite**
2. Players: `Client/BattleCity.Client.exe` → paste invite into login **Server**

Details: [docs/HOSTING.md](docs/HOSTING.md).

### Dev online

```powershell
# Terminal 1
dotnet run --project src/BattleCity.Server.Host/BattleCity.Server.Host.csproj

# Terminal 2
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
```

Smoke test (mayor + 3 soldiers):

```powershell
dotnet run --project tools/BattleCity.Smoke/BattleCity.Smoke.csproj
```

### Solution layout

| Project | Role |
|---------|------|
| `BattleCity.Shared` | Constants, catalogs, network packets |
| `BattleCity.Core` | Headless ECS simulation |
| `BattleCity.Client` | MonoGame client |
| `BattleCity.Server` | Authoritative TCP server (port 5643) |
| `BattleCity.Server.Host` | WinForms host UI for non-devs |
| `tests/*` | Unit tests |

## Legacy resources

* [How to Setup your Development Environment (legacy C++)](https://github.com/Deceth/Battle-City/wiki/How-to-Setup-your-Development-Environment)
* [Game Design](https://github.com/Deceth/Battle-City/wiki#game-design)
