using System;
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Manages match lifecycle: Lobby → Countdown → Playing → Ended.
    /// Networked — only the host/state authority modifies phase and timers.
    /// </summary>
    public class MatchTimer : NetworkBehaviour
    {
        public const int PhaseLobby = 0;
        public const int PhaseCountdown = 1;
        public const int PhasePlaying = 2;
        public const int PhaseEnded = 3;

        [Header("Match Settings")]
        [SerializeField] private float matchDurationSeconds = 120f;
        [SerializeField] private float countdownDurationSeconds = 3f;

        /// <summary>Current match phase (see Phase* constants).</summary>
        [Networked] public int MatchPhase { get; set; }

        /// <summary>Time remaining in the current phase (countdown or playing).</summary>
        [Networked] public float RemainingTime { get; set; }

        /// <summary>Fired on all clients when the countdown begins.</summary>
        public event Action OnCountdownStarted;

        /// <summary>Fired on all clients when gameplay starts.</summary>
        public event Action OnMatchStarted;

        /// <summary>Fired on all clients when the match ends.</summary>
        public event Action OnMatchEnded;

        public bool IsLobby => MatchPhase == PhaseLobby;
        public bool IsCountdown => MatchPhase == PhaseCountdown;
        public bool IsPlaying => MatchPhase == PhasePlaying;
        public bool HasEnded => MatchPhase == PhaseEnded;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                // Start in lobby — wait for host to trigger countdown
                MatchPhase = PhaseLobby;
                RemainingTime = 0f;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (MatchPhase == PhaseCountdown)
            {
                RemainingTime -= Runner.DeltaTime;
                if (RemainingTime <= 0f)
                {
                    RemainingTime = matchDurationSeconds;
                    MatchPhase = PhasePlaying;
                    RPC_NotifyMatchStarted();
                }
            }
            else if (MatchPhase == PhasePlaying)
            {
                RemainingTime -= Runner.DeltaTime;
                if (RemainingTime <= 0f)
                {
                    RemainingTime = 0f;
                    MatchPhase = PhaseEnded;
                    RPC_NotifyMatchEnded();
                }
            }
        }

        /// <summary>
        /// Called by the host to begin the countdown. Only works from Lobby phase.
        /// </summary>
        public void StartCountdown()
        {
            if (!Object.HasStateAuthority || MatchPhase != PhaseLobby)
                return;

            MatchPhase = PhaseCountdown;
            RemainingTime = countdownDurationSeconds;
            RPC_NotifyCountdownStarted();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCountdownStarted()
        {
            OnCountdownStarted?.Invoke();
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
