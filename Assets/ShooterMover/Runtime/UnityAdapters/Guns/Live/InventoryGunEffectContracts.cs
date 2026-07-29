using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public sealed class InventoryGunEffectProfile
    {
        public InventoryGunEffectProfile(
            GunDefinitionId definitionId,
            double fireRate,
            int cooldownTicks,
            int projectileCount,
            double spreadDegrees,
            double projectileSpeed,
            double range,
            double directDamagePerProjectile,
            int pierce,
            double areaDamagePerTrigger,
            double explosionRadius,
            double damageOverTimePerSecond,
            double damageOverTimeDuration,
            double poolRadius,
            double poolDuration,
            int chainTargets,
            double chainRange,
            double knockback,
            string damageType)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            FireRate = fireRate;
            CooldownTicks = cooldownTicks;
            ProjectileCount = projectileCount;
            SpreadDegrees = spreadDegrees;
            ProjectileSpeed = projectileSpeed;
            Range = range;
            DirectDamagePerProjectile = directDamagePerProjectile;
            Pierce = pierce;
            AreaDamagePerTrigger = areaDamagePerTrigger;
            ExplosionRadius = explosionRadius;
            DamageOverTimePerSecond = damageOverTimePerSecond;
            DamageOverTimeDuration = damageOverTimeDuration;
            PoolRadius = poolRadius;
            PoolDuration = poolDuration;
            ChainTargets = chainTargets;
            ChainRange = chainRange;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
            CanonicalText = BuildCanonicalText();
            Fingerprint = GunExecutionFingerprint.Compute(CanonicalText);
        }

        public GunDefinitionId DefinitionId { get; }
        public double FireRate { get; }
        public int CooldownTicks { get; }
        public int ProjectileCount { get; }
        public double SpreadDegrees { get; }
        public double ProjectileSpeed { get; }
        public double Range { get; }
        public double DirectDamagePerProjectile { get; }
        public int Pierce { get; }
        public double AreaDamagePerTrigger { get; }
        public double ExplosionRadius { get; }
        public double DamageOverTimePerSecond { get; }
        public double DamageOverTimeDuration { get; }
        public double PoolRadius { get; }
        public double PoolDuration { get; }
        public int ChainTargets { get; }
        public double ChainRange { get; }
        public double Knockback { get; }
        public string DamageType { get; }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        internal static InventoryGunEffectProfile From(
            EffectiveGun gun,
            GunLiveFiringProfile runtimeProfile)
        {
            if (gun == null)
            {
                throw new ArgumentNullException(nameof(gun));
            }
            if (runtimeProfile == null)
            {
                throw new ArgumentNullException(nameof(runtimeProfile));
            }
            if (!gun.DefinitionId.Equals(runtimeProfile.DefinitionId))
            {
                throw new ArgumentException(
                    "The runtime profile must describe the supplied effective gun.",
                    nameof(runtimeProfile));
            }

            return new InventoryGunEffectProfile(
                gun.DefinitionId,
                gun.FireSettings.ShotsPerSecond,
                runtimeProfile.CooldownTicks,
                runtimeProfile.ProjectileCount,
                runtimeProfile.SpreadDegrees,
                runtimeProfile.ProjectileSpeed,
                runtimeProfile.ProjectileRange,
                runtimeProfile.DirectDamage,
                runtimeProfile.Pierce,
                runtimeProfile.AreaDamage,
                runtimeProfile.ExplosionRadius,
                runtimeProfile.DotDps,
                runtimeProfile.DotDuration,
                runtimeProfile.PoolRadius,
                runtimeProfile.PoolDuration,
                runtimeProfile.ChainTargets,
                runtimeProfile.ChainRange,
                runtimeProfile.Knockback,
                runtimeProfile.DamageType);
        }

        internal static InventoryGunEffectProfile From(
            GunDefinitionData definition,
            int simulationTicksPerSecond)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int cooldownTicks = Math.Max(
                1,
                (int)Math.Ceiling(simulationTicksPerSecond / definition.FireRate));
            return new InventoryGunEffectProfile(
                new GunDefinitionId(definition.DefinitionId),
                definition.FireRate,
                cooldownTicks,
                definition.ProjectilesPerTrigger,
                definition.SpreadDegrees,
                definition.ProjectileSpeed,
                definition.Range,
                definition.DamagePerProjectile,
                definition.Pierce,
                definition.AreaDamagePerTrigger,
                definition.ExplosionRadius,
                definition.DotDps,
                definition.DotDuration,
                definition.PoolRadius,
                definition.PoolDuration,
                definition.ChainTargets,
                definition.ChainRange,
                definition.Knockback,
                definition.DamageType);
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, "definition_id", DefinitionId.ToString());
            Append(builder, "fire_rate", Format(FireRate));
            Append(builder, "cooldown_ticks", CooldownTicks.ToString(CultureInfo.InvariantCulture));
            Append(builder, "projectile_count", ProjectileCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, "spread_degrees", Format(SpreadDegrees));
            Append(builder, "projectile_speed", Format(ProjectileSpeed));
            Append(builder, "range", Format(Range));
            Append(builder, "direct_damage", Format(DirectDamagePerProjectile));
            Append(builder, "pierce", Pierce.ToString(CultureInfo.InvariantCulture));
            Append(builder, "area_damage", Format(AreaDamagePerTrigger));
            Append(builder, "explosion_radius", Format(ExplosionRadius));
            Append(builder, "dot_dps", Format(DamageOverTimePerSecond));
            Append(builder, "dot_duration", Format(DamageOverTimeDuration));
            Append(builder, "pool_radius", Format(PoolRadius));
            Append(builder, "pool_duration", Format(PoolDuration));
            Append(builder, "chain_targets", ChainTargets.ToString(CultureInfo.InvariantCulture));
            Append(builder, "chain_range", Format(ChainRange));
            Append(builder, "knockback", Format(Knockback));
            Append(builder, "damage_type", DamageType);
            return builder.ToString();
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append('=').Append(value ?? "null").Append('\n');
        }
    }

    public sealed class InventoryGunEffectBatch
    {
        public InventoryGunEffectBatch(
            GunEffectBatch coreBatch,
            InventoryGunEffectProfile profile)
        {
            CoreBatch = coreBatch ?? throw new ArgumentNullException(nameof(coreBatch));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (coreBatch.Identity == null
                || !profile.DefinitionId.Equals(coreBatch.Identity.GunDefinitionId))
            {
                throw new ArgumentException(
                    "The catalog profile must describe the gun definition carried by the core batch.",
                    nameof(profile));
            }

            CanonicalText = "core_batch=" + coreBatch.Fingerprint + "\n"
                + "profile=" + profile.Fingerprint + "\n";
            Fingerprint = GunExecutionFingerprint.Compute(CanonicalText);
        }

        public GunEffectBatch CoreBatch { get; }
        public InventoryGunEffectProfile Profile { get; }
        public GunEffectIdentity Identity { get { return CoreBatch.Identity; } }
        public int EffectCount { get { return CoreBatch.EffectCount; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }
    }

    public enum InventoryGunExecutionOutcomeKind
    {
        AcceptedEmissionDelivery = 1,
        ReplayedEmissionDelivery = 2,
        AcceptedScheduleQueued = 3,
        ReplayedScheduleRetained = 4,
        AcceptedNoEmissionTransition = 5,
        ReplayedNoEmissionTransition = 6,
        WaitingForCadence = 7,
        Released = 8,
        NoDueDelivery = 9,
        RetryableDeliveryFailure = 10,
        SchedulerRejected = 11,
        IntegrationRejected = 12,
    }

    /// <summary>
    /// Honest live-operation result. Complete ordered per-mount outcomes and downstream deliveries
    /// are separate immutable observations, so failures cannot hide successful schedules or batches.
    /// </summary>
    public sealed class InventoryGunExecutionResult
    {
        private readonly ReadOnlyCollection<InventoryGunEffectBatch> deliveredBatches;
        private readonly ReadOnlyCollection<InventoryGunSchedulingOutcome> schedulingOutcomes;
        private readonly ReadOnlyCollection<InventoryGunMountExecutionOutcome> mountOutcomes;

        internal InventoryGunExecutionResult(
            EquipmentInstanceId equipmentInstanceId,
            InventoryGunExecutionOutcomeKind outcomeKind,
            GunExecutionStatus status,
            string rejectionCode,
            GunExecutionResult execution,
            GunFiringScheduleStatus? schedulerStatus,
            bool isExactReplay,
            int scheduledEmissionCount,
            int acceptedDeliveryCount,
            int alreadyAcceptedDeliveryCount,
            int pendingDeliveryCount,
            IEnumerable<InventoryGunEffectBatch> delivered,
            IEnumerable<InventoryGunSchedulingOutcome> scheduling = null,
            IEnumerable<InventoryGunMountExecutionOutcome> mounts = null)
        {
            if (scheduledEmissionCount < 0
                || acceptedDeliveryCount < 0
                || alreadyAcceptedDeliveryCount < 0
                || pendingDeliveryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledEmissionCount));
            }
            if (!Enum.IsDefined(typeof(InventoryGunExecutionOutcomeKind), outcomeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(outcomeKind));
            }

            EquipmentInstanceId = equipmentInstanceId;
            OutcomeKind = outcomeKind;
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Execution = execution;
            SchedulerStatus = schedulerStatus;
            IsExactReplay = isExactReplay;
            ScheduledEmissionCount = scheduledEmissionCount;
            AcceptedDeliveryCount = acceptedDeliveryCount;
            AlreadyAcceptedDeliveryCount = alreadyAcceptedDeliveryCount;
            PendingDeliveryCount = pendingDeliveryCount;
            deliveredBatches = new ReadOnlyCollection<InventoryGunEffectBatch>(
                delivered == null
                    ? new List<InventoryGunEffectBatch>()
                    : new List<InventoryGunEffectBatch>(delivered));
            schedulingOutcomes = new ReadOnlyCollection<InventoryGunSchedulingOutcome>(
                scheduling == null
                    ? new List<InventoryGunSchedulingOutcome>()
                    : new List<InventoryGunSchedulingOutcome>(scheduling));
            mountOutcomes = new ReadOnlyCollection<InventoryGunMountExecutionOutcome>(
                mounts == null
                    ? new List<InventoryGunMountExecutionOutcome>()
                    : new List<InventoryGunMountExecutionOutcome>(mounts));
        }

        /// <summary>
        /// Retained source-compatible constructor for legacy callers that already own a concrete
        /// effect delivery result. It must not be used for no-emission scheduler transitions.
        /// </summary>
        public InventoryGunExecutionResult(
            EquipmentInstanceId equipmentInstanceId,
            GunExecutionResult execution,
            InventoryGunEffectBatch effectBatch)
            : this(
                equipmentInstanceId,
                execution != null
                    && execution.Status == GunExecutionStatus.ReplayAccepted
                        ? InventoryGunExecutionOutcomeKind.ReplayedEmissionDelivery
                        : execution != null
                            && execution.Status == GunExecutionStatus.Accepted
                                ? InventoryGunExecutionOutcomeKind.AcceptedEmissionDelivery
                                : InventoryGunExecutionOutcomeKind.IntegrationRejected,
                execution == null
                    ? GunExecutionStatus.InvalidCommand
                    : execution.Status,
                execution == null ? "gun-live-execution-result-null" : execution.RejectionCode,
                execution,
                null,
                execution != null
                    && execution.Status == GunExecutionStatus.ReplayAccepted,
                0,
                execution != null
                    && execution.Status == GunExecutionStatus.Accepted
                    && effectBatch != null ? 1 : 0,
                execution != null
                    && execution.Status == GunExecutionStatus.ReplayAccepted
                    && effectBatch != null ? 1 : 0,
                0,
                effectBatch == null
                    ? new InventoryGunEffectBatch[0]
                    : new[] { effectBatch })
        {
        }

        public EquipmentInstanceId EquipmentInstanceId { get; }
        public InventoryGunExecutionOutcomeKind OutcomeKind { get; }
        public GunExecutionResult Execution { get; }
        public GunExecutionStatus Status { get; }
        public string RejectionCode { get; }
        public GunFiringScheduleStatus? SchedulerStatus { get; }
        public bool IsExactReplay { get; }
        public int ScheduledEmissionCount { get; }
        public int AcceptedDeliveryCount { get; }
        public int AlreadyAcceptedDeliveryCount { get; }
        public int DeliveredBatchCount { get { return deliveredBatches.Count; } }
        public int PendingDeliveryCount { get; }
        public IReadOnlyList<InventoryGunEffectBatch> DeliveredBatches
        {
            get { return deliveredBatches; }
        }
        public IReadOnlyList<InventoryGunMountExecutionOutcome> MountOutcomes
        {
            get { return mountOutcomes; }
        }
        /// <summary>
        /// Retained successful-scheduling-only compatibility projection. New callers should inspect
        /// MountOutcomes, which includes every success and rejection.
        /// </summary>
        public IReadOnlyList<InventoryGunSchedulingOutcome> SchedulingOutcomes
        {
            get { return schedulingOutcomes; }
        }
        public InventoryGunEffectBatch EffectBatch
        {
            get { return deliveredBatches.Count == 1 ? deliveredBatches[0] : null; }
        }
        public bool HasShotSequence { get { return deliveredBatches.Count > 0; } }
        public long? LastDeliveredShotSequence
        {
            get
            {
                return deliveredBatches.Count == 0
                    ? (long?)null
                    : deliveredBatches[deliveredBatches.Count - 1]
                        .Identity.ShotSequence;
            }
        }
        public bool IsNoEmissionTransition
        {
            get
            {
                for (int index = 0; index < mountOutcomes.Count; index++)
                {
                    if (mountOutcomes[index].IsNoEmissionTransition)
                    {
                        return true;
                    }
                }
                for (int index = 0; index < schedulingOutcomes.Count; index++)
                {
                    if (schedulingOutcomes[index].IsNoEmissionTransition)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool HasAcceptedNoEmissionTransition
        {
            get
            {
                return HasMountOutcome(
                    InventoryGunExecutionOutcomeKind.AcceptedNoEmissionTransition)
                    || HasSchedulingOutcome(
                        InventoryGunExecutionOutcomeKind.AcceptedNoEmissionTransition);
            }
        }
        public bool HasReplayedNoEmissionTransition
        {
            get
            {
                return HasMountOutcome(
                    InventoryGunExecutionOutcomeKind.ReplayedNoEmissionTransition)
                    || HasSchedulingOutcome(
                        InventoryGunExecutionOutcomeKind.ReplayedNoEmissionTransition);
            }
        }
        public bool IsWaitingForCadence
        {
            get
            {
                for (int index = 0; index < mountOutcomes.Count; index++)
                {
                    if (mountOutcomes[index].IsNoEmissionTransition
                        && mountOutcomes[index].SchedulerStatus
                            == GunFiringScheduleStatus.WaitingForCadence)
                    {
                        return true;
                    }
                }
                for (int index = 0; index < schedulingOutcomes.Count; index++)
                {
                    if (schedulingOutcomes[index].IsWaitingForCadence)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool IsReleaseTransition
        {
            get
            {
                for (int index = 0; index < mountOutcomes.Count; index++)
                {
                    if (mountOutcomes[index].IsNoEmissionTransition
                        && mountOutcomes[index].SchedulerStatus
                            == GunFiringScheduleStatus.Released)
                    {
                        return true;
                    }
                }
                for (int index = 0; index < schedulingOutcomes.Count; index++)
                {
                    if (schedulingOutcomes[index].IsReleaseTransition)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool Succeeded
        {
            get
            {
                return OutcomeKind != InventoryGunExecutionOutcomeKind.RetryableDeliveryFailure
                    && OutcomeKind != InventoryGunExecutionOutcomeKind.SchedulerRejected
                    && OutcomeKind != InventoryGunExecutionOutcomeKind.IntegrationRejected;
            }
        }
        public GunDefinitionId GunDefinitionId
        {
            get { return EffectBatch == null ? null : EffectBatch.Profile.DefinitionId; }
        }

        internal InventoryGunExecutionResult WithMountOutcomes(
            IEnumerable<InventoryGunMountExecutionOutcome> outcomes,
            int scheduledEmissionCount)
        {
            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }
            var copy = new List<InventoryGunMountExecutionOutcome>(outcomes);
            EquipmentInstanceId commonEquipmentInstanceId =
                FindCommonEquipmentInstanceId(copy, deliveredBatches);
            GunFiringScheduleStatus? singleStatus = copy.Count == 1
                ? copy[0].SchedulerStatus
                : null;
            return new InventoryGunExecutionResult(
                commonEquipmentInstanceId,
                OutcomeKind,
                Status,
                RejectionCode,
                Execution,
                singleStatus,
                IsExactReplay,
                scheduledEmissionCount,
                AcceptedDeliveryCount,
                AlreadyAcceptedDeliveryCount,
                PendingDeliveryCount,
                deliveredBatches,
                schedulingOutcomes,
                copy);
        }

        internal InventoryGunExecutionResult WithSchedulingOutcomes(
            IList<InventoryGunSchedulingOutcome> outcomes,
            int scheduledEmissionCount)
        {
            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }
            EquipmentInstanceId commonEquipmentInstanceId =
                FindCommonEquipmentInstanceId(outcomes, deliveredBatches);
            GunFiringScheduleStatus? singleStatus = outcomes.Count == 1
                ? (GunFiringScheduleStatus?)outcomes[0].SchedulerStatus
                : null;
            return new InventoryGunExecutionResult(
                commonEquipmentInstanceId,
                OutcomeKind,
                Status,
                RejectionCode,
                Execution,
                singleStatus,
                IsExactReplay,
                scheduledEmissionCount,
                AcceptedDeliveryCount,
                AlreadyAcceptedDeliveryCount,
                PendingDeliveryCount,
                deliveredBatches,
                outcomes,
                mountOutcomes);
        }

        internal static InventoryGunExecutionResult Schedule(
            EquipmentInstanceId equipmentInstanceId,
            bool replay,
            int scheduledEmissionCount,
            int pendingDeliveryCount)
        {
            InventoryGunExecutionOutcomeKind kind = replay
                ? InventoryGunExecutionOutcomeKind.ReplayedScheduleRetained
                : InventoryGunExecutionOutcomeKind.AcceptedScheduleQueued;
            GunExecutionStatus status = replay
                ? GunExecutionStatus.ReplayAccepted
                : GunExecutionStatus.Accepted;
            GunFiringScheduleStatus schedulerStatus = replay
                ? GunFiringScheduleStatus.Replayed
                : GunFiringScheduleStatus.Accepted;
            var scheduling = new[]
            {
                new InventoryGunSchedulingOutcome(
                    equipmentInstanceId,
                    kind,
                    status,
                    schedulerStatus,
                    replay,
                    scheduledEmissionCount),
            };
            return new InventoryGunExecutionResult(
                equipmentInstanceId,
                kind,
                status,
                string.Empty,
                null,
                schedulerStatus,
                replay,
                scheduledEmissionCount,
                0,
                0,
                pendingDeliveryCount,
                new InventoryGunEffectBatch[0],
                scheduling);
        }

        internal static InventoryGunExecutionResult Transition(
            EquipmentInstanceId equipmentInstanceId,
            bool replay,
            GunFiringScheduleStatus schedulerStatus,
            int pendingDeliveryCount)
        {
            InventoryGunExecutionOutcomeKind kind = replay
                ? InventoryGunExecutionOutcomeKind.ReplayedNoEmissionTransition
                : InventoryGunExecutionOutcomeKind.AcceptedNoEmissionTransition;
            GunExecutionStatus status = schedulerStatus
                    == GunFiringScheduleStatus.WaitingForCadence
                ? GunExecutionStatus.CooldownActive
                : replay
                    ? GunExecutionStatus.ReplayAccepted
                    : GunExecutionStatus.Accepted;
            var scheduling = new[]
            {
                new InventoryGunSchedulingOutcome(
                    equipmentInstanceId,
                    kind,
                    status,
                    schedulerStatus,
                    replay,
                    0),
            };
            return new InventoryGunExecutionResult(
                equipmentInstanceId,
                kind,
                status,
                string.Empty,
                null,
                schedulerStatus,
                replay,
                0,
                0,
                0,
                pendingDeliveryCount,
                new InventoryGunEffectBatch[0],
                scheduling);
        }

        internal static InventoryGunExecutionResult Delivery(
            EquipmentInstanceId equipmentInstanceId,
            IList<InventoryGunEffectBatch> delivered,
            int acceptedCount,
            int alreadyAcceptedCount,
            int pendingDeliveryCount)
        {
            if (delivered == null || delivered.Count < 1)
            {
                throw new ArgumentException(
                    "At least one delivered batch is required.",
                    nameof(delivered));
            }

            int totalEffects = 0;
            EquipmentInstanceId commonEquipmentInstanceId =
                delivered[0].Identity.EquipmentInstanceId;
            for (int index = 0; index < delivered.Count; index++)
            {
                totalEffects = checked(totalEffects + delivered[index].EffectCount);
                if (commonEquipmentInstanceId != null
                    && !commonEquipmentInstanceId.Equals(
                        delivered[index].Identity.EquipmentInstanceId))
                {
                    commonEquipmentInstanceId = null;
                }
            }
            if (equipmentInstanceId != null
                && commonEquipmentInstanceId != null
                && !equipmentInstanceId.Equals(commonEquipmentInstanceId))
            {
                throw new ArgumentException(
                    "The delivery summary equipment identity does not match its batches.",
                    nameof(equipmentInstanceId));
            }

            long lastShotSequence = delivered[delivered.Count - 1]
                .Identity.ShotSequence;
            bool replayOnly = acceptedCount == 0 && alreadyAcceptedCount > 0;
            GunExecutionResult execution = replayOnly
                ? GunExecutionResult.Replay(totalEffects, lastShotSequence)
                : GunExecutionResult.Accept(totalEffects, lastShotSequence);
            return new InventoryGunExecutionResult(
                commonEquipmentInstanceId,
                replayOnly
                    ? InventoryGunExecutionOutcomeKind.ReplayedEmissionDelivery
                    : InventoryGunExecutionOutcomeKind.AcceptedEmissionDelivery,
                execution.Status,
                execution.RejectionCode,
                execution,
                null,
                replayOnly,
                0,
                acceptedCount,
                alreadyAcceptedCount,
                pendingDeliveryCount,
                delivered);
        }

        internal static InventoryGunExecutionResult NoDue(int pendingDeliveryCount)
        {
            return new InventoryGunExecutionResult(
                null,
                InventoryGunExecutionOutcomeKind.NoDueDelivery,
                GunExecutionStatus.Accepted,
                string.Empty,
                null,
                null,
                false,
                0,
                0,
                0,
                pendingDeliveryCount,
                new InventoryGunEffectBatch[0]);
        }

        internal static InventoryGunExecutionResult Reject(
            EquipmentInstanceId equipmentInstanceId,
            GunExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection,
            bool retryableDeliveryFailure,
            int pendingDeliveryCount,
            IEnumerable<InventoryGunEffectBatch> delivered,
            int acceptedDeliveryCount,
            int alreadyAcceptedDeliveryCount,
            GunFiringScheduleStatus? schedulerStatus = null)
        {
            IList<InventoryGunEffectBatch> safeDelivered = delivered == null
                ? (IList<InventoryGunEffectBatch>)new InventoryGunEffectBatch[0]
                : new List<InventoryGunEffectBatch>(delivered);
            long lastShotSequence = safeDelivered.Count == 0
                ? 0L
                : safeDelivered[safeDelivered.Count - 1].Identity.ShotSequence;
            GunExecutionResult execution = GunExecutionResult.Reject(
                status,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "gun-live-integration-rejected"
                    : rejectionCode,
                lastShotSequence);
            return new InventoryGunExecutionResult(
                equipmentInstanceId,
                retryableDeliveryFailure
                    ? InventoryGunExecutionOutcomeKind.RetryableDeliveryFailure
                    : schedulerRejection
                        ? InventoryGunExecutionOutcomeKind.SchedulerRejected
                        : InventoryGunExecutionOutcomeKind.IntegrationRejected,
                execution.Status,
                execution.RejectionCode,
                execution,
                schedulerStatus,
                false,
                0,
                acceptedDeliveryCount,
                alreadyAcceptedDeliveryCount,
                pendingDeliveryCount,
                safeDelivered);
        }

        private bool HasMountOutcome(InventoryGunExecutionOutcomeKind kind)
        {
            for (int index = 0; index < mountOutcomes.Count; index++)
            {
                if (mountOutcomes[index].OutcomeKind == kind)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasSchedulingOutcome(InventoryGunExecutionOutcomeKind kind)
        {
            for (int index = 0; index < schedulingOutcomes.Count; index++)
            {
                if (schedulingOutcomes[index].OutcomeKind == kind)
                {
                    return true;
                }
            }
            return false;
        }

        private static EquipmentInstanceId FindCommonEquipmentInstanceId(
            IList<InventoryGunMountExecutionOutcome> outcomes,
            IList<InventoryGunEffectBatch> delivered)
        {
            EquipmentInstanceId common = null;
            bool found = false;
            bool conflict = false;
            for (int index = 0; index < outcomes.Count; index++)
            {
                IncludeCommon(outcomes[index].EquipmentInstanceId,
                    ref common, ref found, ref conflict);
            }
            for (int index = 0; index < delivered.Count; index++)
            {
                IncludeCommon(delivered[index].Identity.EquipmentInstanceId,
                    ref common, ref found, ref conflict);
            }
            return conflict ? null : common;
        }

        private static EquipmentInstanceId FindCommonEquipmentInstanceId(
            IList<InventoryGunSchedulingOutcome> outcomes,
            IList<InventoryGunEffectBatch> delivered)
        {
            EquipmentInstanceId common = null;
            bool found = false;
            bool conflict = false;
            for (int index = 0; index < outcomes.Count; index++)
            {
                IncludeCommon(outcomes[index].EquipmentInstanceId,
                    ref common, ref found, ref conflict);
            }
            for (int index = 0; index < delivered.Count; index++)
            {
                IncludeCommon(delivered[index].Identity.EquipmentInstanceId,
                    ref common, ref found, ref conflict);
            }
            return conflict ? null : common;
        }

        private static void IncludeCommon(
            EquipmentInstanceId candidate,
            ref EquipmentInstanceId common,
            ref bool found,
            ref bool conflict)
        {
            if (candidate == null)
            {
                return;
            }
            if (!found)
            {
                common = candidate;
                found = true;
            }
            else if (!common.Equals(candidate))
            {
                conflict = true;
            }
        }
    }
}
