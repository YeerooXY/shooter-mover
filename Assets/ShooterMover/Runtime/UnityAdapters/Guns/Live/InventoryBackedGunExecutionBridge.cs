using System;
using System.Collections.Generic;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public sealed class InventoryGunPendingDeliveryAttempt
    {
        private InventoryGunPendingDeliveryAttempt(
            bool succeeded,
            GunEffectBatchSinkStatus? sinkStatus,
            string rejectionCode)
        {
            Succeeded = succeeded;
            SinkStatus = sinkStatus;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }
        public GunEffectBatchSinkStatus? SinkStatus { get; }
        public string RejectionCode { get; }
        public bool WasAlreadyAccepted
        {
            get { return SinkStatus == GunEffectBatchSinkStatus.AlreadyAccepted; }
        }

        internal static InventoryGunPendingDeliveryAttempt Accept(
            GunEffectBatchSinkStatus status)
        {
            if (status != GunEffectBatchSinkStatus.Accepted
                && status != GunEffectBatchSinkStatus.AlreadyAccepted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            return new InventoryGunPendingDeliveryAttempt(
                true,
                status,
                string.Empty);
        }

        internal static InventoryGunPendingDeliveryAttempt Retry(string code)
        {
            return new InventoryGunPendingDeliveryAttempt(
                false,
                null,
                string.IsNullOrWhiteSpace(code)
                    ? "gun-live-retryable-delivery-failure"
                    : code);
        }
    }

    /// <summary>
    /// Resolves one exact inventory equipment instance into EffectiveGun, schedules only through
    /// GunFiringScheduler, validates and adapts accepted emissions, and admits their immutable
    /// projections into caller-owned pending-delivery state. Sink delivery is assembly-internal and
    /// may only be invoked after the owning runtime selects its exact retained first due entry.
    /// </summary>
    public sealed class InventoryBackedGunExecutionBridge :
        IEquippedGunInstanceResolver
    {
        private readonly IPlayerEquipmentInstanceLookup equipmentLookup;
        private readonly IGunActorOwnershipResolver ownershipResolver;
        private readonly InventoryGunEffectiveResolver effectiveResolver;
        private readonly GunFiringScheduler scheduler;
        private readonly AcceptedEmissionLiveBridge emissionAdapter;
        private readonly IInventoryGunEffectBatchSink effectSink;

        public InventoryBackedGunExecutionBridge(
            IPlayerEquipmentInstanceLookup lookup,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IGunActorOwnershipResolver ownership,
            IInventoryGunEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IGunMappingPolicyResolver mappingPolicyResolver,
            IGunAugmentModifierSetResolver augmentModifierResolver,
            GunBehaviorRegistry behaviorRegistry = null)
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
            effectiveResolver = new InventoryGunEffectiveResolver(
                equipmentCatalog
                    ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                gunCatalog
                    ?? throw new ArgumentNullException(nameof(gunCatalog)),
                mappingPolicyResolver
                    ?? throw new ArgumentNullException(
                        nameof(mappingPolicyResolver)),
                augmentModifierResolver
                    ?? throw new ArgumentNullException(
                        nameof(augmentModifierResolver)));
            scheduler = new GunFiringScheduler(
                new GunFiringClock(simulationTicksPerSecond));
            emissionAdapter = new AcceptedEmissionLiveBridge(
                behaviorRegistry ?? GunBehaviorRegistry.CreateWithBuiltIns());
        }

        /// <summary>
        /// Source-compatibility constructor for retained tooling and tests. It no longer creates or
        /// invokes a behavior selector. With no explicit mapping policies it fails closed before fire.
        /// Production composition must call the mapping-policy constructor above.
        /// </summary>
        public InventoryBackedGunExecutionBridge(
            IPlayerEquipmentInstanceLookup lookup,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IGunActorOwnershipResolver ownership,
            IInventoryGunEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IGunBehaviorSelector behaviorSelector = null,
            GunBehaviorRegistry behaviorRegistry = null)
            : this(
                lookup,
                equipmentCatalog,
                gunCatalog,
                ownership,
                downstreamEffectSink,
                simulationTicksPerSecond,
                new GunMappingPolicyRegistry(
                    new GunCatalogBlueprintMappingIntent[0]),
                new GunAugmentResolver(),
                behaviorRegistry)
        {
            // The legacy selector argument is intentionally ignored. Behavior selection is derived
            // only from the resolved modular/effective gun structure.
        }

        public InventoryBackedGunExecutionBridge(
            IPlayerHoldingsState holdings,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IGunActorOwnershipResolver ownership,
            IInventoryGunEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IGunMappingPolicyResolver mappingPolicyResolver,
            IGunAugmentModifierSetResolver augmentModifierResolver,
            GunBehaviorRegistry behaviorRegistry = null)
            : this(
                new PlayerHoldingsEquipmentInstanceLookup(
                    holdings ?? throw new ArgumentNullException(nameof(holdings))),
                equipmentCatalog,
                gunCatalog,
                ownership,
                downstreamEffectSink,
                simulationTicksPerSecond,
                mappingPolicyResolver,
                augmentModifierResolver,
                behaviorRegistry)
        {
        }

        public InventoryBackedGunExecutionBridge(
            IPlayerHoldingsState holdings,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IGunActorOwnershipResolver ownership,
            IInventoryGunEffectBatchSink downstreamEffectSink,
            int simulationTicksPerSecond,
            IGunBehaviorSelector behaviorSelector = null,
            GunBehaviorRegistry behaviorRegistry = null)
            : this(
                new PlayerHoldingsEquipmentInstanceLookup(
                    holdings ?? throw new ArgumentNullException(nameof(holdings))),
                equipmentCatalog,
                gunCatalog,
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
        public InventoryGunExecutionResult TryExecute(
            InventoryGunFireRequest request)
        {
            return BuildRejectResult(
                request == null ? null : request.EquipmentInstanceId,
                GunExecutionStatus.InvalidCommand,
                "gun-live-firing-state-required",
                false,
                null,
                0);
        }

        [Obsolete(
            "Live firing requires pending-delivery state as well as scheduler state.",
            false)]
        public InventoryGunExecutionTransition TryExecute(
            InventoryGunFireRequest request,
            GunFiringSessionState previousState)
        {
            return RejectTransition(
                request == null ? null : request.EquipmentInstanceId,
                previousState ?? GunFiringSessionState.Empty,
                InventoryGunPendingDeliveryState.Empty,
                GunExecutionStatus.InvalidCommand,
                "gun-live-pending-delivery-state-required",
                false,
                null);
        }

        public InventoryGunExecutionTransition TryExecute(
            InventoryGunFireRequest request,
            GunFiringSessionState previousState,
            InventoryGunPendingDeliveryState previousPendingState)
        {
            if (!IsValidRequest(request)
                || previousState == null
                || !previousState.HasValidFingerprint()
                || previousPendingState == null)
            {
                return RejectTransition(
                    request == null ? null : request.EquipmentInstanceId,
                    previousState ?? GunFiringSessionState.Empty,
                    previousPendingState ?? InventoryGunPendingDeliveryState.Empty,
                    GunExecutionStatus.InvalidCommand,
                    "gun-live-request-or-state-invalid",
                    false,
                    null);
            }

            EquipmentInstance equipmentInstance;
            if (!TryResolveEquippedGun(
                    request.ActorId,
                    request.EquipmentInstanceId,
                    out equipmentInstance))
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    GunExecutionStatus.MissingEquippedEquipment,
                    "gun-live-equipment-unresolved",
                    false,
                    null);
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
                    GunExecutionStatus.UnknownActorOwnership,
                    "gun-live-actor-ownership-unresolved",
                    false,
                    null);
            }

            EffectiveGun effectiveGun = null;
            string effectiveRejection = string.Empty;
            bool effectiveResolved;
            try
            {
                effectiveResolved = effectiveResolver.TryResolve(
                    equipmentInstance,
                    out effectiveGun,
                    out effectiveRejection);
            }
            catch (OverflowException)
            {
                effectiveResolved = false;
                effectiveGun = null;
                effectiveRejection =
                    "gun-live-effective-resolution-numerical-exception";
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                effectiveResolved = false;
                effectiveGun = null;
                effectiveRejection = "gun-live-effective-resolution-exception";
            }

            if (!effectiveResolved || effectiveGun == null)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    MapEffectiveResolutionFailure(effectiveRejection),
                    effectiveRejection,
                    false,
                    null);
            }

            var command = new GunFireCommand(
                request.ActorId,
                request.EquipmentInstanceId,
                request.FireOperationId,
                request.LifecycleGeneration,
                request.SimulationTick,
                request.DeterministicSeed,
                request.Origin,
                request.AimDirection);
            var firingRequest = new GunFiringRequest(
                effectiveGun,
                command,
                participantId,
                request.TriggerSignal);

            GunFiringDecision decision;
            try
            {
                decision = scheduler.Schedule(firingRequest, previousState);
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    GunExecutionStatus.InvalidTuning,
                    "gun-live-scheduler-exception",
                    true,
                    null);
            }

            if (decision == null || decision.NextState == null)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    GunExecutionStatus.InvalidTuning,
                    "gun-live-scheduler-result-invalid",
                    true,
                    null);
            }
            if (decision.Kind == GunFiringDecisionKind.Rejection)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    MapScheduleFailure(decision.Status),
                    string.IsNullOrWhiteSpace(decision.RejectionCode)
                        ? "gun-live-scheduler-rejected"
                        : decision.RejectionCode,
                    true,
                    decision.Status);
            }

            if (decision.AcceptedSchedule == null)
            {
                bool publish = decision.Kind
                    == GunFiringDecisionKind.SuccessfulTransition;
                return new InventoryGunExecutionTransition(
                    InventoryGunExecutionResult.Transition(
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
                    effectiveGun,
                    decision.AcceptedSchedule))
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    GunExecutionStatus.InvalidEffectBatch,
                    "gun-live-accepted-schedule-invalid",
                    false,
                    decision.Status);
            }

            var pendingEntries = new List<InventoryGunPendingDeliveryEntry>(
                decision.AcceptedSchedule.EmissionCount);
            for (int index = 0;
                index < decision.AcceptedSchedule.Emissions.Count;
                index++)
            {
                GunFiringScheduler.AcceptedEmission emission =
                    decision.AcceptedSchedule.Emissions[index];
                AcceptedEmissionLiveBridgeResult adapted =
                    emissionAdapter.Adapt(effectiveGun, emission);
                if (adapted == null || !adapted.Succeeded)
                {
                    return RejectTransition(
                        request.EquipmentInstanceId,
                        previousState,
                        previousPendingState,
                        MapAdapterFailure(
                            adapted == null
                                ? AcceptedEmissionLiveBridgeStatus.InvalidInput
                                : adapted.Status),
                        adapted == null
                            ? "gun-live-emission-adapter-null-result"
                            : adapted.RejectionCode,
                        false,
                        decision.Status);
                }

                try
                {
                    var projected = new InventoryGunEffectBatch(
                        adapted.Batch,
                        InventoryGunEffectProfile.From(
                            effectiveGun,
                            adapted.Profile));
                    pendingEntries.Add(
                        InventoryGunPendingDeliveryEntry.From(
                            emission,
                            projected));
                }
                catch (OverflowException)
                {
                    return RejectTransition(
                        request.EquipmentInstanceId,
                        previousState,
                        previousPendingState,
                        GunExecutionStatus.InvalidEffectBatch,
                        "gun-live-effect-count-overflow",
                        false,
                        decision.Status);
                }
                catch (ArgumentException)
                {
                    return RejectTransition(
                        request.EquipmentInstanceId,
                        previousState,
                        previousPendingState,
                        GunExecutionStatus.InvalidEffectBatch,
                        "gun-live-effect-projection-invalid",
                        false,
                        decision.Status);
                }
            }

            if (pendingEntries.Count == 0)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    GunExecutionStatus.InvalidEffectBatch,
                    "gun-live-empty-accepted-schedule",
                    false,
                    decision.Status);
            }

            InventoryGunPendingAdmissionResult admission =
                previousPendingState.Admit(pendingEntries);
            if (admission == null || !admission.Succeeded)
            {
                InventoryGunPendingAdmissionStatus status = admission == null
                    ? InventoryGunPendingAdmissionStatus.InvalidEntry
                    : admission.Status;
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    MapPendingFailure(status),
                    admission == null
                        ? "gun-live-pending-admission-null-result"
                        : admission.RejectionCode,
                    false,
                    decision.Status);
            }

            if (decision.IsReplay && admission.AddedCount != 0)
            {
                return RejectTransition(
                    request.EquipmentInstanceId,
                    previousState,
                    previousPendingState,
                    GunExecutionStatus.ConflictingDuplicate,
                    "gun-live-replay-pending-entry-missing",
                    false,
                    decision.Status);
            }

            bool publishStatePair = !decision.IsReplay;
            InventoryGunPendingDeliveryState nextPendingState = decision.IsReplay
                ? previousPendingState
                : admission.NextState;
            return new InventoryGunExecutionTransition(
                InventoryGunExecutionResult.Schedule(
                    request.EquipmentInstanceId,
                    decision.IsReplay,
                    decision.AcceptedSchedule.EmissionCount,
                    nextPendingState.PendingCount),
                decision.NextState,
                nextPendingState,
                publishStatePair);
        }

        /// <summary>
        /// Assembly-internal sink bridge. The state-owning runtime must first select the exact
        /// retained first due entry under its lock and enforce active lifecycle and disposal state.
        /// </summary>
        internal InventoryGunPendingDeliveryAttempt TryDeliverPending(
            InventoryGunPendingDeliveryEntry entry)
        {
            if (entry == null || !entry.HasValidFingerprint())
            {
                return InventoryGunPendingDeliveryAttempt.Retry(
                    "gun-live-pending-delivery-invalid");
            }

            GunEffectBatchSinkResult sinkResult;
            try
            {
                sinkResult = effectSink.TryAccept(entry.ProjectedBatch);
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                return InventoryGunPendingDeliveryAttempt.Retry(
                    "gun-live-retryable-sink-exception");
            }

            if (sinkResult == null || !sinkResult.IsAcceptance)
            {
                string sinkCode = sinkResult == null
                    || string.IsNullOrWhiteSpace(sinkResult.RejectionCode)
                    ? "unknown"
                    : sinkResult.RejectionCode;
                return InventoryGunPendingDeliveryAttempt.Retry(
                    "gun-live-retryable-sink-rejected:" + sinkCode);
            }

            return InventoryGunPendingDeliveryAttempt.Accept(sinkResult.Status);
        }

        public bool TryResolveEquippedGun(
            GunActorInstanceId actorId,
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
            InventoryGunFireRequest request,
            RunParticipantId participantId,
            EffectiveGun effectiveGun,
            GunFiringScheduler.AcceptedSchedule schedule)
        {
            if (schedule == null
                || !schedule.HasValidFingerprint(effectiveGun)
                || !schedule.ActorId.Equals(request.ActorId)
                || !schedule.ParticipantId.Equals(participantId)
                || !schedule.EquipmentInstanceId.Equals(
                    request.EquipmentInstanceId)
                || !schedule.GunDefinitionId.Equals(
                    effectiveGun.DefinitionId)
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
                GunFiringScheduler.AcceptedEmission emission =
                    schedule.Emissions[index];
                if (emission == null
                    || !emission.HasValidFingerprint(effectiveGun)
                    || emission.EmissionOrdinal != index
                    || !emission.Command.ActorId.Equals(request.ActorId)
                    || !emission.ParticipantId.Equals(participantId)
                    || !emission.EquipmentInstanceId.Equals(
                        request.EquipmentInstanceId)
                    || !emission.GunDefinitionId.Equals(
                        effectiveGun.DefinitionId)
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
            InventoryGunFireRequest request)
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
                    typeof(GunTriggerSignal),
                    request.TriggerSignal);
        }

        private static GunExecutionStatus MapEffectiveResolutionFailure(
            string rejectionCode)
        {
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                return GunExecutionStatus.InvalidEquipment;
            }
            if (rejectionCode.IndexOf(
                    "definition-unresolved",
                    StringComparison.Ordinal) >= 0)
            {
                return GunExecutionStatus.UnknownGunDefinition;
            }
            if (rejectionCode.IndexOf(
                    "blueprint",
                    StringComparison.Ordinal) >= 0
                || rejectionCode.IndexOf(
                    "augment",
                    StringComparison.Ordinal) >= 0
                || rejectionCode.IndexOf(
                    "effective-resolution",
                    StringComparison.Ordinal) >= 0)
            {
                return GunExecutionStatus.InvalidTuning;
            }
            return GunExecutionStatus.InvalidEquipment;
        }

        private static GunExecutionStatus MapScheduleFailure(
            GunFiringScheduleStatus status)
        {
            switch (status)
            {
                case GunFiringScheduleStatus.ConflictingDuplicate:
                    return GunExecutionStatus.ConflictingDuplicate;
                case GunFiringScheduleStatus.IdentityMismatch:
                    return GunExecutionStatus.InvalidEquipment;
                case GunFiringScheduleStatus.UnsupportedConfiguration:
                    return GunExecutionStatus.UnsupportedEffects;
                case GunFiringScheduleStatus.CooldownActive:
                    return GunExecutionStatus.CooldownActive;
                case GunFiringScheduleStatus.ScheduleCapacityExceeded:
                case GunFiringScheduleStatus.NumericalFailure:
                    return GunExecutionStatus.InvalidTuning;
                default:
                    return GunExecutionStatus.InvalidCommand;
            }
        }

        private static GunExecutionStatus MapAdapterFailure(
            AcceptedEmissionLiveBridgeStatus status)
        {
            switch (status)
            {
                case AcceptedEmissionLiveBridgeStatus.IdentityMismatch:
                    return GunExecutionStatus.InvalidEquipment;
                case AcceptedEmissionLiveBridgeStatus.UnknownBehavior:
                    return GunExecutionStatus.UnknownBehavior;
                case AcceptedEmissionLiveBridgeStatus.BehaviorRejected:
                    return GunExecutionStatus.BehaviorRejected;
                case AcceptedEmissionLiveBridgeStatus.InvalidEffectBatch:
                    return GunExecutionStatus.InvalidEffectBatch;
                case AcceptedEmissionLiveBridgeStatus.NumericalFailure:
                    return GunExecutionStatus.InvalidTuning;
                case AcceptedEmissionLiveBridgeStatus.UnsupportedFireMode:
                case AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern:
                case AcceptedEmissionLiveBridgeStatus.UnsupportedProjectile:
                case AcceptedEmissionLiveBridgeStatus.UnsupportedGuidance:
                case AcceptedEmissionLiveBridgeStatus.UnsupportedImpact:
                case AcceptedEmissionLiveBridgeStatus.UnsupportedEffects:
                case AcceptedEmissionLiveBridgeStatus
                    .FractionalPierceUnsupported:
                    return GunExecutionStatus.UnsupportedEffects;
                default:
                    return GunExecutionStatus.InvalidCommand;
            }
        }

        private static GunExecutionStatus MapPendingFailure(
            InventoryGunPendingAdmissionStatus status)
        {
            switch (status)
            {
                case InventoryGunPendingAdmissionStatus.ConflictingDuplicate:
                    return GunExecutionStatus.ConflictingDuplicate;
                case InventoryGunPendingAdmissionStatus.CapacityExceeded:
                    return GunExecutionStatus.InvalidTuning;
                default:
                    return GunExecutionStatus.InvalidEffectBatch;
            }
        }

        private static InventoryGunExecutionResult BuildRejectResult(
            EquipmentInstanceId equipmentInstanceId,
            GunExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection,
            GunFiringScheduleStatus? schedulerStatus,
            int pendingCount)
        {
            string code = string.IsNullOrWhiteSpace(rejectionCode)
                ? "gun-live-integration-rejected"
                : rejectionCode;
            GunExecutionResult execution = GunExecutionResult.Reject(
                status,
                code,
                0L);
            return new InventoryGunExecutionResult(
                equipmentInstanceId,
                schedulerRejection
                    ? InventoryGunExecutionOutcomeKind.SchedulerRejected
                    : InventoryGunExecutionOutcomeKind.IntegrationRejected,
                status,
                code,
                execution,
                schedulerStatus,
                false,
                0,
                0,
                0,
                pendingCount,
                new InventoryGunEffectBatch[0]);
        }

        private static InventoryGunExecutionTransition RejectTransition(
            EquipmentInstanceId equipmentInstanceId,
            GunFiringSessionState unchangedState,
            InventoryGunPendingDeliveryState unchangedPendingState,
            GunExecutionStatus status,
            string rejectionCode,
            bool schedulerRejection,
            GunFiringScheduleStatus? schedulerStatus)
        {
            InventoryGunPendingDeliveryState safePending =
                unchangedPendingState ?? InventoryGunPendingDeliveryState.Empty;
            return new InventoryGunExecutionTransition(
                BuildRejectResult(
                    equipmentInstanceId,
                    status,
                    rejectionCode,
                    schedulerRejection,
                    schedulerStatus,
                    safePending.PendingCount),
                unchangedState ?? GunFiringSessionState.Empty,
                safePending,
                false);
        }
    }
}
