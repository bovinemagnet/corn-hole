# Backlog — User Stories (mapped to phases + acceptance tests)

> Conventions
>
> * **Phase:** 0–8 (as previously defined)
> * **Acceptance tests:** written as “Given / When / Then”
> * **Priority:** P0 (must), P1 (should), P2 (nice)

---

## Phase 0 — Foundations + Single-Player Prototype

### US-0.1 — Move the hole with touch input

* **Priority:** P0
* **As a** player
* **I want** to control the hole with simple touch input
* **So that** I can move around the map easily

**Acceptance tests**

* Given I am in a match, when I drag/joystick in any direction, then the hole moves smoothly on the XZ plane in that direction.
* Given I stop input, when I release touch, then the hole decelerates smoothly to a stop (no abrupt snapping).
* Given I push input diagonally, then the movement speed remains consistent (no faster diagonals).

---

### US-0.2 — Follow camera with size-based zoom

* **Priority:** P0
* **As a** player
* **I want** the camera to follow my hole and zoom out as I grow
* **So that** I can see what’s going on around me

**Acceptance tests**

* Given I move, when I travel across the map, then the camera follows with a slight damping (no jitter).
* Given my hole grows, when radius increases, then camera height/FOV increases smoothly within a configured range.
* Given my hole shrinks/respawns (later), when radius decreases, then the camera zooms back in smoothly.

---

### US-0.3 — Consume eligible objects and grow

* **Priority:** P0
* **As a** player
* **I want** to consume objects smaller than my hole
* **So that** I grow and can consume bigger items

**Acceptance tests**

* Given an object with requiredRadius <= my current radius, when it enters my inner consume zone, then it is consumed and my holeArea increases.
* Given an object with requiredRadius > my current radius, when it overlaps my hole, then it is not consumed.
* Given repeated consumption, when I eat multiple objects, then my radius increases according to the area/mass growth rule.

---

### US-0.4 — Basic match timer and end screen

* **Priority:** P0
* **As a** player
* **I want** a timed session with an end screen
* **So that** there is a clear outcome

**Acceptance tests**

* Given a match is started, when the timer reaches 0, then gameplay stops and an end screen appears.
* Given match end, when the end screen appears, then it displays my score and (if applicable) ranking.

---

## Phase 1 — Production-Ready Single-Player Loop

### US-1.1 — Pool props and effects

* **Priority:** P0
* **As a** developer
* **I want** object pooling for props and VFX
* **So that** performance is stable on mobile

**Acceptance tests**

* Given gameplay runs for 5 minutes, when consuming objects repeatedly, then allocations per frame remain low and there is no repeated instantiate/destroy churn.
* Given a pool limit, when the pool is exhausted, then objects are safely reused or spawning is limited without crashes.

---

### US-1.2 — Add satisfying “drop into hole” feedback

* **Priority:** P1
* **As a** player
* **I want** strong visual/audio feedback when I consume objects
* **So that** the game feels satisfying

**Acceptance tests**

* Given I consume an object, when it is eaten, then a drop animation plays and a sound triggers.
* Given I consume a larger object, when it is eaten, then feedback intensity scales (e.g., larger sound/particles).

---

### US-1.3 — Improve physics/interaction stability

* **Priority:** P1
* **As a** player
* **I want** objects to behave consistently around the hole
* **So that** I don’t see glitchy interactions

**Acceptance tests**

* Given objects are near the hole rim, when I move past them, then they don’t jitter or explode due to collider issues.
* Given lots of props, when the hole moves through dense areas, then frame rate remains within target range.

---

### US-1.4 — HUD with timer, score, and size indicator

* **Priority:** P0
* **As a** player
* **I want** a simple HUD
* **So that** I can track progress

**Acceptance tests**

* Given I’m in match, then HUD shows timer counting down, current score, and a size/radius indicator.
* Given I consume objects, then score and size indicator update promptly.

---

## Phase 2 — Multiplayer Host Mode + Join Code Lobby

