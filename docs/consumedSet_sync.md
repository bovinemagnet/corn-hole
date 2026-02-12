consumedSet sync (minimal model + late join/reconnect)
Authoritative structure (server)

consumedSet: bitset or chunked bitset

bitset: one bit per objectId (0 = exists, 1 = consumed)

chunked: split into chunks (e.g. 1024 objects per chunk) and send only chunks that changed or have any consumed bits

Sync strategy (recommended minimal)

During match

Server emits ObjectConsumed(objectId, eaterId, eventSeq...)

Client flips that bit locally and hides prop

Late join / reconnect

Server sends SnapshotFull containing:

mapSeed

players[]

consumedSet (bitset or chunked)

eventSeq

Client spawns all props from seed then applies consumed bits

Repair (optional but nice)

If client detects it missed events (gap in eventSeq) or after long jitter:

Client requests (or server pushes) SnapshotDelta for the gap window:

consumedDeltaIds or changedChunks

Why chunking matters (later)

When you scale map size, a full bitset can get big. Chunking keeps snapshots small:

Full snapshot: only chunks with any consumed bits

Delta snapshot: only chunks changed since last acknowledged snapshot

Determinism and “contested eat” (where it fits in the diagram)

In the diagrams, ConsumeArbiter is the central authority:

It evaluates eligible consumers for an object in the same server tick

It chooses a single winner (distance, then radius, then stable tie-break)

It flips the authoritative bit in ConsumedSet

It emits exactly one ObjectConsumed event per objectId

That’s what prevents double-consume and keeps every client consistent.
