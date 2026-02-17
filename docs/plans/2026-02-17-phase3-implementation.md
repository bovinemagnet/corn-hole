# Phase 3: Shared World — Authoritative Consume Events — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** When any player eats an object, it disappears for all clients. No double-consume. Consistent world state across all clients throughout the match.

**Architecture:** Replace per-object `NetworkObject` spawning with deterministic seed-based local spawning. A new `ConsumeArbiter` NetworkBehaviour on the host evaluates consumption each tick, picks deterministic winners, and broadcasts `RPC_ObjectConsumed` to all clients. Consumables become plain MonoBehaviours.

**Tech Stack:** Unity 6.3, C# (.NET Standard 2.1), Photon Fusion (Host Mode), `System.Random` for deterministic PRNG, `System.Collections.BitArray` for consumed set.

**Assembly:** `CornHole.Scripts` (references: `Fusion.Unity`, `Unity.TextMeshPro`, `UnityEngine.UI`)

---

## Task 1: Simplify ConsumableObject — Remove NetworkBehaviour

**Goal:** Convert `ConsumableObject` from a `NetworkBehaviour` to a plain `MonoBehaviour`. Remove all Fusion dependencies. Add `ObjectId` and a `PlayConsumeAnimation` method that animates the real object (no clone).

**Files:**
- Modify: `Assets/Scripts/ConsumableObject.cs` (full rewrite)

**Step 1: Rewrite ConsumableObject**

Replace the entire contents of `Assets/Scripts/ConsumableObject.cs` with:

```csharp
using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// A locally-spawned consumable object. Not networked — spawned deterministically
    /// from a shared seed on all clients. The server decides consumption via ConsumeArbiter.
    /// </summary>
    public class ConsumableObject : MonoBehaviour
    {
        [Header("Object Properties")]
        [SerializeField] private float objectSize = 1f;
        [SerializeField] private int pointValue = 10;
        [SerializeField] private float sizeValue = 0.1f;

        [Header("Physics")]
        [SerializeField] private Rigidbody rb;

        [Header("Consume Effects")]
        [SerializeField] private ParticleSystem consumeParticles;
        [SerializeField] private AudioClip consumeSound;
        [SerializeField] private float baseSoundVolume = 0.5f;

        /// <summary>Deterministic ID assigned by ObjectSpawner.</summary>
        public int ObjectId { get; set; }

        /// <summary>Minimum hole radius needed to consume this object.</summary>
        public float ObjectSize => objectSize;

        public int PointValue => pointValue;
        public float SizeValue => sizeValue;
        public bool IsConsumed { get; private set; }

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (rb != null)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        /// <summary>
        /// Check whether this object can be consumed by a hole of the given radius.
        /// </summary>
        public bool CanBeConsumed(float holeRadius)
        {
            return !IsConsumed && objectSize <= holeRadius;
        }

        /// <summary>
        /// Called on all clients when the server confirms this object was consumed.
        /// Plays the shrink/spin/sink animation on the real object, then disables it.
        /// </summary>
        public void PlayConsumeAnimation(Vector3 holePosition)
        {
            if (IsConsumed)
                return;

            IsConsumed = true;

            // Audio — play before animation disables the object
            if (consumeSound != null)
            {
                float intensity = Mathf.Clamp01(sizeValue);
                float volume = baseSoundVolume * (0.5f + intensity);
                AudioSource.PlayClipAtPoint(consumeSound, transform.position, volume);
            }

            // Particle effect — detach so it outlives the disabled object
            if (consumeParticles != null)
            {
                consumeParticles.transform.SetParent(null);
                var main = consumeParticles.main;
                main.startSizeMultiplier *= (1f + Mathf.Clamp01(sizeValue));
                consumeParticles.Play();
                Destroy(consumeParticles.gameObject, main.duration + main.startLifetime.constantMax);
            }

            // Disable physics so the object stops interacting
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // Add ConsumeEffect to drive the shrink/spin/sink animation
            var effect = gameObject.AddComponent<ConsumeEffect>();
            effect.Initialise(holePosition, sizeValue);
            // ConsumeEffect will Destroy(gameObject) when done — but we want to
            // just disable it. Override: we'll modify ConsumeEffect to disable instead.
        }

        /// <summary>
        /// Called by ConsumeEffect when the animation finishes.
        /// Disables the GameObject rather than destroying it.
        /// </summary>
        public void OnConsumeAnimationComplete()
        {
            gameObject.SetActive(false);
        }
    }
}
```

