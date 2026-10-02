# Legacy vs Rewrite — Locked Deltas

**Last updated:** 2026-10-01

The rewrite aims for **gameplay and protocol parity** with `legacy/`, not a pixel copy of the C++ UI. This file is the list of choices that must **not** be reverted just because the original did something else.

Read this before changing combat, inventory, cities, spawn, or rankings. Playtest notes that led here also live in [REMAKE-REVIEW.md](REMAKE-REVIEW.md). The cities / spawn / compass audit history is in [LEGACY-AUDIT-PLAN.md](LEGACY-AUDIT-PLAN.md).

## How to use this file

| If you are about to… | Do this |
|----------------------|---------|
| “Fix” something back to the C++ formula | Check **Do not reverse** below. If it is listed, leave it. |
| Port a missing server packet | Check **Still open for parity**. Those are fair game. |
| Touch cities, orbs, or leave-game | Read **How cities work now**. Orb wipe and abandoned-city wipe are different on purpose. |

---

## Do not reverse

### Inventory and factories

| Choice | How it works now | Original (do not restore) |
|--------|------------------|---------------------------|
| Auto-inventory | Factory production puts **Flare**, **Cloak**, and **Bomb** into a living city member (mayor first). | Every product sat inactive on the bay. Nothing was deposited. |
| Bay items | Walls, turrets, medkits, mines, orbs, sleepers, plasma, DFG, and Cougar missiles still appear on the factory bay. | Same bay spawn for every product. |
| Cloak and flare | Count stays **1**. Use starts a **10 second** recharge while that factory stands. Destroying the factory **removes** it from inventory. Rebuilding the factory puts one back. Use does **not** consume the stack. | Stack up to 4. Each use consumed one. No recharge bar. Destroying the factory deleted every item it had made, including ones in hand. |
| Bomb (“Bazooka”) | Stacks to **20**, spent one per shot. **Not** wiped when its factory is destroyed. **Not** dumped back to the bay on death. | Death deleted every held item (`deleteItemsByPlayer`). Factory destruction deleted that factory’s items. |
| Other placeables on death | Walls, turrets, medkits, mines, orbs, and the other bay items you were carrying go back to the factory bay. | Death deleted them. |
| Menu names | Build menu says **Cloak** and **Bomb**. The Cougar building is **Missile Research / Missile Factory** (type 100). The tank’s normal shot is still the laser. | Cloak building was labeled **Time Bomb**. Bomb building was labeled **Bazooka**. Do not put those labels back. |

Code: `FactoryProductionSystem`, `BuildingCommandService.DeleteItemsByFactory`, `PlayerInventory`.

### Combat, death, and authority

| Choice | How it works now | Original (do not restore) |
|--------|------------------|---------------------------|
| Online building / item removal | The client does **not** delete a destroyed building or placed item. The server broadcasts `smRemBuilding` / `smRemItem`. | The client could report `cmHitObject` and the server deleted that item id. That trusted the client and left tiles blocked. **Do not re-add `cmHitObject`.** |
| Death pose | Online, the tank stays dead where it fell until the server `smWarp`. No local revive on the death spot first. | Client revived locally, then warped. |
| Respawn point | Command-center **drive pad** (`TryGetHomeReferenceWorldPosition`). | Join stored `CityX` / `CityY` from the city-index formula and never updated them. |
| Kill points | A human victim with **more than 100** points loses 2, and the killer’s city gains 2 each, **except same-city kills do not take the 2**. Killing an **AI tank** pays **+2** to each human in the killer’s city. | Same-city kills still took 2 from the victim and paid nobody. AI tanks were not accounts, so they paid nothing. |
| Turret muzzle | Same pivot as tanks: top-left + `(6,10)` + direction (`WeaponGeometry`). | `grid*48 - 24 + (6,10) + dir`. |
| Sound falloff | Full volume within **1200** px, then a linear fade to silence at the **client** radar range of **2400** px. Stereo pan is the MonoGame pan argument, not pitch. | FMOD 3D. |
| Chat radar | Server local chat still uses the original **1800** px. Only the drawn radar and volume falloff use 2400. | One 3D distance. |

### Cities, orbs, and leaving

