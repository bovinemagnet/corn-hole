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
