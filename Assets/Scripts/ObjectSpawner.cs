using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Spawns consumable objects randomly in the game world
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

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

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
            // Select random prefab
            int prefabIndex = Random.Range(0, consumablePrefabs.Length);
            NetworkPrefabRef prefab = consumablePrefabs[prefabIndex];

            // Random position within spawn area
            Vector3 randomPos = new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                spawnHeight,
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            // Spawn the object
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