| Choice | How it works now |
|--------|------------------|
| Online world | `LoadMultiplayerWorld()` — every command center from `map.dat`, **no** demo houses or factories. Players build from House / Missile research / Turret research. |
| Offline world | Still loads that city’s `.city` demo layout. Do not make offline CC-only, and do not put demo layouts back on the online server. |
| Orb wipe | `CityOrbedService.ApplyOrbed` destroys that city’s buildings **except houses and the command center**, deletes its placed items, and resets the build tree. Houses stay so the city overlay still has something to draw. Legacy wiped houses too. **Do not “fix” orb to delete houses unless that choice is explicitly changed.** |
| Abandoned city | Different from an orb. See **How abandoned cities work** below. That path **does** remove houses. |
| Orbed players | `LeaveGame(showLeftMessage: false, transferMayor: false)`. No “left the battlefield” line and no `smFired`. The `smOrbed` packet sends them to the meeting room. Do not send Fired on an orb again. |
| Compass | Home arrow aims at the drive pad and uses the legacy **8-sector** `imgArrows` / `imgArrowsRed` frames. A second arrow aims at the **nearest** orbable city’s command center. Keep both. The original info button picked the highest orb *value*, not the nearest city; that button is still missing (see open list). |
| Meeting list | Hiring cities plus a spiral of empty cities, budget `⌊lobby players / 5⌋ + 6`, seeded from the Buenos Aires neighborhood `{18,19,20,26,27,28,34,35,36}`. |

### UI and hosting (product, not a port)

| Choice | How it works now |
|--------|------------------|
| Money HUD | `smFinance` is **out of scope**. Do not add income / upkeep / cash unless scope is explicitly expanded. |
| Screen | Logical UI **1920×1080**. World tiles stay **48** px. |
| Chat type | In-game chat and meeting-room chat draw at **85%** of the UI font. |
| Minimap names | City names are drawn larger, white, with a black outline. Your city is gold. The original minimap had no names. |
| Missile on spawn | If the Missile factory exists, spawn still grants **1** missile. Extra missiles stay on the bay. They are not auto-inventory. |
| Hosting | Friends use **Server.Host** (Start, Copy Invite). **Play Online (Local Server)** starts an embedded `GameServer` on `127.0.0.1:5643`, or reuses a server already listening. |
| Admin | SQLite `is_admin`. Username `admin` stays admin. Host UI toggles admin, bans, news, starting city, and AI City. |
| AI cities | Host toggle. Not in the original. Bots drive, shoot, die, and respawn at their city. They do not use account rankings. They do **not** yet place a building template. Keep the toggle; the missing template is in the open list. |
| Season board | Extra rankings tab. Host “Start new season” zeros **season** points only. Lifetime and monthly points stay. Keep it alongside the original boards. |

---

## How abandoned cities work

Legacy: `CPlayer::LeaveGame` and `CCity::cycle` / `CCity::destroy`.

When the mayor leaves and `MayorSuccessorResolver` finds no teammate still in that city:

1. If the city is **not** orbable (`CityBuildState.IsOrbable` is false), it is destroyed immediately.
2. If it **is** orbable, `CityDestructSchedule` waits **120 seconds** (`EconomyConstants.TimerCityDestruct`). A new mayor cancels the timer. If the timer finishes and the city is still empty, it is destroyed.
3. Destroy runs `GameSimulation.DestroyAbandonedCity` and broadcasts `smDestroyCity` (one byte, city id).

Destroy removes every building of that city **except the command center**, deletes that city’s placed items, and resets the build tree to the starting permissions (House, Missile research, Turret research). Orb count, bomb-factory, and orb-factory flags go back to zero.

`/heir Name` only sets who should become mayor if you leave. `/mayor Name` hands the city over **now** (`cmSetMayor`). The old mayor stays in the city. The target must already be in that city.

Code: `GameServer.BeginCityAbandon`, `CityDestructSchedule`, `GameSimulation.DestroyAbandonedCity`.

---

## How rankings work

Meeting room → **L** or the Ranks button. Four boards, **top 10** each (`RankBoardPacket.MaxRows`):

