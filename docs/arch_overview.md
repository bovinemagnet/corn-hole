Perfect — your constraints are super clear now, and they point to a really clean build path:

* **Mostly same Wi-Fi now**
* **Join-code only**
* **Shared world state** (if someone eats an object, it’s gone for everyone)
* **Battle royale rule**: big hole can eat smaller hole and inherit their “mass/loot”
* **Future**: dedicated server on a Raspberry Pi at home, and later remote play + maybe consoles/desktop

## The stack I’d use

### Client + game code

**Unity (C#)**

### Real-time networking

**Photon Fusion**

Fusion gives you a good migration story:

* start with **Host Mode** (one player is the server + a player)
* later move to **Server Mode** (a dedicated headless server with no local player)

Photon’s own docs explicitly recommend this “start Host, move to Server” progression. ([doc.photonengine.com][1])

---

# How I’d architect your exact game

## 1) Start now: LAN-friendly Host Mode + join code

For “kids on the same network”, **Host Mode** is ideal:

* One device hosts the match (fast, simple, no server machine needed)
* Everyone else joins
* No ops, no boxes, no port forwarding

Fusion Host Mode is a first-class topology and is easy for early development. ([doc.photonengine.com][2])

### Join-code only (no accounts, no friends lists)

Implement the “party code” pattern:

* Host presses **Create Match**
* Game generates a short code like `FROG-92`
* Joiners enter the code

Under the hood you map that code to the room/session name. Keep it dead simple:

* no login
* optional nickname

**Bonus for LAN:** you can optionally show a “Found local match: FROG-92” via UDP broadcast, but still require the code entry to actually join (so it remains “join-code only”).

---

## 2) Shared world state without syncing thousands of objects

This is the key to making **8–20 players** work smoothly.

### Don’t network physics props.

Instead, do **deterministic spawning + authoritative “eat events”**:

1. At match start, the host/server sends:

   * a **map seed**
   * a **spawn table** (or a deterministic spawn algorithm version)
2. Every client spawns the same set of objects locally.
3. When someone eats an object, the host/server broadcasts:

   * `objectId`
   * `eaterPlayerId`
   * optional `serverTick`

All clients then:

* despawn that `objectId`
* play the local animation/VFX

**Why this is so effective**

* Bandwidth stays tiny
* Server CPU stays low (important for future dedicated servers)
* Late-joiners can be supported by sending a compact “already eaten IDs” list (or a bloom / chunk bitset if you want to be fancy later)

### On the server: don’t simulate prop physics at all

For Hole.io style, props don’t need real rigidbodies on the server.
Your “truth” is basically:

* player transforms + radii
* which objects still exist

Consumption checks can be **distance-based**:

* if object center is within inner radius of hole AND holeRadius ≥ objectRequiredRadius → eat

No MeshColliders, no fancy physics.

---

## 3) Eating a smaller hole and inheriting everything they ate

This is just an extension of the same “authoritative outcomes” idea.

### Represent “loot” as mass/area (plus maybe score)

Keep a canonical server-side stat:

* `holeArea` (or `mass`)
* `radius = sqrt(holeArea / PI)`

When player A eats player B:

1. Server validates the kill:

   * `A.radius >= B.radius * killMargin`
   * `distance(A,B) <= A.innerRadius`
2. Server applies transfer:

   * `A.holeArea += B.holeArea * transferFactor` (usually 1.0 feels right)
   * `A.score += B.score` (optional, but your rule suggests yes)
3. Server eliminates B:

   * battle royale: mark eliminated, no respawn
   * casual mode: respawn B after delay with small size

**Important design detail:** add a small **kill margin** (like 1.05–1.15) so kills don’t feel random when radii are close.

### What about “all the things they ate”?

You *could* literally transfer an inventory list of eaten object types, but in this genre it’s almost always better to transfer:

* total **mass/area**
* maybe a few “powerups” if you add them later

Because it’s simple, fair, and easy to sync.

---

# Moving to a dedicated server later (your Raspberry Pi plan)

Fusion supports a dedicated server concept by running in **Server Mode** (no local player, server has authority). ([doc.photonengine.com][1])

Unity also has an official “Dedicated Server” build target / workflow (Linux Server, headless builds, etc.). ([Unity Documentation][3])

### The Raspberry Pi reality check

Running a Unity headless server on ARM has historically been the painful part, but there are signs of **Linux ARM64 dedicated server support** in Unity’s ecosystem (including references to Linux Arm64 server builds with IL2CPP). ([Unity Discussions][4])
However, there have also been reports/threads about build issues for Linux Server ARM64 in some Unity versions. ([Unity Discussions][5])

**My practical recommendation:**

* **Plan A (smoothest):** run the dedicated server on any cheap x86_64 box (old mini PC, NUC, small VM). It’s boring and it works.
* **Plan B (experiment later):** when you’re ready, try ARM64 builds for Pi (especially if you’re on newer Unity versions), but assume you might hit tooling quirks.

This is why I like your phased approach: you don’t need the Pi on day one.

---

# Remote play later (outside your Wi-Fi)

When you go remote, the big change is **connectivity**.

Fusion docs describe **Server Mode** as a dedicated server with a public IP, and Host Mode as player-hosted. ([doc.photonengine.com][2])

If your dedicated server is at home behind NAT, you’ll need one of:

* **port forwarding** (simplest if you control the router)
* or put the server on a **cheap VPS** with a public IP (often easiest for reliability)

For “kids + friends”, a tiny VPS in one region is usually fine and costs very little compared to “proper” orchestration.

---

# Future ports: desktop + consoles

Starting in Unity is the right move if you *might* want:

* Microsoft Windows / macOS builds later (fairly straightforward)
* eventual Nintendo Switch / PlayStation 5 (more process + platform programs + cert, but technically realistic)

The biggest “future-proofing” you should do now:

* abstract input (touch joystick now, controller later)
* keep UI scalable (safe areas, different aspect ratios)
* avoid mobile-only assumptions in gameplay code

---

# A concrete build plan I’d follow

## Phase 0 — Single-player prototype (1–2 weekends)

* movement + camera
* eat objects + grow
* satisfyingly “sucks in” animation

## Phase 1 — LAN multiplayer Host Mode (first playable for kids)

* Host creates match → join code
* 4–8 players
* authoritative eat events (objects disappear for everyone)
* basic scoreboard + match timer

## Phase 2 — Battle royale rules

* hole-vs-hole kill + mass transfer
* elimination + spectate
* “final 3” drama (tight map, fewer big objects late game)

## Phase 3 — Dedicated server

* run headless Linux server (x86 first)
* keep join-code flow
* optional: server browser hidden behind “Enter code”

## Phase 4 — Remote play

* VPS public IP (or home port-forward)
* basic region choice (later)

---

# One last opinion (to save you pain)

For your genre, **avoid syncing any rigidbody physics** across the network. It’s the #1 way projects like this die in complexity. If you sync only:

* players (pos/vel + radius)
* “object eaten” events
* “player eliminated” events
  …then 20 players is completely achievable and will feel great.

---

If you want, I can sketch the exact authoritative event model (message types + fields), including how to handle:

* two players “almost” eating the same object at once (server arbitration)
* late joiners / reconnects
* anti-cheat basics (even for kids, it’s worth not trusting the client for growth)

[1]: https://doc.photonengine.com/fusion/current/concepts-and-patterns/dedicated-server-overview?utm_source=chatgpt.com "Dedicated Servers - Photon Fusion 2"
[2]: https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/1-getting-started?utm_source=chatgpt.com "1 - Getting Started - Photon Fusion 2"
[3]: https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server.html?utm_source=chatgpt.com "Dedicated Server"
[4]: https://discussions.unity.com/t/unity-does-not-support-building-for-linux-arm/908326?utm_source=chatgpt.com "Unity does not support building for Linux ARM ?"
[5]: https://discussions.unity.com/t/in-118136-fail-to-build-linux-server-arm64-conversion-to-bee-architecture-not-implemented-for-linux-architecture-arm64/1685359?utm_source=chatgpt.com "[IN-118136] Fail to build Linux Server ARM64"