**Step 2: Update ConsumeEffect to disable instead of destroy**

Modify `Assets/Scripts/ConsumeEffect.cs` — change the `Destroy(gameObject)` call at line 57 to notify the `ConsumableObject` parent and then just remove the effect component:

Replace lines 55-58 in `ConsumeEffect.cs`:

```csharp
            if (t >= 1f)
            {
                var consumable = GetComponent<ConsumableObject>();
                if (consumable != null)
                {
                    consumable.OnConsumeAnimationComplete();
                }
                else
                {
                    // Fallback for clones (legacy path)
                    Destroy(gameObject);
                }
            }
```

**Step 3: Verify no compilation errors**

Open Unity Editor. Check the Console for errors. The project will have compilation errors because `HolePlayer` still references `ConsumableObject` as a `NetworkBehaviour` — that's expected and will be fixed in Task 3.

**Step 4: Commit**

```bash
git add Assets/Scripts/ConsumableObject.cs Assets/Scripts/ConsumeEffect.cs
git commit -m "refactor: convert ConsumableObject to plain MonoBehaviour

Remove NetworkBehaviour, add ObjectId, replace visual clone with
real-object animation. Part of Phase 3 shared world."
```

---

## Task 2: Rewrite ObjectSpawner — Deterministic Local Spawning

**Goal:** Convert `ObjectSpawner` from a `NetworkBehaviour` to a plain `MonoBehaviour`. Use `System.Random` with a seed for deterministic spawning. Maintain an object registry (`Dictionary<int, ConsumableObject>`). Spawn an initial batch at match start, then waves on a tick schedule.

**Files:**
- Modify: `Assets/Scripts/ObjectSpawner.cs` (full rewrite)

**Step 1: Rewrite ObjectSpawner**

