using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Shared.Presentation
{
    /// <summary>
    /// Optional short-lived presentation marker detached from a projectile on a
    /// confirmed hit. It owns no damage, target identity, target selection, audio,
    /// or final visual-effect policy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TemporaryHitPresentation : MonoBehaviour
    {
        public const float MinimumLifetimeSeconds = 0.01f;
        public const float MaximumLifetimeSeconds = 1f;
        public const float DefaultLifetimeSeconds = 0.12f;

        [SerializeField, Min(MinimumLifetimeSeconds)]
        private float defaultLifetimeSeconds = DefaultLifetimeSeconds;

        private float remainingLifetimeSeconds;
        private bool isPlaying;

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        public float RemainingLifetimeSeconds
        {
            get { return remainingLifetimeSeconds; }
        }

        public float DefaultConfiguredLifetimeSeconds
        {
            get { return defaultLifetimeSeconds; }
        }

        public bool TryPlay(Vector2 worldPosition)
        {
            return TryPlay(worldPosition, defaultLifetimeSeconds);
        }

        public bool TryPlay(Vector2 worldPosition, float lifetimeSeconds)
        {
            if (isPlaying
                || !IsFinite(worldPosition.x)
                || !IsFinite(worldPosition.y)
                || !IsValidLifetime(lifetimeSeconds))
            {
                return false;
            }

            transform.SetParent(null, true);
            transform.position = worldPosition;
            remainingLifetimeSeconds = lifetimeSeconds;
            isPlaying = true;
            gameObject.SetActive(true);
            return true;
        }

        public void Cancel()
        {
            if (!isPlaying)
            {
                return;
            }

            isPlaying = false;
            remainingLifetimeSeconds = 0f;
            Destroy(gameObject);
        }

        public static bool IsValidLifetime(float lifetimeSeconds)
        {
            return IsFinite(lifetimeSeconds)
                && lifetimeSeconds >= MinimumLifetimeSeconds
                && lifetimeSeconds <= MaximumLifetimeSeconds;
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            remainingLifetimeSeconds -= Time.deltaTime;
            if (remainingLifetimeSeconds > 0f)
            {
                return;
            }

            isPlaying = false;
            remainingLifetimeSeconds = 0f;
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (!isPlaying)
            {
                remainingLifetimeSeconds = 0f;
            }
        }

        private void OnValidate()
        {
            if (!IsValidLifetime(defaultLifetimeSeconds))
            {
                defaultLifetimeSeconds = DefaultLifetimeSeconds;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
