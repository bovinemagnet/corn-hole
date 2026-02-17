using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Network player component for the hole.
    /// Uses Fusion input system for client-side prediction.
    /// Handles movement and visual scale. Consumption is handled by ConsumeArbiter.
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

        private MatchTimer _matchTimer;

        /// <summary>Maximum hole area, derived from maxRadius.</summary>
        public float MaxArea => Mathf.PI * maxRadius * maxRadius;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                HoleRadius = initialRadius;
                HoleArea = Mathf.PI * initialRadius * initialRadius;
                Score = 0;
                IsReady = false;
            }

            _matchTimer = FindAnyObjectByType<MatchTimer>();
            UpdateHoleScale();

            if (Object.HasInputAuthority)
            {
                RPC_SetPlayerName($"Player {Object.InputAuthority.PlayerId}");
            }
        }

        public override void FixedUpdateNetwork()
        {
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

            Vector2 rawInput = data.MoveDirection;
            if (rawInput.sqrMagnitude > 1f)
            {
                rawInput.Normalize();
            }

            Vector3 inputDirection = new Vector3(rawInput.x, 0f, rawInput.y);
            Vector3 vel = Velocity;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 targetVelocity = inputDirection * moveSpeed;
                vel = Vector3.MoveTowards(vel, targetVelocity, acceleration * dt);

                if (vel.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(vel.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, targetRotation, rotationSpeed * dt);
                }
            }
            else
            {
                vel = Vector3.MoveTowards(vel, Vector3.zero, deceleration * dt);
            }

            Velocity = vel;
            transform.position += vel * dt;
        }

        private void UpdateHoleScale()
        {
            if (holeVisual != null)
            {
                float scale = HoleRadius * 2f;
                holeVisual.localScale = new Vector3(scale, 1f, scale);
            }

            if (consumeCollider != null)
            {
                consumeCollider.radius = HoleRadius;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(NetworkString<_16> name)
        {
            PlayerName = name;
        }
    }
}