Replace the entire contents of `Assets/Scripts/ObjectSpawner.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Spawns consumable objects deterministically from a shared seed.
    /// Runs identically on all clients — no networking. The ConsumeArbiter
    /// provides the seed and triggers spawning at match start.
    /// </summary>
    public class ObjectSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject[] consumablePrefabs;

        [Header("Spawn Settings")]
        [SerializeField] private int initialBatchSize = 30;
        [SerializeField] private int maxObjects = 50;
        [SerializeField] private int waveSize = 5;
        [SerializeField] private float waveIntervalSeconds = 5f;
        [SerializeField] private Vector3 spawnAreaSize = new Vector3(40, 0, 40);
        [SerializeField] private float spawnHeight = 10f;

        private readonly Dictionary<int, ConsumableObject> _objectRegistry =
            new Dictionary<int, ConsumableObject>();

        private System.Random _rng;
        private int _nextObjectId;
        private int _activeCount;
        private bool _initialBatchSpawned;
        private float _waveTimer;

        /// <summary>Total objects spawned so far (consumed or not).</summary>
        public int TotalSpawned => _nextObjectId;

        /// <summary>
        /// Initialise the spawner with a deterministic seed. Called by ConsumeArbiter
        /// when the match transitions to Playing phase. All clients call this with the
        /// same seed to produce identical worlds.
        /// </summary>
        public void Initialise(int seed)
        {
            _rng = new System.Random(seed);
            _nextObjectId = 0;
            _activeCount = 0;
            _initialBatchSpawned = false;
            _waveTimer = 0f;
        }

        /// <summary>
        /// Spawn the initial batch of objects. Called once when match starts.
        /// </summary>
        public void SpawnInitialBatch()
        {
            if (_rng == null || _initialBatchSpawned)
                return;

            int count = Mathf.Min(initialBatchSize, maxObjects);
            for (int i = 0; i < count; i++)
            {
                SpawnDeterministicObject();
            }

            _initialBatchSpawned = true;
            _waveTimer = waveIntervalSeconds;
        }

        /// <summary>
        /// Called each frame during the Playing phase to spawn waves.
        /// Uses real time (Time.deltaTime) since all clients run this identically.
        /// </summary>
        public void UpdateWaveSpawning()
        {
            if (_rng == null || !_initialBatchSpawned)
                return;

            if (_activeCount >= maxObjects)
                return;

            _waveTimer -= Time.deltaTime;
            if (_waveTimer <= 0f)
            {
                int toSpawn = Mathf.Min(waveSize, maxObjects - _activeCount);
                for (int i = 0; i < toSpawn; i++)
                {
                    SpawnDeterministicObject();
                }
                _waveTimer = waveIntervalSeconds;
            }
        }

        /// <summary>
        /// Look up a consumable by its deterministic object ID.
        /// </summary>
        public ConsumableObject GetObject(int objectId)
        {
            _objectRegistry.TryGetValue(objectId, out ConsumableObject obj);
            return obj;
        }

        /// <summary>
        /// Mark an object as consumed (decrements active count).
        /// Called by ConsumeArbiter when processing a consume event.
        /// </summary>
        public void MarkConsumed(int objectId)
        {
            if (_activeCount > 0)
            {
                _activeCount--;
            }
        }

        /// <summary>
        /// Returns all unconsumed objects for spatial queries.
        /// </summary>
        public IEnumerable<ConsumableObject> GetActiveObjects()
        {
            foreach (var kvp in _objectRegistry)
            {
                if (kvp.Value != null && !kvp.Value.IsConsumed)
                {
                    yield return kvp.Value;
                }
            }
        }

        /// <summary>
        /// Destroy all spawned objects and reset state.
        /// Called when returning to menu or starting a new match.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _objectRegistry)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _objectRegistry.Clear();
            _nextObjectId = 0;
            _activeCount = 0;
            _initialBatchSpawned = false;
            _rng = null;
        }

        private void SpawnDeterministicObject()
        {
            if (consumablePrefabs == null || consumablePrefabs.Length == 0)
                return;

            // Deterministic prefab selection
            int prefabIndex = _rng.Next(0, consumablePrefabs.Length);
            GameObject prefab = consumablePrefabs[prefabIndex];

            // Deterministic position
            float x = (float)(_rng.NextDouble() * spawnAreaSize.x) - spawnAreaSize.x / 2f;
            float z = (float)(_rng.NextDouble() * spawnAreaSize.z) - spawnAreaSize.z / 2f;
            Vector3 position = new Vector3(x, spawnHeight, z);

            // Deterministic rotation
            float yRot = (float)(_rng.NextDouble() * 360.0);
            Quaternion rotation = Quaternion.Euler(0f, yRot, 0f);

            GameObject spawned = Instantiate(prefab, position, rotation);
            ConsumableObject consumable = spawned.GetComponent<ConsumableObject>();

            if (consumable != null)
            {
                int objectId = _nextObjectId++;
                consumable.ObjectId = objectId;
                _objectRegistry[objectId] = consumable;
                _activeCount++;
            }
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/ObjectSpawner.cs
git commit -m "refactor: rewrite ObjectSpawner for deterministic local spawning

Use System.Random with shared seed. Hybrid model: initial batch at
match start plus timed waves. Object registry by ID. No networking.
Part of Phase 3 shared world."
```

---

## Task 3: Strip Consumption Logic from HolePlayer

**Goal:** Remove `OnTriggerEnter`, `ConsumeObject()`, and the `_spawner` reference from `HolePlayer`. The arbiter now owns all consumption logic. Keep movement, networked state, lobby properties, and scale updates.

**Files:**
- Modify: `Assets/Scripts/HolePlayer.cs:39,52,134-164`

**Step 1: Remove consumption code from HolePlayer**

In `Assets/Scripts/HolePlayer.cs`, make the following changes:

1. Remove the `_spawner` field (line 39):
   ```csharp
   // DELETE: private ObjectSpawner _spawner;
   ```

2. Remove the `_spawner` lookup in `Spawned()` (line 52):
   ```csharp
   // DELETE: _spawner = FindAnyObjectByType<ObjectSpawner>();
   ```

3. Remove the entire `OnTriggerEnter` method (lines 134-148):
   ```csharp
   // DELETE entire method
   ```

4. Remove the entire `ConsumeObject` method (lines 150-164):
   ```csharp
   // DELETE entire method
   ```

The remaining `HolePlayer` should contain: movement, networked properties (`HoleArea`, `HoleRadius`, `Score`, `PlayerName`, `IsReady`, `Velocity`), `UpdateHoleScale`, and the two lobby RPCs.

**Step 2: Verify the cleaned-up HolePlayer compiles**

The file should look like this after edits:

