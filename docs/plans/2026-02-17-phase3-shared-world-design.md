# Phase 3 Design: Shared World — Authoritative Consume Events

**Date:** 2026-02-17
**Author:** Paul Snow
**Status:** Approved
**GitHub Issue:** #5

## Goal

When any player eats an object, it disappears for all clients. No double-consume. Consistent world state across all clients throughout the match.

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Spawn model | Hybrid: initial batch + deterministic waves | Populated world from tick 1, ongoing freshness |
| Object counts | Configurable (`SerializeField`) | Default 30 initial / 50 max, tuneable without code changes |
| VFX on consume | Animate real object (shrink/spin/sink) | No network despawn means no clone needed |
| Architecture | Separate `ConsumeArbiter` + local `ObjectSpawner` | Clean separation of concerns, testable, matches PRD |

## Architecture Overview

```
Host tick:
  ConsumeArbiter.FixedUpdateNetwork()
    -> spatial query (players x objects)
    -> eligibility + arbitration
    -> update player.HoleArea, player.Score
    -> RPC_ObjectConsumed -> all clients

All clients (including host):
  RPC_ObjectConsumed handler
    -> ObjectSpawner.GetObject(objectId)
    -> consumable.PlayConsumeAnimation(holePosition)
    -> disable GameObject
```

## Component Design

### 1. ObjectSpawner (Rewrite)

**Changes from Phase 2:** No longer a `NetworkBehaviour`. Becomes a plain `MonoBehaviour` that runs on all clients identically. No `Runner.Spawn()` — uses `Instantiate()` from local prefab references.

**Behaviour:**
- Receives `mapSeed` (uint) from `ConsumeArbiter`'s networked state
- Uses `System.Random(mapSeed)` for deterministic spawning
- Spawns an initial batch at match start with deterministic positions, rotations, and prefab selection
- Each object gets a sequential `objectId` (0, 1, 2, ...) stored in `Dictionary<int, ConsumableObject>`
- After the initial batch, spawns waves on a deterministic tick schedule using the same seeded PRNG
- Wave spawning driven by match tick count so all clients stay in lockstep

**Configurable fields:**
- `initialBatchSize` (default 30)
- `maxObjects` (default 50)
- `waveInterval` — ticks between waves
- `waveSize` — objects per wave

**Key invariant:** Given the same seed, every client produces the same objects in the same order at the same positions.

### 2. ConsumableObject (Simplify)

**Changes from Phase 2:** No longer a `NetworkBehaviour`. Becomes a plain `MonoBehaviour`. No `NetworkObject` component. No `[Networked]` state.

**Fields:**
- `public int ObjectId { get; set; }` — assigned by the spawner
- `public float ObjectSize` — existing, used for eligibility checks
- `public int PointValue` / `public float SizeValue` — existing, unchanged
- `public bool IsConsumed` — simple bool, no longer networked

**Consume animation:** When `PlayConsumeAnimation(holePosition)` is called, the `ConsumeEffect` component drives a shrink/spin/sink animation on the real GameObject, then disables it. No visual clone needed.

### 3. ConsumeArbiter (New)

**Role:** Server-authoritative consumption logic. Evaluates which player eats which object each tick.

**Networked state:**
- `[Networked] public uint MapSeed { get; set; }` — set by host at countdown start
- `[Networked] public int EventSeq { get; set; }` — monotonic event counter

**Per-tick logic (`FixedUpdateNetwork`, host only):**
1. For each active player, find nearby unconsumed objects (distance check against `HoleRadius`)
2. For each candidate object, collect all eligible players (`player.HoleRadius >= object.ObjectSize`)
3. Pick winner deterministically:
   - Closest distance to object centre
   - Tie-break: larger radius
   - Tie-break: lowest `PlayerId`
4. Award: update winner's `HoleArea` and `Score` directly, mark object consumed in local `BitArray`
5. Fire `RPC_ObjectConsumed(objectId, eaterPlayerId, eaterHoleAreaAfter, eaterScoreAfter, eventSeq)`

**Client-side RPC handler:**
- Look up object by ID in the spawner's dictionary
- Play consume animation on the real GameObject
- Update UI from authoritative values

### 4. HolePlayer (Modify)

**Removed:**
- `OnTriggerEnter` consumption logic
- `ConsumeObject()` method
- `_spawner` reference

**Kept:**
- Movement input handling
- `HoleArea` / `HoleRadius` / `Score` as `[Networked]`
- Visual scale updates
- Lobby properties (`PlayerName`, `IsReady`)

**Note:** The arbiter writes `HoleArea` and `Score` directly on the player's `NetworkObject` (host has state authority over all player objects).

### 5. NetworkManager (Minor changes)

- Spawn `ConsumeArbiter` alongside `MatchTimer` when first player joins
- Expose `ConsumeArbiter` reference for other components

### 6. ConsumeEffect (Unchanged)

Kept as-is — still drives the shrink/spin/sink animation. Now attached to the real object rather than a clone.

## Files Changed / Created

| File | Action |
|---|---|
| `Assets/Scripts/ConsumeArbiter.cs` | **New** — server-side arbitration + RPC |
| `Assets/Scripts/ObjectSpawner.cs` | **Rewrite** — local deterministic spawning, object registry |
| `Assets/Scripts/ConsumableObject.cs` | **Simplify** — remove NetworkBehaviour, add objectId, animate-and-disable |
| `Assets/Scripts/HolePlayer.cs` | **Modify** — remove OnTriggerEnter/ConsumeObject |
| `Assets/Scripts/NetworkManager.cs` | **Minor** — spawn ConsumeArbiter |
| `Assets/Scripts/ConsumeEffect.cs` | **Keep as-is** |

## Arbitration Rules

For each candidate object per tick, when multiple players overlap:

1. **Closest distance** to object centre wins
2. **Tie within epsilon (~0.01):** larger `HoleRadius` wins
3. **Still tied:** lowest `PlayerId` wins

Exactly one `ObjectConsumed` event is emitted per `objectId`. No double-consume is possible.

## Out of Scope

- Late join / reconnect with `SnapshotFull` (Phase 5)
- Hole-vs-hole `PlayerEaten` events (Phase 4)
- Chunked bitset optimisation (Phase 5+)
- Dedicated server mode (Phase 6)
- Client-side prediction of consumption (future polish)
