# Remake vs original — please double-check

This is a checklist of places the C# remake does **not** match `legacy/`. Each row is something to confirm or reject. “Original” means the C++ client/server in `legacy/`.

**Locked choices** (do not revert these while “fixing parity”) are written up with how the code works in [LEGACY-DELTAS.md](LEGACY-DELTAS.md). This file is the playtest record that led to those choices.

The four playtest fixes from this pass are at the top. The rest is the deeper audit.

## 1. What just changed

| What you asked | What the remake does now | Original |
| --- | --- | --- |
| Only the Flare Gun, the Cloak, and the Bazooka go straight into inventory | Factory production puts **Flare**, **Cloak**, and **Bomb** into a living city member’s inventory (mayor first). Walls, turrets, medkits, mines, orbs, sleepers, plasma, DFG, and Cougar missiles still appear on the factory bay. | Every factory product spawned as an inactive item on the bay every 7 seconds, up to that item’s max. You picked them up. Nothing was auto-deposited. |
| Only Cloak and Flare Gun clear and refresh | Cloak and flare stay at **one**. Using one starts a **10 second** recharge while that factory still stands. Destroying the Cloak or Flare factory **removes it from inventory**. Rebuilding the factory puts one back. The Bazooka (**Bomb**) **stacks up to 20**, is spent one at a time, and is **not** wiped when its factory is destroyed and **not** dumped back to the bay when you die. | Destroying a factory deleted **every** item that factory had made, including ones a player was holding (`CItemList::deleteItemsByFactory`). Death deleted every item that player was holding (`deleteItemsByPlayer`). There was no 10 second recharge. |

**Naming, so “Bazooka” is the right item:**

- The original item list calls it **Bomb** (`Structs.cpp` `ItemList`).
- The original build menu calls the building **Bazooka Research / Bazooka Factory** (types 401 / 101). That building produces Bombs.
- The remake build menu says **Bomb Research / Bomb Factory**. That stays the bomb, and it keeps the current bomb rules.
- The building that makes the Cougar missile is **Missile Research / Missile Factory** (type 100). The tank’s normal shot is still the laser.
- The original build menu called the cloak building **Time Bomb Research / Time Bomb Factory**. The remake menu says **Cloak**.

Bomb stays Bomb. The old “Bazooka Factory” label is not coming back.

## 2. Kill points

Original (`CProcess::ProcessDeath`): if the victim has **more than 100** points, the victim loses **2**. If the killer is a **different city**, every in-game player in the killer’s city gains **2**. Same-city kills still take the 2 from the victim, and nobody is paid.

Remake:

- A human victim with **more than 100** points loses 2, and the other city gains 2 each, **except same-city kills do not take the 2**. Original still took the 2 on friendly fire.
- Killing an **AI tank** pays **+2** to each human in the killer’s city. Original never did this, because AI tanks are not accounts and have no points.

Confirm the friendly-fire exception and the AI-kill payout.

## 3. Sound distance

Original used FMOD 3D, so volume and position came from the sound engine.

Remake: full volume within **1200** pixels (on screen or just off it), then a linear fade to silence at the **client radar** range of **2400** pixels. Sounds past that are not played. Stereo pan is still applied.

The **server** radar used for local chat is still the original **1800**. Only the drawn radar and this volume falloff use 2400.

## 4. Death and respawn

Original told everyone you died, deleted the items you were holding, and later warped you back to your city.

Remake, online: your tank stays dead where it fell until the server warp. You do not get a local revive on the death spot first. Respawn position is the city pad from that warp.

An orbed city does **not** send “fired” or “left the battlefield.” `smOrbed` is what returns those players to the meeting room. That matches `LeaveGame(false, false)` in the original.

Walls, turrets, medkits, mines, orbs, and the other placeables you were carrying still go back to the factory bay on death. The Bazooka (Bomb) stays in inventory. Cloak and flare stay, unless their factory is gone.

## 5. Buildings you shoot

Original: the server removed a destroyed turret, so the tile was free.

