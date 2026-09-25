# Legacy Audit & Parity Plan

**Last updated:** 2026-09-08  
**Purpose:** Single place to track **what changed** vs the original C++ game (`legacy/`), **what should match**, and **what to fix next** — especially cities, spawning, and compass (reported as feeling “off”).

**Companion docs:**

| Doc | Role |
|-----|------|
| [LEGACY-DELTAS.md](LEGACY-DELTAS.md) | Short delta cheat sheet (intentional changes + naming traps) |
| [REWRITE-PROGRESS.md](REWRITE-PROGRESS.md) | Phase checklist (network packets, features shipped) |
| [PROJECT-STATUS.md](PROJECT-STATUS.md) | Handoff / how to run |

---

## How to use this document

1. Pick a **parity tier** for each item before coding (see below).
2. Compare rewrite code to cited **legacy files** — not memory or old docs alone.
3. Add a **regression test** in `tests/` when the rule is numeric or deterministic.
4. When you fix something, move it from **Accidental / open** → **Verified match** and note the test.

### Parity tiers

| Tier | Meaning |
|------|---------|
| **P0 — Must match legacy** | Multiplayer rules, spawn packets, city ids, compass target semantics |
| **P1 — Match unless documented** | Meeting-room UX, multi-city world layout |
| **Intentional product** | Modern HUD, no finance, cloak recharge, etc. — do not “fix back” without discussion |
| **Intentional correction** | Rewrite deliberately fixes a legacy quirk (e.g. turret muzzle math) — keep, document |

---

## Master change inventory

### Intentional product changes (keep)

| Area | Legacy | Rewrite | Doc |
|------|--------|---------|-----|
| Finance HUD | `smFinance` | Not implemented | LEGACY-DELTAS |
| In-game chrome | Right-side DirectDraw rail | 1080p full-screen HUD | LEGACY-DELTAS |
| Cloak / flare | Factory inventory items | 10s recharge when research+factory exists | LEGACY-DELTAS |
| Hosting | C++ server EXE | Server.Host + invite string | PROJECT-STATUS |
| Dual compass rings | Home arrow only | Home CC + nearest orbable city (modern HUD) | *This doc § Compass* |
| Offline `.city` layouts | N/A at runtime in legacy MP | Loaded for **offline** sandbox only; online uses CC-only map | *This doc § Cities* |

### Intentional corrections (keep, test)

| Area | Legacy quirk | Rewrite | Evidence |
|------|--------------|---------|----------|
| Turret muzzle | Extra −24 on grid | Tank pivot `(6,10) + dir` | `WeaponGeometry`, LEGACY-DELTAS |
| Positional audio | FMOD 3D | MonoGame volume + **pan** (not pitch) | LEGACY-DELTAS |
| Spawn collision | Fixed formula pixel coords | Drive platform on real CC + open-tile search | `CommandCenterLookup`, *§ Spawning* |
| Spawn + compass home | Same formula `CityX/Y` | **Both** use drive-platform tank center (2026-09-08) | `TryGetHomeReferenceWorldPosition` |

### Verified aligned (spot-check when touching nearby code)

- TCP port **5643**, packet framing (`LegacyPacketCodec`)
- City catalog order (64 names) — `Structs.cpp` ↔ `CityCatalog.cs`
- CC map scan **63 → 0** on tile type 3 — `CMap.cpp` ↔ `CityBuildInitializer`
- GridAnchor = footprint SE corner (+ offset 2)
- Starting build permissions `[1,2,4]` — `CCity::resetToDefault`
- Respawn timer **10s** — `TIMER_RESPAWN` ↔ `GameConstants.TimerRespawn`
- Network death/respawn/warp packets — Phases 16, 30
- House population 50+50, bullet immunity when staffed

### Accidental / open mismatches (fix candidates)

