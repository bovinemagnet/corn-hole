using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Drives the suction/drop animation for consumed objects.
    /// Attached to a non-networked visual clone created in the RPC handler.
    /// Pulls the clone toward the hole centre, shrinks it, spins it, then self-destructs.
    /// </summary>
    public class ConsumeEffect : MonoBehaviour
    {
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private Vector3 _startScale;
        private float _duration;
        private float _elapsed;
        private bool _initialised;

        private const float MinDuration = 0.2f;
        private const float MaxDuration = 0.5f;
        private const float SpinSpeed = 540f;
        private const float SinkOffset = -0.5f;

        public void Initialise(Vector3 targetPosition, float sizeValue)
        {
            _startPosition = transform.position;
            _targetPosition = targetPosition;
            _startScale = transform.localScale;
            _duration = Mathf.Lerp(MinDuration, MaxDuration, Mathf.Clamp01(sizeValue));
            _elapsed = 0f;
            _initialised = true;
        }

        private void Update()
        {
            if (!_initialised)
                return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Quadratic ease-in — accelerating suction feel
            float easedT = t * t;

            // Position: lerp toward hole centre with downward sink
            Vector3 sinkTarget = _targetPosition + new Vector3(0f, SinkOffset, 0f);
            transform.position = Vector3.Lerp(_startPosition, sinkTarget, easedT);

            // Scale: shrink to nothing
            transform.localScale = Vector3.Lerp(_startScale, Vector3.zero, easedT);

            // Spin: vortex swirl
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
