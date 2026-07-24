using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public sealed class InventoryWeaponEffectProfile
    {
        public InventoryWeaponEffectProfile(
            WeaponDefinitionId definitionId,
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
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponDefinitionId DefinitionId { get; }
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

        internal static InventoryWeaponEffectProfile From(
            EffectiveWeapon weapon,
            WeaponRuntimeFiringProfile runtimeProfile)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }
            if (runtimeProfile == null)
            {
                throw new ArgumentNullException(nameof(runtimeProfile));
            }
            if (!weapon.DefinitionId.Equals(runtimeProfile.DefinitionId))
            {
                throw new ArgumentException(
                    "The runtime profile must describe the supplied effective weapon.",
                    nameof(runtimeProfile));
            }

            return new InventoryWeaponEffectProfile(
                weapon.DefinitionId,
                weapon.FireSettings.ShotsPerSecond,
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

        internal static InventoryWeaponEffectProfile From(
            WeaponDefinitionData definition,
            int simulationTicksPerSecond)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int cooldownTicks = Math.Max(
                1,
                (int)Math.Ceiling(simulationTicksPerSecond / definition.FireRate));
            return new InventoryWeaponEffectProfile(
                new WeaponDefinitionId(definition.DefinitionId),
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

    public sealed class InventoryWeaponEffectBatch
    {
        public InventoryWeaponEffectBatch(
            WeaponEffectBatch coreBatch,
            InventoryWeaponEffectProfile profile)
        {
            CoreBatch = coreBatch ?? throw new ArgumentNullException(nameof(coreBatch));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (coreBatch.Identity == null
                || !profile.DefinitionId.Equals(coreBatch.Identity.WeaponDefinitionId))
            {
                throw new ArgumentException(
                    "The catalog profile must describe the weapon definition carried by the core batch.",
                    nameof(profile));
            }

            CanonicalText = "core_batch=" + coreBatch.Fingerprint + "\n"
                + "profile=" + profile.Fingerprint + "\n";
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponEffectBatch CoreBatch { get; }
        public InventoryWeaponEffectProfile Profile { get; }
        public WeaponEffectIdentity Identity { get { return CoreBatch.Identity; } }
        public int EffectCount { get { return CoreBatch.EffectCount; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }
    }

    public enum InventoryWeaponExecutionOutcomeKind
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
    /// Honest live-operation result. Scheduler outcomes and downstream deliveries are retained as
    /// separate immutable observations, so one call may report a release/wait transition and also
    /// report every due batch delivered during that same simulation tick.
    /// </summary>
    public sealed class InventoryWeaponExecutionResult
    {
        private readonly ReadOnlyCollection<InventoryWeaponEffectBatch> deliveredBatches;
        private readonly ReadOnlyCollection<InventoryWeaponSchedulingOutcome> schedulingOutcomes;

        internal InventoryWeaponExecutionResult(
            EquipmentInstanceId equipmentInstanceId,
            InventoryWeaponExecutionOutcomeKind outcomeKind,
            WeaponExecutionStatus status,
            string rejectionCode,
            WeaponExecutionResult execution,
            WeaponFiringScheduleStatus? schedulerStatus,
            bool isExactReplay,
            int scheduledEmissionCount,
            int acceptedDeliveryCount,
            int alreadyAcceptedDeliveryCount,
            int pendingDeliveryCount,
            IList<InventoryWeaponEffectBatch> delivered,
            IList<InventoryWeaponSchedulingOutcome> scheduling = null)
        {
            if (scheduledEmissionCount < 0
                || acceptedDeliveryCount < 0
                || alreadyAcceptedDeliveryCount < 0
                || pendingDeliveryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledEmissionCount));
            }
            if (!Enum.IsDefined(typeof(InventoryWeaponExecutionOutcomeKind), outcomeKind))
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
            deliveredBatches = new ReadOnlyCollection<InventoryWeaponEffectBatch>(
                new List<InventoryWeaponEffectBatch>(
                    delivered ?? new InventoryWeaponEffectBatch[0]));
            schedulingOutcomes = new ReadOnlyCollection<InventoryWeaponSchedulingOutcome>(
                new List<InventoryWeaponSchedulingOutcome>(
                    scheduling ?? new InventoryWeaponSchedulingOutcome[0]));
        }

        /// <summary>
        /// Retained source-compatible constructor for legacy callers that already own a concrete
        /// effect delivery result. It must not be used for no-emission scheduler transitions.
        /// </summary>
        public InventoryWeaponExecutionResult(
            EquipmentInstanceId equipmentInstanceId,
            WeaponExecutionResult execution,
            InventoryWeaponEffectBatch effectBatch)
            : this(
                equipmentInstanceId,
                execution != null
                    && execution.Status == WeaponExecutionStatus.ReplayAccepted
                        ? InventoryWeaponExecutionOutcomeKind.ReplayedEmissionDelivery
                        : execution != null
                            && execution.Status == WeaponExecutionStatus.Accepted
                                ? InventoryWeaponExecutionOutcomeKind.AcceptedEmissionDelivery
                                : InventoryWeaponExecutionOutcomeKind.IntegrationRejected,
                execution == null
                    ? WeaponExecutionStatus.InvalidCommand
                    : execution.Status,
                execution == null ? "weapon-live-execution-result-null" : execution.RejectionCode,
                execution,
                null,
                execution != null
                    && execution.Status == WeaponExecutionStatus.ReplayAccepted,
                0,
                execution != null
                    && execution.Status == WeaponExecutionStatus.Accepted
                    && effectBatch != null ? 1 : 0,
                execution != null
                    && execution.Status == WeaponExecutionStatus.ReplayAccepted
                    && effectBatch != null ? 1 : 0,
                0,
                effectBatch == null
                    ? new InventoryWeaponEffectBatch[0]
                    : new[] { effectBatch })
        {
        }

        public EquipmentInstanceId EquipmentInstanceId { get; }
        public InventoryWeaponExecutionOutcomeKind OutcomeKind { get; }
        public WeaponExecutionResult Execution { get; }
        public WeaponExecutionStatus Status { get; }
        public string RejectionCode { get; }
        public WeaponFiringScheduleStatus? SchedulerStatus { get; }
        public bool IsExactReplay { get; }
        public int ScheduledEmissionCount { get; }
        public int AcceptedDeliveryCount { get; }
        public int AlreadyAcceptedDeliveryCount { get; }
        public int DeliveredBatchCount { get { return deliveredBatches.Count; } }
        public int PendingDeliveryCount { get; }
        public IReadOnlyList<InventoryWeaponEffectBatch> DeliveredBatches
        {
            get { return deliveredBatches; }
        }
        public IReadOnlyList<InventoryWeaponSchedulingOutcome> SchedulingOutcomes
        {
            get { return schedulingOutcomes; }
        }
        public InventoryWeaponEffectBatch EffectBatch
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
                for (int index = 0; index < schedulingOutcomes.Count; index++)
                {
                    if (schedulingOutcomes[index].OutcomeKind
                        == InventoryWeaponExecutionOutcomeKind.AcceptedNoEmissionTransition)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool HasReplayedNoEmissionTransition
        {
            get
            {
                for (int index = 0; index < schedulingOutcomes.Count; index++)
                {
                    if (schedulingOutcomes[index].OutcomeKind
                        == InventoryWeaponExecutionOutcomeKind.ReplayedNoEmissionTransition)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool IsWaitingForCadence
        {
            get
            {
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
                return OutcomeKind != InventoryWeaponExecutionOutcomeKind.RetryableDeliveryFailure
                    && OutcomeKind != InventoryWeaponExecutionOutcomeKind.SchedulerRejected
                    && OutcomeKind != InventoryWeaponExecutionOutcomeKind.IntegrationRejected;
            }
        }

        public WeaponDefinitionId WeaponDefinitionId
        {
            get { return EffectBatch == null ? null : EffectBatch.Profile.DefinitionId; }
        }

        internal InventoryWeaponExecutionResult WithSchedulingOutcomes(
            IList<InventoryWeaponSchedulingOutcome> outcomes,
            int scheduledEmissionCount)
        {
            if (outcomes == null)
            {
                throw new ArgumentNullException(nameof(outcomes));
            }

            EquipmentInstanceId commonEquipmentInstanceId = FindCommonEquipmentInstanceId(
                outcomes,
                deliveredBatches);
            WeaponFiringScheduleStatus? singleStatus = outcomes.Count == 1
                ? (WeaponFiringScheduleStatus?)outcomes[0].SchedulerStatus
                : null;
            return new InventoryWeaponExecutionResult(
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
                outcomes);
        }

        internal static InventoryWeaponExecutionResult Schedule(
            EquipmentInstanceId equipmentInstanceId,
            bool replay,
            int scheduledEmissionCount,
            int pendingDeliveryCount)
        {
            InventoryWeaponExecutionOutcomeKind kind = replay
                ? InventoryWeaponExecutionOutcomeKind.ReplayedScheduleRetained
                : InventoryWeaponExecutionOutcomeKind.AcceptedScheduleQueued;
            WeaponExecutionStatus status = replay
                ? WeaponExecutionStatus.ReplayAccepted
                : WeaponExecutionStatus.Accepted;
            WeaponFiringScheduleStatus schedulerStatus = replay
                ? WeaponFiringScheduleStatus.Replayed
                : WeaponFiringScheduleStatus.Accepted;
            var scheduling = new[]
            {
                new InventoryWeaponSchedulingOutcome(
                    equipmentInstanceId,
                    kind,
                    status,
                    schedulerStatus,
                    replay,
                    scheduledEmissionCount),
            };
            return new InventoryWeaponExecutionResult(
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
                new InventoryWeaponEffectBatch[0],
                scheduling);
        }

        internal static InventoryWeaponExecutionResult Transition(
            EquipmentInstanceId equipmentInstanceId,
            bool replay,
            WeaponFiringScheduleStatus schedulerStatus,
            int pendingDeliveryCount)
        {
            InventoryWeaponExecutionOutcomeKind kind = replay
                ? InventoryWeaponExecutionOutcomeKind.ReplayedNoEmissionTransition
                : InventoryWeaponExecutionOutcomeKind.AcceptedNoEmissionTransition;
            WeaponExecutionStatus status = schedulerStatus
                    == WeaponFiringScheduleStatus.WaitingForCadence
                ? WeaponExecutionStatus.CooldownActive
                : replay
                    ? WeaponExecutionStatus.ReplayAccepted
                    : WeaponExecutionStatus.Accepted;
            var scheduling = new[]
            {
                new InventoryWeaponSchedulingOutcome(
                    equipmentInstanceId,
                    kind,
                    status,
                    schedulerStatus,
                    replay,
                    0),
            };
            return new InventoryWeaponExecutionResult(
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
                new InventoryWeaponEffectBatch[0],
                scheduling);
        }

        internal static InventoryWeaponExecutionResult Delivery(
            EquipmentInstanceId equipmentInstanceId,
            IList<InventoryWeaponEffectBatch> delivered,
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
            WeaponExecutionResult execution = replayOnly
                ? WeaponExecutionResult.Replay(totalEffects, lastShotSequence)
                : WeaponExecutionResult.Accept(totalEffects, lastShotSequence);
            return new InventoryWeaponExecutionResult(
                commonEquipmentInstanceId,
                replayOnly
                    ? InventoryWeaponExecutionOutcomeKind.ReplayedEmissionDelivery
                    : InventoryWeaponExecutionOutcomeKind.AcceptedEmissionDelivery,
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

        internal static InventoryWeaponExecutionResult NoDue(int pendingDeliveryCount)
        {
            return new InventoryWeaponExecutionResult(
                null,
                InventoryWeaponExecutionOutcomeKind.NoDueDelivery,
                WeaponExecutionStatus.Accepted,
                string.Empty,
                null,
                null,
                false,
                0,
                0,
                0,
                pendingDeliveryCount,
                new InventoryWeaponEffectBatch[0]);
        }

        internal static InventoryWeaponExecutionResult Reject(
            EquipmentInstanceId equipmentInstanceId,
            WeaponExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection,
            bool retryableDeliveryFailure,
            int pendingDeliveryCount,
            IList<InventoryWeaponEffectBatch> delivered,
            int acceptedDeliveryCount,
            int alreadyAcceptedDeliveryCount)
        {
            IList<InventoryWeaponEffectBatch> safeDelivered = delivered
                ?? new InventoryWeaponEffectBatch[0];
            long lastShotSequence = safeDelivered.Count == 0
                ? 0L
                : safeDelivered[safeDelivered.Count - 1].Identity.ShotSequence;
            WeaponExecutionResult execution = WeaponExecutionResult.Reject(
                status,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "weapon-live-integration-rejected"
                    : rejectionCode,
                lastShotSequence);
            return new InventoryWeaponExecutionResult(
                equipmentInstanceId,
                retryableDeliveryFailure
                    ? InventoryWeaponExecutionOutcomeKind.RetryableDeliveryFailure
                    : schedulerRejection
                        ? InventoryWeaponExecutionOutcomeKind.SchedulerRejected
                        : InventoryWeaponExecutionOutcomeKind.IntegrationRejected,
                execution.Status,
                execution.RejectionCode,
                execution,
                null,
                false,
                0,
                acceptedDeliveryCount,
                alreadyAcceptedDeliveryCount,
                pendingDeliveryCount,
                safeDelivered);
        }

        private static EquipmentInstanceId FindCommonEquipmentInstanceId(
            IList<InventoryWeaponSchedulingOutcome> outcomes,
            IList<InventoryWeaponEffectBatch> delivered)
        {
            EquipmentInstanceId common = null;
            bool found = false;
            bool conflict = false;

            for (int index = 0; index < outcomes.Count; index++)
            {
                EquipmentInstanceId candidate = outcomes[index].EquipmentInstanceId;
                if (candidate == null)
                {
                    continue;
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
            for (int index = 0; index < delivered.Count; index++)
            {
                EquipmentInstanceId candidate =
                    delivered[index].Identity.EquipmentInstanceId;
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

            return conflict ? null : common;
        }
    }
}
