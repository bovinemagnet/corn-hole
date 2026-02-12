using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Network Player component for the hole
    /// Handles movement, growth, and consuming objects
    /// </summary>
    public class HolePlayer : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Hole Settings")]
        [SerializeField] private float initialRadius = 1f;
        [SerializeField] private float maxRadius = 10f;
        [SerializeField] private float growthRate = 0.1f;

        [Header("References")]
        [SerializeField] private Transform holeVisual;
        [SerializeField] private SphereCollider consumeCollider;

        // Networked properties
        [Networked] public float HoleRadius { get; set; }
        [Networked] public int Score { get; set; }
        [Networked] public Vector3 Position { get; set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                HoleRadius = initialRadius;
                Score = 0;
                Position = transform.position;
            }

            UpdateHoleScale();
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
            {
                HandleMovement();
                Position = transform.position;
            }
            else
            {
                // Interpolate position for remote players
                transform.position = Position;
            }

            UpdateHoleScale();
        }

        private void HandleMovement()
        {
            Vector3 moveDirection = Vector3.zero;

#if UNITY_ANDROID || UNITY_IOS
            // Mobile touch input
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Vector3 touchWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, 10f));
                moveDirection = (touchWorldPos - transform.position).normalized;
                moveDirection.y = 0;
            }
#else
            // Desktop input for testing
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            moveDirection = new Vector3(horizontal, 0, vertical).normalized;
#endif

            if (moveDirection != Vector3.zero)
            {
                // Move the hole
                transform.position += moveDirection * moveSpeed * Runner.DeltaTime;

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
            }
        }

        private void UpdateHoleScale()
        {
            if (holeVisual != null)
            {
                float scale = HoleRadius * 2f; // Diameter
                holeVisual.localScale = new Vector3(scale, 1f, scale);
            }

            if (consumeCollider != null)
            {
                consumeCollider.radius = HoleRadius;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

            ConsumableObject consumable = other.GetComponent<ConsumableObject>();
            if (consumable != null && consumable.CanBeConsumed(HoleRadius))
            {
                ConsumeObject(consumable);
            }
        }

        private void ConsumeObject(ConsumableObject consumable)
        {
            // Increase score
            Score += consumable.PointValue;

            // Grow the hole
            HoleRadius = Mathf.Min(HoleRadius + growthRate * consumable.SizeValue, maxRadius);

            // Destroy the consumable
            consumable.Consume();
        }

        public void OnPlayerLeft()
        {
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }
    }
}
