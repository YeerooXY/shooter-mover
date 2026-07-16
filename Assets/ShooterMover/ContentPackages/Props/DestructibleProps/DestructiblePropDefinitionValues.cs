using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    public enum DestructiblePropColliderShape
    {
        UseAssigned = 0,
        Box = 1,
        Circle = 2,
        Capsule = 3,
    }

    public enum DestructiblePropDestroyedColliderPolicy
    {
        Disable = 0,
        KeepEnabled = 1,
        ConvertToTrigger = 2,
    }

    [Flags]
    public enum DestructiblePropValueOverrideMask
    {
        None = 0,
        MaximumHealth = 1 << 0,
        ColliderShape = 1 << 1,
        ColliderSize = 1 << 2,
        ColliderOffset = 1 << 3,
        IntactPresentation = 1 << 4,
        DestructionAnimation = 1 << 5,
        DestroyedColliderPolicy = 1 << 6,
        RewardProfile = 1 << 7,
        All = MaximumHealth
            | ColliderShape
            | ColliderSize
            | ColliderOffset
            | IntactPresentation
            | DestructionAnimation
            | DestroyedColliderPolicy
            | RewardProfile,
    }

    [Serializable]
    public sealed class DestructiblePropDefinitionValuesAuthoring
    {
        [Min(0.01f)]
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private DestructiblePropColliderShape colliderShape =
            DestructiblePropColliderShape.Box;
        [SerializeField] private Vector2 colliderSize = new Vector2(2.2f, 1.35f);
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [SerializeField] private Sprite intactSprite;
        [SerializeField] private string intactPresentationId = "presentation.none";
        [SerializeField] private DestructiblePropDestructionAnimation destructionAnimation;
        [SerializeField] private string destructionAnimationId = "animation.none";
        [SerializeField] private DestructiblePropDestroyedColliderPolicy destroyedColliderPolicy =
            DestructiblePropDestroyedColliderPolicy.Disable;
        [SerializeField] private ScriptableObject rewardProfileSource;
        [SerializeField] private string rewardProfileId = "reward-profile.none";

        public DestructiblePropResolvedValues Build()
        {
            return new DestructiblePropResolvedValues(
                maximumHealth,
                colliderShape,
                colliderSize,
                colliderOffset,
                intactSprite,
                StableId.Parse(intactPresentationId),
                destructionAnimation,
                StableId.Parse(destructionAnimationId),
                destroyedColliderPolicy,
                rewardProfileSource,
                StableId.Parse(rewardProfileId));
        }

        public static DestructiblePropDefinitionValuesAuthoring CreateRuntime(
            double maximumHealth,
            DestructiblePropColliderShape colliderShape,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            Sprite intactSprite,
            string intactPresentationId,
            DestructiblePropDestructionAnimation destructionAnimation,
            string destructionAnimationId,
            DestructiblePropDestroyedColliderPolicy destroyedColliderPolicy,
            ScriptableObject rewardProfileSource,
            string rewardProfileId)
        {
            var values = new DestructiblePropDefinitionValuesAuthoring
            {
                maximumHealth = (float)maximumHealth,
                colliderShape = colliderShape,
                colliderSize = colliderSize,
                colliderOffset = colliderOffset,
                intactSprite = intactSprite,
                intactPresentationId = intactPresentationId,
                destructionAnimation = destructionAnimation,
                destructionAnimationId = destructionAnimationId,
                destroyedColliderPolicy = destroyedColliderPolicy,
                rewardProfileSource = rewardProfileSource,
                rewardProfileId = rewardProfileId,
            };
            values.Build();
            return values;
        }
    }

    public sealed class DestructiblePropResolvedValues
    {
        public DestructiblePropResolvedValues(
            double maximumHealth,
            DestructiblePropColliderShape colliderShape,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            Sprite intactSprite,
            StableId intactPresentationId,
            DestructiblePropDestructionAnimation destructionAnimation,
            StableId destructionAnimationId,
            DestructiblePropDestroyedColliderPolicy destroyedColliderPolicy,
            ScriptableObject rewardProfileSource,
            StableId rewardProfileId)
        {
            if (double.IsNaN(maximumHealth)
                || double.IsInfinity(maximumHealth)
                || maximumHealth <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            if (!IsPositiveFinite(colliderSize.x) || !IsPositiveFinite(colliderSize.y))
            {
                throw new ArgumentOutOfRangeException(nameof(colliderSize));
            }

            if (!IsFinite(colliderOffset.x) || !IsFinite(colliderOffset.y))
            {
                throw new ArgumentOutOfRangeException(nameof(colliderOffset));
            }

            if (!Enum.IsDefined(typeof(DestructiblePropColliderShape), colliderShape))
            {
                throw new ArgumentOutOfRangeException(nameof(colliderShape));
            }

            if (!Enum.IsDefined(
                typeof(DestructiblePropDestroyedColliderPolicy),
                destroyedColliderPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(destroyedColliderPolicy));
            }

            MaximumHealth = maximumHealth;
            ColliderShape = colliderShape;
            ColliderSize = colliderSize;
            ColliderOffset = colliderOffset;
            IntactSprite = intactSprite;
            IntactPresentationId = intactPresentationId
                ?? throw new ArgumentNullException(nameof(intactPresentationId));
            DestructionAnimation = destructionAnimation;
            DestructionAnimationId = destructionAnimationId
                ?? throw new ArgumentNullException(nameof(destructionAnimationId));
            DestroyedColliderPolicy = destroyedColliderPolicy;
            RewardProfileSource = rewardProfileSource;
            RewardProfileId = rewardProfileId
                ?? throw new ArgumentNullException(nameof(rewardProfileId));
            Fingerprint = ComputeFingerprint();
        }

        public double MaximumHealth { get; }
        public DestructiblePropColliderShape ColliderShape { get; }
        public Vector2 ColliderSize { get; }
        public Vector2 ColliderOffset { get; }
        public Sprite IntactSprite { get; }
        public StableId IntactPresentationId { get; }
        public DestructiblePropDestructionAnimation DestructionAnimation { get; }
        public StableId DestructionAnimationId { get; }
        public DestructiblePropDestroyedColliderPolicy DestroyedColliderPolicy { get; }
        public ScriptableObject RewardProfileSource { get; }
        public StableId RewardProfileId { get; }
        public string Fingerprint { get; }

        public DestructiblePropResolvedValues Apply(
            DestructiblePropValueOverrideMask mask,
            DestructiblePropDefinitionValuesAuthoring authoredValues)
        {
            if (authoredValues == null)
            {
                throw new ArgumentNullException(nameof(authoredValues));
            }

            DestructiblePropResolvedValues candidate = authoredValues.Build();
            return new DestructiblePropResolvedValues(
                Has(mask, DestructiblePropValueOverrideMask.MaximumHealth)
                    ? candidate.MaximumHealth
                    : MaximumHealth,
                Has(mask, DestructiblePropValueOverrideMask.ColliderShape)
                    ? candidate.ColliderShape
                    : ColliderShape,
                Has(mask, DestructiblePropValueOverrideMask.ColliderSize)
                    ? candidate.ColliderSize
                    : ColliderSize,
                Has(mask, DestructiblePropValueOverrideMask.ColliderOffset)
                    ? candidate.ColliderOffset
                    : ColliderOffset,
                Has(mask, DestructiblePropValueOverrideMask.IntactPresentation)
                    ? candidate.IntactSprite
                    : IntactSprite,
                Has(mask, DestructiblePropValueOverrideMask.IntactPresentation)
                    ? candidate.IntactPresentationId
                    : IntactPresentationId,
                Has(mask, DestructiblePropValueOverrideMask.DestructionAnimation)
                    ? candidate.DestructionAnimation
                    : DestructionAnimation,
                Has(mask, DestructiblePropValueOverrideMask.DestructionAnimation)
                    ? candidate.DestructionAnimationId
                    : DestructionAnimationId,
                Has(mask, DestructiblePropValueOverrideMask.DestroyedColliderPolicy)
                    ? candidate.DestroyedColliderPolicy
                    : DestroyedColliderPolicy,
                Has(mask, DestructiblePropValueOverrideMask.RewardProfile)
                    ? candidate.RewardProfileSource
                    : RewardProfileSource,
                Has(mask, DestructiblePropValueOverrideMask.RewardProfile)
                    ? candidate.RewardProfileId
                    : RewardProfileId);
        }

        private string ComputeFingerprint()
        {
            string text = string.Join(
                "|",
                "hp=" + MaximumHealth.ToString("R", CultureInfo.InvariantCulture),
                "shape=" + ((int)ColliderShape).ToString(CultureInfo.InvariantCulture),
                "size=" + Vector(ColliderSize),
                "offset=" + Vector(ColliderOffset),
                "presentation=" + IntactPresentationId,
                "animation=" + DestructionAnimationId,
                "destroyed-collider="
                    + ((int)DestroyedColliderPolicy).ToString(CultureInfo.InvariantCulture),
                "reward=" + RewardProfileId);
            return Sha256(text);
        }

        private static string Vector(Vector2 value)
        {
            return value.x.ToString("R", CultureInfo.InvariantCulture)
                + ","
                + value.y.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool Has(
            DestructiblePropValueOverrideMask mask,
            DestructiblePropValueOverrideMask value)
        {
            return (mask & value) == value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        internal static string Sha256(string text)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder("sha256:");
                for (int index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
