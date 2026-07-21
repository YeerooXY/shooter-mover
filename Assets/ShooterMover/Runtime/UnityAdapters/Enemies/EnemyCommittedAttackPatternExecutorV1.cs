using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.UnityAdapters.Enemies
{
    public enum EnemyCommittedAttackPatternStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        CooldownActive = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public sealed class EnemyCommittedAttackPatternResultV1
    {
        public EnemyCommittedAttackPatternResultV1(
            EnemyCommittedAttackPatternStatusV1 status,
            StableId operationStableId,
            EnemyAttackExecutionRequestV1 execution,
            EnemyAttackSequenceV1 sequence,
            EnemyAttackPatternDispatchResultV1 dispatch,
            string rejectionCode)
        {
            Status = status;
            OperationStableId = operationStableId;
            Execution = execution;
            Sequence = sequence;
            Dispatch = dispatch;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public EnemyCommittedAttackPatternStatusV1 Status { get; }
        public StableId OperationStableId { get; }
        public EnemyAttackExecutionRequestV1 Execution { get; }
        public EnemyAttackSequenceV1 Sequence { get; }
        public EnemyAttackPatternDispatchResultV1 Dispatch { get; }
        public string RejectionCode { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyCommittedAttackPatternStatusV1.Applied
                    || Status == EnemyCommittedAttackPatternStatusV1.ExactReplay;
            }
        }
    }

    public interface IEnemyCommittedAttackPatternPortV1
    {
        EnemyCommittedAttackPatternResultV1 Commit(
            StableId operationStableId,
            EnemyAttackIntent committedIntent);

        EnemyAttackPatternCancellationResultV1 CancelLifecycle(
            StableId operationStableId,
            double occurredAtSeconds);
    }

    /// <summary>
    /// Binds one schema-v2 attack descriptor to one source lifecycle. Sequence authority and
    /// dispatch are committed as one outer operation: transient downstream failure is retryable,
    /// and cooldown/replay state is recorded only after atomic dispatch acceptance.
    /// </summary>
    public sealed class EnemyCommittedAttackPatternExecutorV1 :
        IEnemyCommittedAttackPatternPortV1
    {
        private sealed class CommitReplay
        {
            public CommitReplay(
                string fingerprint,
                EnemyCommittedAttackPatternResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public EnemyCommittedAttackPatternResultV1 Result { get; }
        }

        private readonly EnemyRuntimeIdentityV1 identity;
        private readonly Func<long> lifecycleGenerationExporter;
        private readonly Func<bool> activeExporter;
        private readonly EnemyAttackCapabilityDescriptorV1 descriptor;
        private readonly StableId equipmentInstanceStableId;
        private readonly IEnemyAttackPatternRunTimeV1 runTime;
        private readonly IEnemyAttackPatternEffectPortV1 effectPort;
        private readonly Dictionary<StableId, CommitReplay> commitReplay =
            new Dictionary<StableId, CommitReplay>();
        private EnemyAttackPatternAuthorityV1 authority;
        private long authorityLifecycleGeneration;
        private double nextAvailableAtSeconds;

        public EnemyCommittedAttackPatternExecutorV1(
            EnemyRuntimeIdentityV1 identity,
            Func<long> lifecycleGenerationExporter,
            Func<bool> activeExporter,
            EnemyAttackCapabilityDescriptorV1 descriptor,
            StableId equipmentInstanceStableId,
            IEnemyAttackPatternRunTimeV1 runTime,
            IEnemyAttackPatternEffectPortV1 effectPort)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this.lifecycleGenerationExporter = lifecycleGenerationExporter
                ?? throw new ArgumentNullException(
                    nameof(lifecycleGenerationExporter));
            this.activeExporter = activeExporter
                ?? throw new ArgumentNullException(nameof(activeExporter));
            this.descriptor = descriptor
                ?? throw new ArgumentNullException(nameof(descriptor));
            this.equipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(equipmentInstanceStableId));
            this.runTime = runTime ?? throw new ArgumentNullException(nameof(runTime));
            this.effectPort = effectPort ?? throw new ArgumentNullException(nameof(effectPort));
            EnsureAuthority();
        }

        public EnemyRuntimeIdentityV1 Identity
        {
            get { return identity; }
        }

        public EnemyAttackCapabilityDescriptorV1 Descriptor
        {
            get { return descriptor; }
        }

        public double NextAvailableAtSeconds
        {
            get { return nextAvailableAtSeconds; }
        }

        public EnemyCommittedAttackPatternResultV1 Commit(
            StableId operationStableId,
            EnemyAttackIntent committedIntent)
        {
            string fingerprint = Fingerprint(operationStableId, committedIntent);
            CommitReplay replay;
            if (operationStableId != null
                && commitReplay.TryGetValue(operationStableId, out replay))
            {
                if (!string.Equals(
                        replay.Fingerprint,
                        fingerprint,
                        StringComparison.Ordinal))
                {
                    return Rejected(
                        EnemyCommittedAttackPatternStatusV1.ConflictingDuplicate,
                        operationStableId,
                        "enemy-pattern-commit-operation-conflict");
                }
                return new EnemyCommittedAttackPatternResultV1(
                    EnemyCommittedAttackPatternStatusV1.ExactReplay,
                    operationStableId,
                    replay.Result.Execution,
                    replay.Result.Sequence,
                    replay.Result.Dispatch,
                    string.Empty);
            }

            EnsureAuthority();
            long lifecycle = lifecycleGenerationExporter();
            if (operationStableId == null
                || committedIntent == null
                || lifecycle <= 0L
                || !activeExporter()
                || committedIntent.AttackerEntityId != identity.EntityInstanceId)
            {
                return Remember(
                    operationStableId,
                    fingerprint,
                    Rejected(
                        EnemyCommittedAttackPatternStatusV1.Rejected,
                        operationStableId,
                        "enemy-pattern-commit-invalid"));
            }
            if (runTime.CurrentTimeSeconds < nextAvailableAtSeconds)
            {
                // Cooldown observation is not terminal operation history. The caller may retry the
                // same immutable operation after authoritative Run Session time advances.
                return Rejected(
                    EnemyCommittedAttackPatternStatusV1.CooldownActive,
                    operationStableId,
                    "enemy-pattern-cooldown-active");
            }

            EnemyAttackIntent rebound = new EnemyAttackIntent(
                identity.EntityInstanceId,
                identity.RunParticipantId,
                committedIntent.TargetEntityId,
                descriptor.AttackId,
                committedIntent.CommittedOrigin,
                committedIntent.CommittedDirection,
                committedIntent.CommittedTargetPoint,
                committedIntent.DecisionId,
                committedIntent.BehaviorPhaseId,
                committedIntent.ReasonCode);
            var execution = new EnemyAttackExecutionRequestV1(
                operationStableId,
                identity,
                lifecycle,
                runTime.CurrentTimeSeconds,
                descriptor,
                rebound,
                equipmentInstanceStableId,
                ResolveExecutionKind(descriptor),
                descriptor.Damage,
                descriptor.CooldownSeconds);
            EnemyAttackPatternStartResultV1 start = authority.Start(execution);
            if (start == null || !start.IsAccepted || start.Sequence == null)
            {
                return Remember(
                    operationStableId,
                    fingerprint,
                    Rejected(
                        EnemyCommittedAttackPatternStatusV1.Rejected,
                        operationStableId,
                        start == null
                            ? "enemy-pattern-authority-null"
                            : "enemy-pattern-authority-" + start.Rejection));
            }

            var dispatch = new EnemyAttackSequenceDispatchV1(
                execution,
                start.Sequence,
                start.Emissions);
            EnemyAttackPatternDispatchResultV1 dispatched =
                effectPort.Dispatch(dispatch);
            if (dispatched == null || !dispatched.IsAccepted)
            {
                var rejected = new EnemyCommittedAttackPatternResultV1(
                    EnemyCommittedAttackPatternStatusV1.Rejected,
                    operationStableId,
                    execution,
                    start.Sequence,
                    dispatched,
                    dispatched == null
                        ? "enemy-pattern-dispatch-null"
                        : "enemy-pattern-dispatch-" + dispatched.Rejection);
                bool terminalConflict = dispatched != null
                    && dispatched.Rejection
                        == EnemyAttackPatternDispatchRejectionCodeV1
                            .ConflictingDuplicate;
                return terminalConflict
                    ? Remember(operationStableId, fingerprint, rejected)
                    : rejected;
            }

            nextAvailableAtSeconds = Math.Max(
                nextAvailableAtSeconds,
                start.Sequence.RecoveryEndsAtSeconds);
            return Remember(
                operationStableId,
                fingerprint,
                new EnemyCommittedAttackPatternResultV1(
                    start.Status == EnemyAttackPatternOperationStatusV1.ExactReplay
                        || dispatched.Status
                            == EnemyAttackPatternOperationStatusV1.ExactReplay
                        ? EnemyCommittedAttackPatternStatusV1.ExactReplay
                        : EnemyCommittedAttackPatternStatusV1.Applied,
                    operationStableId,
                    execution,
                    start.Sequence,
                    dispatched,
                    string.Empty));
        }

        public EnemyAttackPatternCancellationResultV1 CancelLifecycle(
            StableId operationStableId,
            double occurredAtSeconds)
        {
            EnsureAuthority();
            var command = new EnemyAttackLifecycleCancellationCommandV1(
                operationStableId,
                identity.EntityInstanceId,
                authorityLifecycleGeneration,
                occurredAtSeconds);
            EnemyAttackPatternCancellationResultV1 cancellation =
                authority.CancelLifecycle(command);
            if (cancellation == null
                || !cancellation.IsAuthorityAccepted
                || cancellation.Fact == null)
            {
                return cancellation;
            }
            EnemyAttackPatternDispatchResultV1 dispatch =
                effectPort.Cancel(cancellation.Fact);
            return new EnemyAttackPatternCancellationResultV1(
                cancellation.Status,
                cancellation.Rejection,
                cancellation.Fact,
                dispatch);
        }

        private void EnsureAuthority()
        {
            long lifecycle = lifecycleGenerationExporter();
            if (lifecycle <= 0L)
            {
                throw new InvalidOperationException(
                    "Enemy attack pattern lifecycle must be positive.");
            }
            if (authority != null && authorityLifecycleGeneration == lifecycle)
            {
                return;
            }
            authorityLifecycleGeneration = lifecycle;
            authority = new EnemyAttackPatternAuthorityV1(identity, lifecycle);
            nextAvailableAtSeconds = 0d;
            commitReplay.Clear();
        }

        private EnemyCommittedAttackPatternResultV1 Remember(
            StableId operationStableId,
            string fingerprint,
            EnemyCommittedAttackPatternResultV1 result)
        {
            if (operationStableId != null)
            {
                commitReplay.Add(
                    operationStableId,
                    new CommitReplay(fingerprint, result));
            }
            return result;
        }

        private static EnemyAttackExecutionKindV1 ResolveExecutionKind(
            EnemyAttackCapabilityDescriptorV1 attack)
        {
            if (attack.MeleePattern != null)
            {
                return attack.MeleePattern.LungeDistance > 0d
                    ? EnemyAttackExecutionKindV1.Pounce
                    : EnemyAttackExecutionKindV1.Contact;
            }
            return attack.ProjectilePayload != null
                && attack.ProjectilePayload.AreaPayload != null
                ? EnemyAttackExecutionKindV1.Area
                : EnemyAttackExecutionKindV1.Projectile;
        }

        private static string Fingerprint(
            StableId operationStableId,
            EnemyAttackIntent intent)
        {
            return (operationStableId == null
                    ? "-"
                    : operationStableId.ToString())
                + "|"
                + (intent == null
                    ? "-"
                    : intent.AttackerEntityId
                        + "|"
                        + intent.TargetEntityId
                        + "|"
                        + intent.AttackId
                        + "|"
                        + intent.CommittedOrigin.X.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ","
                        + intent.CommittedOrigin.Y.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + "|"
                        + intent.CommittedDirection.X.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ","
                        + intent.CommittedDirection.Y.ToString(
                            "R",
                            CultureInfo.InvariantCulture));
        }

        private static EnemyCommittedAttackPatternResultV1 Rejected(
            EnemyCommittedAttackPatternStatusV1 status,
            StableId operationStableId,
            string rejectionCode)
        {
            return new EnemyCommittedAttackPatternResultV1(
                status,
                operationStableId,
                null,
                null,
                null,
                rejectionCode);
        }
    }
}
