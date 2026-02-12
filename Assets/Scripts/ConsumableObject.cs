using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Represents an object that can be consumed by the hole
    /// </summary>
    public class ConsumableObject : NetworkBehaviour
    {
        [Header("Object Properties")]
        [SerializeField] private float objectSize = 1f;
        [SerializeField] private int pointValue = 10;
        [SerializeField] private float sizeValue = 0.1f;

        [Header("Physics")]
        [SerializeField] private Rigidbody rb;

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
            }

            IsConsumed = false;
        }

        public bool CanBeConsumed(float holeRadius)
        {
            // Object can only be consumed if it's smaller than the hole
            // and if it hasn't already been consumed
            return !IsConsumed && objectSize <= holeRadius;
        }

        public void Consume()
        {
            if (Object.HasStateAuthority && !IsConsumed)
            {
                IsConsumed = true;
                RPC_PlayConsumeEffect();
                Runner.Despawn(Object);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayConsumeEffect()
        {
            // Play particle effect or sound here
            // For now, just destroy immediately
        }
    }
}
