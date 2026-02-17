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
