using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Spawns consumable objects randomly in the game world.
    /// Only spawns during the Playing phase.
    /// </summary>
    public class ObjectSpawner : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private NetworkPrefabRef[] consumablePrefabs;
        [SerializeField] private int maxObjects = 50;
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private Vector3 spawnAreaSize = new Vector3(40, 10, 40);
        [SerializeField] private float spawnHeight = 10f;

        [Networked] private TickTimer SpawnTimer { get; set; }
        private int currentObjectCount = 0;
        private MatchTimer _matchTimer;
        private bool _matchStartHandled;

        public override void Spawned()
        {
            _matchTimer = FindAnyObjectByType<MatchTimer>();

            if (Object.HasStateAuthority)
            {
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            // Only spawn objects during the Playing phase
            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            if (_matchTimer == null || !_matchTimer.IsPlaying)
                return;

            // Reset count when match starts
            if (!_matchStartHandled)
            {
                currentObjectCount = 0;
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
                _matchStartHandled = true;
            }

            if (SpawnTimer.Expired(Runner))
            {
                if (currentObjectCount < maxObjects && consumablePrefabs.Length > 0)
                {
                    SpawnRandomObject();
                }
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            }
        }

        private void SpawnRandomObject()
        {
            int prefabIndex = Random.Range(0, consumablePrefabs.Length);
            NetworkPrefabRef prefab = consumablePrefabs[prefabIndex];

            Vector3 randomPos = new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                spawnHeight,
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            NetworkObject spawnedObject = Runner.Spawn(prefab, randomPos, Quaternion.identity);

            if (spawnedObject != null)
            {
                currentObjectCount++;
            }
        }

        public void OnObjectConsumed()
        {
            if (currentObjectCount > 0)
            {
                currentObjectCount--;
            }
        }
    }
}