### US-2.1 — Create a private match with join code

* **Priority:** P0
* **As a** host
* **I want** to create a private match and share a join code
* **So that** friends can join easily

**Acceptance tests**

* Given I am on the main menu, when I tap “Create Match”, then a lobby is created and a join code is displayed.
* Given the join code, when another device enters it, then they join the lobby.

---

### US-2.2 — Join match by entering code

* **Priority:** P0
* **As a** player
* **I want** to join a match using a code
* **So that** it stays private and simple

**Acceptance tests**

* Given I enter a valid code, when I submit, then I join the lobby within a reasonable time.
* Given I enter an invalid code, when I submit, then I see a clear error message.
* Given the lobby is full, when I try to join, then I receive a “match full” error.

---

### US-2.3 — Lobby roster and ready state

* **Priority:** P1
* **As a** host/player
* **I want** to see who is in the lobby and who is ready
* **So that** we can start together

**Acceptance tests**

* Given multiple players joined, then the lobby shows each player name and a ready indicator.
* Given I toggle ready, then my state updates for everyone.
* Given the host starts match, then only ready players are included (or the game clearly explains the rule).

---

### US-2.4 — Authoritative movement over network (inputs only)

* **Priority:** P0
* **As a** player
* **I want** smooth multiplayer movement
* **So that** the game feels responsive

**Acceptance tests**

* Given I move my joystick, when I change direction, then my local view responds quickly (prediction/interp allowed).
* Given the server is authoritative, when a client attempts abnormal movement (e.g., injected position), then the server state overrides it.
* Given 8 players on LAN, when everyone moves, then movement appears stable with minimal rubber-banding.

---

### US-2.5 — Match start/end synchronize across clients

* **Priority:** P0
* **As a** group
* **I want** matches to start and end consistently for everyone
* **So that** results are fair

**Acceptance tests**

* Given host starts match, when match begins, then all clients enter gameplay state within a small window.
* Given timer ends, then all clients end at the same time (server authoritative time).

---

## Phase 3 — Shared World: Deterministic Spawn + Authoritative Consume Events

### US-3.1 — Spawn identical props on all clients from seed

* **Priority:** P0
* **As a** developer
* **I want** deterministic prop spawning using a map seed
* **So that** I can reference props by stable objectId

**Acceptance tests**

* Given two clients in the same match, when match starts, then prop count and positions match exactly (within deterministic tolerance).
* Given deterministic spawn order, then `objectId` assignment is identical across clients.

---

### US-3.2 — Server authoritative “ObjectConsumed” event

* **Priority:** P0
* **As a** player
* **I want** objects I consume to disappear for everyone
* **So that** the world stays consistent

**Acceptance tests**

* Given I consume a prop, when the server confirms, then the prop disappears on all clients.
* Given a client predicts an eat that the server denies, then the client corrects without breaking the match.
* Given an object is already consumed, when another player overlaps it, then it does not reappear or re-consume.

---

### US-3.3 — Contested consumption arbitration

* **Priority:** P0
* **As a** player
* **I want** fair resolution when two holes reach the same object
* **So that** outcomes aren’t random

**Acceptance tests**

* Given two players are eligible to consume the same object in the same tick, when arbitration runs, then exactly one winner is selected deterministically.
* Given equal distances within epsilon, when tie-break triggers, then larger radius wins; if still tied, lowest playerId wins.
* Given arbitration, then only one ObjectConsumed event is emitted for that objectId.

---

### US-3.4 — Server authoritative growth and score updates

* **Priority:** P0
* **As a** player
* **I want** growth/score to be authoritative
* **So that** cheating and desync are reduced

**Acceptance tests**

* Given I consume an object, when the event arrives, then my displayed holeArea/score matches the server authoritative values.
* Given a client attempts to locally modify growth, then server updates override it on the next state update/event.

---

## Phase 4 — Battle Royale (Hole vs Hole)