```csharp
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Network player component for the hole.
    /// Uses Fusion input system for client-side prediction.
    /// Handles movement and visual scale. Consumption is handled by ConsumeArbiter.
    /// </summary>
    public class HolePlayer : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 10f;

        [Header("Hole Settings")]
        [SerializeField] private float initialRadius = 1f;
        [SerializeField] private float maxRadius = 10f;

        [Header("References")]
        [SerializeField] private Transform holeVisual;
        [SerializeField] private SphereCollider consumeCollider;

        // Networked properties — synchronised by Fusion
        [Networked] public float HoleRadius { get; set; }
        [Networked] public float HoleArea { get; set; }
        [Networked] public int Score { get; set; }

        // Lobby properties
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }

        // Movement state — networked for rollback prediction
        [Networked] private Vector3 Velocity { get; set; }

        private MatchTimer _matchTimer;

        /// <summary>Maximum hole area, derived from maxRadius.</summary>
        public float MaxArea => Mathf.PI * maxRadius * maxRadius;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                HoleRadius = initialRadius;
                HoleArea = Mathf.PI * initialRadius * initialRadius;
                Score = 0;
                IsReady = false;
            }

            _matchTimer = FindAnyObjectByType<MatchTimer>();
            UpdateHoleScale();

            if (Object.HasInputAuthority)
            {
                RPC_SetPlayerName($"Player {Object.InputAuthority.PlayerId}");
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            if (_matchTimer != null && _matchTimer.IsPlaying)
            {
                HandleMovement();
            }

            UpdateHoleScale();
        }

        private void HandleMovement()
        {
            if (!GetInput(out NetworkInputData data))
                return;

            float dt = Runner.DeltaTime;

            Vector2 rawInput = data.MoveDirection;
            if (rawInput.sqrMagnitude > 1f)
            {
                rawInput.Normalize();
            }

            Vector3 inputDirection = new Vector3(rawInput.x, 0f, rawInput.y);
            Vector3 vel = Velocity;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 targetVelocity = inputDirection * moveSpeed;
                vel = Vector3.MoveTowards(vel, targetVelocity, acceleration * dt);

                if (vel.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(vel.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, targetRotation, rotationSpeed * dt);
                }
            }
            else
            {
                vel = Vector3.MoveTowards(vel, Vector3.zero, deceleration * dt);
            }

            Velocity = vel;
            transform.position += vel * dt;
        }

        private void UpdateHoleScale()
        {
            if (holeVisual != null)
            {
                float scale = HoleRadius * 2f;
                holeVisual.localScale = new Vector3(scale, 1f, scale);
            }

            if (consumeCollider != null)
            {
                consumeCollider.radius = HoleRadius;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(NetworkString<_16> name)
        {
            PlayerName = name;
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/HolePlayer.cs
git commit -m "refactor: remove consumption logic from HolePlayer

ConsumeArbiter now owns all consumption. Remove OnTriggerEnter,
ConsumeObject, and ObjectSpawner reference. Add MaxArea property.
Part of Phase 3 shared world."
```

---

## Task 4: Create ConsumeArbiter — Server-Side Arbitration

**Goal:** Create the `ConsumeArbiter` NetworkBehaviour. It owns `MapSeed`, generates it at countdown, tells `ObjectSpawner` to initialise, evaluates consumption each tick on the host, picks deterministic winners, and fires `RPC_ObjectConsumed` to all clients.

**Files:**
- Create: `Assets/Scripts/ConsumeArbiter.cs`

**Step 1: Create ConsumeArbiter**