| ID | Area | Symptom | Severity |
|----|------|---------|----------|
| ~~**C-1**~~ | Cities | Online server loads only BA demo.city | **Done 2026-09-08** — CC-only `LoadMultiplayerWorld` (C-B1) |
| ~~**C-2**~~ | Cities | Meeting room list ≠ legacy | **Done 2026-09-08** — `SendCommandos` + spiral `SendTheCities` |
| ~~**C-3**~~ | Cities | `DefaultCityId = 0` for empty slots | **Done 2026-09-08** — random BA neighborhood starting city |
| ~~**C-4**~~ | Cities | Offline `TryGetCityBuild(0)` aliasing | **Done 2026-09-08** — offline uses catalog city id |
| ~~**S-1**~~ | Spawning | Spawn vs compass home mismatch | **Done 2026-09-08** — both use drive platform |
| ~~**S-2**~~ | Spawning | No legacy formula tests | **Done 2026-09-08** — `LegacyCitySpawnFormulaTests` |
| ~~**S-3**~~ | Spawning | Death camera wrong CC | **Done 2026-09-08** — filters by home grid |
| ~~**S-4**~~ | Spawning | Centroid `GetSpawnPosition` join fallback | **Done 2026-09-08** — removed from join/respawn paths |
| ~~**P-1**~~ | Compass | Offline orb arrow unfiltered | **Done 2026-09-08** — `IsOrbable` filter |
| **P-2** | Compass | Continuous rotation vs legacy **8 discrete** sprites | **Done** — home arrow uses `imgArrows` / `imgArrowsRed` 8-sector frames |

---

## Area audit: Cities

### Legacy behavior

**World map**

- `legacy/server/CMap.cpp` — `CalculateTiles()` scans `map.dat` for tile `3` (command center cluster).
- City index counts down **63 → 0** per CC found.
- Sets **`City[citIndex]->x/y`** via index formula (not building tile):

```cpp
// legacy/server/CMap.cpp (lines 101–102)
City[citIndex]->x = (512*48) - (32 + (citIndex % 8 * 64) + 1) * 48;
City[citIndex]->y = (512*48) - (32 + (citIndex / 8 * 64) + 1) * 48;
```

**Runtime city state**

- `legacy/server/CCity.cpp` — per-city mayor, build tree, factories, hiring flags.
- Multiplayer starts with **CC only**; players build everything else (no `.city` file load on server).

**Meeting room (`smAddRemCity`)**

- `legacy/server/CSend.cpp` — `SendCityList()` sends three streams:
  1. Player **rental city** (if valid)
  2. **`SendCommandos`** — mayor’d cities hiring commandos
  3. **`SendTheCities`** — spiral from `startingCity`, count = `⌊players/5⌋ + 6`
- `startingCity` randomized from `{18,19,20,26,27,28,34,35,36}` (Buenos Aires neighborhood).

### Rewrite behavior

| Piece | Location |
|-------|----------|
| Catalog | `src/BattleCity.Shared/Catalogs/CityCatalog.cs` |
| CC grid from map | `src/BattleCity.Core/City/CityBuildInitializer.cs` |
| Server boot | `GameServer.Start()` → `LoadMultiplayerWorld()` (CC-only from map.dat) |
| Per-city build state | `GameSimulation._cityBuilds`, seeded for every map CC |
| Meeting list | `src/BattleCity.Server/CityRegistry.cs` — `BuildCityList()` |
| Empty-city preference | Spiral from randomized `StartingCityId` in BA neighborhood `{18..36}` |
| Offline sandbox | Still loads `.city` demo via `LoadCityLayout` + `SpawnDemoItems` |

### Gap analysis

| Check | Match? | Notes |
|-------|--------|-------|
| 64 city names / order | Yes | |
| CC scan order 63→0 | Yes | |
| Shared world, all CCs on map | Yes | `LevelLoader.SpawnAllCommandCenters` |
| Each city’s **physical buildings** on shared map | **Yes (C-1)** | CC only until players build (legacy MP) |
| Meeting list algorithm | **Yes (C-2, C-3)** | Hiring + spiral `⌊players/5⌋+6` from BA seed |
| Offline demo layouts | Intentional | Offline still uses `.city` files |

