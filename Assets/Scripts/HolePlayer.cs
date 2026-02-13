using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Network Player component for the hole.
    /// Handles movement, area-based growth, and consuming objects.
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

        // Networked properties
        [Networked] public float HoleRadius { get; set; }
        [Networked] public float HoleArea { get; set; }
        [Networked] public int Score { get; set; }
        [Networked] public Vector3 Position { get; set; }

        private Vector3 _velocity;
        private ObjectSpawner _spawner;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                HoleRadius = initialRadius;
                HoleArea = Mathf.PI * initialRadius * initialRadius;
                Score = 0;
                Position = transform.position;
            }

            _spawner = FindAnyObjectByType<ObjectSpawner>();
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
            float dt = Runner.DeltaTime;
            Vector3 inputDirection = Vector3.zero;

#if UNITY_ANDROID || UNITY_IOS
            // Mobile touch input
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Vector3 touchWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, 10f));
                inputDirection = (touchWorldPos - transform.position).normalized;
                inputDirection.y = 0;
            }
#else
            // Desktop input for testing — use GetAxisRaw to avoid Unity's built-in smoothing
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            inputDirection = new Vector3(horizontal, 0, vertical);

            // Clamp magnitude to 1 to prevent faster diagonal movement
            if (inputDirection.sqrMagnitude > 1f)
            {
                inputDirection.Normalize();
            }
#endif

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                // Accelerate towards target velocity
                Vector3 targetVelocity = inputDirection * moveSpeed;
                _velocity = Vector3.MoveTowards(_velocity, targetVelocity, acceleration * dt);

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(_velocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * dt);
            }
            else
            {
                // Decelerate to stop
                _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, deceleration * dt);
            }

            transform.position += _velocity * dt;
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

            // Grow the hole using area-based formula
            float maxArea = Mathf.PI * maxRadius * maxRadius;
            HoleArea = Mathf.Min(HoleArea + consumable.SizeValue, maxArea);
            HoleRadius = Mathf.Sqrt(HoleArea / Mathf.PI);

            // Notify the spawner so it can decrement its count
            if (_spawner != null)
            {
                _spawner.OnObjectConsumed();
            }

            // Destroy the consumable (pass hole position for suction animation)
            consumable.Consume(transform.position);
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