Create `Assets/Scripts/ConsumeArbiter.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Server-authoritative consumption arbiter. Runs on the host only.
    /// Evaluates which player eats which object each tick, picks deterministic
    /// winners, and broadcasts RPC_ObjectConsumed to all clients.
    /// </summary>
    public class ConsumeArbiter : NetworkBehaviour
    {
        /// <summary>
        /// Shared seed for deterministic object spawning. Set by host at countdown.
        /// All clients read this to initialise their ObjectSpawner identically.
        /// </summary>
        [Networked] public int MapSeed { get; set; }

        /// <summary>Monotonic event counter for ordering consume events.</summary>
        [Networked] public int EventSeq { get; set; }

        /// <summary>
        /// Set to true by the host once the seed is assigned and spawning should begin.
        /// Clients watch this to know when to initialise their ObjectSpawner.
        /// </summary>
        [Networked] public NetworkBool IsWorldReady { get; set; }

        private ObjectSpawner _spawner;
        private MatchTimer _matchTimer;
        private BitArray _consumedSet;
        private bool _localSpawnerInitialised;

        // Reusable list to avoid per-tick allocations
        private readonly List<CandidateEntry> _candidates = new List<CandidateEntry>();
        // Reusable set of objects to consume this tick (avoids modifying during iteration)
        private readonly List<ConsumeResult> _consumeResults = new List<ConsumeResult>();

        private const float DistanceEpsilon = 0.01f;

        private struct CandidateEntry
        {
            public HolePlayer Player;
            public float Distance;
        }

        private struct ConsumeResult
        {
            public int ObjectId;
            public HolePlayer Winner;
            public ConsumableObject Consumable;
        }

        public override void Spawned()
        {
            _spawner = FindAnyObjectByType<ObjectSpawner>();
            _matchTimer = FindAnyObjectByType<MatchTimer>();

            if (Object.HasStateAuthority)
            {
                EventSeq = 0;
                IsWorldReady = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (_spawner == null)
            {
                _spawner = FindAnyObjectByType<ObjectSpawner>();
            }

            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            // Host: generate seed when countdown starts
            if (Object.HasStateAuthority && !IsWorldReady
                && _matchTimer != null && _matchTimer.IsCountdown)
            {
                MapSeed = (int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF);
                IsWorldReady = true;
                InitialiseLocalSpawner();
            }

            // All clients: initialise spawner once seed is ready
            if (!_localSpawnerInitialised && IsWorldReady && _spawner != null)
            {
                InitialiseLocalSpawner();
            }

            // All clients: spawn waves during Playing phase
            if (_localSpawnerInitialised && _matchTimer != null && _matchTimer.IsPlaying)
            {
                _spawner.UpdateWaveSpawning();
            }

            // Host only: evaluate consumption during Playing phase
            if (!Object.HasStateAuthority)
                return;

            if (_matchTimer == null || !_matchTimer.IsPlaying)
                return;

            EvaluateConsumption();
        }

        private void InitialiseLocalSpawner()
        {
            if (_spawner == null || _localSpawnerInitialised)
                return;

            _spawner.Initialise(MapSeed);
            _spawner.SpawnInitialBatch();
            _consumedSet = new BitArray(_spawner.TotalSpawned + 256);
            _localSpawnerInitialised = true;
        }

        private void EvaluateConsumption()
        {
            // Find all active players
            HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
            if (players.Length == 0)
                return;

            _consumeResults.Clear();

            foreach (ConsumableObject obj in _spawner.GetActiveObjects())
            {
                if (obj == null || obj.IsConsumed)
                    continue;

                // Ensure consumed set is large enough
                if (obj.ObjectId >= _consumedSet.Length)
                {
                    _consumedSet.Length = obj.ObjectId + 128;
                }

                if (_consumedSet[obj.ObjectId])
                    continue;

                Vector3 objPos = obj.transform.position;
                _candidates.Clear();

                foreach (HolePlayer player in players)
                {
                    if (player.Object == null)
                        continue;

                    // Eligibility: player's hole must be large enough
                    if (player.HoleRadius < obj.ObjectSize)
                        continue;

                    // Distance check: object must be within the hole's consume radius
                    Vector3 playerPos = player.transform.position;
                    float dist = Vector3.Distance(
                        new Vector3(playerPos.x, 0f, playerPos.z),
                        new Vector3(objPos.x, 0f, objPos.z));

                    if (dist <= player.HoleRadius)
                    {
                        _candidates.Add(new CandidateEntry
                        {
                            Player = player,
                            Distance = dist
                        });
                    }
                }

                if (_candidates.Count == 0)
                    continue;

                // Deterministic winner selection
                HolePlayer winner = SelectWinner(_candidates);
                _consumeResults.Add(new ConsumeResult
                {
                    ObjectId = obj.ObjectId,
                    Winner = winner,
                    Consumable = obj
                });
            }

            // Process results outside the iterator
            foreach (ConsumeResult result in _consumeResults)
            {
                ProcessConsume(result);
            }
        }

        private HolePlayer SelectWinner(List<CandidateEntry> candidates)
        {
            CandidateEntry best = candidates[0];

            for (int i = 1; i < candidates.Count; i++)
            {
                CandidateEntry c = candidates[i];

                float distDiff = c.Distance - best.Distance;

                if (distDiff < -DistanceEpsilon)
                {
                    // Strictly closer
                    best = c;
                }
                else if (Mathf.Abs(distDiff) <= DistanceEpsilon)
                {
                    // Tie on distance — larger radius wins
                    if (c.Player.HoleRadius > best.Player.HoleRadius)
                    {
                        best = c;
                    }
                    else if (Mathf.Approximately(c.Player.HoleRadius, best.Player.HoleRadius))
                    {
                        // Tie on radius — lowest PlayerId wins
                        if (c.Player.Object.InputAuthority.PlayerId
                            < best.Player.Object.InputAuthority.PlayerId)
                        {
                            best = c;
                        }
                    }
                }
                // else: c is farther, skip
            }

            return best.Player;
        }

        private void ProcessConsume(ConsumeResult result)
        {
            HolePlayer winner = result.Winner;
            ConsumableObject consumable = result.Consumable;

            // Update server-side consumed set
            _consumedSet[result.ObjectId] = true;
            _spawner.MarkConsumed(result.ObjectId);

            // Update winner's networked state
            winner.Score += consumable.PointValue;
            float newArea = Mathf.Min(
                winner.HoleArea + consumable.SizeValue,
                winner.MaxArea);
            winner.HoleArea = newArea;
            winner.HoleRadius = Mathf.Sqrt(newArea / Mathf.PI);

            // Increment event sequence
            int seq = ++EventSeq;

            // Broadcast to all clients
            RPC_ObjectConsumed(
                result.ObjectId,
                winner.Object.InputAuthority.PlayerId,
                winner.HoleArea,
                winner.Score,
                seq,
                winner.transform.position);
        }

        /// <summary>
        /// Authoritative consume event broadcast from host to all clients.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ObjectConsumed(
            int objectId,
            int eaterPlayerId,
            float eaterHoleAreaAfter,
            int eaterScoreAfter,
            int eventSeq,
            Vector3 holePosition)
        {
            if (_spawner == null)
                return;

            ConsumableObject obj = _spawner.GetObject(objectId);
            if (obj == null || obj.IsConsumed)
                return;

            // Ensure consumed set tracks on clients too
            if (_consumedSet != null && objectId < _consumedSet.Length)
            {
                _consumedSet[objectId] = true;
            }
            _spawner.MarkConsumed(objectId);

            // Play consume animation on the real object
            obj.PlayConsumeAnimation(holePosition);
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/ConsumeArbiter.cs
git commit -m "feat: add ConsumeArbiter for server-authoritative consumption

Evaluates consumption per tick on host. Deterministic winner selection
(closest, then largest, then lowest playerId). Broadcasts
RPC_ObjectConsumed to all clients. Owns MapSeed and EventSeq.
Part of Phase 3 shared world."
```

