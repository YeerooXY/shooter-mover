using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    public enum BreakableColliderShape
    {
        Box = 0,
        Circle = 1,
        Capsule = 2
    }

    public enum BreakableDestroyedCollisionPolicy
    {
        Disable = 0,
        KeepBlocking = 1,
        KeepAsTrigger = 2
    }

    [Serializable]
    public sealed class BreakableDefinitionValues
    {
        [Min(0.01f)]
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private BreakableColliderShape colliderShape =
            BreakableColliderShape.Box;
        [SerializeField] private Vector2 colliderSize = new Vector2(2.2f, 1.35f);
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [SerializeField] private string intactPresentationId = "presentation.unassigned";
        [SerializeField] private Sprite intactSprite;
        [SerializeField] private string destructionAnimationId = "animation.none";
        [SerializeField] private BreakableAnimation destructionAnimation;
        [SerializeField] private BreakableDestroyedCollisionPolicy destroyedCollisionPolicy =
            BreakableDestroyedCollisionPolicy.Disable;
        [SerializeField] private string inheritedRewardProfileId = "reward-profile.none";
        [SerializeField] private ScriptableObject inheritedRewardProfile;

        public BreakableDefinitionValues()
        {
        }

        public BreakableDefinitionValues(
            double maximumHealth,
            BreakableColliderShape colliderShape,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            string intactPresentationId,
            Sprite intactSprite,
            string destructionAnimationId,
            BreakableAnimation destructionAnimation,
            BreakableDestroyedCollisionPolicy destroyedCollisionPolicy,
            string inheritedRewardProfileId,
            ScriptableObject inheritedRewardProfile)
        {
            if (maximumHealth > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            this.maximumHealth = (float)maximumHealth;
            this.colliderShape = colliderShape;
            this.colliderSize = colliderSize;
            this.colliderOffset = colliderOffset;
            this.intactPresentationId = intactPresentationId;
            this.intactSprite = intactSprite;
            this.destructionAnimationId = destructionAnimationId;
            this.destructionAnimation = destructionAnimation;
            this.destroyedCollisionPolicy = destroyedCollisionPolicy;
            this.inheritedRewardProfileId = inheritedRewardProfileId;
            this.inheritedRewardProfile = inheritedRewardProfile;
            ValidateOrThrow();
        }

        public BreakableResolvedValues BuildResolvedValues()
        {
            ValidateOrThrow();
            return new BreakableResolvedValues(
                maximumHealth,
                colliderShape,
                colliderSize,
                colliderOffset,
                StableId.Parse(intactPresentationId),
                intactSprite,
                StableId.Parse(destructionAnimationId),
                destructionAnimation,
                destroyedCollisionPolicy,
                StableId.Parse(inheritedRewardProfileId),
                inheritedRewardProfile);
        }

        public void ValidateOrThrow()
        {
            RequirePositiveFinite(maximumHealth, nameof(maximumHealth));
            RequirePositiveVector(colliderSize, nameof(colliderSize));
            RequireVectorFinite(colliderOffset, nameof(colliderOffset));
            RequireEnum(colliderShape, nameof(colliderShape));
            RequireEnum(destroyedCollisionPolicy, nameof(destroyedCollisionPolicy));
            ValidateOptionalReference(
                StableId.Parse(destructionAnimationId),
                "animation.none",
                destructionAnimation,
                "destruction animation");
            ValidateOptionalReference(
                StableId.Parse(inheritedRewardProfileId),
                "reward-profile.none",
                inheritedRewardProfile,
                "inherited reward profile");
            StableId.Parse(intactPresentationId);
        }

        internal static void RequirePositiveFinite(float value, string fieldName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new InvalidOperationException(fieldName + " must be positive and finite.");
            }
        }

        internal static void RequirePositiveVector(Vector2 value, string fieldName)
        {
            RequireVectorFinite(value, fieldName);
            if (value.x <= 0f || value.y <= 0f)
            {
                throw new InvalidOperationException(fieldName + " must be positive.");
            }
        }

        internal static void RequireVectorFinite(Vector2 value, string fieldName)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y))
            {
                throw new InvalidOperationException(fieldName + " must be finite.");
            }
        }

        internal static void RequireEnum<T>(T value, string fieldName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new InvalidOperationException(fieldName + " is invalid.");
            }
        }

        internal static void ValidateOptionalReference(
            StableId id,
            string noneId,
            UnityEngine.Object reference,
            string label)
        {
            if (reference == null && !id.Equals(StableId.Parse(noneId)))
            {
                throw new InvalidOperationException(
                    "A non-none " + label + " ID requires an assigned asset.");
            }
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(0.01f, maximumHealth);
            colliderSize.x = Mathf.Max(0.01f, colliderSize.x);
            colliderSize.y = Mathf.Max(0.01f, colliderSize.y);
        }
    }

    [Serializable]
    public sealed class BreakableValueOverrides
    {
        [SerializeField] private bool overrideMaximumHealth;
        [Min(0.01f)]
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private bool overrideColliderShape;
        [SerializeField] private BreakableColliderShape colliderShape =
            BreakableColliderShape.Box;
        [SerializeField] private bool overrideColliderSize;
        [SerializeField] private Vector2 colliderSize = new Vector2(2.2f, 1.35f);
        [SerializeField] private bool overrideColliderOffset;
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [SerializeField] private bool overrideIntactPresentation;
        [SerializeField] private string intactPresentationId = "presentation.unassigned";
        [SerializeField] private Sprite intactSprite;
        [SerializeField] private bool overrideDestructionAnimation;
        [SerializeField] private string destructionAnimationId = "animation.none";
        [SerializeField] private BreakableAnimation destructionAnimation;
        [SerializeField] private bool overrideDestroyedCollisionPolicy;
        [SerializeField] private BreakableDestroyedCollisionPolicy destroyedCollisionPolicy =
            BreakableDestroyedCollisionPolicy.Disable;
        [SerializeField] private bool overrideInheritedRewardProfile;
        [SerializeField] private string inheritedRewardProfileId = "reward-profile.none";
        [SerializeField] private ScriptableObject inheritedRewardProfile;

        public BreakableResolvedValues Apply(BreakableResolvedValues inherited)
        {
            if (inherited == null)
            {
                throw new ArgumentNullException(nameof(inherited));
            }

            if (overrideMaximumHealth)
            {
                BreakableDefinitionValues.RequirePositiveFinite(
                    maximumHealth,
                    nameof(maximumHealth));
            }

            if (overrideColliderShape)
            {
                BreakableDefinitionValues.RequireEnum(
                    colliderShape,
                    nameof(colliderShape));
            }

            if (overrideColliderSize)
            {
                BreakableDefinitionValues.RequirePositiveVector(
                    colliderSize,
                    nameof(colliderSize));
            }

            if (overrideColliderOffset)
            {
                BreakableDefinitionValues.RequireVectorFinite(
                    colliderOffset,
                    nameof(colliderOffset));
            }

            if (overrideDestroyedCollisionPolicy)
            {
                BreakableDefinitionValues.RequireEnum(
                    destroyedCollisionPolicy,
                    nameof(destroyedCollisionPolicy));
            }

            return new BreakableResolvedValues(
                overrideMaximumHealth ? maximumHealth : inherited.MaximumHealth,
                overrideColliderShape ? colliderShape : inherited.ColliderShape,
                overrideColliderSize ? colliderSize : inherited.ColliderSize,
                overrideColliderOffset ? colliderOffset : inherited.ColliderOffset,
                overrideIntactPresentation
                    ? StableId.Parse(intactPresentationId)
                    : inherited.IntactPresentationId,
                overrideIntactPresentation ? intactSprite : inherited.IntactSprite,
                overrideDestructionAnimation
                    ? StableId.Parse(destructionAnimationId)
                    : inherited.DestructionAnimationId,
                overrideDestructionAnimation
                    ? destructionAnimation
                    : inherited.DestructionAnimation,
                overrideDestroyedCollisionPolicy
                    ? destroyedCollisionPolicy
                    : inherited.DestroyedCollisionPolicy,
                overrideInheritedRewardProfile
                    ? StableId.Parse(inheritedRewardProfileId)
                    : inherited.InheritedRewardProfileId,
                overrideInheritedRewardProfile
                    ? inheritedRewardProfile
                    : inherited.InheritedRewardProfile);
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(0.01f, maximumHealth);
            colliderSize.x = Mathf.Max(0.01f, colliderSize.x);
            colliderSize.y = Mathf.Max(0.01f, colliderSize.y);
        }
    }

    public sealed class BreakableResolvedValues
    {
        internal BreakableResolvedValues(
            double maximumHealth,
            BreakableColliderShape colliderShape,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            StableId intactPresentationId,
            Sprite intactSprite,
            StableId destructionAnimationId,
            BreakableAnimation destructionAnimation,
            BreakableDestroyedCollisionPolicy destroyedCollisionPolicy,
            StableId inheritedRewardProfileId,
            ScriptableObject inheritedRewardProfile)
        {
            if (double.IsNaN(maximumHealth)
                || double.IsInfinity(maximumHealth)
                || maximumHealth <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            BreakableDefinitionValues.RequirePositiveVector(
                colliderSize,
                nameof(colliderSize));
            BreakableDefinitionValues.RequireVectorFinite(
                colliderOffset,
                nameof(colliderOffset));
            BreakableDefinitionValues.RequireEnum(colliderShape, nameof(colliderShape));
            BreakableDefinitionValues.RequireEnum(
                destroyedCollisionPolicy,
                nameof(destroyedCollisionPolicy));

            MaximumHealth = maximumHealth;
            ColliderShape = colliderShape;
            ColliderSize = colliderSize;
            ColliderOffset = colliderOffset;
            IntactPresentationId = intactPresentationId
                ?? throw new ArgumentNullException(nameof(intactPresentationId));
            IntactSprite = intactSprite;
            DestructionAnimationId = destructionAnimationId
                ?? throw new ArgumentNullException(nameof(destructionAnimationId));
            BreakableDefinitionValues.ValidateOptionalReference(
                DestructionAnimationId,
                "animation.none",
                destructionAnimation,
                "destruction animation");
            DestructionAnimation = destructionAnimation;
            DestroyedCollisionPolicy = destroyedCollisionPolicy;
            InheritedRewardProfileId = inheritedRewardProfileId
                ?? throw new ArgumentNullException(nameof(inheritedRewardProfileId));
            BreakableDefinitionValues.ValidateOptionalReference(
                InheritedRewardProfileId,
                "reward-profile.none",
                inheritedRewardProfile,
                "inherited reward profile");
            InheritedRewardProfile = inheritedRewardProfile;
        }

        public double MaximumHealth { get; }
        public BreakableColliderShape ColliderShape { get; }
        public Vector2 ColliderSize { get; }
        public Vector2 ColliderOffset { get; }
        public StableId IntactPresentationId { get; }
        public Sprite IntactSprite { get; }
        public StableId DestructionAnimationId { get; }
        public BreakableAnimation DestructionAnimation { get; }
        public BreakableDestroyedCollisionPolicy DestroyedCollisionPolicy { get; }
        public StableId InheritedRewardProfileId { get; }
        public ScriptableObject InheritedRewardProfile { get; }

        internal string ToCanonicalString()
        {
            return "hp=" + MaximumHealth.ToString("R", CultureInfo.InvariantCulture)
                + "|shape=" + ((int)ColliderShape).ToString(CultureInfo.InvariantCulture)
                + "|size=" + VectorText(ColliderSize)
                + "|offset=" + VectorText(ColliderOffset)
                + "|presentation=" + IntactPresentationId
                + "|animation=" + DestructionAnimationId
                + "|collision=" + ((int)DestroyedCollisionPolicy).ToString(
                    CultureInfo.InvariantCulture)
                + "|reward=" + InheritedRewardProfileId;
        }

        internal CapabilityDefinition ToCapabilityDefinition()
        {
            return new CapabilityDefinition(
                StableId.Parse(BreakableFamilyDefinitionAsset.CapabilityIdText),
                new[]
                {
                    Field("prop.maximum-health", CapabilityFieldValue.FromDecimal(MaximumHealth)),
                    Field("prop.collider-shape", CapabilityFieldValue.FromInteger((int)ColliderShape)),
                    Field("prop.collider-size-x", CapabilityFieldValue.FromDecimal(ColliderSize.x)),
                    Field("prop.collider-size-y", CapabilityFieldValue.FromDecimal(ColliderSize.y)),
                    Field("prop.collider-offset-x", CapabilityFieldValue.FromDecimal(ColliderOffset.x)),
                    Field("prop.collider-offset-y", CapabilityFieldValue.FromDecimal(ColliderOffset.y)),
                    Field("prop.intact-presentation", CapabilityFieldValue.FromStableId(IntactPresentationId)),
                    Field("prop.destruction-animation", CapabilityFieldValue.FromStableId(DestructionAnimationId)),
                    Field(
                        "prop.destroyed-collision-policy",
                        CapabilityFieldValue.FromInteger((int)DestroyedCollisionPolicy)),
                    Field("prop.reward-profile", CapabilityFieldValue.FromStableId(InheritedRewardProfileId))
                });
        }

        private static CapabilityField Field(string id, CapabilityFieldValue value)
        {
            return new CapabilityField(StableId.Parse(id), value);
        }

        private static string VectorText(Vector2 value)
        {
            return value.x.ToString("R", CultureInfo.InvariantCulture)
                + "," + value.y.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    [Serializable]
    public sealed class BreakableVariantDefinition
    {
        [SerializeField] private string variantId = "variant.unassigned";
        [SerializeField] private bool hasObjectLevel;
        [SerializeField] private int objectLevel;
        [SerializeField] private BreakableValueOverrides overrides =
            new BreakableValueOverrides();

        public StableId VariantId => StableId.Parse(variantId);
        public int? ObjectLevel => hasObjectLevel ? (int?)objectLevel : null;

        internal BreakableResolvedValues Resolve(
            BreakableResolvedValues familyDefaults)
        {
            return (overrides ?? new BreakableValueOverrides()).Apply(familyDefaults);
        }
    }

    public sealed class BreakableResolvedPreview
    {
        internal BreakableResolvedPreview(
            StableId familyId,
            StableId variantId,
            StableId placedInstanceId,
            int? objectLevel,
            BreakableResolvedValues values,
            string familyFingerprint,
            string variantFingerprint,
            string resolvedFingerprint)
        {
            FamilyId = familyId;
            VariantId = variantId;
            PlacedInstanceId = placedInstanceId;
            ObjectLevel = objectLevel;
            Values = values;
            FamilyFingerprint = familyFingerprint;
            VariantFingerprint = variantFingerprint;
            ResolvedFingerprint = resolvedFingerprint;
        }

        public StableId FamilyId { get; }
        public StableId VariantId { get; }
        public StableId PlacedInstanceId { get; }
        public int? ObjectLevel { get; }
        public BreakableResolvedValues Values { get; }
        public string FamilyFingerprint { get; }
        public string VariantFingerprint { get; }
        public string ResolvedFingerprint { get; }
    }

    [CreateAssetMenu(
        fileName = "BreakableFamilyDefinition",
        menuName = "Shooter Mover/Props/Destructible Prop Family")]
    public sealed class BreakableFamilyDefinitionAsset :
        ScriptableObject,
        IObjectFamilyDefinitionSource
    {
        public const string CapabilityIdText = "capability.breakable";

        [SerializeField] private string familyId = "family.unassigned";
        [SerializeField] private string displayName = "Unassigned breakable";
        [SerializeField] private string defaultVariantId = "variant.unassigned";
        [SerializeField] private BreakableDefinitionValues familyDefaults =
            new BreakableDefinitionValues();
        [SerializeField] private BreakableVariantDefinition[] variants =
            Array.Empty<BreakableVariantDefinition>();

        public StableId FamilyId => StableId.Parse(familyId);
        public string Fingerprint => Fingerprint64(BuildFamilyCanonical());

        public ObjectFamilyDefinition BuildDefinition()
        {
            BreakableResolvedValues defaults = BuildDefaults();
            List<BreakableVariantDefinition> validated = ReadValidatedVariants();
            List<ObjectVariantDefinition> built =
                new List<ObjectVariantDefinition>(validated.Count);
            for (int index = 0; index < validated.Count; index++)
            {
                BreakableVariantDefinition variant = validated[index];
                built.Add(new ObjectVariantDefinition(
                    variant.VariantId,
                    variant.ObjectLevel,
                    new[]
                    {
                        CapabilitySelection.Override(
                            variant.Resolve(defaults).ToCapabilityDefinition())
                    }));
            }

            return new ObjectFamilyDefinition(
                FamilyId,
                displayName ?? string.Empty,
                StableId.Parse(defaultVariantId),
                new[] { defaults.ToCapabilityDefinition() },
                built);
        }

        public BreakableResolvedPreview Resolve(
            StableId selectedVariantId,
            BreakableValueOverrides instanceOverrides,
            StableId placedInstanceId)
        {
            if (placedInstanceId == null)
            {
                throw new ArgumentNullException(nameof(placedInstanceId));
            }

            StableId variantId = selectedVariantId ?? StableId.Parse(defaultVariantId);
            BreakableResolvedValues defaults = BuildDefaults();
            BreakableVariantDefinition variant = FindVariant(variantId);
            BreakableResolvedValues variantValues = variant.Resolve(defaults);
            BreakableResolvedValues resolved =
                (instanceOverrides ?? new BreakableValueOverrides()).Apply(variantValues);
            string familyCanonical = BuildFamilyCanonical();
            string variantCanonical = BuildVariantCanonical(
                familyCanonical,
                variant,
                variantValues);
            string resolvedCanonical = variantCanonical
                + "|placed=" + placedInstanceId
                + "|values=" + resolved.ToCanonicalString();

            return new BreakableResolvedPreview(
                FamilyId,
                variantId,
                placedInstanceId,
                variant.ObjectLevel,
                resolved,
                Fingerprint64(familyCanonical),
                Fingerprint64(variantCanonical),
                Fingerprint64(resolvedCanonical));
        }

        public void ValidateOrThrow()
        {
            BuildDefinition();
            BuildFamilyCanonical();
        }

        private BreakableResolvedValues BuildDefaults()
        {
            if (familyDefaults == null)
            {
                throw new InvalidOperationException(
                    "Destructible prop family '" + familyId + "' has no defaults.");
            }

            return familyDefaults.BuildResolvedValues();
        }

        private BreakableVariantDefinition FindVariant(StableId variantId)
        {
            List<BreakableVariantDefinition> validated = ReadValidatedVariants();
            for (int index = 0; index < validated.Count; index++)
            {
                if (validated[index].VariantId.Equals(variantId))
                {
                    return validated[index];
                }
            }

            throw new InvalidOperationException(
                "Variant '" + variantId + "' does not exist in family '" + FamilyId + "'.");
        }

        private List<BreakableVariantDefinition> ReadValidatedVariants()
        {
            List<BreakableVariantDefinition> result =
                new List<BreakableVariantDefinition>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (variants != null)
            {
                for (int index = 0; index < variants.Length; index++)
                {
                    BreakableVariantDefinition variant = variants[index];
                    if (variant == null)
                    {
                        throw new InvalidOperationException(
                            "Destructible prop family '" + familyId + "' contains a null variant.");
                    }

                    if (!seen.Add(variant.VariantId.ToString()))
                    {
                        throw new InvalidOperationException(
                            "Destructible prop family '" + familyId
                            + "' contains duplicate variant '" + variant.VariantId + "'.");
                    }

                    result.Add(variant);
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException(
                    "Destructible prop family '" + familyId + "' requires at least one variant.");
            }

            return result;
        }

        private string BuildFamilyCanonical()
        {
            BreakableResolvedValues defaults = BuildDefaults();
            List<BreakableVariantDefinition> ordered = ReadValidatedVariants();
            ordered.Sort((left, right) => left.VariantId.CompareTo(right.VariantId));
            StringBuilder builder = new StringBuilder();
            builder.Append("family=").Append(FamilyId);
            builder.Append("|default=").Append(StableId.Parse(defaultVariantId));
            builder.Append("|defaults=").Append(defaults.ToCanonicalString());
            builder.Append("|variants=");
            for (int index = 0; index < ordered.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(';');
                }

                BreakableVariantDefinition variant = ordered[index];
                builder.Append(BuildVariantCanonical(
                    string.Empty,
                    variant,
                    variant.Resolve(defaults)));
            }

            return builder.ToString();
        }

        private static string BuildVariantCanonical(
            string familyCanonical,
            BreakableVariantDefinition variant,
            BreakableResolvedValues values)
        {
            return familyCanonical
                + "|variant=" + variant.VariantId
                + "|level=" + (variant.ObjectLevel.HasValue
                    ? variant.ObjectLevel.Value.ToString(CultureInfo.InvariantCulture)
                    : "none")
                + "|values=" + values.ToCanonicalString();
        }

        private void OnValidate()
        {
            if (familyDefaults == null)
            {
                familyDefaults = new BreakableDefinitionValues();
            }

            if (variants == null)
            {
                variants = Array.Empty<BreakableVariantDefinition>();
            }
        }

        private static string Fingerprint64(string input)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                for (int index = 0; index < input.Length; index++)
                {
                    char value = input[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }
}
