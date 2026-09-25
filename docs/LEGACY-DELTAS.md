# Legacy vs Rewrite — Important Deltas

The rewrite aims for **gameplay and protocol parity** with `legacy/`, not pixel-identical UI or identical C++ structure. This lists the deltas contributors should know.

**For a full audit (cities, spawning, compass, fix backlog), see [LEGACY-AUDIT-PLAN.md](LEGACY-AUDIT-PLAN.md).**

## Protocol & sim (mostly aligned)

- TCP port **5643**, legacy framing + checksum (`LegacyPacketCodec`)
- Meeting room, mayor/hire, build/demolish, shoot/pickup/drop, death/respawn/warp, cloak, chat (`/g`, `/pm`), factory counts, explosions
- Buildings use **GridAnchor** = footprint SE corner (cluster origin + `BuildingCollisionOffset` = 2)
- Command center from map **CityCenter** clusters (city id 63→0), see `CityBuildInitializer.ResolveCommandCenter`
- House population: two **50**-pop slots for research/factory/hospital; house pop = sum (max 100) — `BuildingPopulationSystem`
- Populated buildings are **bullet-immune**; bombs still destroy (legacy `delBuilding` detach rules)

## Intentional product changes

| Topic | Legacy | Rewrite |
|-------|--------|---------|
| Finance HUD | `smFinance` money UI | **Out of scope** — not implemented |
| In-game chrome | Right-side DirectDraw rail | Modern full-screen **1080p HUD** (`DisplaySettings.UseLegacyUi = false`) |
| Compass HUD | Single 8-way home arrow (`CityX/CityY`) | Dual ring: home CC + nearest orbable city (continuous rotation) |
| Resolution | Fixed / low-res era | Logical UI 1920×1080; world still 48px tiles (sprites often 2× source, dest stays 48) |
| Cloak / flare | Inventory consumables (city factories produce items) | When city has matching **research + factory**, abilities are **10s recharge** (HUD bar); inventory still works as fallback |
| Hosting | Dedicated C++ server EXE | Prefer **Server.Host** WinForms + invite string; console `BattleCity.Server` still works |
| Admin | Legacy admin accounts | SQLite `is_admin`; login bit; username `admin` always admin; Host UI toggles |

## Behavioral fixes / remake corrections

These differ from a naive port of legacy formulas because the rewrite’s coordinate model is clearer:

| Issue | Legacy quirk | Rewrite behavior |
|---------------------|------------------|
| Turret muzzle | `grid*48 - 24 + (6,10) + dir` (tank-style −24 on top-left grid) | Same pivot as tanks: top-left + `(6,10)` + dir (`WeaponGeometry`) |
| Positional audio | FMOD 3D | MonoGame `Play(volume, pitch, pan)` — pan must not go in the pitch slot |
| Minimap buildings | 3×3 markers on footprint | Same; center at `GridAnchor - 1` |
| Spawn / compass “home” point | Join sets `CityX/CityY` from **city index formula** (`CMap.cpp`); compass uses those coords forever | **Both** use CC **drive platform** tank center (`TryGetHomeReferenceWorldPosition`) — intentional correction (2026-09-08) |
| Meeting room city list | Rental + hiring cities + spiral `⌊players/5⌋+6` from BA neighborhood | **Match** — `CityRegistry` hiring + spiral from randomized BA seed (2026-09-08) |
| Multi-city buildings (MP) | Each city starts with CC only; shared world | **Match** — `LoadMultiplayerWorld()` CC-only (2026-09-08); offline still uses demo.city |

## Naming traps

- Build menu still labels some slots **“Time Bomb”** while `FactoryProducts` maps tree index **2** to **Cloak** (`EconomyConstants.CloakResearchTreeIndex`). Treat catalogs as source of truth for item type.
- “Play Online (Local Server)” auto-starts an embedded `GameServer` on `127.0.0.1:5643` (or reuses an existing listener). Friend hosting still uses **Server.Host**; the menu path is for same-PC / solo online testing.

## Still missing vs legacy multiplayer

Tracked in [REWRITE-PROGRESS.md](REWRITE-PROGRESS.md) and [LEGACY-AUDIT-PLAN.md](LEGACY-AUDIT-PLAN.md):

- `smFinance` (only if scope expands)
- Broader packet audit for unhandled admin/turret packets
- Optional: legacy 8-sector compass sprites (**P-2**)
- ~~Multi-city layout~~ — fixed 2026-09-08 (**C-1**, CC-only MP)
- ~~Meeting-room city list~~ — fixed 2026-09-08 (**C-2**, **C-3**)
- ~~Spawn/compass/death-camera mismatch~~ — fixed 2026-09-08

When unsure, compare remake code to the cited legacy file (`legacy/client/CItem.cpp`, `CDrawing.cpp`, server constants) and prefer shared Core helpers over duplicating rules in Client vs Server.
