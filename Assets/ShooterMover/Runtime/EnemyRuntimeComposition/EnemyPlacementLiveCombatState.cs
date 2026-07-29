using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyPlacementLiveInstance
    {
        public EnemyAttackExecutionResult TryExecuteAttack(
            EnemyPlacementDecision decision,
            StableId operationStableId,
            double occurredAtSeconds)
        {
            return TryExecuteAttackCore(
                decision,
                null,
                false,
                operationStableId,
                occurredAtSeconds);
        }

        // Compatibility overload. The supplied projection is validation-only; execution always rebuilds
        // the authoritative context from the issued decision and this runtime's difficulty context.
        public EnemyAttackExecutionResult TryExecuteAttack(
            EnemyPlacementDecision decision,
            EnemyTargetingAimContext context,
            StableId operationStableId,
            double occurredAtSeconds)
        {
            return TryExecuteAttackCore(
                decision,
                context,
                true,
                operationStableId,
                occurredAtSeconds);
        }

        private EnemyAttackExecutionResult TryExecuteAttackCore(
            EnemyPlacementDecision decision,
            EnemyTargetingAimContext suppliedContext,
            bool callerSuppliedContext,
            StableId operationStableId,
            double occurredAtSeconds)
        {
            if (operationStableId == null) throw new ArgumentNullException(nameof(operationStableId));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));

            IssuedDecisionRecord issued;
            EnemyLiveRejectionCode validation = ValidateDecisionCode(decision, out issued);
            EnemyAttackIntent requested = validation == EnemyLiveRejectionCode.None
                ? issued.Decision.Evaluation.Decision.RequestedAttack
                : decision == null ? null : decision.Evaluation.Decision.RequestedAttack;
            EnemyLiveAttackBinding binding = null;
            if (requested != null) attacksById.TryGetValue(requested.AttackId, out binding);

            EnemyTargetingAimContext authoritativeContext = decision == null
                ? null
                : new EnemyTargetingAimContext(
                    validation == EnemyLiveRejectionCode.None
                        ? issued.Decision.Perception
                        : decision.Perception,
                    Request.Difficulty.Scalar);
            EnemyTargetingAimContext signatureContext = callerSuppliedContext
                ? suppliedContext
                : authoritativeContext;
            string decisionFingerprint = issued == null
                ? EnemyLiveStateFingerprint.Decision(decision)
                : issued.Fingerprint;
            string signature = EnemyLiveStateFingerprint.AttackAttempt(
                decisionFingerprint,
                signatureContext,
                false,
                occurredAtSeconds,
                Request.Difficulty,
                DifficultyScaling,
                binding);

            AttackReplayRecord replay;
            if (attackReplay.TryGetValue(operationStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedAttack(EnemyLiveRejectionCode.ConflictingDuplicate);
                return new EnemyAttackExecutionResult(
                    EnemyLiveOperationStatus.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Request);
            }

            EnemyAttackExecutionResult result;
            bool recordReplay = true;
            if (validation != EnemyLiveRejectionCode.None)
            {
                result = RejectedAttack(validation);
            }
            else if (callerSuppliedContext
                && (suppliedContext == null
                    || !string.Equals(
                        EnemyLiveStateFingerprint.AimContext(suppliedContext),
                        EnemyLiveStateFingerprint.AimContext(authoritativeContext),
                        StringComparison.Ordinal)))
            {
                result = RejectedAttack(EnemyLiveRejectionCode.InvalidCommand);
            }
            else if (!actorState.IsActive)
            {
                result = RejectedAttack(EnemyLiveRejectionCode.ActorTerminal);
            }
            else if (requested == null)
            {
                result = new EnemyAttackExecutionResult(
                    EnemyLiveOperationStatus.NoEffect,
                    EnemyLiveRejectionCode.MissingAttackIntent,
                    null);
            }
            else if (binding == null)
            {
                result = RejectedAttack(EnemyLiveRejectionCode.UnknownAttack);
            }
            else
            {
                double readyAt;
                nextReadyAtByAttack.TryGetValue(requested.AttackId, out readyAt);
                if (occurredAtSeconds < readyAt)
                {
                    result = RejectedAttack(EnemyLiveRejectionCode.CooldownActive);
                }
                else
                {
                    EnemyAttackIntent committed = binding.TargetingAim.Policy.Commit(
                        requested,
                        authoritativeContext,
                        binding.TargetingAim.Configuration);
                    if (!CommittedIntentPreservesIssuedSelection(requested, committed))
                    {
                        result = RejectedAttack(EnemyLiveRejectionCode.InvalidCommand);
                    }
                    else
                    {
                        var executionContext = new EnemyAttackExecutionContext(
                            operationStableId,
                            Identity,
                            LifecycleGeneration,
                            occurredAtSeconds,
                            DifficultyScaling);
                        StableId itemInstance = ResolveAttackItemInstance(binding.Descriptor.AttackId);
                        EnemyAttackExecutionRequest execution =
                            binding.Capability.Adapter.BuildExecution(
                                binding.Descriptor,
                                committed,
                                itemInstance,
                                binding.Capability.Configuration,
                                executionContext);
                        if (!ExecutionMatchesAuthoritativeInputs(
                            execution,
                            operationStableId,
                            occurredAtSeconds,
                            binding,
                            committed,
                            itemInstance))
                        {
                            result = RejectedAttack(EnemyLiveRejectionCode.InvalidCommand);
                        }
                        else
                        {
                            EnemyAttackPatternDispatchResult dispatch;
                            if (EnemyAttackEffectEmissionDispatch
                                .IsLegacyCompatibilityExecution(execution))
                            {
                                // Schema-v1 production content intentionally retains the historical
                                // one-call effect boundary and does not enter pattern authority.
                                dispatch = EnemyAttackEffectEmissionDispatch.DispatchLegacy(
                                    downstream.AttackEffects,
                                    execution);
                            }
                            else
                            {
                                EnemyAttackPatternStartResult pattern =
                                    StartAttackPattern(execution);
                                dispatch = pattern.IsAccepted
                                    ? DispatchAttackPattern(execution, pattern)
                                    : null;
                            }

                            if (dispatch == null || !dispatch.IsAccepted)
                            {
                                // Only an explicit downstream failure is transient. Contract,
                                // capability, or fingerprint rejections are deterministic and may
                                // enter the outer replay ledger.
                                recordReplay = dispatch == null
                                    || dispatch.Rejection
                                        != EnemyAttackPatternDispatchRejectionCode
                                            .DownstreamFailure;
                                result = RejectedAttack(
                                    EnemyLiveRejectionCode.InvalidCommand);
                            }
                            else
                            {
                                string executionFingerprint =
                                    EnemyLiveStateFingerprint.Execution(
                                        execution,
                                        issued.Fingerprint);
                                AcceptedExecutionRecord existing;
                                if (acceptedExecutions.TryGetValue(
                                    operationStableId,
                                    out existing))
                                {
                                    if (!string.Equals(
                                        existing.Fingerprint,
                                        executionFingerprint,
                                        StringComparison.Ordinal))
                                    {
                                        result = RejectedAttack(
                                            EnemyLiveRejectionCode
                                                .ConflictingDuplicate);
                                    }
                                    else
                                    {
                                        nextReadyAtByAttack[requested.AttackId] =
                                            occurredAtSeconds
                                            + execution.ResolvedCooldownSeconds;
                                        result = new EnemyAttackExecutionResult(
                                            EnemyLiveOperationStatus.Applied,
                                            EnemyLiveRejectionCode.None,
                                            existing.Execution);
                                    }
                                }
                                else
                                {
                                    acceptedExecutions.Add(
                                        operationStableId,
                                        new AcceptedExecutionRecord(
                                            executionFingerprint,
                                            issued.Fingerprint,
                                            execution));
                                    nextReadyAtByAttack[requested.AttackId] =
                                        occurredAtSeconds
                                        + execution.ResolvedCooldownSeconds;
                                    result = new EnemyAttackExecutionResult(
                                        EnemyLiveOperationStatus.Applied,
                                        EnemyLiveRejectionCode.None,
                                        execution);
                                }
                            }
                        }
                    }
                }
            }

            if (recordReplay)
                attackReplay.Add(operationStableId, new AttackReplayRecord(signature, result));
            return result;
        }

        public EnemyPlayerDamagePortResult RoutePlayerImpact(
            EnemyAttackExecutionRequest execution,
            StableId hitEventStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration)
        {
            if (hitEventStableId == null) throw new ArgumentNullException(nameof(hitEventStableId));
            if (targetEntityStableId == null) throw new ArgumentNullException(nameof(targetEntityStableId));
            if (observedTargetLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(observedTargetLifecycleGeneration));
            if (execution == null
                || execution.Identity == null
                || execution.Identity.EntityInstanceId != Identity.EntityInstanceId)
            {
                return RejectedPlayerImpact(EnemyLiveRejectionCode.EntityMismatch);
            }
            if (execution.LifecycleGeneration != LifecycleGeneration)
                return RejectedPlayerImpact(EnemyLiveRejectionCode.StaleLifecycle);

            AcceptedExecutionRecord accepted;
            if (!acceptedExecutions.TryGetValue(execution.OperationStableId, out accepted))
                return RejectedPlayerImpact(EnemyLiveRejectionCode.ExecutionNotIssued);
            string suppliedFingerprint = EnemyLiveStateFingerprint.Execution(
                execution,
                accepted.DecisionFingerprint);
            if (!string.Equals(accepted.Fingerprint, suppliedFingerprint, StringComparison.Ordinal))
                return RejectedPlayerImpact(EnemyLiveRejectionCode.InvalidCommand);

            string signature = EnemyLiveStateFingerprint.Impact(
                accepted.Fingerprint,
                targetEntityStableId,
                observedTargetLifecycleGeneration);
            ImpactReplayRecord replay;
            if (impactReplay.TryGetValue(hitEventStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedPlayerImpact(EnemyLiveRejectionCode.ConflictingDuplicate);
                return new EnemyPlayerDamagePortResult(
                    EnemyLiveOperationStatus.ExactReplay,
                    replay.Result.Rejection);
            }

            EnemyAttackExecutionRequest canonical = accepted.Execution;
            var request = new EnemyPlayerDamageRequest(
                hitEventStableId,
                canonical.OperationStableId,
                Identity.EntityInstanceId,
                Identity.RunParticipantId,
                targetEntityStableId,
                observedTargetLifecycleGeneration,
                LifecycleGeneration,
                canonical.ResolvedDamage,
                canonical.Descriptor.DamageChannelId,
                canonical.CommittedIntent);
            EnemyPlayerDamagePortResult result = downstream.PlayerDamage.Route(request)
                ?? throw new InvalidOperationException("Player damage ports must return a result.");
            impactReplay.Add(hitEventStableId, new ImpactReplayRecord(signature, result));
            return result;
        }

        private static bool CommittedIntentPreservesIssuedSelection(
            EnemyAttackIntent requested,
            EnemyAttackIntent committed)
        {
            return requested != null
                && committed != null
                && committed.AttackerEntityId == requested.AttackerEntityId
                && committed.SourceRunParticipantId == requested.SourceRunParticipantId
                && committed.TargetEntityId == requested.TargetEntityId
                && committed.AttackId == requested.AttackId
                && committed.CommittedOrigin.Equals(requested.CommittedOrigin)
                && committed.DecisionId == requested.DecisionId
                && committed.BehaviorPhaseId == requested.BehaviorPhaseId
                && committed.ReasonCode == requested.ReasonCode;
        }

        private bool ExecutionMatchesAuthoritativeInputs(
            EnemyAttackExecutionRequest execution,
            StableId operationStableId,
            double occurredAtSeconds,
            EnemyLiveAttackBinding binding,
            EnemyAttackIntent committed,
            StableId itemInstance)
        {
            return execution != null
                && execution.OperationStableId == operationStableId
                && EnemyLiveStateFingerprint.IdentityEquals(execution.Identity, Identity)
                && execution.LifecycleGeneration == LifecycleGeneration
                && execution.OccurredAtSeconds == occurredAtSeconds
                && string.Equals(
                    EnemyLiveStateFingerprint.Descriptor(execution.Descriptor),
                    EnemyLiveStateFingerprint.Descriptor(binding.Descriptor),
                    StringComparison.Ordinal)
                && EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(
                    execution.Descriptor)
                    == EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(
                        binding.Descriptor)
                && string.Equals(
                    EnemyLiveStateFingerprint.AttackIntent(execution.CommittedIntent),
                    EnemyLiveStateFingerprint.AttackIntent(committed),
                    StringComparison.Ordinal)
                && execution.ItemInstanceStableId == itemInstance
                && execution.ExecutionKind == binding.Capability.Configuration.ExecutionKind;
        }
    }
}
