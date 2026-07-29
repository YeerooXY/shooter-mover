using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    [Flags]
    public enum EnemyAttackParameterKinds
    {
        None = 0,
        Projectile = 1,
        Area = 2,
        Melee = 4,
    }

    public enum EnemyCatalogRoomClearRole
    {
        RequiredEnemy = 1,
        OptionalEnemy = 2,
        ObjectiveEntity = 3,
        DoesNotAffectRoomClear = 4,
    }

    public enum EnemySequenceAimPolicy
    {
        LockAtSequenceStart = 1,
        ReaimEachShot = 2,
        TrackUntilShot = 3,
    }

    public enum EnemyAttackInterruptionPolicy
    {
        CancelPendingOnLifecycleEnd = 1,
        CompleteCommittedSequence = 2,
    }

    public enum EnemyMeleeAimCommitPolicy
    {
        LockAtWindUp = 1,
        TrackUntilActiveWindow = 2,
        LockPerStrike = 3,
    }

    public enum EnemyMeleeTerminalOnImpactPolicy
    {
        ContinueSequence = 1,
        EndSequenceOnAnyImpact = 2,
        EndSequenceOnBlockingImpact = 3,
    }

    public sealed class EnemyAttackCapabilityRegistration
    {
        public EnemyAttackCapabilityRegistration(
            StableId capabilityId,
            EnemyAttackParameterKinds requiredParameters,
            EnemyAttackParameterKinds allowedParameters)
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
        public EnemyAttackParameterKinds RequiredParameters { get; }
        public EnemyAttackParameterKinds AllowedParameters { get; }

        private static bool AreValidFlags(EnemyAttackParameterKinds value)
        {
            const EnemyAttackParameterKinds all =
                EnemyAttackParameterKinds.Projectile
                | EnemyAttackParameterKinds.Area
                | EnemyAttackParameterKinds.Melee;
            return (value & ~all) == 0;
        }
    }

    public interface IEnemyCatalogRegistry
    {
        bool IsMovementPolicyRegistered(StableId movementPolicyId);
        bool IsDecisionPolicyRegistered(StableId decisionPolicyId);
        bool TryResolveAttackCapability(
            StableId capabilityId,
            out EnemyAttackCapabilityRegistration registration);
        bool IsSpecialCapabilityRegistered(StableId capabilityId);
        bool IsPresentationRegistered(StableId presentationId);
        bool IsProjectileProfileRegistered(StableId projectileProfileId);
        bool IsDamageChannelRegistered(StableId damageChannelId);
        bool IsExperienceProfileRegistered(StableId experienceProfileId);
        bool IsDropProfileRegistered(StableId dropProfileId);
    }

    public sealed class EnemyCatalogRegistry : IEnemyCatalogRegistry
    {
        private readonly HashSet<StableId> movementPolicies;
        private readonly HashSet<StableId> decisionPolicies;
        private readonly Dictionary<StableId, EnemyAttackCapabilityRegistration> attackCapabilities;
        private readonly HashSet<StableId> specialCapabilities;
        private readonly HashSet<StableId> presentations;
        private readonly HashSet<StableId> projectileProfiles;
        private readonly HashSet<StableId> damageChannels;
        private readonly HashSet<StableId> experienceProfiles;
        private readonly HashSet<StableId> dropProfiles;

        public EnemyCatalogRegistry(
            IEnumerable<StableId> movementPolicyIds,
            IEnumerable<StableId> decisionPolicyIds,
            IEnumerable<EnemyAttackCapabilityRegistration> attackCapabilityRegistrations,
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
            out EnemyAttackCapabilityRegistration registration)
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

        private static Dictionary<StableId, EnemyAttackCapabilityRegistration>
            CopyAttackCapabilities(
                IEnumerable<EnemyAttackCapabilityRegistration> values,
                string parameterName)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new Dictionary<StableId, EnemyAttackCapabilityRegistration>();
            foreach (EnemyAttackCapabilityRegistration value in values)
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

    public sealed class EnemyLevelScalingProfile
    {
        public EnemyLevelScalingProfile(
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

    public sealed class EnemyAreaPayload
    {
        public EnemyAreaPayload(double radius, double durationSeconds, int maximumTargets)
        {
            Radius = radius;
            DurationSeconds = durationSeconds;
            MaximumTargets = maximumTargets;
        }

        public double Radius { get; }
        public double DurationSeconds { get; }
        public int MaximumTargets { get; }
    }

    public sealed class EnemyProjectilePayload
    {
        public EnemyProjectilePayload(
            StableId projectileProfileId,
            double speed,
            double maximumTravelDistance,
            double collisionRadius,
            int pierceCount,
            EnemyAreaPayload areaPayload)
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
        public EnemyAreaPayload AreaPayload { get; }
    }

    public sealed class EnemyShootingPattern
    {
        public EnemyShootingPattern(
            int shotsPerSequence,
            double intervalBetweenShotsSeconds,
            int projectilesPerShot,
            double perShotSpreadDegrees,
            EnemySequenceAimPolicy sequenceAimPolicy,
            double windUpSeconds,
            double postSequenceRecoverySeconds,
            EnemyAttackInterruptionPolicy interruptionPolicy)
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
        public EnemySequenceAimPolicy SequenceAimPolicy { get; }
        public double WindUpSeconds { get; }
        public double PostSequenceRecoverySeconds { get; }
        public EnemyAttackInterruptionPolicy InterruptionPolicy { get; }

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

    public sealed class EnemyMeleePattern
    {
        public EnemyMeleePattern(
            double windUpSeconds,
            double activeWindowSeconds,
            int strikeCount,
            double intervalBetweenStrikesSeconds,
            double contactRadius,
            double lungeDistance,
            EnemyMeleeAimCommitPolicy aimCommitPolicy,
            double recoverySeconds,
            int hitsPerTarget,
            EnemyMeleeTerminalOnImpactPolicy terminalOnImpactPolicy,
            EnemyAttackInterruptionPolicy interruptionPolicy)
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
        public EnemyMeleeAimCommitPolicy AimCommitPolicy { get; }
        public double RecoverySeconds { get; }
        public int HitsPerTarget { get; }
        public EnemyMeleeTerminalOnImpactPolicy TerminalOnImpactPolicy { get; }
        public EnemyAttackInterruptionPolicy InterruptionPolicy { get; }

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
    /// EnemyShootingPattern plus EnemyProjectilePayload.
    /// </summary>
    public sealed class EnemyProjectileAttackParameters
    {
        public EnemyProjectileAttackParameters(
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

    public sealed class EnemyAreaAttackParameters
    {
        public EnemyAreaAttackParameters(double radius, double durationSeconds, int maximumTargets)
        {
            Radius = radius;
            DurationSeconds = durationSeconds;
            MaximumTargets = maximumTargets;
        }

        public double Radius { get; }
        public double DurationSeconds { get; }
        public int MaximumTargets { get; }
    }

    public sealed class EnemyMeleeAttackParameters
    {
        public EnemyMeleeAttackParameters(
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

    public sealed class EnemyAttackCapabilityDescriptor
    {
        public EnemyAttackCapabilityDescriptor(
            StableId attackId,
            StableId capabilityId,
            int selectionPriority,
            double attackArcDegrees,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            double damage,
            StableId damageChannelId,
            EnemyShootingPattern shootingPattern,
            EnemyProjectilePayload projectilePayload,
            EnemyMeleePattern meleePattern)
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

        public EnemyAttackCapabilityDescriptor(
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
            EnemyProjectileAttackParameters projectile,
            EnemyAreaAttackParameters area,
            EnemyMeleeAttackParameters melee)
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
        public EnemyShootingPattern ShootingPattern { get; }
        public EnemyProjectilePayload ProjectilePayload { get; }
        public EnemyMeleePattern MeleePattern { get; }

        public EnemyProjectileAttackParameters Projectile { get; }
        public EnemyAreaAttackParameters Area { get; }
        public EnemyMeleeAttackParameters Melee { get; }

        public EnemyAttackParameterKinds ParameterKinds
        {
            get
            {
                EnemyAttackParameterKinds result = EnemyAttackParameterKinds.None;
                if (ShootingPattern != null && ProjectilePayload != null)
                    result |= EnemyAttackParameterKinds.Projectile;
                if (ProjectilePayload != null && ProjectilePayload.AreaPayload != null)
                    result |= EnemyAttackParameterKinds.Area;
                if (MeleePattern != null)
                    result |= EnemyAttackParameterKinds.Melee;
                return result;
            }
        }

        public EnemyAttackInterruptionPolicy InterruptionPolicy
        {
            get
            {
                return ShootingPattern != null
                    ? ShootingPattern.InterruptionPolicy
                    : MeleePattern == null
                        ? EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd
                        : MeleePattern.InterruptionPolicy;
            }
        }

        private static EnemyShootingPattern BuildShootingPattern(
            EnemyProjectileAttackParameters projectile,
            double cooldownSeconds)
        {
            return projectile == null
                ? null
                : new EnemyShootingPattern(
                    1,
                    0d,
                    projectile.ProjectileCount,
                    projectile.SpreadDegrees,
                    EnemySequenceAimPolicy.LockAtSequenceStart,
                    0d,
                    cooldownSeconds,
                    EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd);
        }

        private static EnemyProjectilePayload BuildProjectilePayload(
            EnemyProjectileAttackParameters projectile,
            EnemyAreaAttackParameters area)
        {
            return projectile == null
                ? null
                : new EnemyProjectilePayload(
                    projectile.ProjectileProfileId,
                    projectile.ProjectileSpeed,
                    projectile.MaximumTravelDistance,
                    projectile.CollisionRadius,
                    projectile.PierceCount,
                    area == null
                        ? null
                        : new EnemyAreaPayload(
                            area.Radius,
                            area.DurationSeconds,
                            area.MaximumTargets));
        }

        private static EnemyMeleePattern BuildMeleePattern(
            EnemyMeleeAttackParameters melee,
            double cooldownSeconds)
        {
            if (melee == null) return null;
            double recovery = Math.Max(
                0d,
                cooldownSeconds - melee.WindUpSeconds - melee.CommitmentSeconds);
            return new EnemyMeleePattern(
                melee.WindUpSeconds,
                melee.CommitmentSeconds,
                1,
                0d,
                melee.ContactRadius,
                melee.PounceDistance,
                EnemyMeleeAimCommitPolicy.LockAtWindUp,
                recovery,
                1,
                EnemyMeleeTerminalOnImpactPolicy.ContinueSequence,
                EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd);
        }

        private static EnemyProjectileAttackParameters BuildLegacyProjectile(
            EnemyShootingPattern pattern,
            EnemyProjectilePayload payload)
        {
            return pattern == null || payload == null
                ? null
                : new EnemyProjectileAttackParameters(
                    payload.ProjectileProfileId,
                    pattern.ProjectilesPerShot,
                    payload.Speed,
                    payload.MaximumTravelDistance,
                    payload.CollisionRadius,
                    pattern.PerShotSpreadDegrees,
                    payload.PierceCount);
        }

        private static EnemyAreaAttackParameters BuildLegacyArea(
            EnemyProjectilePayload payload)
        {
            EnemyAreaPayload area = payload == null ? null : payload.AreaPayload;
            return area == null
                ? null
                : new EnemyAreaAttackParameters(
                    area.Radius,
                    area.DurationSeconds,
                    area.MaximumTargets);
        }

        private static EnemyMeleeAttackParameters BuildLegacyMelee(
            EnemyMeleePattern pattern)
        {
            return pattern == null
                ? null
                : new EnemyMeleeAttackParameters(
                    pattern.ContactRadius,
                    pattern.LungeDistance,
                    pattern.WindUpSeconds,
                    pattern.ActiveWindowSeconds);
        }
    }

    public sealed class EnemyDefinition
    {
        private readonly ReadOnlyCollection<EnemyAttackCapabilityDescriptor> attacks;
        private readonly ReadOnlyCollection<StableId> specialCapabilityIds;

        public EnemyDefinition(
            StableId definitionId,
            StableId presentationId,
            double baseHealth,
            EnemyLevelScalingProfile levelScaling,
            StableId factionId,
            double detectionRadius,
            double visionArcDegrees,
            StableId movementPolicyId,
            StableId decisionPolicyId,
            IEnumerable<EnemyAttackCapabilityDescriptor> attacks,
            StableId experienceProfileId,
            StableId dropProfileId,
            EnemyCatalogRoomClearRole roomClearRole,
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
        public EnemyLevelScalingProfile LevelScaling { get; }

        /// <summary>
        /// Open stable identity. The repository has no canonical faction registry at this boundary.
        /// </summary>
        public StableId FactionId { get; }
        public double DetectionRadius { get; }
        public double VisionArcDegrees { get; }
        public StableId MovementPolicyId { get; }
        public StableId DecisionPolicyId { get; }
        public IReadOnlyList<EnemyAttackCapabilityDescriptor> Attacks { get { return attacks; } }
        public StableId ExperienceProfileId { get; }
        public StableId DropProfileId { get; }
        public EnemyCatalogRoomClearRole RoomClearRole { get; }
        public IReadOnlyList<StableId> SpecialCapabilityIds { get { return specialCapabilityIds; } }

        public double ResolveHealth(int level)
        {
            return LevelScaling == null ? double.NaN : LevelScaling.ResolveHealth(BaseHealth, level);
        }

        public string Fingerprint
        {
            get { return EnemyCatalogFingerprint.BuildDefinition(this); }
        }

        public EnemyDefinition WithAttacks(
            IEnumerable<EnemyAttackCapabilityDescriptor> replacementAttacks)
        {
            return new EnemyDefinition(
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

        private static ReadOnlyCollection<EnemyAttackCapabilityDescriptor> CopyAttacks(
            IEnumerable<EnemyAttackCapabilityDescriptor> values)
        {
            return new ReadOnlyCollection<EnemyAttackCapabilityDescriptor>(
                values == null
                    ? new List<EnemyAttackCapabilityDescriptor>()
                    : new List<EnemyAttackCapabilityDescriptor>(values));
        }

        private static ReadOnlyCollection<StableId> CopyIds(IEnumerable<StableId> values)
        {
            return new ReadOnlyCollection<StableId>(
                values == null ? new List<StableId>() : new List<StableId>(values));
        }
    }
}
