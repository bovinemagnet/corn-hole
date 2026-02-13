using System;
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Manages match lifecycle: waiting, playing, and ended phases
    /// with a networked countdown timer.
    /// </summary>
    public class MatchTimer : NetworkBehaviour
    {
        [Header("Match Settings")]
        [SerializeField] private float matchDurationSeconds = 120f;

        /// <summary>Match phase: 0 = Waiting, 1 = Playing, 2 = Ended.</summary>
        [Networked] public int MatchPhase { get; set; }

        /// <summary>Time remaining in seconds.</summary>
        [Networked] public float RemainingTime { get; set; }

        /// <summary>Fired on all clients when the match starts.</summary>
        public event Action OnMatchStarted;

        /// <summary>Fired on all clients when the match ends.</summary>
        public event Action OnMatchEnded;

        public bool IsPlaying => MatchPhase == 1;
        public bool HasEnded => MatchPhase == 2;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                // Auto-start immediately for Phase 0 simplicity
                MatchPhase = 1;
                RemainingTime = matchDurationSeconds;
                RPC_NotifyMatchStarted();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (MatchPhase != 1)
                return;

            RemainingTime -= Runner.DeltaTime;

            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f;
                MatchPhase = 2;
                RPC_NotifyMatchEnded();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyMatchStarted()
        {
            OnMatchStarted?.Invoke();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyMatchEnded()
        {
            OnMatchEnded?.Invoke();
        }
    }
}
