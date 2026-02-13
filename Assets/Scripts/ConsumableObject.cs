using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Represents an object that can be consumed by the hole.
    /// Includes particle and audio feedback scaled by object size.
    /// </summary>
    public class ConsumableObject : NetworkBehaviour
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

        [Networked] public bool IsConsumed { get; set; }

        public int PointValue => pointValue;
        public float SizeValue => sizeValue;

        public override void Spawned()
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

            IsConsumed = false;
        }

        public bool CanBeConsumed(float holeRadius)
        {
            // Object can only be consumed if it's smaller than the hole
            // and if it hasn't already been consumed
            return !IsConsumed && objectSize <= holeRadius;
        }

        public void Consume(Vector3 holePosition)
        {
            if (Object.HasStateAuthority && !IsConsumed)
            {
                IsConsumed = true;
                RPC_PlayConsumeEffect(holePosition);
                Runner.Despawn(Object);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayConsumeEffect(Vector3 holePosition)
        {
            // Suction animation — visual clone that outlives the despawned object
            CreateVisualClone(holePosition);

            // Scale intensity by object size for proportional feedback
            float intensity = Mathf.Clamp01(sizeValue);

            // Particle effect — detach so it outlives the despawned object
            if (consumeParticles != null)
            {
                consumeParticles.transform.SetParent(null);

                var main = consumeParticles.main;
                main.startSizeMultiplier *= (1f + intensity);

                consumeParticles.Play();

                // Auto-destroy the orphaned particle system after its duration
                Destroy(consumeParticles.gameObject, main.duration + main.startLifetime.constantMax);
            }

            // Audio — volume scaled by size
            if (consumeSound != null)
            {
                float volume = baseSoundVolume * (0.5f + intensity);
                AudioSource.PlayClipAtPoint(consumeSound, transform.position, volume);
            }
        }

        private void CreateVisualClone(Vector3 holePosition)
        {
            var sourceRenderer = GetComponentInChildren<MeshRenderer>();
            if (sourceRenderer == null)
                return;

            var sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
                return;

            var clone = new GameObject("ConsumeClone");
            clone.transform.position = sourceRenderer.transform.position;
            clone.transform.rotation = sourceRenderer.transform.rotation;
            clone.transform.localScale = sourceRenderer.transform.lossyScale;

            var meshFilter = clone.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            var meshRenderer = clone.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            clone.AddComponent<ConsumeEffect>().Initialise(holePosition, sizeValue);
        }
    }
}
