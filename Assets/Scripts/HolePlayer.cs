using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Network player component for the hole.
    /// Uses Fusion input system for client-side prediction.
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

        // Networked properties — synchronised by Fusion
        [Networked] public float HoleRadius { get; set; }
        [Networked] public float HoleArea { get; set; }
        [Networked] public int Score { get; set; }

        // Lobby properties
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }

        // Movement state — networked for rollback prediction
        [Networked] private Vector3 Velocity { get; set; }

        private ObjectSpawner _spawner;
        private MatchTimer _matchTimer;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                HoleRadius = initialRadius;
                HoleArea = Mathf.PI * initialRadius * initialRadius;
                Score = 0;
                IsReady = false;
            }

            _spawner = FindAnyObjectByType<ObjectSpawner>();
            _matchTimer = FindAnyObjectByType<MatchTimer>();
            UpdateHoleScale();
        }

        public override void FixedUpdateNetwork()
        {
            // Only move during the Playing phase
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

            // Normalise to prevent speed cheating (magnitude clamped to 1)
            Vector2 rawInput = data.MoveDirection;
            if (rawInput.sqrMagnitude > 1f)
            {
                rawInput.Normalize();
            }

            Vector3 inputDirection = new Vector3(rawInput.x, 0f, rawInput.y);
            Vector3 vel = Velocity;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                // Accelerate towards target velocity
                Vector3 targetVelocity = inputDirection * moveSpeed;
                vel = Vector3.MoveTowards(vel, targetVelocity, acceleration * dt);

                // Rotate towards movement direction
                if (vel.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(vel.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, targetRotation, rotationSpeed * dt);
                }
            }
            else
            {
                // Decelerate to stop
                vel = Vector3.MoveTowards(vel, Vector3.zero, deceleration * dt);
            }

            Velocity = vel;
            transform.position += vel * dt;
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

            // Only consume during Playing phase
            if (_matchTimer == null || !_matchTimer.IsPlaying)
                return;

            ConsumableObject consumable = other.GetComponent<ConsumableObject>();
            if (consumable != null && consumable.CanBeConsumed(HoleRadius))
            {
                ConsumeObject(consumable);
            }
        }

        private void ConsumeObject(ConsumableObject consumable)
        {
            Score += consumable.PointValue;

            float maxArea = Mathf.PI * maxRadius * maxRadius;
            HoleArea = Mathf.Min(HoleArea + consumable.SizeValue, maxArea);
            HoleRadius = Mathf.Sqrt(HoleArea / Mathf.PI);

            if (_spawner != null)
            {
                _spawner.OnObjectConsumed();
            }

            consumable.Consume(transform.position);
        }

        /// <summary>
        /// Toggle ready state. Called via RPC from input authority.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }

        /// <summary>
        /// Set player name. Called via RPC from input authority on spawn.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(NetworkString<_16> name)
        {
            PlayerName = name;
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
