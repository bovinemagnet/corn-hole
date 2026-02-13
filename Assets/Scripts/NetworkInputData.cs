using Fusion;
using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Network input data sent from clients to the host each tick.
    /// Contains only the movement direction — server computes everything else.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        /// <summary>Normalised movement direction from touch/keyboard input.</summary>
        public Vector2 MoveDirection;
    }
}
