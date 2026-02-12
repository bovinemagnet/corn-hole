Below is a **Product Requirements Document (PRD)** for a Hole.io-style multiplayer game, incorporating everything we discussed: cross-platform mobile first, join-code lobbies, authoritative server model, shared world (objects disappear for everyone), battle royale hole-vs-hole “inherit loot”, late join/reconnect, and a phased implementation plan that can evolve from LAN play to dedicated servers (and later consoles/desktop).

---

# Product Requirements Document

## Project: “Growing Hole” Real-Time Multiplayer

### Document status

* **Owner:** You
* **Audience:** You (primary dev), future collaborators
* **Version:** 0.1
* **Date:** 12 Feb 2026

---

## 1) Executive summary

Build a **family-friendly, real-time multiplayer** 3D game inspired by Hole.io: each player controls a moving hole that consumes objects on the ground. As objects are consumed, the hole grows, enabling it to consume larger objects. The initial release targets **phones and iPads** on the same network, using **join-code only** private matches for kids and their friends.

Multiplayer gameplay will be **server authoritative** and optimized for mobile by networking only:

* player movement + growth state (mass/area/radius)
* discrete authoritative events (object consumed, player eaten, phase changes)

All environment objects are spawned deterministically on every client using a shared seed. When the server decides an object is eaten, it broadcasts a compact “consume event” so the object disappears for everyone. A battle royale mode adds hole-vs-hole interactions: larger holes can consume smaller holes and **inherit their accumulated mass**.

Development will proceed in phases:

1. single-player feel and loop
2. LAN host-mode multiplayer + join codes
3. authoritative shared-world consumption
4. battle royale (hole-vs-hole)
5. late join/reconnect hardening
6. dedicated server deployment (home server; later remote)
7. expansion to desktop and eventually console platforms like Nintendo Switch and PlayStation 5 (subject to platform programs and certification)

Recommended technology stack:

