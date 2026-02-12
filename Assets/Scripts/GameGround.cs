using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Simple ground plane for the game
    /// </summary>
    public class GameGround : MonoBehaviour
    {
        [Header("Ground Settings")]
        [SerializeField] private Vector2 groundSize = new Vector2(100, 100);
        [SerializeField] private Material groundMaterial;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(groundSize.x, 0.1f, groundSize.y));
        }
    }
}