---

## Task 5: Wire ConsumeArbiter into NetworkManager

**Goal:** The host spawns a `ConsumeArbiter` NetworkObject alongside the `MatchTimer`. Add a `consumeArbiterPrefab` field to `NetworkManager`.

**Files:**
- Modify: `Assets/Scripts/NetworkManager.cs:17-18,45,82-83,235-241`

**Step 1: Add the arbiter prefab field and property**

In `NetworkManager.cs`, add after line 18 (`matchTimerPrefab`):

```csharp
        [SerializeField] private NetworkPrefabRef consumeArbiterPrefab;
```

Add a public property after the `MatchTimer` property (line 165):

```csharp
        /// <summary>Reference to the ConsumeArbiter once spawned.</summary>
        public ConsumeArbiter ConsumeArbiter { get; private set; }
```

**Step 2: Spawn the arbiter in OnPlayerJoined**

In the `OnPlayerJoined` method, after the MatchTimer spawn block (after line 241), add:

```csharp
                    // Spawn consume arbiter
                    var arbiterObj = runner.Spawn(consumeArbiterPrefab, Vector3.zero, Quaternion.identity);
                    ConsumeArbiter = arbiterObj.GetComponent<ConsumeArbiter>();
```

**Step 3: Reset arbiter in Disconnect**

In the `Disconnect` method, after `MatchTimer = null;` (line 82), add:

```csharp
            ConsumeArbiter = null;
```

**Step 4: Commit**

```bash
git add Assets/Scripts/NetworkManager.cs
git commit -m "feat: wire ConsumeArbiter into NetworkManager

Spawn ConsumeArbiter alongside MatchTimer on first player join.
Expose public property and reset on disconnect.
Part of Phase 3 shared world."
```

---

## Task 6: Update Consumable Prefabs — Remove NetworkObject (Unity Editor)