### Recommended fixes (cities)

**Phase C-A — Meeting room parity (P1)** — **DONE 2026-09-08**  
**Phase C-B — Multi-city world layout (P0)** — **DONE 2026-09-08 (C-B1)**  
- Server + online client: `LoadMultiplayerWorld()` — every map CC, no demo.city  
- Offline unchanged: `LoadCityLayout` + demo items  

**Phase C-C — City id hygiene (P1)** — **DONE 2026-09-08**  
- Offline UI resolves catalog city id (e.g. Buenos Aires = 27).

---

## Area audit: Spawning

### Legacy behavior

**Join**

- `legacy/server/CPlayer.cpp` — `JoinGame()` sets `stategame.x/y = city->x/y` (formula above), sends `smStateGame`.

**Client join**

- `legacy/client/CProcess.cpp` — `ProcessEnterGame()` sets `CityX = game->x`, `CityY = game->y` (used by compass forever; **not updated on warp**).

**Respawn**

- `legacy/server/CServer.cpp` — `respawnPlayers()` after 10s sends `smWarp` (x,y,city) + `smRespawn`.
- Respawn position uses same **`city->x/y`** as join.

### Rewrite behavior

| Piece | Location |
|-------|----------|
| Join spawn (server) | `GameServer.JoinGame()` → `TryGetCityRespawnPosition` → `FindOpenTankSpawnNear` |
| Respawn position | `CommandCenterLookup.GetDrivePlatformSpawnPosition()` — southern drivable CC row |
| Respawn timing | `CombatLifeSystem` + `GameSimulation.ProcessNetworkPlayerRespawns` |
| Online local player | `SuppressLocalPlayerRespawn`; reconciled via `smWarp` |
| Death camera (online) | `InGameOnlineScene` — **unfiltered** `TryGetWorldPosition(world)` |

### Three different “home” points — **resolved 2026-09-08**

| Use | Legacy | Rewrite (current) |
|-----|--------|-------------------|
| Join / respawn packet | `city->x/y` formula | CC **drive platform** (+ open tile search) |
| Compass home arrow | `CityX/CityY` (= join coords) | **Same drive-platform tank center** |
| Death camera snap | (legacy UI pan) | Home CC drive-platform reference (filtered by city) |

**Decision recorded:** Strategy **C / hybrid** — keep drive-platform spawn (gameplay-correct); point compass + death camera at the same pad. Documented as intentional correction vs legacy index formula (`LegacyCitySpawnFormula`).

### Recommended fixes (spawning)

**Phase S-A — Unify home reference (P0)** — **DONE 2026-09-08**  
**Phase S-B — Death camera (P0)** — **DONE 2026-09-08**  
**Phase S-C — Remove weak fallbacks (P1)** — **DONE 2026-09-08**

---

## Area audit: Compass

### Legacy behavior

- `legacy/client/CDrawing.cpp` — `DrawArrow()` only.
- Target: **`Player[me]->CityX/Y`** (set once at join from `smStateGame`).
- 8-way discrete sprites; ratio threshold `|difX/difY| > 2`.
- Updates every **100 ms**; under-attack red flash **500 ms** toggle.
- Position: right UI rail (`MaxMapX + 5`, y=160).
- **No orb / enemy-city arrow.**

### Rewrite behavior

- `src/BattleCity.Client/Rendering/UnderAttackPanelRenderer.cs` — dual ring HUD.
- Inner arrow → `RenderContext.CityCenterWorldPosition` (CC center lookup).
- Outer arrow → nearest other CC where `IsOrbable` (**online only**).
- Continuous rotation via `CompassArrowHelper.ComputeArrowRadians`.
- Under-attack: `CityAlertSystem` 3s / 500ms flash — aligned in spirit.

