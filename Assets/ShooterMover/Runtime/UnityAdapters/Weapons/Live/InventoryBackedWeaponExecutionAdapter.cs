using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public sealed class InventoryWeaponPendingDeliveryAttempt
    {
        private InventoryWeaponPendingDeliveryAttempt(
            bool succeeded,
            WeaponEffectBatchSinkStatus? sinkStatus,
            string rejectionCode)
        {
            Succeeded = succeeded;
            SinkStatus = sinkStatus;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }
        public WeaponEffectBatchSinkStatus? SinkStatus { get; }
        public string RejectionCode { get; }
        public bool WasAlreadyAccepted
        {
            get { return SinkStatus == WeaponEffectBatchSinkStatus.AlreadyAccepted; }
        }

        internal static InventoryWeaponPendingDeliveryAttempt Accept(
            WeaponEffectBatchSinkStatus status)
        {
            if (status != WeaponEffectBatchSinkStatus.Accepted
                && status != WeaponEffectBatchSinkStatus.AlreadyAccepted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            return new InventoryWeaponPendingDeliveryAttempt(true, status, string.Empty);
        }

        internal static InventoryWeaponPendingDeliveryAttempt Retry(string code)
        {
            return new InventoryWeaponPendingDeliveryAttempt(
                false,
                null,
                string.IsNullOrWhiteSpace(code)
                    ? "weapon-live-retryable-delivery-failure"
                    : code);
        }
    }

    /// <summary>
    /// Resolves one exact inventory equipment instance into EffectiveWeapon, schedules only through
    /// WeaponFiringScheduler, validates and adapts accepted emissions, and admits their immutable
    /// projections into caller-owned pending-delivery state. It never submits a future emission.
    /// </summary>
    public sealed class InventoryBackedWeaponExecutionAdapter :
        IEquippedWeaponInstanceResolver
    {
        private readonly IPlayerEquipmentInstanceLookup equipmentLookup;
        private readonly IWeaponActorOwnershipResolver ownershipResolver;
        private readonly InventoryWeaponEffectiveResolver effectiveResolver;
        private readonly WeaponFiringScheduler scheduler;
        private readonly AcceptedEmissionRuntimeAdapter emissionAdapter;
        private readonly IInventoryWeaponEffectBatchSink effectSink;

        public InventoryBackedWeaponExecutionAdapter(
            IPlayerEquipmentInstanceLookup lookup,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IWeaponActorOwnershipResolver ownership,
            IInventoryWeaponEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IWeaponBlueprintMappingPolicyResolver mappingPolicyResolver,
            IWeaponAugmentModifierSetResolver augmentModifierResolver,
            WeaponBehaviorRegistry behaviorRegistry = null)
        {
            if (simulationTicksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond));
            }

            equipmentLookup = lookup
                ?? throw new ArgumentNullException(nameof(lookup));
            ownershipResolver = ownership
                ?? throw new ArgumentNullException(nameof(ownership));
            effectSink = downstreamEffectSink
                ?? throw new ArgumentNullException(nameof(downstreamEffectSink));
            effectiveResolver = new InventoryWeaponEffectiveResolver(
                equipmentCatalog
                    ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                weaponCatalog
                    ?? throw new ArgumentNullException(nameof(weaponCatalog)),
                mappingPolicyResolver
                    ?? throw new ArgumentNullException(
                        nameof(mappingPolicyResolver)),
                augmentModifierResolver
                    ?? throw new ArgumentNullException(
                        nameof(augmentModifierResolver)));
            scheduler = new WeaponFiringScheduler(
                new WeaponFiringClock(simulationTicksPerSecond));
            emissionAdapter = new AcceptedEmissionRuntimeAdapter(
                behaviorRegistry ?? WeaponBehaviorRegistry.CreateWithBuiltIns());
        }

        /// <summary>
        /// Source-compatibility constructor for retained tooling and tests. It no longer creates or
        /// invokes a behavior selector. With no explicit mapping policies it fails closed before fire.
        /// Production composition must call the mapping-policy constructor above.
        /// </summary>
        public InventoryBackedWeaponExecutionAdapter(
            IPlayerEquipmentInstanceLookup lookup,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IWeaponActorOwnershipResolver ownership,
            IInventoryWeaponEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IWeaponBehaviorSelector behaviorSelector = null,
            WeaponBehaviorRegistry behaviorRegistry = null)
            : this(
                lookup,
                equipmentCatalog,
                weaponCatalog,
                ownership,
                downstreamEffectSink,
                simulationTicksPerSecond,
                new WeaponBlueprintMappingPolicyRegistry(
                    new WeaponCatalogBlueprintMappingIntent[0]),
                new UnaugmentedWeaponModifierSetResolver(),
                behaviorRegistry)
        {
            // The legacy selector argument is intentionally ignored. Behavior selection is derived
            // only from the resolved modular/effective weapon structure.
        }

        public InventoryBackedWeaponExecutionAdapter(
            IPlayerHoldingsAuthorityV1 holdings,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IWeaponActorOwnershipResolver ownership,
            IInventoryWeaponEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IWeaponBlueprintMappingPolicyResolver mappingPolicyResolver,
            IWeaponAugmentModifierSetResolver augmentModifierResolver,
            WeaponBehaviorRegistry behaviorRegistry = null)
            : this(
                new PlayerHoldingsEquipmentInstanceLookup(
                    holdings ?? throw new ArgumentNullException(nameof(holdings))),
                equipmentCatalog,
                weaponCatalog,
                ownership,
                downstreamEffectSink,
                simulationTicksPerSecond,
                mappingPolicyResolver,
                augmentModifierResolver,
                behaviorRegistry)
        {
        }

        public InventoryBackedWeaponExecutionAdapter(
            IPlayerHoldingsAuthorityV1 holdings,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IWeaponActorOwnershipResolver ownership,
            IInventoryWeaponEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IWeaponBehaviorSelector behaviorSelector = null,
            WeaponBehaviorRegistry behaviorRegistry = null)
            : this(
                new PlayerHoldingsEquipmentInstanceLookup(
                    holdings ?? throw new ArgumentNullException(nameof(holdings))),
                equipmentCatalog,
                weaponCatalog,
                ownership,
                downstreamEffectSink,
                simulationTicksPerSecond,
                behaviorSelector,
                behaviorRegistry)
        {
        }

        [Obsolete(
            "Live firing requires caller-owned scheduler and pending-delivery state.",
            false)]
        public InventoryWeaponExecutionResult TryExecute(
            InventoryWeaponFireRequest request)
        {
            return InventoryWeaponExecutionResult.Reject(
                request == null ? null : request.EquipmentInstanceId,
                WeaponExecutionStatus.InvalidCommand,
                "weapon-live-firing-state-required",
                false,
                false,
                0,
                null,
                0,
                0);
        }

        [Obsolete(
            "Live firing requires pending-delivery state as well as scheduler state.",
            false)]
        public InventoryWeaponExecutionTransition TryExecute(
            InventoryWeaponFireRequest request,
            WeaponFiringSessionState previousState)
        {
            return RejectTransition(
                request == null ? null : request.EquipmentInstanceId,
                previousState ?? WeaponFiringSessionState.Empty,
                InventoryWeaponPendingDeliveryState.Empty,
                WeaponExecutionStatus.InvalidCommand,
                "weapon-live-pending-delivery-state-required",
                false);
        }

        public InventoryWeaponExecutionTransition TryExecute(
            InventoryWeaponFireRequest request,
            WeaponFiringSessionState previousState,
            InventoryWeaponPendingDeliveryState previousPendingState)
        {
            if (!IsValidRequest(request)
                || previousState == null
                || !previousState.HasValidFingerprint()
                || previousPendingState == null)
            {
                return RejectTransition(
                    request == null ? null : request.EquipmentInstanceId,
                    previousState ?? WeaponFiringSessionState.Empty,
                    previousPendingState ?? InventoryWeaponPendingDeliveryState.Empty,
                    WeaponExecutionStatus.InvalidCommand,
                    "weapon-live-request-or-state-invalid",
                    false);
            }

            EquipmentInstance equipmentInstance;
            if (!TryResolveEquippedWeapon(
                    request.ActorId,
                    request.EquipmentInstanceId,
                    out equipmentInstance))
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.MissingEquippedEquipment,
                    "weapon-live-equipment-unresolved",
                    false);
            }

            RunParticipantId participantId;
            if (!ownershipResolver.TryResolveParticipant(
                    request.ActorId,
                    request.LifecycleGeneration,
                    out participantId)
                || participantId == null)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.UnknownActorOwnership,
                    "weapon-live-actor-ownership-unresolved",
                    false);
            }

            EffectiveWeapon effectiveWeapon;
            string effectiveRejection;
            if (!effectiveResolver.TryResolve(
                    equipmentInstance,
                    out effectiveWeapon,
                    out effectiveRejection)
                || effectiveWeapon == null)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    MapEffectiveResolutionFailure(effectiveRejection),
                    effectiveRejection,
                    false);
            }

            var command = new WeaponFireCommand(
                request.ActorId,
                request.EquipmentInstanceId,
                request.FireOperationId,
                request.LifecycleGeneration,
                request.SimulationTick,
                request.DeterministicSeed,
                request.Origin,
                request.AimDirection);
            var firingRequest = new WeaponFiringRequest(
                effectiveWeapon,
                command,
                participantId,
                request.TriggerSignal);

            WeaponFiringDecision decision;
            try
            {
                decision = scheduler.Schedule(firingRequest, previousState);
            }
            catch
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.InvalidTuning,
                    "weapon-live-scheduler-exception",
                    true);
            }

            if (decision == null || decision.NextState == null)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.InvalidTuning,
                    "weapon-live-scheduler-result-invalid",
                    true);
            }
            if (decision.Kind == WeaponFiringDecisionKind.Rejection)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    MapScheduleFailure(decision.Status),
                    string.IsNullOrWhiteSpace(decision.RejectionCode)
                        ? "weapon-live-scheduler-rejected"
                        : decision.RejectionCode,
                    true);
            }

            if (decision.AcceptedSchedule == null)
            {
                bool publish = decision.Kind
                    == WeaponFiringDecisionKind.SuccessfulTransition;
                return new InventoryWeaponExecutionTransition(
                    InventoryWeaponExecutionResult.Transition(
                        request.EquipmentInstanceId,
                        decision.IsReplay,
                        decision.Status,
                        previousPendingState.PendingCount),
                    decision.NextState,
                    previousPendingState,
                    publish);
            }

            if (!HasExpectedSchedule(
                    request,
                    participantId,
                    effectiveWeapon,
                    decision.AcceptedSchedule))
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.InvalidEffectBatch,
                    "weapon-live-accepted-schedule-invalid",
                    false);
            }

            var pendingEntries = new List<InventoryWeaponPendingDeliveryEntry>(
                decision.AcceptedSchedule.EmissionCount);
            for (int index = 0;
                index < decision.AcceptedSchedule.Emissions.Count;
                index++)
            {
                WeaponFiringScheduler.AcceptedEmission emission =
                    decision.AcceptedSchedule.Emissions[index];
                AcceptedEmissionRuntimeAdapterResult adapted =
                    emissionAdapter.Adapt(effectiveWeapon, emission);
                if (adapted == null || !adapted.Succeeded)
                {
                    return RejectTransition(
                        request.EquipmentInstanceId,
                        previousState,
                        previousPendingState,
                        MapAdapterFailure(
                            adapted == null
                                ? AcceptedEmissionRuntimeAdapterStatus.InvalidInput
                                : adapted.Status),
                        adapted == null
                            ? "weapon-live-emission-adapter-null-result"
                            : adapted.RejectionCode,
                        false);
                }

                try
                {
                    var projected = new InventoryWeaponEffectBatch(
                        adapted.Batch,
                        InventoryWeaponEffectProfile.From(
                            effectiveWeapon,
                            adapted.Profile));
                    pendingEntries.Add(
                        InventoryWeaponPendingDeliveryEntry.From(
                            emission,
                            projected));
                }
                catch (OverflowException)
                {
                    return RejectTransition(
                        request.EquipmentInstanceId,
                        previousState,
                        previousPendingState,
                        WeaponExecutionStatus.InvalidEffectBatch,
                        "weapon-live-effect-count-overflow",
                        false);
                }
                catch (ArgumentException)
                {
                    return RejectTransition(
                        request.EquipmentInstanceId,
                        previousState,
                        previousPendingState,
                        WeaponExecutionStatus.InvalidEffectBatch,
                        "weapon-live-effect-projection-invalid",
                        false);
                }
            }

            if (pendingEntries.Count == 0)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.InvalidEffectBatch,
                    "weapon-live-empty-accepted-schedule",
                    false);
            }

            InventoryWeaponPendingAdmissionResult admission =
                previousPendingState.Admit(pendingEntries);
            if (admission == null || !admission.Succeeded)
            {
                InventoryWeaponPendingAdmissionStatus status = admission == null
                    ? InventoryWeaponPendingAdmissionStatus.InvalidEntry
                    : admission.Status;
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    MapPendingFailure(status),
                    admission == null
                        ? "weapon-live-pending-admission-null-result"
                        : admission.RejectionCode,
                    false);
            }

            if (decision.IsReplay && admission.AddedCount != 0)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    WeaponExecutionStatus.ConflictingDuplicate,
                    "weapon-live-replay-pending-entry-missing",
                    false);
            }

            bool publishStatePair = !decision.IsReplay;
            InventoryWeaponPendingDeliveryState nextPendingState = decision.IsReplay
                ? previousPendingState
                : admission.NextState;
            return new InventoryWeaponExecutionTransition(
                InventoryWeaponExecutionResult.Schedule(
                    request.EquipmentInstanceId,
                    decision.IsReplay,
                    decision.AcceptedSchedule.EmissionCount,
                    nextPendingState.PendingCount),
                decision.NextState,
                nextPendingState,
                publishStatePair);
        }

        public InventoryWeaponPendingDeliveryAttempt TryDeliverPending(
            InventoryWeaponPendingDeliveryEntry entry)
        {
            if (entry == null || !entry.HasValidFingerprint())
            {
                return InventoryWeaponPendingDeliveryAttempt.Retry(
                    "weapon-live-pending-delivery-invalid");
            }

            WeaponEffectBatchSinkResult sinkResult;
            try
            {
                sinkResult = effectSink.TryAccept(entry.ProjectedBatch);
            }
            catch
            {
                return InventoryWeaponPendingDeliveryAttempt.Retry(
                    "weapon-live-retryable-sink-exception");
            }

            if (sinkResult == null || !sinkResult.IsAcceptance)
            {
                string sinkCode = sinkResult == null
                    || string.IsNullOrWhiteSpace(sinkResult.RejectionCode)
                    ? "unknown"
                    : sinkResult.RejectionCode;
                return InventoryWeaponPendingDeliveryAttempt.Retry(
                    "weapon-live-retryable-sink-rejected:" + sinkCode);
            }

            return InventoryWeaponPendingDeliveryAttempt.Accept(sinkResult.Status);
        }

        public bool TryResolveEquippedWeapon(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId requestedEquipmentInstanceId,
            out EquipmentInstance equipmentInstance)
        {
            equipmentInstance = null;
            return actorId != null
                && requestedEquipmentInstanceId != null
                && equipmentLookup.TryResolve(
                    requestedEquipmentInstanceId,
                    out equipmentInstance)
                && equipmentInstance != null
                && equipmentInstance.InstanceId.Equals(
                    requestedEquipmentInstanceId.Value);
        }

        private static bool HasExpectedSchedule(
            InventoryWeaponFireRequest request,
            RunParticipantId participantId,
            EffectiveWeapon effectiveWeapon,
            WeaponFiringScheduler.AcceptedSchedule schedule)
        {
            if (schedule == null
                || !schedule.HasValidFingerprint(effectiveWeapon)
                || !schedule.ActorId.Equals(request.ActorId)
                || !schedule.ParticipantId.Equals(participantId)
                || !schedule.EquipmentInstanceId.Equals(
                    request.EquipmentInstanceId)
                || !schedule.WeaponDefinitionId.Equals(
                    effectiveWeapon.DefinitionId)
                || !schedule.LifecycleGeneration.Equals(
                    request.LifecycleGeneration)
                || !schedule.SourceFireOperationId.Equals(
                    request.FireOperationId)
                || schedule.TriggerSignal != request.TriggerSignal
                || schedule.SourceCommand.SimulationTick
                    != request.SimulationTick
                || schedule.SourceCommand.DeterministicSeed
                    != request.DeterministicSeed
                || !schedule.SourceCommand.Origin.Equals(request.Origin)
                || !schedule.SourceCommand.AimDirection.Equals(
                    request.AimDirection))
            {
                return false;
            }

            for (int index = 0; index < schedule.Emissions.Count; index++)
            {
                WeaponFiringScheduler.AcceptedEmission emission =
                    schedule.Emissions[index];
                if (emission == null
                    || !emission.HasValidFingerprint(effectiveWeapon)
                    || emission.EmissionOrdinal != index
                    || !emission.Command.ActorId.Equals(request.ActorId)
                    || !emission.ParticipantId.Equals(participantId)
                    || !emission.EquipmentInstanceId.Equals(
                        request.EquipmentInstanceId)
                    || !emission.WeaponDefinitionId.Equals(
                        effectiveWeapon.DefinitionId)
                    || !emission.Command.LifecycleGeneration.Equals(
                        request.LifecycleGeneration)
                    || !emission.SourceFireOperationId.Equals(
                        request.FireOperationId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidRequest(
            InventoryWeaponFireRequest request)
        {
            return request != null
                && request.ActorId != null
                && request.EquipmentInstanceId != null
                && request.FireOperationId != null
                && request.LifecycleGeneration != null
                && request.SimulationTick >= 0L
                && request.Origin != null
                && request.Origin.IsFinite
                && request.AimDirection != null
                && request.AimDirection.IsFinite
                && request.AimDirection.LengthSquared > 0.000000000001d
                && Enum.IsDefined(
                    typeof(WeaponTriggerSignal),
                    request.TriggerSignal);
        }

        private static WeaponExecutionStatus MapEffectiveResolutionFailure(
            string rejectionCode)
        {
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                return WeaponExecutionStatus.InvalidEquipment;
            }
            if (rejectionCode.IndexOf(
                    "definition-unresolved",
                    StringComparison.Ordinal) >= 0)
            {
                return WeaponExecutionStatus.UnknownWeaponDefinition;
            }
            if (rejectionCode.IndexOf(
                    "blueprint",
                    StringComparison.Ordinal) >= 0
                || rejectionCode.IndexOf(
                    "augment",
                    StringComparison.Ordinal) >= 0)
            {
                return WeaponExecutionStatus.InvalidTuning;
            }
            return WeaponExecutionStatus.InvalidEquipment;
        }

        private static WeaponExecutionStatus MapScheduleFailure(
            WeaponFiringScheduleStatus status)
        {
            switch (status)
            {
                case WeaponFiringScheduleStatus.ConflictingDuplicate:
                    return WeaponExecutionStatus.ConflictingDuplicate;
                case WeaponFiringScheduleStatus.IdentityMismatch:
                    return WeaponExecutionStatus.InvalidEquipment;
                case WeaponFiringScheduleStatus.UnsupportedConfiguration:
                    return WeaponExecutionStatus.UnsupportedEffects;
                case WeaponFiringScheduleStatus.CooldownActive:
                    return WeaponExecutionStatus.CooldownActive;
                case WeaponFiringScheduleStatus.ScheduleCapacityExceeded:
                case WeaponFiringScheduleStatus.NumericalFailure:
                    return WeaponExecutionStatus.InvalidTuning;
                default:
                    return WeaponExecutionStatus.InvalidCommand;
            }
        }

        private static WeaponExecutionStatus MapAdapterFailure(
            AcceptedEmissionRuntimeAdapterStatus status)
        {
            switch (status)
            {
                case AcceptedEmissionRuntimeAdapterStatus.IdentityMismatch:
                    return WeaponExecutionStatus.InvalidEquipment;
                case AcceptedEmissionRuntimeAdapterStatus.UnknownBehavior:
                    return WeaponExecutionStatus.UnknownBehavior;
                case AcceptedEmissionRuntimeAdapterStatus.BehaviorRejected:
                    return WeaponExecutionStatus.BehaviorRejected;
                case AcceptedEmissionRuntimeAdapterStatus.InvalidEffectBatch:
                    return WeaponExecutionStatus.InvalidEffectBatch;
                case AcceptedEmissionRuntimeAdapterStatus.NumericalFailure:
                    return WeaponExecutionStatus.InvalidTuning;
                case AcceptedEmissionRuntimeAdapterStatus.UnsupportedFireMode:
                case AcceptedEmissionRuntimeAdapterStatus.UnsupportedShotPattern:
                case AcceptedEmissionRuntimeAdapterStatus.UnsupportedProjectile:
                case AcceptedEmissionRuntimeAdapterStatus.UnsupportedGuidance:
                case AcceptedEmissionRuntimeAdapterStatus.UnsupportedImpact:
                case AcceptedEmissionRuntimeAdapterStatus.UnsupportedEffects:
                case AcceptedEmissionRuntimeAdapterStatus
                    .FractionalPierceUnsupported:
                    return WeaponExecutionStatus.UnsupportedEffects;
                default:
                    return WeaponExecutionStatus.InvalidCommand;
            }
        }

        private static WeaponExecutionStatus MapPendingFailure(
            InventoryWeaponPendingAdmissionStatus status)
        {
            switch (status)
            {
                case InventoryWeaponPendingAdmissionStatus.ConflictingDuplicate:
                    return WeaponExecutionStatus.ConflictingDuplicate;
                case InventoryWeaponPendingAdmissionStatus.CapacityExceeded:
                    return WeaponExecutionStatus.InvalidTuning;
                default:
                    return WeaponExecutionStatus.InvalidEffectBatch;
            }
        }

        private static InventoryWeaponExecutionTransition RejectTransition(
            EquipmentInstanceId equipmentInstanceId,
            WeaponFiringSessionState unchangedState,
            InventoryWeaponPendingDeliveryState unchangedPendingState,
            WeaponExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection)
        {
            return new InventoryWeaponExecutionTransition(
                InventoryWeaponExecutionResult.Reject(
                    equipmentInstanceId,
                    status,
                    rejectionCode,
                    schedulerRejection,
                    false,
                    unchangedPendingState == null
                        ? 0
                        : unchangedPendingState.PendingCount,
                    null,
                    0,
                    0),
                unchangedState ?? WeaponFiringSessionState.Empty,
                unchangedPendingState ?? InventoryWeaponPendingDeliveryState.Empty,
                false);
        }
    }
}