* Game client: Unity (C#)
* Networking: Photon Fusion
* Dedicated server (future): headless Linux server on home hardware; experiment with Raspberry Pi later if feasible

---

## 2) Problem statement

Kids want a simple, fun, competitive multiplayer game they can play together with minimal setup. Existing similar games are often public-matchmaking heavy, account-based, or monetized. This project aims to deliver a **private, join-code-only** experience that is:

* easy to start and join
* smooth on mobile
* fair enough (server authority)
* stable enough (reconnect support)
* extensible to remote play and more platforms over time

---

## 3) Goals and success criteria

### Goals

1. **Fast private matchmaking**: create match → share join code → play
2. **Responsive controls**: touch movement feels “arcade tight”
3. **Shared world**: consumed objects disappear for all players
4. **Scalable to 20 players** by avoiding syncing thousands of physics objects
5. **Battle royale mode**: holes can eat holes and inherit mass
6. **Robust sessions**: late join and reconnect supported (eventually)
7. **Future-ready**: architecture supports dedicated servers and remote play

### Success criteria (initial)

* “Time to fun”: from app open to match start in **< 60 seconds** for a host
* Stable 8-player LAN match on mid-range devices at **~60fps** (or acceptable 30fps baseline)
* Consistent outcomes: no double-consume, no persistent desync
* Battle royale rules feel fair and predictable (clear “who wins”)

---

## 4) Target users and player scenarios

### Primary users

* Kids (approx. 8–13) playing with friends locally (same Wi-Fi)

### Secondary users

* Parents hosting games, troubleshooting join, controlling privacy
* Future: remote friends playing from different networks

### Core scenarios

1. **LAN party**: Kids at home, one hosts, others join via code
2. **School friends remote** (future): join code shared via message, dedicated server hosts
3. **Battle royale**: last hole standing; kills transfer mass

---

## 5) Platforms and constraints

### Phase 1–4 target

* iOS (iPhone/iPad)
* Android (phones/tablets)

### Future targets

* Desktop: Microsoft Windows and macOS
* Consoles: Switch, PS5 (requires platform programs/dev kits/certification)
* Dedicated server: Linux headless build (home server first; remote VPS later)

### Constraints

* Join-code only (no accounts required)
* No public matchmaking initially
* Must work well on home Wi-Fi
* Must remain simple to operate (minimal infrastructure early)

---

## 6) Gameplay requirements

### 6.1 Core loop

* Move hole around the map
* Consume objects smaller than current size threshold
* Grow hole (mass/area increases → radius increases)
* Compete for largest hole / highest score by end of match

### 6.2 Movement & camera

* Top-down (Hole.io style)
* Touch joystick or drag-to-move
* Smooth acceleration/deceleration, capped speed
* Camera follows player; zooms out as hole grows

### 6.3 Objects and consumption

* Objects have:

  * size requirement (required radius or tier)
  * value contribution (mass/area gain; optional score gain)
* Consumption feel:

  * optional “suction” force / attraction as hole nears
  * cosmetic “drop into hole” animation (local)
* Must be deterministic and fair in multiplayer:

  * server decides who consumed what

### 6.4 Growth model

* Use **area/mass** as the authoritative growth stat:

  * `holeArea += objectValue`
  * `radius = sqrt(holeArea / PI)`
* Server authoritative; client displays derived radius consistently

### 6.5 Game modes

1. **Classic timed match**

   * End after timer; highest score wins
2. **Battle Royale**

   * Players can eat smaller holes
   * When prey is eaten:

     * prey is eliminated (spectate)
     * predator inherits prey mass (and optionally score)
   * Last hole standing wins

---

## 7) Multiplayer and networking requirements

### 7.1 Match hosting topologies (phased)

* **Phase 2–4**: Host Mode (one player device is server + player)
* **Phase 6+**: Dedicated server (server authoritative, no local player)

### 7.2 Match entry

* Host creates match → join code generated
* Joiners enter join code
* Lobby shows connected players and readiness

### 7.3 Authority model (non-negotiable)

* Clients send **input only**
* Server computes:

  * movement simulation
  * consumption outcomes
  * growth/score updates
  * hole-vs-hole results
  * match timing and phases

### 7.4 Shared world strategy

**Do not network individual prop physics objects.**
Instead:

* All clients spawn identical objects deterministically using a `mapSeed` and spawn algorithm version.
* Server keeps “truth” of which objects are still available.
* Server broadcasts **ObjectConsumed** events (objectId + eaterId).

### 7.5 Authoritative event model (required)

Minimum authoritative event types:

* `ObjectConsumed`

  * object disappears for everyone
  * server updates eater’s mass/score
* `PlayerEaten`

  * predator consumes prey hole
  * prey eliminated (BR) or respawned (non-BR)
  * predator inherits mass/score as configured
* `MatchPhaseChanged`

  * countdown → live → ending

### 7.6 Server arbitration for near-simultaneous consumption

If two players “almost” consume the same object:

* server evaluates candidates per tick
* winner is chosen deterministically by:

  1. closest distance to object center
  2. if tie within epsilon: larger radius wins
  3. if still tie: stable tie-break (lowest playerId)
* exactly one `ObjectConsumed` emitted per objectId

### 7.7 Late joiners & reconnects

* Late joiner must receive a **SnapshotFull** containing:

  * current match timer/phase
  * player states (pos, mass/score, alive)
  * consumed set (bitset or chunked bitset)
* Reconnect must:

  * reuse same playerId if reconnect token valid
  * send SnapshotFull and resume
* Disconnect grace period:

  * mark player disconnected; keep state for N seconds
  * on reconnect: short “spawn protection” window (to avoid unfair instant death)

### 7.8 Anti-cheat basics (even for kids)

* No client can claim “I ate X” or “I grew by Y”
* All growth/score changes come only from server authoritative events
* Input validation:

  * clamp movement input ranges
  * enforce max speed/accel in server simulation
* Event sequencing:

  * every authoritative event has monotonic `eventSeq`
  * clients ignore duplicates/out-of-order beyond small buffer

---

## 8) Data model and identifiers

### 8.1 Identifiers

* `matchId : uint64`
* `playerId : uint8` (0..19)
* `objectId : uint32` (deterministic spawn index or chunked ID)

### 8.2 Canonical stats per player

* `holeArea : uint32` (authoritative)
* `score : uint32`
* derived:

  * `radius` (computed from holeArea)
  * optional `speedMultiplier` (balance tuning)

### 8.3 Consumed set representation

* MVP: bitset with N bits for N spawned objects
* Later: chunked bitsets (only chunks that have changes)

---

## 9) UX requirements

### 9.1 Lobby

* Create match (shows join code)
* Join match (enter join code)
* Show list of players
* Ready / Start (host controls)

### 9.2 In-match UI

* timer
* leaderboard (top 3 + your rank)
* your size/score indicator
* optional minimap (later)

### 9.3 Kid-friendly defaults

* No chat (or fully disabled by default)
* Mute sound toggle
* Simple names (local nickname)

---

## 10) Performance requirements

### Mobile performance targets

* 8 players LAN: aim 60fps on modern devices; acceptable 30fps on older
* 20 players: stable simulation and no memory spikes; keep bandwidth low

### Technical performance rules

* Object pooling for props and VFX
* Simplify colliders (box/sphere/capsule)
* Avoid networked rigidbody physics props
* Server uses spatial indexing (grid) for consumption checks (Phase 4+)

---

## 11) Phased delivery plan

### Phase 0 — Foundations and single-player prototype

**Goal:** prove the core feel is fun.

Deliverables:

* top-down camera follow + zoom with growth
* touch movement
* basic map with props
* local consumption + growth model (holeArea → radius)
* simple win condition (timer)

Acceptance:

* feels good to move and eat objects
* growth pacing feels satisfying

---

### Phase 1 — Production-ready single-player loop

**Goal:** make it stable and “game-like”.

Deliverables:

* object pooling
* improved suction/drop animation
* UI (timer, score)
* basic audio/VFX
* performance baseline

Acceptance:

* runs smoothly on target phones/tablets
* no major GC spikes from spawning/destroying

---

### Phase 2 — Multiplayer Host Mode + join-code lobby

**Goal:** kids can host/join on Wi-Fi and move together.

Deliverables:

* create match → join code
* join match by code
* multiplayer movement replication
* authoritative server simulation of player movement (inputs only)
* basic scoreboard sync

Acceptance:

* 4–8 players can connect and move without rubber-banding
* match starts reliably via host

---

### Phase 3 — Shared world (authoritative consume events)

**Goal:** if someone eats an object, it disappears for everyone.

Deliverables:

* deterministic prop spawning via seed
* objectId scheme
* server authoritative `ObjectConsumed` event
* clients apply consume event to hide prop + update UI
* arbitration logic for “two players almost ate it”

Acceptance:

* no double-consume
* consistent world across clients throughout match

---

### Phase 4 — Battle Royale mode (hole-vs-hole)

**Goal:** implement “eat smaller hole and inherit mass”.

Deliverables:

* server authoritative `PlayerEaten` event
* kill margin rules and tie behaviors
* elimination + spectate
* winner determination (last alive)

Acceptance:

* kills feel fair and deterministic
* mass transfer is consistent and satisfying

---

### Phase 5 — Late joiners and reconnects

**Goal:** sessions survive disconnects and can accept late join (configurable).

Deliverables:

* `SnapshotFull` for joiners/reconnects including consumed bitset
* reconnect token logic
* disconnect grace window
* rejoin protection bubble (brief)

Acceptance:

* reconnect returns player to correct state
* late joiner sees correct world state and active players

---

### Phase 6 — Dedicated server (home hardware first)

**Goal:** remove host advantage and improve stability.

Deliverables:

* server build pipeline (headless Linux)
* server mode session start
* join code connects to dedicated server
* basic server logs + crash restart behavior

Acceptance:

* matches run without a host phone as authority
* stable 8–20 player matches with predictable latency handling

---

### Phase 7 — Remote play

**Goal:** friends can join from outside home Wi-Fi.

Deliverables:

* networking config for public IP / VPS
* basic hardening: join throttling, error messages, timeouts
* optional region selection (later)

Acceptance:

* join by code works remotely with acceptable latency
* reconnect works across networks

---

### Phase 8 — Platform expansion

**Goal:** broaden availability.

Deliverables:

* Windows/macOS builds
* controller input support
* performance/UX polish for larger screens
* evaluate console feasibility (dev programs/dev kits/cert)

Acceptance:

* desktop builds playable and stable
* controller support feels natural

---

## 12) Risks and mitigations

### Risk: multiplayer complexity overwhelms the project

Mitigation:

* keep authority model strict (inputs only)
* avoid syncing prop physics at all costs
* deliver in phases; don’t start BR until shared consume events are solid

### Risk: dedicated server on Raspberry Pi is tricky

Mitigation:

* use x86 Linux server first (mini PC or VPS)
* treat Pi as an experiment later, not a blocker

### Risk: “feel” isn’t fun enough

Mitigation:

* Phase 0 focuses on feel first
* add suction/drop animation and tuning before deep multiplayer

---

## 13) Open questions (non-blocking, but useful later)

* Map size and match length (3 min? 5 min?)
* Do objects respawn in Classic mode?
* Do you want bots to fill lobbies to reach 8–20?
* Do you want powerups (speed boost, magnet, shield), or keep pure?

---

## 14) Appendix: authoritative event schema (reference)

Minimum message types and fields (conceptual):

* `ObjectConsumed(serverTick, eventSeq, objectId, eaterPlayerId, eaterHoleAreaAfter, eaterScoreAfter)`
* `PlayerEaten(serverTick, eventSeq, predatorId, preyId, predatorHoleAreaAfter, predatorScoreAfter, preyEliminated, preyRespawnTick?)`
* `SnapshotFull(snapshotId, serverTick, matchTimeRemainingMs, mapSeed, spawnAlgoVersion, players[], consumedBitset/chunks)`
* `InputFrame(clientTick, moveX, moveY, buttons, seq)` (client → server)

Arbitration rule for contested object:

1. smallest distance wins
2. tie: larger radius wins
3. tie: lowest playerId wins

