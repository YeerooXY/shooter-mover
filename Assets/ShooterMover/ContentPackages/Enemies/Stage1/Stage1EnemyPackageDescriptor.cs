using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Enemies.Stage1
{
    /// <summary>
    /// Closed Stage 1 package classification. This is an authoring classification,
    /// not durable mission or encounter state.
    /// </summary>
    public enum Stage1EnemyPackageClassification
    {
        Ordinary = 1,
        Elite = 2,
    }

    /// <summary>
    /// Closed v1 authoring vocabulary for the deliberately small Stage 1 roster.
    /// Values describe package boundaries only; runtime behavior remains in the
    /// accepted enemy, combat, movement, and encounter adapters.
    /// </summary>
    [Flags]
    public enum Stage1EnemyCapability : ulong
    {
        None = 0,
        DirectPursuit = 1UL << 0,
        OrdinaryContactDamage = 1UL << 1,
        DisposableImpactAttack = 1UL << 2,
        MobilePositioning = 1UL << 3,
        StationaryPositioning = 1UL << 4,
        BlasterProjectile = 1UL << 5,
        FourBlasterOrigins = 1UL << 6,
        MildBoundedSpread = 1UL << 7,
        SafeRecoveryWindow = 1UL << 8,
        LineOfFireTelegraph = 1UL << 9,

        PhaseTransition = 1UL << 16,
        DenialPulse = 1UL << 17,
        MortarAttack = 1UL << 18,
        ReinforcementCall = 1UL << 19,
        Teleport = 1UL << 20,
        ComplexRepositioning = 1UL << 21,
        BulletHell = 1UL << 22,
    }

    /// <summary>
    /// Immutable, engine-independent authoring boundary for one amended Stage 1
    /// enemy package. Validation is deliberately separate so intentionally invalid
    /// fixtures can be represented and reported deterministically.
    /// </summary>
    public sealed class Stage1EnemyPackageDescriptor :
        IEquatable<Stage1EnemyPackageDescriptor>,
        IComparable<Stage1EnemyPackageDescriptor>,
        IComparable
    {
        public const int CurrentDescriptorVersion = 1;

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private static readonly StableId PursuerDroneIdValue =
            StableId.Parse("enemy.pursuer-drone");
        private static readonly StableId RamDroidIdValue =
            StableId.Parse("enemy.ram-droid");
        private static readonly StableId MobileBlasterDroidIdValue =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId BlasterTurretIdValue =
            StableId.Parse("enemy.blaster-turret");
        private static readonly StableId FourBlasterEliteIdValue =
            StableId.Parse("enemy.four-blaster-elite");
        private static readonly StableId BlasterMachineGunIdValue =
            StableId.Parse("weapon.blaster-machine-gun");

        private static readonly ReadOnlyCollection<StableId> AcceptedEnemyIdsValue =
            Array.AsReadOnly(
                new[]
                {
                    PursuerDroneIdValue,
                    RamDroidIdValue,
                    MobileBlasterDroidIdValue,
                    BlasterTurretIdValue,
                    FourBlasterEliteIdValue,
                });

        private readonly string canonicalText;

        private Stage1EnemyPackageDescriptor(
            int descriptorVersion,
            ContentDefinitionDescriptor contentDefinition,
            Stage1EnemyPackageClassification classification,
            CombatChannel damageChannel,
            CombatWeightClass weightClass,
            ContentReference movementReference,
            ContentReference attackReference,
            ContentReference telegraphReference,
            Stage1EnemyCapability capabilities)
        {
            DescriptorVersion = descriptorVersion;
            ContentDefinition = contentDefinition;
            Classification = classification;
            DamageChannel = damageChannel;
            WeightClass = weightClass;
            MovementReference = movementReference;
            AttackReference = attackReference;
            TelegraphReference = telegraphReference;
            Capabilities = capabilities;
            canonicalText = BuildCanonicalText();
        }

        public int DescriptorVersion { get; }

        /// <summary>
        /// Typed Content Definitions v1 registry input owned by the concrete package.
        /// EN-001 never writes the generated registry documents.
        /// </summary>
        public ContentDefinitionDescriptor ContentDefinition { get; }

        public StableId DefinitionId
        {
            get { return ContentDefinition == null ? null : ContentDefinition.DefinitionId; }
        }

        public Stage1EnemyPackageClassification Classification { get; }

        public CombatChannel DamageChannel { get; }

        public CombatWeightClass WeightClass { get; }

        /// <summary>
        /// Shared-module reference consumed by the accepted enemy movement adapter.
        /// </summary>
        public ContentReference MovementReference { get; }

        /// <summary>
        /// Shared-module or accepted Blaster weapon reference consumed by the attack adapter.
        /// </summary>
        public ContentReference AttackReference { get; }

        /// <summary>
        /// Shared-module reference consumed by color-independent telegraph presentation.
        /// </summary>
        public ContentReference TelegraphReference { get; }

        public Stage1EnemyCapability Capabilities { get; }

        public bool IsElite
        {
            get { return Classification == Stage1EnemyPackageClassification.Elite; }
        }

        public static StableId PursuerDroneId
        {
            get { return PursuerDroneIdValue; }
        }

        public static StableId RamDroidId
        {
            get { return RamDroidIdValue; }
        }

        public static StableId MobileBlasterDroidId
        {
            get { return MobileBlasterDroidIdValue; }
        }

        public static StableId BlasterTurretId
        {
            get { return BlasterTurretIdValue; }
        }

        public static StableId FourBlasterEliteId
        {
            get { return FourBlasterEliteIdValue; }
        }

        public static StableId BlasterMachineGunId
        {
            get { return BlasterMachineGunIdValue; }
        }

        public static IReadOnlyList<StableId> AcceptedEnemyIds
        {
            get { return AcceptedEnemyIdsValue; }
        }

        public static Stage1EnemyPackageDescriptor Create(
            int descriptorVersion,
            ContentDefinitionDescriptor contentDefinition,
            Stage1EnemyPackageClassification classification,
            CombatChannel damageChannel,
            CombatWeightClass weightClass,
            ContentReference movementReference,
            ContentReference attackReference,
            ContentReference telegraphReference,
            Stage1EnemyCapability capabilities)
        {
            return new Stage1EnemyPackageDescriptor(
                descriptorVersion,
                contentDefinition,
                classification,
                damageChannel,
                weightClass,
                movementReference,
                attackReference,
                telegraphReference,
                capabilities);
        }

        /// <summary>
        /// Projects an accepted package identity into the existing Typed Content
        /// References v1 boundary. It does not resolve or generate registry data.
        /// </summary>
        public ContentReference CreateEnemyReference()
        {
            if (ContentDefinition == null)
            {
                throw new InvalidOperationException(
                    "A package without a content definition cannot create an enemy reference.");
            }

            return ContentReference.Create(
                ContentDefinition.DefinitionId,
                ContentDefinitionKind.Enemy,
                ContentDefinition.DefinitionVersion);
        }

        /// <summary>
        /// Projects the package role into the generic Encounter Lifecycle v1 entry.
        /// Encounter identity, actor identity, ordering, spawning, and lifecycle stay
        /// owned by the encounter runtime.
        /// </summary>
        public EncounterParticipantEntry CreateEncounterParticipantEntry(
            StableId entryId,
            StableId actorId,
            int order)
        {
            if (DefinitionId == null)
            {
                throw new InvalidOperationException(
                    "A package without a content definition cannot create an encounter entry.");
            }

            return new EncounterParticipantEntry(entryId, actorId, DefinitionId, order);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public override string ToString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1EnemyPackageDescriptor other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1EnemyPackageDescriptor);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        public int CompareTo(Stage1EnemyPackageDescriptor other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            if (DefinitionId == null)
            {
                if (other.DefinitionId != null)
                {
                    return 1;
                }
            }
            else
            {
                if (other.DefinitionId == null)
                {
                    return -1;
                }

                int idComparison = DefinitionId.CompareTo(other.DefinitionId);
                if (idComparison != 0)
                {
                    return idComparison;
                }
            }

            return string.CompareOrdinal(canonicalText, other.canonicalText);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            Stage1EnemyPackageDescriptor other = obj as Stage1EnemyPackageDescriptor;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a Stage1EnemyPackageDescriptor.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("descriptor_version=")
                .Append(DescriptorVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nclassification=")
                .Append(((int)Classification).ToString(CultureInfo.InvariantCulture))
                .Append("\ndamage_channel=")
                .Append(((int)DamageChannel).ToString(CultureInfo.InvariantCulture))
                .Append("\nweight_class=")
                .Append(((int)WeightClass).ToString(CultureInfo.InvariantCulture))
                .Append("\ncapabilities=")
                .Append(((ulong)Capabilities).ToString(CultureInfo.InvariantCulture))
                .Append("\nmovement_reference=")
                .Append(ToReferenceToken(MovementReference))
                .Append("\nattack_reference=")
                .Append(ToReferenceToken(AttackReference))
                .Append("\ntelegraph_reference=")
                .Append(ToReferenceToken(TelegraphReference))
                .Append("\ncontent_definition:\n")
                .Append(ContentDefinition == null
                    ? "null"
                    : ContentDefinition.ToCanonicalString());
            return builder.ToString();
        }

        private static string ToReferenceToken(ContentReference reference)
        {
            if (reference == null)
            {
                return "null";
            }

            return ((int)reference.ExpectedKind).ToString(CultureInfo.InvariantCulture)
                + "|"
                + reference.DefinitionId
                + "|"
                + reference.ExpectedVersion.ToString(CultureInfo.InvariantCulture);
        }
    }
}
