using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Inspector-facing tuning for one placed prop variant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePropAuthoring2D : MonoBehaviour
    {
        [Min(0.01f)]
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private Vector2 colliderSize = new Vector2(2.2f, 1.35f);
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [Tooltip("Optional. Leave empty until destruction sprites are ready.")]
        [SerializeField]
        private DestructiblePropDestructionAnimation destructionAnimation;

        public double MaximumHealth => maximumHealth;

        public Vector2 ColliderSize => colliderSize;

        public Vector2 ColliderOffset => colliderOffset;

        public DestructiblePropDestructionAnimation DestructionAnimation =>
            destructionAnimation;

        public void ConfigureGenerated(
            double configuredMaximumHealth,
            Vector2 configuredColliderSize,
            Vector2 configuredColliderOffset,
            DestructiblePropDestructionAnimation configuredAnimation)
        {
            Validate(configuredMaximumHealth, configuredColliderSize);
            maximumHealth = (float)configuredMaximumHealth;
            colliderSize = configuredColliderSize;
            colliderOffset = configuredColliderOffset;
            destructionAnimation = configuredAnimation;
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(0.01f, maximumHealth);
            colliderSize.x = Mathf.Max(0.01f, colliderSize.x);
            colliderSize.y = Mathf.Max(0.01f, colliderSize.y);
        }

        private static void Validate(
            double configuredMaximumHealth,
            Vector2 configuredColliderSize)
        {
            if (double.IsNaN(configuredMaximumHealth)
                || double.IsInfinity(configuredMaximumHealth)
                || configuredMaximumHealth <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredMaximumHealth));
            }

            if (!IsPositiveFinite(configuredColliderSize.x)
                || !IsPositiveFinite(configuredColliderSize.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredColliderSize));
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
