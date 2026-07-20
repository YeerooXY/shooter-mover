using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    [Flags]
    public enum EnemyAttackParameterKindsV1
    {
        None = 0,
        Projectile = 1,
        Area = 2,
        Melee = 4,
    }

    public enum EnemyCatalogRoomClearRoleV1
    {
        RequiredEnemy = 1,
        OptionalEnemy = 2,
        ObjectiveEntity = 3,
        DoesNotAffectRoomClear = 4,
    }

    public sealed class EnemyAttackCapabilityRegistrationV1
    {
        public EnemyAttackCapabilityRegistrationV1(
            StableId capabilityId,
            EnemyAttackParameterKindsV1 requiredParameters,
            EnemyAttackParameterKindsV1 allowedParameters)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            if (!AreValidFlags(requiredParameters))
            {
                throw new ArgumentOutOfRangeException(nameof(requiredParameters));
            }
            if (!AreValidFlags(allowedParameters))
            {
                throw new ArgumentOutOfRangeException(nameof(allowedParameters));
            }
            if ((requiredParameters & allowedParameters) != requiredParameters)
            {
                throw new ArgumentException(
                    "Required attack parameters must be a subset of allowed parameters.");
            }

            RequiredParameters = requiredParameters;
            AllowedParameters = allowedParameters;
        }

        public StableId CapabilityId { get; }

        public EnemyAttackParameterKindsV1 RequiredParameters { get; }

        public EnemyAttackParameterKindsV1 AllowedParameters { get; }

        private static bool AreValidFlags(EnemyAttackParameterKindsV1 value)
        {
            const EnemyAttackParameterKindsV1 all =
                EnemyAttackParameterKindsV1.Projectile
                | EnemyAttackParameterKindsV1.Area
                | EnemyAttackParameterKindsV1.Melee;
            return (value & ~all) == 0;
        }
    }

    public interface IEnemyCatalogRegistryV1
    {
        bool IsMovementPolicyRegistered(StableId movementPolicyId);

        bool IsDecisionPolicyRegistered(StableId decisionPolicyId);

        bool TryResolveAttackCapability(
            StableId capabilityId,
            out EnemyAttackCapabilityRegistrationV1 registration);

        bool IsSpecialCapabilityRegistered(StableId capabilityId);

        bool IsPresentationRegistered(StableId presentationId);

        bool IsDamageChannelRegistered(StableId damageChannelId);

        bool IsExperienceProfileRegistered(StableId experienceProfileId);

        bool IsDropProfileRegistered(StableId dropProfileId);
    }

    public sealed class EnemyCatalogRegistryV1 : IEnemyCatalogRegistryV1
    {
        private readonly HashSet<StableId> movementPolicies;
        private readonly HashSet<StableId> decisionPolicies;
        private readonly Dictionary<StableId, EnemyAttackCapabilityRegistrationV1> attackCapabilities;
        private readonly HashSet<StableId> specialCapabilities;
        private readonly HashSet<StableId> presentations;
        private readonly HashSet<StableId> damageChannels;
        private readonly HashSet<StableId> experienceProfiles;
        private readonly HashSet<StableId> dropProfiles;

        public EnemyCatalogRegistryV1(
            IEnumerable<StableId> movementPolicyIds,
            IEnumerable<StableId> decisionPolicyIds,
            IEnumerable<EnemyAttackCapabilityRegistrationV1> attackCapabilityRegistrations,
            IEnumerable<StableId> specialCapabilityIds,
            IEnumerable<StableId> presentationIds,
            IEnumerable<StableId> damageChannelIds,
            IEnumerable<StableId> experienceProfileIds,
            IEnumerable<StableId> dropProfileIds)
        {
            movementPolicies = CopyIds(movementPolicyIds, nameof(movementPolicyIds));
            decisionPolicies = CopyIds(decisionPolicyIds, nameof(decisionPolicyIds));
            attackCapabilities = CopyAttackCapabilities(
                attackCapabilityRegistrations,
                nameof(attackCapabilityRegistrations));
            specialCapabilities = CopyIds(specialCapabilityIds, nameof(specialCapabilityIds));
            presentations = CopyIds(presentationIds, nameof(presentationIds));
            damageChannels = CopyIds(damageChannelIds, nameof(damageChannelIds));
            experienceProfiles = CopyIds(experienceProfileIds, nameof(experienceProfileIds));
            dropProfiles = CopyIds(dropProfileIds, nameof(dropProfileIds));
        }

        public bool IsMovementPolicyRegistered(StableId movementPolicyId)
        {
            return movementPolicyId != null && movementPolicies.Contains(movementPolicyId);
        }

        public bool IsDecisionPolicyRegistered(StableId decisionPolicyId)
        {
            return decisionPolicyId != null && decisionPolicies.Contains(decisionPolicyId);
        }

        public bool TryResolveAttackCapability(
            StableId capabilityId,
            out EnemyAttackCapabilityRegistrationV1 registration)
        {
            registration = null;
            return capabilityId != null
                && attackCapabilities.TryGetValue(capabilityId, out registration)
                && registration != null;
        }

        public bool IsSpecialCapabilityRegistered(StableId capabilityId)
        {
            return capabilityId != null && specialCapabilities.Contains(capabilityId);
        }

        public bool IsPresentationRegistered(StableId presentationId)
        {
            return presentationId != null && presentations.Contains(presentationId);
        }

        public bool IsDamageChannelRegistered(StableId damageChannelId)
        {
            return damageChannelId != null && damageChannels.Contains(damageChannelId);
        }

        public bool IsExperienceProfileRegistered(StableId experienceProfileId)
        {
            return experienceProfileId != null && experienceProfiles.Contains(experienceProfileId);
        }

        public bool IsDropProfileRegistered(StableId dropProfileId)
        {
            return dropProfileId != null && dropProfiles.Contains(dropProfileId);
        }

        private static HashSet<StableId> CopyIds(
            IEnumerable<StableId> values,
            string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var result = new HashSet<StableId>();
            foreach (StableId value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Enemy catalog registries cannot contain null IDs.",
                        parameterName);
                }
                if (!result.Add(value))
                {
                    throw new ArgumentException(
                        "Enemy catalog registry ID is duplicated: " + value,
                        parameterName);
                }
            }
            return result;
        }

        private static Dictionary<StableId, EnemyAttackCapabilityRegistrationV1>
            CopyAttackCapabilities(
                IEnumerable<EnemyAttackCapabilityRegistrationV1> values,
                string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var result = new Dictionary<StableId, EnemyAttackCapabilityRegistrationV1>();
            foreach (EnemyAttackCapabilityRegistrationV1 value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Attack capability registries cannot contain null registrations.",
                        parameterName);
                }
                if (result.ContainsKey(value.CapabilityId))
                {
                    throw new ArgumentException(
                        "Attack capability registration is duplicated: " + value.CapabilityId,
                        parameterName);
                }
                result.Add(value.CapabilityId, value);
            }
            return result;
        }
    }

    public sealed class EnemyLevelScalingProfileV1
    {
        public EnemyLevelScalingProfileV1(
            int baseLevel,
            int maximumLevel,
            double additiveHealthPerLevel,
            double multiplicativeHealthPerLevel)
        {
            BaseLevel = baseLevel;
            MaximumLevel = maximumLevel;
            AdditiveHealthPerLevel = additiveHealthPerLevel;
            MultiplicativeHealthPerLevel = multiplicativeHealthPerLevel;
        }

        public int BaseLevel { get; }

        public int MaximumLevel { get; }

        public double AdditiveHealthPerLevel { get; }

        public double MultiplicativeHealthPerLevel { get; }

        public double ResolveHealth(double baseHealth, int level)
        {
            int delta = level - BaseLevel;
            return (baseHealth + (AdditiveHealthPerLevel * delta))
                * Math.Pow(MultiplicativeHealthPerLevel, delta);
        }
    }

    public sealed class EnemyProjectileAttackParametersV1
    {
        public EnemyProjectileAttackParametersV1(
            StableId projectileProfileId,
            int projectileCount,
            double projectileSpeed,
            double maximumTravelDistance,
            double collisionRadius,
            double spreadDegrees,
            int pierceCount)
        {
            ProjectileProfileId = projectileProfileId;
            ProjectileCount = projectileCount;
            ProjectileSpeed = projectileSpeed;
            MaximumTravelDistance = maximumTravelDistance;
            CollisionRadius = collisionRadius;
            SpreadDegrees = spreadDegrees;
            PierceCount = pierceCount;
        }

        public StableId ProjectileProfileId { get; }

        public int ProjectileCount { get; }

        public double ProjectileSpeed { get; }

        public double MaximumTravelDistance { get; }

        public double CollisionRadius { get; }

        public double SpreadDegrees { get; }

        public int PierceCount { get; }
    }

    public sealed class EnemyAreaAttackParametersV1
    {
        public EnemyAreaAttackParametersV1(
            double radius,
            double durationSeconds,
            int maximumTargets)
        {
            Radius = radius;
            DurationSeconds = durationSeconds;
            MaximumTargets = maximumTargets;
        }

        public double Radius { get; }

        public double DurationSeconds { get; }

        public int MaximumTargets { get; }
    }

    public sealed class EnemyMeleeAttackParametersV1
    {
        public EnemyMeleeAttackParametersV1(
            double contactRadius,
            double pounceDistance,
            double windUpSeconds,
            double commitmentSeconds)
        {
            ContactRadius = contactRadius;
            PounceDistance = pounceDistance;
            WindUpSeconds = windUpSeconds;
            CommitmentSeconds = commitmentSeconds;
        }

        public double ContactRadius { get; }

        public double PounceDistance { get; }

        public double WindUpSeconds { get; }

        public double CommitmentSeconds { get; }
    }

    public sealed class EnemyAttackCapabilityDescriptorV1
    {
        public EnemyAttackCapabilityDescriptorV1(
            StableId attackId,
            StableId capabilityId,
            double cooldownSeconds,
            double damage,
            StableId damageChannelId,
            EnemyProjectileAttackParametersV1 projectile,
            EnemyAreaAttackParametersV1 area,
            EnemyMeleeAttackParametersV1 melee)
        {
            AttackId = attackId;
            CapabilityId = capabilityId;
            CooldownSeconds = cooldownSeconds;
            Damage = damage;
            DamageChannelId = damageChannelId;
            Projectile = projectile;
            Area = area;
            Melee = melee;
        }

        public StableId AttackId { get; }

        public StableId CapabilityId { get; }

        public double CooldownSeconds { get; }

        public double Damage { get; }

        public StableId DamageChannelId { get; }

        public EnemyProjectileAttackParametersV1 Projectile { get; }

        public EnemyAreaAttackParametersV1 Area { get; }

        public EnemyMeleeAttackParametersV1 Melee { get; }

        public EnemyAttackParameterKindsV1 ParameterKinds
        {
            get
            {
                EnemyAttackParameterKindsV1 result = EnemyAttackParameterKindsV1.None;
                if (Projectile != null) result |= EnemyAttackParameterKindsV1.Projectile;
                if (Area != null) result |= EnemyAttackParameterKindsV1.Area;
                if (Melee != null) result |= EnemyAttackParameterKindsV1.Melee;
                return result;
            }
        }
    }

    public sealed class EnemyDefinitionV1
    {
        private readonly ReadOnlyCollection<EnemyAttackCapabilityDescriptorV1> attacks;
        private readonly ReadOnlyCollection<StableId> specialCapabilityIds;

        public EnemyDefinitionV1(
            StableId definitionId,
            StableId presentationId,
            double baseHealth,
            EnemyLevelScalingProfileV1 levelScaling,
            StableId factionId,
            double detectionRadius,
            double visionArcDegrees,
            double attackArcDegrees,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            StableId movementPolicyId,
            StableId decisionPolicyId,
            IEnumerable<EnemyAttackCapabilityDescriptorV1> attacks,
            StableId experienceProfileId,
            StableId dropProfileId,
            EnemyCatalogRoomClearRoleV1 roomClearRole,
            IEnumerable<StableId> specialCapabilityIds)
        {
            DefinitionId = definitionId;
            PresentationId = presentationId;
            BaseHealth = baseHealth;
            LevelScaling = levelScaling;
            FactionId = factionId;
            DetectionRadius = detectionRadius;
            VisionArcDegrees = visionArcDegrees;
            AttackArcDegrees = attackArcDegrees;
            MinimumAttackRange = minimumAttackRange;
            PreferredAttackRange = preferredAttackRange;
            MaximumAttackRange = maximumAttackRange;
            MovementPolicyId = movementPolicyId;
            DecisionPolicyId = decisionPolicyId;
            this.attacks = Copy(attacks, nameof(attacks));
            ExperienceProfileId = experienceProfileId;
            DropProfileId = dropProfileId;
            RoomClearRole = roomClearRole;
            this.specialCapabilityIds = CopyIds(
                specialCapabilityIds,
                nameof(specialCapabilityIds));
        }

        public StableId DefinitionId { get; }

        public StableId PresentationId { get; }

        public double BaseHealth { get; }

        public EnemyLevelScalingProfileV1 LevelScaling { get; }

        public StableId FactionId { get; }

        public double DetectionRadius { get; }

        public double VisionArcDegrees { get; }

        public double AttackArcDegrees { get; }

        public double MinimumAttackRange { get; }

        public double PreferredAttackRange { get; }

        public double MaximumAttackRange { get; }

        public StableId MovementPolicyId { get; }

        public StableId DecisionPolicyId { get; }

        public IReadOnlyList<EnemyAttackCapabilityDescriptorV1> Attacks
        {
            get { return attacks; }
        }

        public StableId ExperienceProfileId { get; }

        public StableId DropProfileId { get; }

        public EnemyCatalogRoomClearRoleV1 RoomClearRole { get; }

        public IReadOnlyList<StableId> SpecialCapabilityIds
        {
            get { return specialCapabilityIds; }
        }

        public double ResolveHealth(int level)
        {
            return LevelScaling == null ? double.NaN : LevelScaling.ResolveHealth(BaseHealth, level);
        }

        public string Fingerprint
        {
            get { return EnemyCatalogFingerprintV1.BuildDefinition(this); }
        }

        private static ReadOnlyCollection<EnemyAttackCapabilityDescriptorV1> Copy(
            IEnumerable<EnemyAttackCapabilityDescriptorV1> values,
            string parameterName)
        {
            if (values == null)
            {
                return new ReadOnlyCollection<EnemyAttackCapabilityDescriptorV1>(
                    new List<EnemyAttackCapabilityDescriptorV1>());
            }

            var result = new List<EnemyAttackCapabilityDescriptorV1>();
            foreach (EnemyAttackCapabilityDescriptorV1 value in values)
            {
                result.Add(value);
            }
            return new ReadOnlyCollection<EnemyAttackCapabilityDescriptorV1>(result);
        }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> values,
            string parameterName)
        {
            if (values == null)
            {
                return new ReadOnlyCollection<StableId>(new List<StableId>());
            }

            var result = new List<StableId>();
            foreach (StableId value in values)
            {
                result.Add(value);
            }
            return new ReadOnlyCollection<StableId>(result);
        }
    }
}
