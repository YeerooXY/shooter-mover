using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.RocketLauncher.Presentation
{
    /// <summary>
    /// Bounded presentation-only warning envelope. It intentionally contains no
    /// renderer, canvas, audio, damage, target-selection, or screen-space authority.
    /// Later presentation work may read this marker without changing rocket rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RocketImpactWarning2D : MonoBehaviour
    {
        public const float MinimumLifetimeSeconds = 0.01f;
        public const float MaximumLifetimeSeconds = 0.5f;
        public const float MaximumRadius = 10f;

        private float radius;
        private float remainingLifetimeSeconds;
        private bool isArmed;

        public bool IsArmed
        {
            get { return isArmed; }
        }

        public float Radius
        {
            get { return radius; }
        }

        public float RemainingLifetimeSeconds
        {
            get { return remainingLifetimeSeconds; }
        }

        public bool TryArm(float warningRadius, float lifetimeSeconds)
        {
            if (isArmed
                || !IsValidRadius(warningRadius)
                || !IsValidLifetime(lifetimeSeconds))
            {
                return false;
            }

            radius = warningRadius;
            remainingLifetimeSeconds = lifetimeSeconds;
            isArmed = true;
            return true;
        }

        public void Cancel()
        {
            isArmed = false;
            radius = 0f;
            remainingLifetimeSeconds = 0f;
        }

        public static bool IsValidRadius(float warningRadius)
        {
            return IsFinite(warningRadius)
                && warningRadius > 0f
                && warningRadius <= MaximumRadius;
        }

        public static bool IsValidLifetime(float lifetimeSeconds)
        {
            return IsFinite(lifetimeSeconds)
                && lifetimeSeconds >= MinimumLifetimeSeconds
                && lifetimeSeconds <= MaximumLifetimeSeconds;
        }

        private void Update()
        {
            if (!isArmed)
            {
                return;
            }

            remainingLifetimeSeconds -= Time.deltaTime;
            if (remainingLifetimeSeconds <= 0f)
            {
                Cancel();
            }
        }

        private void OnDisable()
        {
            Cancel();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