Remake, online: your client does not delete the building or placed item itself. The bullet stops, and the server broadcasts the removal. That was the “I destroyed the turret but still could not build there” bug.

## 6. Enemy AI

Not in the original as a hosted “AI City.”

Remake additions you already asked for, listed so you can reject any of them:

- Host can turn on AI cities. Bots drive, shoot, and die.
- A dead bot is hidden and comes back at its city. It is not left sitting on the wreck.
- If the next tile is rock or lava, the bot picks a nearby facing for 0.8 seconds instead of driving straight into it.
- Bots do not use the account rankings.

AI cities do **not** yet stamp a building template around their command center. That is still not built.

## 7. Meeting room and rankings

Original meeting room had a Top 20 for **lifetime points**, **monthly points**, and **points per death** (only accounts with more than 100 deaths). There was no named season.

Remake:

- Meeting chat shows who is online, how many are in a city, and which cities are taken, including AI cities.
- **L** or the Ranks button opens a page with **overall**, **this month**, a **season** board, and **per death**. The season name and the reset are set on the server host (“Start new season”).
- **Points per death** score is `points × 10000 / deaths`. Only accounts with more than 100 deaths are listed. Each board shows **10** names. The original showed **20** and had no season.

Monthly points roll over on the host’s local calendar month, not UTC. A new season zeros season points and does not touch lifetime or monthly points.

## 8. Other intentional differences

These were already chosen before this pass. Say if any of them should go back to the original.

| Topic | Original | Remake |
| --- | --- | --- |
| Money HUD | `smFinance` income, upkeep, and cash on the rail | Not implemented |
| Screen | Fixed low-res rail UI | 1920×1080 HUD. World tiles are still 48 pixels |
| Compass | One 8-way arrow toward the city index formula | Arrow toward your command center’s drive pad, plus a second arrow toward the nearest city you can orb |
| Home / respawn point | Join stored `CityX` / `CityY` from the city-index formula | Command center drive pad |
| New cities | Each city starts with a command center only | Same online. Offline still uses the demo city layout |
| Cougar missile | Produced on the missile factory bay and picked up | Spawn still grants **1** if the Missile factory exists. Extra missiles stay on the bay (they are not auto-inventory) |
| Cloak / flare without the 10 second bar | You carried a stack (max 4) and each use consumed one | While the factory stands, use does not consume the stack. The bar refills after 10 seconds. Inventory count stays at 1 |
| Chat type size | Bitmap font at the original UI scale | In-game chat and meeting-room chat draw at **85%** of the UI font. The rest of the HUD is unchanged |
| Minimap city names | No names on the minimap | Names drawn larger, in white (gold for your city), with a black outline |

## 9. Server behavior added after the playtest notes

These match the original and should stay.

| What | Remake |
| --- | --- |
| Last mayor leaves, city is not orbable | City is destroyed immediately. Houses and factories go. The command center stays. The build tree resets. |
| Last mayor leaves, city is orbable | The city stays for **2 minutes**. A new mayor cancels that. If nobody comes back, it is destroyed the same way. |
| Hand the city over now | `/mayor Name` (`cmSetMayor`). You stay in the city. `/heir` is only who inherits if you leave. |
| Orb vs abandon | An **orb** keeps houses. An **abandoned** city does not. That split is intentional. See [LEGACY-DELTAS.md](LEGACY-DELTAS.md). |

## 10. Still not done

The working list is [LEGACY-DELTAS.md](LEGACY-DELTAS.md) → “Still open for parity.” Short version:

- AI city building (a small template around the command center)
- Finance HUD (`smFinance`) — out of scope until explicitly reopened
- Auto-build from a city file (`cmAutoBuild`)
- Account self-edit and email recovery
- Custom tank select (`cmChangeTank`)
- Click-player and right-click city info panels
- Rank boards show 10 names; the original showed 20
- A full stress / soak pass of the server

If a row above is not what you wanted, say which number and what it should do instead.
