# Contributing / Continuing the Rewrite

## Before you change gameplay

1. Prefer putting rules in **`BattleCity.Core`** so offline, client prediction, and server stay aligned.
2. Check [LEGACY-DELTAS.md](LEGACY-DELTAS.md) so you don’t “fix” an intentional remake choice.
3. For network work: add/adjust packets in `BattleCity.Shared`, handle in `GameServer`, apply in `GameSimulation` / online scene.
4. Add or extend a test under `tests/` when the rule is pure logic (collision, population, packets).

## Everyday commands

```powershell
dotnet build src/BattleCity.sln
dotnet test src/BattleCity.sln
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
dotnet run --project tools/BattleCity.Smoke/BattleCity.Smoke.csproj
./tools/ContentBuild.ps1          # after changing legacy BMPs / map.dat / wav
./tools/Publish-Release.ps1       # refresh friend-shareable zip
```

## Content & art

- Source of truth for many sheets: `legacy/data` BMPs → `ContentBuild.ps1` → `src/BattleCity.Client/Content/Sprites/`
- Do **not** commit Affinity Photo / scratch files (`*.afphoto`, `image Edits/`, `* - Copy.png`)
- `dist/` is generated — ignored by git; rebuild with Publish-Release when sharing builds

## Suggested contribution sizes

| Size | Examples |
|------|----------|
| Small | Bugfix with a Core unit test; HUD label; docs |
| Medium | One legacy packet end-to-end (`smUnderAttack`, `smItemLife`, …) |
| Large | New scene, matchmaking, or finance reinvention — discuss scope first |

## PR / commit tips

- Keep commits focused (docs vs host tooling vs gameplay)
- Mention legacy file references in the PR body when matching old behavior
- Run smoke after online/sync changes

## Where to start reading

1. [PROJECT-STATUS.md](PROJECT-STATUS.md) — checkpoint overview  
2. [REWRITE-PROGRESS.md](REWRITE-PROGRESS.md) — what phases already cover  
3. `GameSimulation` tick order — systems pipeline  
4. `GameServer` message switch — online authority  
5. `InGameScene` / `InGameOnlineScene` — client loop  