**Goal:** This is a manual Unity Editor step. The consumable prefabs currently have `NetworkObject` and `NetworkTransform` components that must be removed, since consumables are no longer networked.

**This task cannot be scripted — it must be done in the Unity Editor.**

**Steps:**

1. Open Unity Editor
2. For each consumable prefab in the project:
   - Remove the `NetworkObject` component
   - Remove the `NetworkTransform` component (if present)
   - The `ConsumableObject` script component is now a `MonoBehaviour` — it should auto-update
   - Keep: `Rigidbody`, `Collider`, `MeshRenderer`, `ConsumableObject`
3. Change the `ObjectSpawner` component on the GameManager:
   - The `consumablePrefabs` field type changed from `NetworkPrefabRef[]` to `GameObject[]`
   - Re-assign the prefab references as plain GameObject references
4. Create a **ConsumeArbiter prefab**:
   - Create empty GameObject named "ConsumeArbiter"
   - Add `NetworkObject` component
   - Add `ConsumeArbiter` component
   - Save as prefab
   - Add to Fusion's Network Prefab Source
5. Assign the `consumeArbiterPrefab` field on the `NetworkManager` component
6. Remove `ObjectSpawner` from Fusion's Network Prefab Source (it's no longer a network object)
7. If `ObjectSpawner` was on a NetworkObject in the scene, remove the `NetworkObject` from it — it's now a plain scene MonoBehaviour
8. Save all prefabs and the scene

**Step: Commit prefab/scene changes**

```bash
git add -A Assets/Prefabs/ Assets/Scenes/
git commit -m "chore: update prefabs for Phase 3 shared world

Remove NetworkObject from consumables. Create ConsumeArbiter prefab.
Update ObjectSpawner to use plain GameObject references."
```

---

## Task 7: Integration Testing — Verify Multiplayer Consumption

**Goal:** Manually test that consumption works across host and client.

**Test plan:**

1. **Build standalone player:**
   - File > Build Settings > Build and Run (Standalone)

2. **Host flow (built game):**
   - Click "Host Game"
   - Note the join code
   - Click "Ready"

3. **Client flow (Unity Editor):**
   - Press Play
   - Click "Join Match", enter join code
   - Click "Ready"

4. **Start match (host):**
   - Click "Start Match"
   - Verify countdown appears on both

5. **Verify shared world:**
   - [ ] Both instances show the same objects at the same positions
   - [ ] Objects spawn in waves after the initial batch

6. **Verify consumption:**
   - [ ] Move hole over an object — it disappears on BOTH instances
   - [ ] Score and size update on the consuming player
   - [ ] The other player sees the object disappear but their score doesn't change
   - [ ] Consume animation (shrink/spin/sink) plays on both instances

7. **Verify no double-consume:**
   - [ ] Move both holes toward the same object simultaneously
   - [ ] Only one player gets the score
   - [ ] Object disappears on both instances

8. **Verify wave spawning:**
   - [ ] New objects appear during the match
   - [ ] Both instances see the same new objects at the same positions

9. **Verify match end:**
   - [ ] Timer reaches 0
   - [ ] End screen shows correct final score and size

---

## Task 8: Commit and Update Issue

**Step 1: Final commit message**

If any loose changes remain:

```bash
git add -A
git commit -m "feat: complete Phase 3 shared world authoritative consumption

All consumable objects spawned deterministically from shared seed.
ConsumeArbiter on host evaluates and arbitrates consumption per tick.
RPC_ObjectConsumed broadcasts to all clients. No double-consume.
Closes #5."
```

**Step 2: Update GitHub issue**

```bash
gh issue close 5 --comment "Phase 3 implementation complete. All deliverables met:
- Deterministic seed-based spawning (hybrid: initial batch + waves)
- Object ID scheme with dictionary registry
- Server-authoritative ConsumeArbiter with deterministic arbitration
- RPC_ObjectConsumed broadcast to all clients
- Real-object consume animation (shrink/spin/sink)
- Event sequencing with monotonic eventSeq"
```

---

## Dependency Order

```
Task 1 (ConsumableObject) ──┐
                             ├── Task 4 (ConsumeArbiter) ── Task 5 (NetworkManager) ── Task 6 (Prefabs) ── Task 7 (Test)
Task 2 (ObjectSpawner) ──────┘
Task 3 (HolePlayer) ─────────┘
```

Tasks 1, 2, and 3 can be done in parallel. Task 4 depends on all three. Tasks 5-8 are sequential.
