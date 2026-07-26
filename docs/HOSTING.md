# Hosting Battle City (PC)

Run a server on a PC that stays on (or mostly on). Friends connect with a host address — same idea as the original game.

For developers and contributors, also see [PROJECT-STATUS.md](PROJECT-STATUS.md) and [CONTRIBUTING.md](CONTRIBUTING.md).

## What’s in the release zip

| Folder | Run this |
|--------|----------|
| `Client/` | `BattleCity.Client.exe` — the game |
| `Server/` | `BattleCity.Server.Host.exe` — Start/Stop, invite copy, admin toggles |

No Visual Studio required. Windows x64.

Default port: **5643** (TCP).

---

## Local (same PC)

1. Start `BattleCity.Server.Host.exe` → **Start**
2. Start `BattleCity.Client.exe` → **Play Online**
3. Server field: `127.0.0.1` (or `127.0.0.1:5643`)
4. Leave username blank for guest, or create an account (F2)

---

## LAN (same Wi‑Fi / network)

1. Host: **Start** → note the big **Share this** address (e.g. `192.168.1.237:5643`)
2. Host: **Copy Invite** and send that text to friends
3. Windows may prompt for Firewall — allow access on private networks (TCP **5643**)
4. Friends: paste the address into Client login → **Server** → connect

If they can’t connect: confirm you’re on the same network, and that Windows Firewall allows inbound TCP 5643 for the host app.

---

## Internet (friends elsewhere) — easiest path

Copy Invite’s `192.168.x.x` address **will not work** over the public internet.

**Recommended: [Tailscale](https://tailscale.com/) (free for personal use)**

1. Host and friends install Tailscale and join the same tailnet  
2. Host starts Battle City Server as usual  
3. Host shares their **Tailscale IP** + port, e.g. `100.x.y.z:5643`  
4. Friends paste that into the Client **Server** field  

No router port forwarding.

### Alternative: port forward

1. Server already listens on `0.0.0.0`  
2. Router: forward TCP **5643** → host PC  
3. Share your public IP:`5643` (not the LAN one)  
4. Some ISPs use CGNAT — if so, Tailscale/VPS is required  

---

## Meeting room → city

1. Login → Meeting Room  
2. First player to apply to an empty city becomes **mayor**  
3. Later players apply → mayor hires them as soldiers (max 4 per city)  

---

## Building a release yourself

From the repo (needs .NET 8 SDK):

```powershell
./tools/Publish-Release.ps1
```

Output: `dist/BattleCity-win-x64/` and `dist/BattleCity-win-x64.zip`

---

## Smoke test (developers)

With the repo checked out:

```powershell
dotnet run --project tools/BattleCity.Smoke/BattleCity.Smoke.csproj
```

Starts an in-process server, joins **1 mayor + 3 soldiers** to Buenos Aires, sends movement updates, then exits with a pass/fail summary.