### US-4.1 — Enable BR mode match flow

* **Priority:** P1
* **As a** host
* **I want** to select Battle Royale mode
* **So that** we can play last-hole-standing

**Acceptance tests**

* Given I create a match, when I choose BR, then rules and win condition change appropriately.
* Given BR is selected, when match ends, then winner is the last alive player.

---

### US-4.2 — Predator hole eats smaller hole

* **Priority:** P0
* **As a** player
* **I want** to eat smaller holes
* **So that** I can eliminate opponents

**Acceptance tests**

* Given predator radius >= prey radius * killMargin, when overlap/consume condition is met, then prey is eaten and eliminated.
* Given predator is not sufficiently larger, when overlap occurs, then no kill occurs.
* Given a kill, then event is broadcast and all clients agree on predator/prey outcome.

---

### US-4.3 — Mass/score transfer on player eaten

* **Priority:** P0
* **As a** player
* **I want** to inherit what the prey accumulated
* **So that** kills are rewarding

**Acceptance tests**

* Given prey is eaten, when transfer applies, then predator holeArea increases by prey holeArea (or configured factor).
* Given prey had score, when transfer is enabled, then predator score increases accordingly.
* Given the event, then all clients show consistent new predator size.

---

### US-4.4 — Spectate mode for eliminated players

* **Priority:** P1
* **As an** eliminated player
* **I want** to spectate
* **So that** I can keep watching the game

**Acceptance tests**

* Given I am eliminated, when elimination occurs, then my camera switches to spectate view.
* Given spectate, when I cycle targets (if implemented), then I can view remaining players.
* Given match ends, then spectating client sees winner screen.

---

## Phase 5 — Late Joiners + Reconnects

### US-5.1 — SnapshotFull for late join

* **Priority:** P0
* **As a** late joiner
* **I want** to join a match already in progress
* **So that** I can still play (or spectate)

**Acceptance tests**

* Given a match in progress, when I join with code, then I receive SnapshotFull including players and consumedSet.
* Given SnapshotFull, when applied, then my world matches others: consumed objects are hidden and player states match.
* Given match rules disallow late join (optional), when joining in-progress, then I am refused or placed into spectate with clear UI.

---

### US-5.2 — Reconnect to the same player state

* **Priority:** P0
* **As a** player
* **I want** to reconnect after a drop
* **So that** I can keep playing

**Acceptance tests**

* Given I disconnect briefly, when I reconnect within grace period with reconnectToken, then I regain the same playerId and state.
* Given reconnect, then SnapshotFull corrects my world and I resume without a broken state.
* Given reconnect grace expired, when I reconnect, then I’m treated as a new joiner or refused (depending on policy).

---

### US-5.3 — Disconnect grace + protection bubble

* **Priority:** P1
* **As a** player
* **I want** fairness around reconnects
* **So that** I don’t die instantly due to network issues

**Acceptance tests**

* Given I reconnect, when I re-enter match, then I have protection for 3–5 seconds (no player-eaten during this window).
* Given grace period, when player stays disconnected too long, then server resolves them per mode policy (idle, eliminated, etc.).

---

### US-5.4 — Event sequencing and repair

* **Priority:** P1
* **As a** client
* **I want** to detect missed events and repair state
* **So that** desync is corrected

**Acceptance tests**

* Given I receive events with eventSeq gaps, when a gap is detected, then I request or receive a SnapshotDelta/Full and reconcile.
* Given repair occurs, then consumedSet and player states match the server afterward.

---

## Phase 6 — Dedicated Server (Home Hardware First)

### US-6.1 — Run headless dedicated server build

* **Priority:** P0
* **As a** host/parent
* **I want** a dedicated server instance to run matches
* **So that** no phone has host advantage

**Acceptance tests**

* Given the server is running, when clients join via code, then they connect and play without any device acting as host authority.
* Given the server, when a match completes, then it can start a new match without restart.
* Given server logs, when a match runs, then join/leave and error messages are recorded.

