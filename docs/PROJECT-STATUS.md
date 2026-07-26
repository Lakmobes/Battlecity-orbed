# Project Status (Handoff)

**Last updated:** 2026-07-26  
**Repo:** C# / MonoGame rewrite of Battle City (legacy C++ remains in `legacy/`)  
**Goal of this checkpoint:** ship a playable PC build friends can host, with docs so a new contributor can continue.

## Where things stand

Phases **0–30** in [REWRITE-PROGRESS.md](REWRITE-PROGRESS.md) are complete: headless ECS sim, offline sandbox, legacy-framed TCP multiplayer (accounts, meeting/hiring, combat sync, build tree, respawn/warp, etc.).

On top of that, this checkpoint adds **PC polish and sharing**:

| Area | What’s in |
|------|-----------|
| Host UI | `BattleCity.Server.Host` WinForms: Start/Stop, LAN invite copy, player list, admin toggles |
| Friend play | `tools/Publish-Release.ps1` → `dist/BattleCity-win-x64.zip` (self-contained Client + Server) |
| Client UI | Modern 1080p HUD, title/menu theme, login server field (`IP` or `IP:port`) |
| Gameplay | House population staffing, populated-building bullet immunity, rechargeable cloak/flare |
| Minimap | Buildings drawn as 3×3 footprint markers |
| Audio | Stereo pan fixed (was incorrectly applied as pitch) |
| Turrets | Muzzle origin aligned with tank pivot (no erroneous −24 offset) |
| Smoke | `tools/BattleCity.Smoke` — 1 mayor + 3 soldiers join + move |

**Finance HUD (`smFinance`)** remains intentionally out of scope.

## Architecture (quick map)

```
legacy/                     Original C++ + map/cities/wav (reference + server data)
src/BattleCity.Shared       Constants, catalogs, network packets
src/BattleCity.Core         Headless ECS simulation (no MonoGame)
src/BattleCity.Client       MonoGame DesktopGL client
src/BattleCity.Server       Authoritative TCP server (port 5643)
src/BattleCity.Server.Host  WinForms wrapper for non-dev hosts
tests/                      Core + Shared unit tests
tools/                      ContentBuild, Publish-Release, Smoke, importers
docs/                       Progress, hosting, this status, legacy deltas
```

Simulation authority: **Server** owns truth online; clients predict locally where legacy did. Shared rules live in Core (`WeaponActions`, `ItemDropActions`, `BuildingPopulationSystem`, …).

## How to run (developers)

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet tool restore
dotnet restore src/BattleCity.sln
./tools/ContentBuild.ps1          # first run or after asset pipeline changes
dotnet build src/BattleCity.sln
dotnet test src/BattleCity.sln

# Offline
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
# → Play Offline (Buenos Aires)

# Online (two terminals)
dotnet run --project src/BattleCity.Server.Host/BattleCity.Server.Host.csproj   # or BattleCity.Server
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
# → Play Online → Server 127.0.0.1 → Meeting Room

# Smoke
dotnet run --project tools/BattleCity.Smoke/BattleCity.Smoke.csproj
```

Guest login: blank username or password `guest`. Optional: create account from client (F2) or server CLI.

## How to share with friends

```powershell
./tools/Publish-Release.ps1
```

Share `dist/BattleCity-win-x64.zip`. Host runs `Server/BattleCity.Server.Host.exe` → **Start** → **Copy Invite**. Players run `Client/BattleCity.Client.exe` and paste the address into login **Server**. Details: [HOSTING.md](HOSTING.md).

## Docs index

| Doc | Purpose |
|-----|---------|
| [PROJECT-STATUS.md](PROJECT-STATUS.md) | This handoff overview |
| [LEGACY-DELTAS.md](LEGACY-DELTAS.md) | Intentional + accidental differences vs C++ |
| [REWRITE-PROGRESS.md](REWRITE-PROGRESS.md) | Phase checklist and packet history |
| [HOSTING.md](HOSTING.md) | LAN / Tailscale / firewall for players |
| [CONTRIBUTING.md](CONTRIBUTING.md) | How to pick up work safely |

## Suggested next work

1. Network `smUnderAttack` (local under-attack UI already exists)
2. `smItemLife` — bomb fuse / item TTL for remotes
3. `smPromotion` — rank-up chat on point thresholds
4. Wire smoke test into CI (`.github/workflows/build.yml`)
5. Clarify or auto-start local server from “Play Online (Local Server)” menu path
6. Broader packet audit: `ServerMessageId` vs handlers in `GameServer`

See [CONTRIBUTING.md](CONTRIBUTING.md) for workflow tips.
