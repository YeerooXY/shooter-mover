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

    public enum EnemySequenceAimPolicyV1
    {
        LockAtSequenceStart = 1,
        ReaimEachShot = 2,
        TrackUntilShot = 3,
    }

    public enum EnemyAttackInterruptionPolicyV1
    {
        CancelPendingOnLifecycleEnd = 1,
        CompleteCommittedSequence = 2,
    }

    public enum EnemyMeleeAimCommitPolicyV1
    {
        LockAtWindUp = 1,
        TrackUntilActiveWindow = 2,
        LockPerStrike = 3,
    }

    public enum EnemyMeleeTerminalOnImpactPolicyV1
    {
        ContinueSequence = 1,
        EndSequenceOnAnyImpact = 2,
        EndSequenceOnBlockingImpact = 3,
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
                throw new ArgumentOutOfRangeException(nameof(requiredParameters));
            if (!AreValidFlags(allowedParameters))
                throw new ArgumentOutOfRangeException(nameof(allowedParameters));
            if ((requiredParameters & allowedParameters) != requiredParameters)
                throw new ArgumentException(
                    "Required attack parameters must be a subset of allowed parameters.");

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
        bool IsProjectileProfileRegistered(StableId projectileProfileId);
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
        private readonly HashSet<StableId> projectileProfiles;
        private readonly HashSet<StableId> damageChannels;
        private readonly HashSet<StableId> experienceProfiles;
        private readonly HashSet<StableId> dropProfiles;

        public EnemyCatalogRegistryV1(
            IEnumerable<StableId> movementPolicyIds,
            IEnumerable<StableId> decisionPolicyIds,
            IEnumerable<EnemyAttackCapabilityRegistrationV1> attackCapabilityRegistrations,
            IEnumerable<StableId> specialCapabilityIds,
            IEnumerable<StableId> presentationIds,
            IEnumerable<StableId> projectileProfileIds,
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
            projectileProfiles = CopyIds(projectileProfileIds, nameof(projectileProfileIds));
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

        public bool IsProjectileProfileRegistered(StableId projectileProfileId)
        {
            return projectileProfileId != null && projectileProfiles.Contains(projectileProfileId);
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
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new HashSet<StableId>();
            foreach (StableId value in values)
            {
                if (value == null)
                    throw new ArgumentException(
                        "Enemy catalog registries cannot contain null IDs.", parameterName);
                if (!result.Add(value))
                    throw new ArgumentException(
                        "Enemy catalog registry ID is duplicated: " + value, parameterName);
            }
            return result;
        }

        private static Dictionary<StableId, EnemyAttackCapabilityRegistrationV1>
            CopyAttackCapabilities(
                IEnumerable<EnemyAttackCapabilityRegistrationV1> values,
                string parameterName)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new Dictionary<StableId, EnemyAttackCapabilityRegistrationV1>();
            foreach (EnemyAttackCapabilityRegistrationV1 value in values)
            {
                if (value == null)
                    throw new ArgumentException(
                        "Attack capability registries cannot contain null registrations.",
                        parameterName);
                if (result.ContainsKey(value.CapabilityId))
                    throw new ArgumentException(
                        "Attack capability registration is duplicated: " + value.CapabilityId,
                        parameterName);
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

    public sealed class EnemyAreaPayloadV1
    {
        public EnemyAreaPayloadV1(double radius, double durationSeconds, int maximumTargets)
        {
            Radius = radius;
            DurationSeconds = durationSeconds;
            MaximumTargets = maximumTargets;
        }

        public double Radius { get; }
        public double DurationSeconds { get; }
        public int MaximumTargets { get; }
    }

    public sealed class EnemyProjectilePayloadV1
    {
        public EnemyProjectilePayloadV1(
            StableId projectileProfileId,
            double speed,
            double maximumTravelDistance,
            double collisionRadius,
            int pierceCount,
            EnemyAreaPayloadV1 areaPayload)
        {
            ProjectileProfileId = projectileProfileId;
            Speed = speed;
            MaximumTravelDistance = maximumTravelDistance;
            CollisionRadius = collisionRadius;
            PierceCount = pierceCount;
            AreaPayload = areaPayload;
        }

        public StableId ProjectileProfileId { get; }
        public double Speed { get; }
        public double MaximumTravelDistance { get; }
        public double CollisionRadius { get; }
        public int PierceCount { get; }
        public EnemyAreaPayloadV1 AreaPayload { get; }
    }

    public sealed class EnemyShootingPatternV1
    {
        public EnemyShootingPatternV1(
            int shotsPerSequence,
            double intervalBetweenShotsSeconds,
            int projectilesPerShot,
            double perShotSpreadDegrees,
            EnemySequenceAimPolicyV1 sequenceAimPolicy,
            double windUpSeconds,
            double postSequenceRecoverySeconds,
            EnemyAttackInterruptionPolicyV1 interruptionPolicy)
        {
            ShotsPerSequence = shotsPerSequence;
            IntervalBetweenShotsSeconds = intervalBetweenShotsSeconds;
            ProjectilesPerShot = projectilesPerShot;
            PerShotSpreadDegrees = perShotSpreadDegrees;
            SequenceAimPolicy = sequenceAimPolicy;
            WindUpSeconds = windUpSeconds;
            PostSequenceRecoverySeconds = postSequenceRecoverySeconds;
            InterruptionPolicy = interruptionPolicy;
        }

        public int ShotsPerSequence { get; }
        public double IntervalBetweenShotsSeconds { get; }
        public int ProjectilesPerShot { get; }
        public double PerShotSpreadDegrees { get; }
        public EnemySequenceAimPolicyV1 SequenceAimPolicy { get; }
        public double WindUpSeconds { get; }
        public double PostSequenceRecoverySeconds { get; }
        public EnemyAttackInterruptionPolicyV1 InterruptionPolicy { get; }

        public double TotalDurationSeconds
        {
            get
            {
                return WindUpSeconds
                    + (Math.Max(0, ShotsPerSequence - 1) * IntervalBetweenShotsSeconds)
                    + PostSequenceRecoverySeconds;
            }
        }
    }

    public sealed class EnemyMeleePatternV1
    {
        public EnemyMeleePatternV1(
            double windUpSeconds,
            double activeWindowSeconds,
            int strikeCount,
            double intervalBetweenStrikesSeconds,
            double contactRadius,
            double lungeDistance,
            EnemyMeleeAimCommitPolicyV1 aimCommitPolicy,
            double recoverySeconds,
            int hitsPerTarget,
            EnemyMeleeTerminalOnImpactPolicyV1 terminalOnImpactPolicy,
            EnemyAttackInterruptionPolicyV1 interruptionPolicy)
        {
            WindUpSeconds = windUpSeconds;
            ActiveWindowSeconds = activeWindowSeconds;
            StrikeCount = strikeCount;
            IntervalBetweenStrikesSeconds = intervalBetweenStrikesSeconds;
            ContactRadius = contactRadius;
            LungeDistance = lungeDistance;
            AimCommitPolicy = aimCommitPolicy;
            RecoverySeconds = recoverySeconds;
            HitsPerTarget = hitsPerTarget;
            TerminalOnImpactPolicy = terminalOnImpactPolicy;
            InterruptionPolicy = interruptionPolicy;
        }

        public double WindUpSeconds { get; }
        public double ActiveWindowSeconds { get; }
        public int StrikeCount { get; }
        public double IntervalBetweenStrikesSeconds { get; }
        public double ContactRadius { get; }
        public double LungeDistance { get; }
        public EnemyMeleeAimCommitPolicyV1 AimCommitPolicy { get; }
        public double RecoverySeconds { get; }
        public int HitsPerTarget { get; }
        public EnemyMeleeTerminalOnImpactPolicyV1 TerminalOnImpactPolicy { get; }
        public EnemyAttackInterruptionPolicyV1 InterruptionPolicy { get; }

        public double TotalDurationSeconds
        {
            get
            {
                return WindUpSeconds
                    + (Math.Max(0, StrikeCount - 1) * IntervalBetweenStrikesSeconds)
                    + ActiveWindowSeconds
                    + RecoverySeconds;
            }
        }
    }

    /// <summary>
    /// Compatibility projection for schema-v1 callers. New content should use
    /// EnemyShootingPatternV1 plus EnemyProjectilePayloadV1.
    /// </summary>
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
        public EnemyAreaAttackParametersV1(double radius, double durationSeconds, int maximumTargets)
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
            int selectionPriority,
            double attackArcDegrees,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            double damage,
            StableId damageChannelId,
            EnemyShootingPatternV1 shootingPattern,
            EnemyProjectilePayloadV1 projectilePayload,
            EnemyMeleePatternV1 meleePattern)
        {
            AttackId = attackId;
            CapabilityId = capabilityId;
            SelectionPriority = selectionPriority;
            AttackArcDegrees = attackArcDegrees;
            MinimumAttackRange = minimumAttackRange;
            PreferredAttackRange = preferredAttackRange;
            MaximumAttackRange = maximumAttackRange;
            Damage = damage;
            DamageChannelId = damageChannelId;
            ShootingPattern = shootingPattern;
            ProjectilePayload = projectilePayload;
            MeleePattern = meleePattern;
            CooldownSeconds = shootingPattern != null
                ? shootingPattern.TotalDurationSeconds
                : meleePattern == null ? 0d : meleePattern.TotalDurationSeconds;
            Projectile = BuildLegacyProjectile(shootingPattern, projectilePayload);
            Area = BuildLegacyArea(projectilePayload);
            Melee = BuildLegacyMelee(meleePattern);
        }

        public EnemyAttackCapabilityDescriptorV1(
            StableId attackId,
            StableId capabilityId,
            int selectionPriority,
            double attackArcDegrees,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            double cooldownSeconds,
            double damage,
            StableId damageChannelId,
            EnemyProjectileAttackParametersV1 projectile,
            EnemyAreaAttackParametersV1 area,
            EnemyMeleeAttackParametersV1 melee)
            : this(
                attackId,
                capabilityId,
                selectionPriority,
                attackArcDegrees,
                minimumAttackRange,
                preferredAttackRange,
                maximumAttackRange,
                damage,
                damageChannelId,
                BuildShootingPattern(projectile, cooldownSeconds),
                BuildProjectilePayload(projectile, area),
                BuildMeleePattern(melee, cooldownSeconds))
        {
        }

        public StableId AttackId { get; }
        public StableId CapabilityId { get; }
        public int SelectionPriority { get; }
        public double AttackArcDegrees { get; }
        public double MinimumAttackRange { get; }
        public double PreferredAttackRange { get; }
        public double MaximumAttackRange { get; }
        public double CooldownSeconds { get; }
        public double Damage { get; }
        public StableId DamageChannelId { get; }
        public EnemyShootingPatternV1 ShootingPattern { get; }
        public EnemyProjectilePayloadV1 ProjectilePayload { get; }
        public EnemyMeleePatternV1 MeleePattern { get; }

        public EnemyProjectileAttackParametersV1 Projectile { get; }
        public EnemyAreaAttackParametersV1 Area { get; }
        public EnemyMeleeAttackParametersV1 Melee { get; }

        public EnemyAttackParameterKindsV1 ParameterKinds
        {
            get
            {
                EnemyAttackParameterKindsV1 result = EnemyAttackParameterKindsV1.None;
                if (ShootingPattern != null && ProjectilePayload != null)
                    result |= EnemyAttackParameterKindsV1.Projectile;
                if (ProjectilePayload != null && ProjectilePayload.AreaPayload != null)
                    result |= EnemyAttackParameterKindsV1.Area;
                if (MeleePattern != null)
                    result |= EnemyAttackParameterKindsV1.Melee;
                return result;
            }
        }

        public EnemyAttackInterruptionPolicyV1 InterruptionPolicy
        {
            get
            {
                return ShootingPattern != null
                    ? ShootingPattern.InterruptionPolicy
                    : MeleePattern == null
                        ? EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd
                        : MeleePattern.InterruptionPolicy;
            }
        }

        private static EnemyShootingPatternV1 BuildShootingPattern(
            EnemyProjectileAttackParametersV1 projectile,
            double cooldownSeconds)
        {
            return projectile == null
                ? null
                : new EnemyShootingPatternV1(
                    1,
                    0d,
                    projectile.ProjectileCount,
                    projectile.SpreadDegrees,
                    EnemySequenceAimPolicyV1.LockAtSequenceStart,
                    0d,
                    cooldownSeconds,
                    EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd);
        }

        private static EnemyProjectilePayloadV1 BuildProjectilePayload(
            EnemyProjectileAttackParametersV1 projectile,
            EnemyAreaAttackParametersV1 area)
        {
            return projectile == null
                ? null
                : new EnemyProjectilePayloadV1(
                    projectile.ProjectileProfileId,
                    projectile.ProjectileSpeed,
                    projectile.MaximumTravelDistance,
                    projectile.CollisionRadius,
                    projectile.PierceCount,
                    area == null
                        ? null
                        : new EnemyAreaPayloadV1(
                            area.Radius,
                            area.DurationSeconds,
                            area.MaximumTargets));
        }

        private static EnemyMeleePatternV1 BuildMeleePattern(
            EnemyMeleeAttackParametersV1 melee,
            double cooldownSeconds)
        {
            if (melee == null) return null;
            double recovery = Math.Max(
                0d,
                cooldownSeconds - melee.WindUpSeconds - melee.CommitmentSeconds);
            return new EnemyMeleePatternV1(
                melee.WindUpSeconds,
                melee.CommitmentSeconds,
                1,
                0d,
                melee.ContactRadius,
                melee.PounceDistance,
                EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                recovery,
                1,
                EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence,
                EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd);
        }

        private static EnemyProjectileAttackParametersV1 BuildLegacyProjectile(
            EnemyShootingPatternV1 pattern,
            EnemyProjectilePayloadV1 payload)
        {
            return pattern == null || payload == null
                ? null
                : new EnemyProjectileAttackParametersV1(
                    payload.ProjectileProfileId,
                    pattern.ProjectilesPerShot,
                    payload.Speed,
                    payload.MaximumTravelDistance,
                    payload.CollisionRadius,
                    pattern.PerShotSpreadDegrees,
                    payload.PierceCount);
        }

        private static EnemyAreaAttackParametersV1 BuildLegacyArea(
            EnemyProjectilePayloadV1 payload)
        {
            EnemyAreaPayloadV1 area = payload == null ? null : payload.AreaPayload;
            return area == null
                ? null
                : new EnemyAreaAttackParametersV1(
                    area.Radius,
                    area.DurationSeconds,
                    area.MaximumTargets);
        }

        private static EnemyMeleeAttackParametersV1 BuildLegacyMelee(
            EnemyMeleePatternV1 pattern)
        {
            return pattern == null
                ? null
                : new EnemyMeleeAttackParametersV1(
                    pattern.ContactRadius,
                    pattern.LungeDistance,
                    pattern.WindUpSeconds,
                    pattern.ActiveWindowSeconds);
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
            MovementPolicyId = movementPolicyId;
            DecisionPolicyId = decisionPolicyId;
            this.attacks = CopyAttacks(attacks);
            ExperienceProfileId = experienceProfileId;
            DropProfileId = dropProfileId;
            RoomClearRole = roomClearRole;
            this.specialCapabilityIds = CopyIds(specialCapabilityIds);
        }

        public StableId DefinitionId { get; }
        public StableId PresentationId { get; }
        public double BaseHealth { get; }
        public EnemyLevelScalingProfileV1 LevelScaling { get; }

        /// <summary>
        /// Open stable identity. The repository has no canonical faction registry at this boundary.
        /// </summary>
        public StableId FactionId { get; }
        public double DetectionRadius { get; }
        public double VisionArcDegrees { get; }
        public StableId MovementPolicyId { get; }
        public StableId DecisionPolicyId { get; }
        public IReadOnlyList<EnemyAttackCapabilityDescriptorV1> Attacks { get { return attacks; } }
        public StableId ExperienceProfileId { get; }
        public StableId DropProfileId { get; }
        public EnemyCatalogRoomClearRoleV1 RoomClearRole { get; }
        public IReadOnlyList<StableId> SpecialCapabilityIds { get { return specialCapabilityIds; } }

        public double ResolveHealth(int level)
        {
            return LevelScaling == null ? double.NaN : LevelScaling.ResolveHealth(BaseHealth, level);
        }

        public string Fingerprint
        {
            get { return EnemyCatalogFingerprintV1.BuildDefinition(this); }
        }

        public EnemyDefinitionV1 WithAttacks(
            IEnumerable<EnemyAttackCapabilityDescriptorV1> replacementAttacks)
        {
            return new EnemyDefinitionV1(
                DefinitionId,
                PresentationId,
                BaseHealth,
                LevelScaling,
                FactionId,
                DetectionRadius,
                VisionArcDegrees,
                MovementPolicyId,
                DecisionPolicyId,
                replacementAttacks,
                ExperienceProfileId,
                DropProfileId,
                RoomClearRole,
                SpecialCapabilityIds);
        }

        private static ReadOnlyCollection<EnemyAttackCapabilityDescriptorV1> CopyAttacks(
            IEnumerable<EnemyAttackCapabilityDescriptorV1> values)
        {
            return new ReadOnlyCollection<EnemyAttackCapabilityDescriptorV1>(
                values == null
                    ? new List<EnemyAttackCapabilityDescriptorV1>()
                    : new List<EnemyAttackCapabilityDescriptorV1>(values));
        }

        private static ReadOnlyCollection<StableId> CopyIds(IEnumerable<StableId> values)
        {
            return new ReadOnlyCollection<StableId>(
                values == null ? new List<StableId>() : new List<StableId>(values));
        }
    }
}
