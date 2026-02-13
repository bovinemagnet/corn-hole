using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Makes the camera follow the local player's hole
    /// with dynamic zoom based on hole size.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0, 15, -10);
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private bool lookAtTarget = true;

        [Header("Zoom Settings")]
        [SerializeField] private float minHeight = 12f;
        [SerializeField] private float maxHeight = 35f;
        [SerializeField] private float minRadius = 1f;
        [SerializeField] private float maxRadius = 10f;
        [SerializeField] private float zoomSmoothSpeed = 3f;

        private Transform target;
        private HolePlayer localPlayer;
        private float _currentZoomHeight;

        private void Start()
        {
            _currentZoomHeight = minHeight;
        }

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
                HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
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
            // Calculate dynamic zoom height based on hole radius
            float radiusFraction = Mathf.InverseLerp(minRadius, maxRadius, localPlayer.HoleRadius);
            float targetZoomHeight = Mathf.Lerp(minHeight, maxHeight, radiusFraction);
            _currentZoomHeight = Mathf.Lerp(_currentZoomHeight, targetZoomHeight, zoomSmoothSpeed * Time.deltaTime);

            // Apply dynamic offset with zoom height
            Vector3 dynamicOffset = new Vector3(offset.x, _currentZoomHeight, offset.z);
            Vector3 desiredPosition = target.position + dynamicOffset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;

            if (lookAtTarget)
            {
                transform.LookAt(target);
            }
        }
    }
}