| Tab | Key | What it sorts |
|-----|-----|----------------|
| Overall | 1 | Lifetime `points` |
| This month | 2 | `monthly_points` (host local calendar month, not UTC) |
| Season | 3 | `season_points` (host can name and reset the season) |
| Per death | 4 | `(points * 10000) / deaths`, and only accounts with **more than 100** deaths and points above 0 |

The original boards were **top 20** and had no season. Top 10 is the current packet size. Raising it to 20 is a parity change, not a bugfix — see the open list.

---

## Still open for parity

These are real original functions the remake does not have yet. They are **not** in the “do not reverse” list.

| Gap | Original | Notes |
|-----|----------|-------|
| Finance HUD | `smFinance` | Out of scope until someone explicitly expands it. |
| AI city buildings | Not in the original as “AI City” | Requested follow-up: a small base (house, hospital, a factory, walls) around the AI command center. Not built. |
| Auto-build | `cmAutoBuild` / `smAutoBuild` | Mayor (or admin) could load a `.city` file when the city was not orbable. No handler. |
| Account self-edit | `cmEditAccount`, `cmAccountUpdate` | Player edits password, email, name, town. Admin `/editaccount` exists. Self-service does not. |
| Password recovery | `cmRecover` | Email lookup. Returns error `L` or `M`. Not ported. |
| Custom tank | `cmChangeTank` | Account had Tank…Tank9 and a display tank. No tank-select UI. |
| Click a player | `cmClickPlayer` / `smClickPlayer` | Orbs, assists, deaths of the clicked player. No panel. |
| Right-click a city | `cmRightClickCity` / `smRightClickCity` | Building count, orbable, orbs, orb points, uptime. No panel. |
| Info button | `cmRequestInfo` / `smInfoButton` | Picked the orbable city with the highest orb value, then the closer one. Compass uses **nearest** orbable city instead. Do not copy the original distance line: it uses XOR (`^`) instead of squaring. |
| Map sectors | `cmMiniMap`, `cmRequestSector`, `smSectorSent` | Original streamed map sectors. The remake loads the full map. Leave this unless a legacy client must connect. |
| Cheat-constant kick | `cmCheatCheck` | Original compared building cost, damage, timers, and speed, then kicked. The remake client does not send it. |
| Client crash log | `cmCrash` | Original appended the text to the server log. Not handled. |
| Rank list length | Top **20** | Remake packets carry **10** rows. |
| Orb house wipe | `deleteBuildingsByCity` removed houses | Orb currently **keeps** houses. Abandoned-city destroy does not. Change the orb path only if that product choice is reversed. |

### Do not port these as written

| Packet | Why |
|--------|-----|
| `cmHitObject` | Client tells the server to delete an item by id. Online removal is server-authoritative on purpose. |
| `cmBan` as the original wrote it | Inserts a ban for the **sender**, using the packet text as the reason. That is not the admin ban. Admin ban is `/ban` (`cmAdmin`). |
| Info-button distance | `(dx ^ 2) + (dy ^ 2)` in `ProcessRequestInfo` is bitwise XOR, not distance. |

---

## Already matched (do not re-open)

- TCP **5643**, legacy framing and checksum (`LegacyPacketCodec`)
- Meeting, hire, fire, comms, `/heir`, build / demolish, shoot, pickup, drop, cloak, medkit, death, respawn, warp
- `smItemLife`, `smPromotion`, `smUnderAttack`, explosions, factory counts, population
- Admin `/kick` `/ban` `/city` `/warp` `/summon` `/spawn` `/shutdown` `/bans` `/unban` `/news` `/setnews` `/startcity` `/account` `/editaccount`
- CC-only multiplayer world, meeting-room spiral, drive-pad spawn and home arrow
- GridAnchor = footprint southeast corner (`BuildingCollisionOffset` = 2)
- House population: two slots of 50; house pop is the sum (max 100). Populated buildings are bullet-immune. Bombs still destroy them.
- Command centers come from map CityCenter tiles, scanned city id 63 → 0

When a rule is shared, put it in Core (`WeaponActions`, `ItemDropActions`, `BuildingPopulationSystem`, `CityOrbedService`) instead of copying it into the client and the server.