### Gap analysis

| Check | Match? | Notes |
|-------|--------|-------|
| Points toward home | **Partial (S-1)** | Different target point |
| 8-way sprites | Intentional HUD | Modern continuous arrows |
| Orb arrow | **New feature** | Document as intentional |
| Offline orb filter | **No (P-1)** | `InGameScene` missing `IsOrbable` predicate |
| Under-attack flash | Mostly yes | Timing similar |

### Recommended fixes (compass)

**Phase P-A — Tie compass to spawn reference (P0)** — **DONE 2026-09-08**  
**Phase P-B — Offline orb filter (P1)** — **DONE 2026-09-08**  
**Phase P-C — Optional legacy 8-sector regression (P2)** — **DONE** (`UnderAttackPanelRenderer` + `CompassArrows.png`)

---

## Prioritized work backlog

Progress as of **2026-09-08**: cities + spawn/compass audit items complete.

| Order | ID | Task | Effort | Status |
|-------|-----|------|--------|--------|
| 1 | S-3 | Fix death camera CC filter | Small | **Done** |
| 2 | S-A | Unify spawn + compass reference; document decision | Medium | **Done** |
| 3 | S-B | Add legacy formula tests + parity note | Small | **Done** |
| 4 | C-2 | Meeting room list parity | Medium | **Done** |
| 5 | C-1 | Multi-city server layout (C-B1 CC-only) | Large | **Done** |
| 6 | C-4 | Remove `cityId 0` aliasing in offline UI | Medium | **Done** |
| 7 | P-1 | Offline orb compass filter | Small | **Done** |
| 8 | S-4 | Remove centroid spawn fallback | Small | **Done** |
| 9 | C-3 | Randomize `startingCity` from BA neighborhood | Small | **Done** |
| 10 | P-2 | Optional 8-sector compass sprites | Low | **Done** |

### Definition of done (overall)

- [x] Contributor can read **LEGACY-AUDIT-PLAN.md** + **LEGACY-DELTAS.md** and know what to match vs change.
- [x] Cities: meeting list + CC-only multiplayer world (legacy MP).
- [x] Spawning: join and respawn place tanks on drivable CC pad for **their** city id.
- [x] Compass: home arrow matches spawn reference; death camera pans to **own** CC.
- [x] Tests cover spawn formula (legacy documented) and per-city CC selection.

---

## Legacy file index (quick reference)

| Topic | Legacy | Rewrite |
|-------|--------|---------|
| City names | `legacy/client/Structs.cpp` | `CityCatalog.cs` |
| CC on map + formula x/y | `legacy/server/CMap.cpp` | `CityBuildInitializer.cs` |
| City sim | `legacy/server/CCity.cpp` | `CityBuildState`, `GameSimulation` |
| Meeting list | `legacy/server/CSend.cpp` | `CityRegistry.cs` |
| Join / warp | `legacy/server/CPlayer.cpp` | `GameServer.JoinGame` |
| Respawn loop | `legacy/server/CServer.cpp` | `CombatLifeSystem`, `GameSimulation` |
| Compass | `legacy/client/CDrawing.cpp` | `UnderAttackPanelRenderer.cs` |
| Join sets CityX/Y | `legacy/client/CProcess.cpp` | *(no equivalent — uses live lookup)* |

---

## Open product decisions (needs owner call)

1. ~~**Server world layout:**~~ **Decided 2026-09-08:** CC-only multiplayer (`LoadMultiplayerWorld`); offline keeps demo.city.
2. ~~**Spawn/coords:**~~ **Decided 2026-09-08:** drive platform for spawn + compass (intentional correction vs formula).
3. **Compass outer ring:** Keep as modern feature or hide until orb mechanics understood?
4. ~~**Meeting room:**~~ **Decided 2026-09-08:** full legacy spiral + hiring filters.

Record answers in [LEGACY-DELTAS.md](LEGACY-DELTAS.md) when decided.
