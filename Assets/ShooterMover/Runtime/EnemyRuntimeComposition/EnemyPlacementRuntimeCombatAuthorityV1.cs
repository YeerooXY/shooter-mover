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
    public sealed partial class EnemyPlacementRuntimeInstanceV1
    {
        public EnemyAttackExecutionResultV1 TryExecuteAttack(
            EnemyPlacementDecisionV1 decision,
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
        public EnemyAttackExecutionResultV1 TryExecuteAttack(
            EnemyPlacementDecisionV1 decision,
            EnemyTargetingAimContextV1 context,
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

        private EnemyAttackExecutionResultV1 TryExecuteAttackCore(
            EnemyPlacementDecisionV1 decision,
            EnemyTargetingAimContextV1 suppliedContext,
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
            EnemyRuntimeRejectionCodeV1 validation = ValidateDecisionCode(decision, out issued);
            EnemyAttackIntent requested = validation == EnemyRuntimeRejectionCodeV1.None
                ? issued.Decision.Evaluation.Decision.RequestedAttack
                : decision == null ? null : decision.Evaluation.Decision.RequestedAttack;
            EnemyRuntimeAttackBindingV1 binding = null;
            if (requested != null) attacksById.TryGetValue(requested.AttackId, out binding);

            EnemyTargetingAimContextV1 authoritativeContext = decision == null
                ? null
                : new EnemyTargetingAimContextV1(
                    validation == EnemyRuntimeRejectionCodeV1.None
                        ? issued.Decision.Perception
                        : decision.Perception,
                    Request.Difficulty.Scalar);
            EnemyTargetingAimContextV1 signatureContext = callerSuppliedContext
                ? suppliedContext
                : authoritativeContext;
            string decisionFingerprint = issued == null
                ? EnemyRuntimeAuthorityFingerprintV1.Decision(decision)
                : issued.Fingerprint;
            string signature = EnemyRuntimeAuthorityFingerprintV1.AttackAttempt(
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
                    return RejectedAttack(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate);
                return new EnemyAttackExecutionResultV1(
                    EnemyRuntimeOperationStatusV1.ExactReplay,
                    replay.Result.Rejection,
                    replay.Result.Request);
            }

            EnemyAttackExecutionResultV1 result;
            bool recordReplay = true;
            if (validation != EnemyRuntimeRejectionCodeV1.None)
            {
                result = RejectedAttack(validation);
            }
            else if (callerSuppliedContext
                && (suppliedContext == null
                    || !string.Equals(
                        EnemyRuntimeAuthorityFingerprintV1.AimContext(suppliedContext),
                        EnemyRuntimeAuthorityFingerprintV1.AimContext(authoritativeContext),
                        StringComparison.Ordinal)))
            {
                result = RejectedAttack(EnemyRuntimeRejectionCodeV1.InvalidCommand);
            }
            else if (!actorState.IsActive)
            {
                result = RejectedAttack(EnemyRuntimeRejectionCodeV1.ActorTerminal);
            }
            else if (requested == null)
            {
                result = new EnemyAttackExecutionResultV1(
                    EnemyRuntimeOperationStatusV1.NoEffect,
                    EnemyRuntimeRejectionCodeV1.MissingAttackIntent,
                    null);
            }
            else if (binding == null)
            {
                result = RejectedAttack(EnemyRuntimeRejectionCodeV1.UnknownAttack);
            }
            else
            {
                double readyAt;
                nextReadyAtByAttack.TryGetValue(requested.AttackId, out readyAt);
                if (occurredAtSeconds < readyAt)
                {
                    result = RejectedAttack(EnemyRuntimeRejectionCodeV1.CooldownActive);
                }
                else
                {
                    EnemyAttackIntent committed = binding.TargetingAim.Policy.Commit(
                        requested,
                        authoritativeContext,
                        binding.TargetingAim.Configuration);
                    if (!CommittedIntentPreservesIssuedSelection(requested, committed))
                    {
                        result = RejectedAttack(EnemyRuntimeRejectionCodeV1.InvalidCommand);
                    }
                    else
                    {
                        var executionContext = new EnemyAttackExecutionContextV1(
                            operationStableId,
                            Identity,
                            LifecycleGeneration,
                            occurredAtSeconds,
                            DifficultyScaling);
                        StableId itemInstance = ResolveAttackItemInstance(binding.Descriptor.AttackId);
                        EnemyAttackExecutionRequestV1 execution =
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
                            result = RejectedAttack(EnemyRuntimeRejectionCodeV1.InvalidCommand);
                        }
                        else
                        {
                            EnemyAttackPatternDispatchResultV1 dispatch;
                            if (EnemyAttackEffectEmissionDispatchV1
                                .IsLegacyCompatibilityExecution(execution))
                            {
                                // Schema-v1 production content intentionally retains the historical
                                // one-call effect boundary and does not enter pattern authority.
                                dispatch = EnemyAttackEffectEmissionDispatchV1.DispatchLegacy(
                                    downstream.AttackEffects,
                                    execution);
                            }
                            else
                            {
                                EnemyAttackPatternStartResultV1 pattern =
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
                                        != EnemyAttackPatternDispatchRejectionCodeV1
                                            .DownstreamFailure;
                                result = RejectedAttack(
                                    EnemyRuntimeRejectionCodeV1.InvalidCommand);
                            }
                            else
                            {
                                string executionFingerprint =
                                    EnemyRuntimeAuthorityFingerprintV1.Execution(
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
                                            EnemyRuntimeRejectionCodeV1
                                                .ConflictingDuplicate);
                                    }
                                    else
                                    {
                                        nextReadyAtByAttack[requested.AttackId] =
                                            occurredAtSeconds
                                            + execution.ResolvedCooldownSeconds;
                                        result = new EnemyAttackExecutionResultV1(
                                            EnemyRuntimeOperationStatusV1.Applied,
                                            EnemyRuntimeRejectionCodeV1.None,
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
                                    result = new EnemyAttackExecutionResultV1(
                                        EnemyRuntimeOperationStatusV1.Applied,
                                        EnemyRuntimeRejectionCodeV1.None,
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

        public EnemyPlayerDamagePortResultV1 RoutePlayerImpact(
            EnemyAttackExecutionRequestV1 execution,
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
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.EntityMismatch);
            }
            if (execution.LifecycleGeneration != LifecycleGeneration)
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.StaleLifecycle);

            AcceptedExecutionRecord accepted;
            if (!acceptedExecutions.TryGetValue(execution.OperationStableId, out accepted))
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.ExecutionNotIssued);
            string suppliedFingerprint = EnemyRuntimeAuthorityFingerprintV1.Execution(
                execution,
                accepted.DecisionFingerprint);
            if (!string.Equals(accepted.Fingerprint, suppliedFingerprint, StringComparison.Ordinal))
                return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.InvalidCommand);

            string signature = EnemyRuntimeAuthorityFingerprintV1.Impact(
                accepted.Fingerprint,
                targetEntityStableId,
                observedTargetLifecycleGeneration);
            ImpactReplayRecord replay;
            if (impactReplay.TryGetValue(hitEventStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                    return RejectedPlayerImpact(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate);
                return new EnemyPlayerDamagePortResultV1(
                    EnemyRuntimeOperationStatusV1.ExactReplay,
                    replay.Result.Rejection);
            }

            EnemyAttackExecutionRequestV1 canonical = accepted.Execution;
            var request = new EnemyPlayerDamageRequestV1(
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
            EnemyPlayerDamagePortResultV1 result = downstream.PlayerDamage.Route(request)
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
            EnemyAttackExecutionRequestV1 execution,
            StableId operationStableId,
            double occurredAtSeconds,
            EnemyRuntimeAttackBindingV1 binding,
            EnemyAttackIntent committed,
            StableId itemInstance)
        {
            return execution != null
                && execution.OperationStableId == operationStableId
                && EnemyRuntimeAuthorityFingerprintV1.IdentityEquals(execution.Identity, Identity)
                && execution.LifecycleGeneration == LifecycleGeneration
                && execution.OccurredAtSeconds == occurredAtSeconds
                && string.Equals(
                    EnemyRuntimeAuthorityFingerprintV1.Descriptor(execution.Descriptor),
                    EnemyRuntimeAuthorityFingerprintV1.Descriptor(binding.Descriptor),
                    StringComparison.Ordinal)
                && EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(
                    execution.Descriptor)
                    == EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(
                        binding.Descriptor)
                && string.Equals(
                    EnemyRuntimeAuthorityFingerprintV1.AttackIntent(execution.CommittedIntent),
                    EnemyRuntimeAuthorityFingerprintV1.AttackIntent(committed),
                    StringComparison.Ordinal)
                && execution.ItemInstanceStableId == itemInstance
                && execution.ExecutionKind == binding.Capability.Configuration.ExecutionKind;
        }
    }
}
