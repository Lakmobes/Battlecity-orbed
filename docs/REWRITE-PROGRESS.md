# Battle City Rewrite Progress

MonoGame + C# rewrite of Battle City. Legacy C++ source lives in `legacy/`.

For the current handoff checkpoint (host UI, modern HUD, population, recharge abilities, release zip), see **[PROJECT-STATUS.md](PROJECT-STATUS.md)** and **[LEGACY-DELTAS.md](LEGACY-DELTAS.md)**.

## Phases

- [x] **Phase 0 — Scaffold** — Solution layout, MonoGame window, CI workflow
- [x] **Phase 1 — Shared Constants & Types**
- [x] **Phase 2 — Asset Pipeline**
- [x] **Phase 3 — ECS Foundation**
- [x] **Phase 4 — Rendering Pipeline**
- [x] **Phase 5 — Input System**
- [x] **Phase 6 — Collision System**
- [x] **Phase 7 — Level Loading**
- [x] **Phase 8 — Gameplay Entities**
- [x] **Phase 9 — Tank / Turret AI**
- [x] **Phase 10 — UI Scenes**
- [x] **Phase 11 — Audio**
- [x] **Phase 12 — Multiplayer (optional)**
- [x] **Phase 13 — Network Gameplay Sync** (shoot, pickup, build/demolish; accounts still open)
- [x] **Phase 14 — Join World Snapshot** (late joiners receive items/buildings/demolishes)
- [x] **Phase 15 — Account Database** (SQLite + PBKDF2; guest login preserved)
- [x] **Phase 16 — Death / Respawn Sync** (`cmDeath` / `smDeath`)
- [x] **Phase 17 — In-Game Chat** (`cmWalkie` / `smChatMessage`)
- [x] **Phase 18 — HP Sync** (`smHp` for remote players)
- [x] **Phase 19 — Defensive Item Range** (legacy `inRange(true)` for turret/wall drops)
- [x] **Phase 20 — Chat Commands & Death Announcements** (`/g`, `/pm`, death lines in chat)
- [x] **Phase 21 — Killer City & Drop Feedback** (attacker city on deaths; chat when drops fail)
- [x] **Phase 22 — Mayor & Radar Chat** (mayor role sync, tank sprites, proximity chat routing)
- [x] **Phase 23 — Meeting Room & Hiring** (city list, mayor/commando apply, interview flow)
- [x] **Phase 24 — Interview Comms & Personnel** (applicant/mayor chat, deny applicants, fire commando)
- [x] **Phase 25 — Build Tree & Population Sync** (`smCanBuild`, `smUpdatePop`)
- [x] **Phase 26 — Points Update on Death** (`smPointsUpdate`)
- [x] **Phase 27 — Online Cloak Sync** (`cmCloak` / `smCloak`)
- [x] **Phase 28 — Factory Item Count Sync** (`smItemCount`)
- [x] **Phase 29 — Explosion Sync** (`smExplosion`)
- [x] **Phase 30 — Respawn / Warp Sync** (`smRespawn`, `smWarp`)

Finance HUD (`smFinance`) is intentionally **out of scope** for this rewrite.

### Post–Phase 30 — PC polish checkpoint (2026-07)

Not numbered as formal phases; shipping together as a contributor handoff:

| Area | Notes |
|------|-------|
| `BattleCity.Server.Host` | WinForms Start/Stop, LAN invite, player list, admin toggles |
| Publish zip | `tools/Publish-Release.ps1` → `dist/BattleCity-win-x64.zip` |
| Modern HUD / title | 1080p overlay, `MenuTheme`, title sprites |
| Population | `BuildingPopulationSystem` — house slots 50+50; bullet-immune when populated |
| Cloak / flare | City research+factory → 10s recharge + HUD bar |
| Minimap | 3×3 building markers |
| Turret muzzle / audio pan | Coordinate + MonoGame `Play` argument fixes |
| Smoke | `tools/BattleCity.Smoke` |

### Remaining network parity (not yet scheduled as phases)

There is **no fixed total phase count** — phases are added incrementally as legacy multiplayer gaps are closed. Likely next targets:

| Candidate | Legacy packets | Notes |
|-----------|----------------|-------|
| City under attack | `smUnderAttack` | Local alert exists; network broadcast still open |
| Item life sync | `smItemLife` | Bomb fuse / item TTL for remotes |
| Promotion | `smPromotion` | Rank-up chat line on point thresholds |

Phases **0–30** are complete (31 numbered milestones including Phase 0).