---

### US-6.2 — Join-code routes to a dedicated server match

* **Priority:** P0
* **As a** player
* **I want** to join by code the same way as before
* **So that** dedicated hosting doesn’t change UX

**Acceptance tests**

* Given a join code, when I enter it, then I connect to the dedicated match.
* Given invalid code, then I see clear error.
* Given match is full, then I get full message.

---

### US-6.3 — Stability soak test

* **Priority:** P1
* **As a** developer
* **I want** long-running stability tests
* **So that** it doesn’t crash during kids sessions

**Acceptance tests**

* Given server runs for 60 minutes with repeated matches, when monitored, then memory and CPU remain stable and no crash occurs.
* Given transient disconnects, when players reconnect, then state remains consistent.

---

## Phase 7 — Remote Play

### US-7.1 — Join matches from outside LAN

* **Priority:** P0
* **As a** player
* **I want** to join by code from a different network
* **So that** friends can play remotely

**Acceptance tests**

* Given server is publicly reachable (VPS or forwarded), when remote client enters code, then they join and can play.
* Given NAT or transient network issues, when connection drops, then reconnect works within grace period.

---

### US-7.2 — Throttle join attempts / basic abuse protection

* **Priority:** P1
* **As a** server operator
* **I want** to limit brute forcing codes
* **So that** random people can’t guess matches easily

**Acceptance tests**

* Given repeated invalid join attempts, when threshold is exceeded, then further attempts are temporarily blocked for that IP/device hash.
* Given valid code entry after block expires, then join works normally.

---

### US-7.3 — Network tuning for higher latency

* **Priority:** P1
* **As a** player
* **I want** gameplay to remain smooth with latency
* **So that** remote play is enjoyable

**Acceptance tests**

* Given 80–150ms latency, when moving and consuming, then movement remains stable and corrections are not overly jarring.
* Given contested eats, when arbitration occurs, then all clients agree on the winner.

---

## Phase 8 — Platform Expansion

### US-8.1 — Desktop build (Windows/macOS)

* **Priority:** P1
* **As a** player
* **I want** to play on desktop
* **So that** we can use PCs later

**Acceptance tests**

* Given a desktop build, when I run it, then I can join by code and play a full match.
* Given desktop input, when using keyboard/controller, then movement feels correct.

---

### US-8.2 — Controller support abstraction

* **Priority:** P1
* **As a** player
* **I want** controller support
* **So that** it works for TV/console style play later

**Acceptance tests**

* Given controller connected, when I move stick, then hole moves with same acceleration/speed curves as touch.
* Given controller disconnect, when it happens, then game falls back gracefully.

---

### US-8.3 — Console readiness (investigation)

* **Priority:** P2
* **As a** developer
* **I want** to validate console feasibility
* **So that** we can plan a Switch/PS5 port

**Acceptance tests**

* Given target consoles, when feasibility is assessed, then constraints are documented (dev program, certification, networking policies).
* Given requirements, when architecture is reviewed, then any blocking issues are identified with mitigation steps.

---

## Cross-cutting (applies across multiple phases)

### US-X.1 — Deterministic event sequencing

* **Phase:** 3+
* **Priority:** P0
* **As a** client
* **I want** monotonic event sequencing
* **So that** I can detect missed events and avoid duplicates

**Acceptance tests**

* Given events arrive out of order, when processed, then older eventSeq are ignored or buffered safely.
* Given duplicates, when re-received, then they are ignored without side effects.

---

### US-X.2 — Server authoritative anti-cheat baseline

* **Phase:** 2+
* **Priority:** P0
* **As a** developer
* **I want** clients to send only inputs and never authoritative outcomes
* **So that** cheating and desync are minimized

**Acceptance tests**

* Given a modified client tries to send growth updates, then server ignores them and the player state remains correct.
* Given a client tries to claim consuming an object, then no consumption occurs unless server simulation confirms it.

---

