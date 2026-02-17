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
