using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Makes the camera follow the local player's hole
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0, 15, -10);
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private bool lookAtTarget = true;

        private Transform target;
        private HolePlayer localPlayer;

        private void Update()
        {
            if (target == null)
            {
                FindLocalPlayer();
            }
            else
            {
                FollowTarget();
            }
        }

        private void FindLocalPlayer()
        {
            if (localPlayer == null)
            {
                HolePlayer[] players = FindObjectsOfType<HolePlayer>();
                foreach (var player in players)
                {
                    if (player.Object != null && player.Object.HasInputAuthority)
                    {
                        localPlayer = player;
                        target = player.transform;
                        break;
                    }
                }
            }
        }

        private void FollowTarget()
        {
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;

            if (lookAtTarget)
            {
                transform.LookAt(target);
            }
        }
    }
}