## Build & Run

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet tool restore
dotnet restore src/BattleCity.sln
dotnet build src/BattleCity.sln
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
dotnet test src/BattleCity.sln
```

Press **Escape** in-game to return to the main menu. From the menu choose **Play Offline**, **Play Online** (local server on port 5643), or **Quit**. In-game: **arrows / WASD**, **Shift / LMB** fire, **Q** drop turret, **M** minimap, **Tab+arrows** camera pan.

```powershell
# Terminal 1 — authoritative server
dotnet run --project src/BattleCity.Server/BattleCity.Server.csproj

# Create a registered account (optional)
dotnet run --project src/BattleCity.Server/BattleCity.Server.csproj -- create-account myuser mypass "Buenos Aires"

# Terminal 2+ — clients
dotnet run --project src/BattleCity.Client/BattleCity.Client.csproj
```

### Phase 12 — Multiplayer (`BattleCity.Server`, `BattleCity.Shared/Network`, `BattleCity.Client/Network`)

| Piece | Role |
|-------|------|
| `LegacyPacketCodec` | Legacy TCP framing + checksum (port **5643**) |
| `GameServer` | Headless authoritative sim; guest login; `cmUpdate` / `smUpdate` |
| `GameClient` | Version → login → join handshake; sends player updates |
| `RemotePlayerSync` | Renders other players from server broadcasts |
| `InGameOnlineScene` | Online sandbox with local prediction + network sync |

> **Superseded:** Phases 15–30 added accounts, chat, combat sync, meeting/hiring, etc. Treat Phase 12 as the movement baseline only.

### Phase 13 — Network Gameplay Sync (authoritative server)

| Piece | Role |
|-------|------|
| `ClientItemDropPacket` / `ServerAddItemPacket` | Legacy `cmItemDrop` / `smAddItem` item placement |
| `ServerOrbedCityPacket` | Legacy `smOrbed` broadcast when orb hits CC |
| `ItemDropActions` | Shared drop validation for offline + server |
| `NetworkItemRef` | Dedupes authoritative item spawns by network id |
| `GameSimulation.SuppressLocalItemDrops` | Online clients defer drops to server |
| `GameSimulation.SuppressLocalOrbEffects` | Online clients defer orb aftermath to server |
| `GameServer.BroadcastPendingOrbEvents` | Server orb → all clients |

| `ClientShotPacket` / `ServerShotPacket` | Legacy `cmShoot` / `smShoot` weapon fire |
| `ClientItemPickupPacket` / `ServerRemoveItemPacket` / `ServerPickedUpPacket` | Legacy `cmItemUp` / `smRemItem` / `smPickedUp` |
| `ClientBuildPacket` / `ClientDemolishPacket` / `ServerBuildingPacket` | Legacy `cmBuild` / `cmDemolish` / `smNewBuilding` / `smRemBuilding` |
| `WeaponActions` / `ItemPickupActions` | Shared fire + pickup rules for offline, client, and server |
| `BuildingRef.NetworkId` | Network building dedupe for demolish sync |
| `GameSimulation.ReportLocalShot` | Online client sends `cmShoot` on local fire (legacy: shooter predicts locally) |

Online flow: client sends item drop → server validates inventory + placement → `smAddItem` to all clients → server sim runs orb/bombs authoritatively → `smOrbed` when CC is hit. Shoot: local fire + `cmShoot` → server validates → `smShoot` to other clients. Pickup: `cmItemUp` → `smRemItem` to all + `smPickedUp` to picker. Build/demolish: `cmBuild` / `cmDemolish` → `smNewBuilding` / `smRemBuilding` to all.

Remaining Phase 13 work: account database.

### Phase 14 — Join World Snapshot

| Piece | Role |
|-------|------|
| `JoinWorldSnapshot` | Collects authoritative items, buildings, and demolishes from server sim |
| `GameSimulation.CollectJoinSnapshot` | Builds `smAddItem` / `smNewBuilding` / `smRemBuilding` lists |
| `GameServer.SendJoinWorldSnapshot` | Sent to each player in `JoinGame` after `smStateGame` |
| `GameClient.PollAvailable` | Drains join burst (snapshot packets) during login handshake |
| Online client | No local `SpawnDemoItems` — ground items come from server snapshot |

Late joiners now receive the same items, building network ids, player-placed structures, and demolishes as players already in the match.

### Phase 15 — Account Database

| Piece | Role |
|-------|------|
| `AccountDatabase` | SQLite file (`accounts.db`) with PBKDF2-SHA256 password hashes |
| Guest login | Password `guest` skips the database (legacy-compatible quick play) |
| `create-account` CLI | `dotnet run --project BattleCity.Server -- create-account user pass [town]` |
| Create Account UI | Login screen → Tab → fill form → server `cmNewAccount` |
| Legacy error codes | `B` wrong password, `C` unknown account, `D` name taken, `E` already online, `A` created |

### Phase 16 — Death / Respawn Sync

| Piece | Role |
|-------|------|
| `ClientDeathPacket` / `ServerDeathPacket` | Legacy `cmDeath` / `smDeath` (3-byte broadcast) |
| Local client | Reports death via `cmDeath`; respawns locally after 10s |
| Server | Applies death to network player entity; increments account deaths |
| Remote clients | Apply `smDeath` to network tank visuals (no local HP death for remotes) |

### Phase 17 — In-Game Chat

| Piece | Role |
|-------|------|
| `ClientMessageId.Walkie` / `ServerMessageId.ChatMessage` | Legacy team/radar chat send + broadcast |
| `InGameChatLog` | 8-line rolling chat history with legacy colors |
| `InGameChatInput` | Enter to open, Esc cancel, Enter send |
| `ChatOverlayRenderer` | Bottom-left chat bar over world viewport |
| `GameServer.HandleChatMessage` | Relays chat to all in-game players except sender |

### Phase 18 — HP Sync

| Piece | Role |
|-------|------|
| `ServerHpPacket` | Legacy `smHp` (player id + current HP) |
| Server sim | Applies bullet damage to network tanks; broadcasts HP changes |
| Online client | Skips local bullet damage on remotes; applies `smHp` updates |
| `ItemDropPlacement` | Defensive drops (turret/wall) placed on adjacent tile, not under tank |

### Phase 19 — Defensive Item Range

| Piece | Role |
|-------|------|
| `DefensiveItemRangeValidator` | Legacy `CBuildingList::inRange(true)` — within 29 tiles of CC or 11 tiles of a building |
| `ItemDropPlacement` / `ItemDropActions` | Rejects wall/turret/mine/etc. drops outside city range |
| `ItemDropSystem` / server drops | Offline + authoritative online validation share the same rules |

### Phase 20 — Chat Commands & Death Announcements

| Piece | Role |
|-------|------|
| `ChatCommandParser` | Parses `/g` (global) and `/pm recipient message` (whisper) |
| `GameServer` | Routes `cmGlobal` to all players; `cmWhisper` to recipient; admin gate on global |
| `DeathChatMessages` | Legacy random death lines + friendly-fire / killer city suffix |
| `InGameChatService` | Global, whisper, system, and death chat colors |

### Phase 21 — Killer City & Drop Feedback

| Piece | Role |
|-------|------|
| `TankLifeState.KillerCityId` | Tracks shooter/mine city on the killing blow |
| `EntityCityLookup` | Resolves city id from bullet owner or mine |
| `cmDeath` / `smDeath` | Client reports killer city; death chat shows `(City)` or `(Friendly Fire!)` |
| `ItemDropFeedback` | Chat system message when drop is out of range or blocked |

### Phase 22 — Mayor & Radar Chat

| Piece | Role |
|-------|------|
| `CityMayorRegistry` | First in-game player per city becomes mayor; transfers on disconnect |
| `smMayorUpdate` | Broadcast mayor flag changes to all clients |
| `MayorStatus` | ECS flag + build-menu gate (mayor or admin only) |
| `TankSpriteSelector` | Legacy four tank rows: friendly/enemy × commando/mayor |
| `RadarChatRouter` | `cmWalkie` → radar + team; `cmChatMessage` → radar only (1800px server range) |

### Phase 23 — Meeting Room & Hiring

| Piece | Role |
|-------|------|
| `MeetingScene` | Legacy meeting room: city list, lobby chat, apply to city |
| `InterviewScene` | Applicant waits while mayor accepts/declines in-game |
| `CityRegistry` | Builds `smAddRemCity` list (mayor-needed + commando slots) |
| `cmJobApp` / `cmHireAccept` | Become mayor instantly or join via mayor interview |

### Phase 24 — Interview Comms & Personnel

| Piece | Role |
|-------|------|
| `cmComms` / `smComms` | Applicant ↔ mayor chat during interview |
| `cmIsHiring` | Mayor toggles deny-applicants flag on city |
| `cmFired` / `smFired` | Mayor removes a commando from the city |
| `InterviewScene` | Chat UI while waiting for hire decision |

### Phase 25 — Build Tree & Population Sync

| Piece | Role |
|-------|------|
| `ServerCanBuildPacket` / `smCanBuild` | Mayor build menu stays in sync (research unlocks, demolish) |
| `ServerUpdatePopPacket` / `smUpdatePop` | Authoritative building population for overlays |
| `CityBuildPopSync` | Server diffs build tree + population each tick |

### Phase 26 — Points Update on Death

| Piece | Role |
|-------|------|
| `ServerPointsUpdatePacket` / `smPointsUpdate` | Session points/deaths synced on join and after deaths |
| `DeathPointTransfers` | Legacy rule: victim loses 2 pts when above 100; killer city allies gain 2 |
| `PlayerRankCatalog` | Legacy rank titles (`Private` through `King`) for chat names |
| `RemotePlayerSync.GetChatDisplayName` | Chat shows rank prefix after points sync |
| Server combat deaths | `BroadcastPendingDeathEvents` records deaths and point transfers |

Online medkit (`cmMedKit` / `smMedKit` + `smHp`) is server-authoritative: **H** sends `cmMedKit`, server heals and broadcasts HP, client consumes inventory on `smMedKit`.

### Phase 27 — Online Cloak Sync

| Piece | Role |
|-------|------|
| `ServerCloakPacket` / `smCloak` | Broadcasts which player activated a cloak |
| `TryUseCloakForNetworkPlayer` | Server validates inventory, activates cloak, consumes item |
| `ApplyNetworkCloak` | All clients apply cloak state + play directional audio |
| `SendNetworkCloak` | **C** sends `cmCloak`; local use blocked while `SuppressLocalItemDrops` is on |

### Phase 28 — Factory Item Count Sync

| Piece | Role |
|-------|------|
| `ServerItemCountPacket` / `smItemCount` | Factory `ItemsLeft` overlay stays in sync |
| `FactoryItemCountSync` | Server diffs factory stock each tick |
| `ApplyNetworkItemCount` | Client updates building overlay numbers |

### Phase 29 — Explosion Sync

| Piece | Role |
|-------|------|
| `ServerExplosionPacket` / `smExplosion` | Legacy bomb blast position (grid anchor + 1) |
| `BombSimulationHooks` | Server reports detonations; online clients suppress local fuse |
| `BroadcastPendingExplosionEvents` | Server broadcasts blast + paired `smRemItem` for the bomb |
| `ApplyNetworkExplosion` | Client plays large explosion visual/audio only (damage stays server-side) |
| `ReportNetworkPlayerKilledByBomb` | Server queues death/HP when bombs kill network players |

### Phase 30 — Respawn / Warp Sync

| Piece | Role |
|-------|------|
| `ServerRespawnPacket` / `smRespawn` | Legacy 1-byte player id broadcast to all except respawner |
| `ServerStateGamePacket` / `smWarp` | Legacy warp payload sent to respawning player (CC position + city) |
| `ReportRespawnEventsToNetwork` | Server respawns network tanks at command center after 10s |
| `SuppressLocalPlayerRespawn` | Online client waits for authoritative `smWarp` |
| `ApplyNetworkWarp` / `ApplyNetworkRespawn` | Local warp + remote hide-until-update (legacy parity) |
| `ApplyPlayerDeath` | Applies `smDeath` to local or remote player correctly |

### Phase 10 — UI Scenes (`BattleCity.Client/Scenes`)

| Piece | Role |
|-------|------|
| `SceneManager` | Boot → Main Menu → Login / InGame transitions |
| `BootScene` | Brief load splash |
| `MainMenuScene` | Play offline, login stub, quit |
| `LoginScene` | Connect to local/legacy TCP server (guest login) |
| `InGameScene` | Full sandbox (replaces `InGameDemoScene`) |
| `ScreenUiRenderer` | Menu text via `Fonts/MenuFont` |
| `UiRenderer` | Legacy interface rail + HUD inventory |

### Phase 11 — Audio (`BattleCity.Client/Audio`, `BattleCity.Core/Audio`)

| Piece | Role |
|-------|------|
| `SoundCatalog` | Maps `SoundId` → legacy WAV → `Content/Audio` paths |
| `SimulationAudioBuffer` | Core systems queue `GameSoundEvent` per tick |
| `AudioService` | MonoGame `SoundEffect` playback, engine loop, stereo pan |
| `GameplayAudioController` | Plays simulation events with listener-relative pan |

Run `./tools/ContentBuild.ps1` to copy `legacy/data/wav/*.wav` into the content pipeline.

### Phase 2 — regenerate content

```powershell
./tools/ContentBuild.ps1
```

This converts legacy BMPs (including minimap palette), imports `map.dat` to JSON, and rebuilds MonoGame content.

### Phase 4 — Rendering (`BattleCity.Client/Rendering`)

| Piece | Role |
|-------|------|
| `RenderPipeline` | World pass (terrain + Y-sorted entities) + screen pass (minimap, UI) |
| `Camera2D` | Zoom, clamp, legacy 600 px game viewport |
| `EntityDrawSorter` | Painter's algorithm by sprite bottom edge |
| `MiniMapRenderer` | Toggle with **M**; legacy terrain palette |
| `UiRenderer` | 200 px right panel stub |

### Phase 8 — Gameplay Entities (`BattleCity.Core/Gameplay`, weapon/bullet systems)

| Piece | Role |
|-------|------|
| `GameplayEntityFactory` | Spawns bullets and placed world items |
| `WeaponSystem` | Laser / rocket / flare from `InputCommand` with legacy cooldowns |
| `BulletSystem` | Legacy movement, animation, lifetime |
| `BulletCollisionSystem` | Bullets vs terrain, buildings, items, tanks |
| `ItemDropSystem` | Drop turret/bomb, use medkit from inventory |
| `Health` / `PlayerInventory` | Player and patrol tank stats |

### Phase 9 — Tank / Turret AI (`BattleCity.Core/Ai`)

| Piece | Role |
|-------|------|
| `TurretTargeting` | Nearest-enemy scan, legacy aim angle + direction math |
| `TurretAiSystem` | Auto-aim and fire (250 ms turn cooldown, 2 s startup) |
| `BotAiSystem` | Enemy bot chases, turns, and fires at other cities |
| `CityAffiliation` | Friendly/enemy filtering by city id |
| `TurretSprites` + client draw | Base/head sheets from legacy `imgTurret*.bmp` |

Run `./tools/ContentBuild.ps1` to convert `imgTurretBase.bmp` / `imgTurretHead.bmp`.

### Phase 7 — Level Loading (`BattleCity.Core/Levels`)

| Piece | Role |
|-------|------|
| `CityLayoutParser` | Reads legacy `.city` files (`menuIndex gridX gridY`) |
| `CityLayoutPaths` | Resolves `legacy/data/cities/{name}/demo.city` |
| `LevelLoader` | Spawns building entities with colliders and sprites |
| `BuildingSprites` | Maps type codes to `imgBuildings.bmp` sheet rows |

Run `./tools/ContentBuild.ps1` to convert `imgBuildings.bmp` → `Sprites/Buildings.png`.

### Phase 6 — Collision (`BattleCity.Core/Collision`, `CollisionSystem`)

| Piece | Role |
|-------|------|
| `AxisAlignedBox` | Legacy AABB overlap (`CCollision::RectCollision`) |
| `TerrainCollision` | Four-corner terrain sampling (lava, rock, city center) |
| `CollisionQueries` | Player vs terrain, entities, map edges |
| `CollisionSystem` | Axis-separated resolution after movement; patrol bounce |

### Phase 5 — Input (`BattleCity.Client/Input`, `BattleCity.Core/Ecs/Systems/InputSystem`)

| Piece | Role |
|-------|------|
| `InputManager` | Polls keyboard/mouse into gameplay + UI state |
| `GameplayInputMap` | Legacy bindings (arrows, Shift, Ctrl, item keys) |
| `InputCommand` | Per-frame command component on player entity |
| `InputSystem` | Turns tank, applies legacy velocity, mouse aim, sprite frame |

### Phase 1 layout (`BattleCity.Shared`)

| Folder | Contents |
|--------|----------|
| `Constants/` | `GameConstants`, `EconomyConstants`, `NetworkConstants`, UI colors, newbie tips |
| `Data/` | `ItemType`, `TerrainTileType`, `ClientGameState`, `SoundId`, etc. |
| `Catalogs/` | `CityCatalog`, `ItemCatalog`, `BuildingCatalog` (from legacy `Structs.cpp` + server tables) |

| Project | Purpose |
|---------|---------|
| `src/BattleCity.Shared` | Constants, enums, catalogs (Phase 1) |
| `src/BattleCity.Core` | ECS simulation (no MonoGame dependency) |
| `src/BattleCity.Client` | MonoGame DesktopGL client |
| `src/BattleCity.Server` | Headless TCP game server (Phase 12) |
| `tests/BattleCity.Core.Tests` | Unit tests for Core |
| `legacy/` | Original C++ client, server, data, external deps |
