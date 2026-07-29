using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.UnityAdapters.Enemies
{
    public enum EnemyCommittedAttackPatternStatus
    {
        Applied = 1,
        ExactReplay = 2,
        CooldownActive = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public sealed class EnemyCommittedAttackPatternResult
    {
        public EnemyCommittedAttackPatternResult(
            EnemyCommittedAttackPatternStatus status,
            StableId operationStableId,
            EnemyAttackExecutionRequest execution,
            EnemyAttackSequence sequence,
            EnemyAttackPatternDispatchResult dispatch,
            string rejectionCode)
        {
            Status = status;
            OperationStableId = operationStableId;
            Execution = execution;
            Sequence = sequence;
            Dispatch = dispatch;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public EnemyCommittedAttackPatternStatus Status { get; }
        public StableId OperationStableId { get; }
        public EnemyAttackExecutionRequest Execution { get; }
        public EnemyAttackSequence Sequence { get; }
        public EnemyAttackPatternDispatchResult Dispatch { get; }
        public string RejectionCode { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyCommittedAttackPatternStatus.Applied
                    || Status == EnemyCommittedAttackPatternStatus.ExactReplay;
            }
        }
    }

    public interface IEnemyCommittedAttackPatternPort
    {
        EnemyCommittedAttackPatternResult Commit(
            StableId operationStableId,
            EnemyAttackIntent committedIntent);

        EnemyAttackPatternCancellationResult CancelLifecycle(
            StableId operationStableId,
            double occurredAtSeconds);
    }

    /// <summary>
    /// Binds one schema-v2 attack descriptor to one source lifecycle. Sequence authority and
    /// dispatch are committed as one outer operation: transient downstream failure is retryable,
    /// and cooldown/replay state is recorded only after atomic dispatch acceptance.
    /// </summary>
    public sealed class EnemyCommittedAttackPatternExecutor :
        IEnemyCommittedAttackPatternPort
    {
        private sealed class CommitReplay
        {
            public CommitReplay(
                string fingerprint,
                EnemyCommittedAttackPatternResult result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public EnemyCommittedAttackPatternResult Result { get; }
        }

        private readonly EnemyLiveIdentity identity;
        private readonly Func<long> lifecycleGenerationExporter;
        private readonly Func<bool> activeExporter;
        private readonly EnemyAttackCapabilityDescriptor descriptor;
        private readonly StableId equipmentInstanceStableId;
        private readonly IEnemyAttackPatternRunTime runTime;
        private readonly IEnemyAttackPatternEffectPort effectPort;
        private readonly Dictionary<StableId, CommitReplay> commitReplay =
            new Dictionary<StableId, CommitReplay>();
        private EnemyAttackPatternState authority;
        private long authorityLifecycleGeneration;
        private double nextAvailableAtSeconds;

        public EnemyCommittedAttackPatternExecutor(
            EnemyLiveIdentity identity,
            Func<long> lifecycleGenerationExporter,
            Func<bool> activeExporter,
            EnemyAttackCapabilityDescriptor descriptor,
            StableId equipmentInstanceStableId,
            IEnemyAttackPatternRunTime runTime,
            IEnemyAttackPatternEffectPort effectPort)
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

        public EnemyLiveIdentity Identity
        {
            get { return identity; }
        }

        public EnemyAttackCapabilityDescriptor Descriptor
        {
            get { return descriptor; }
        }

        public double NextAvailableAtSeconds
        {
            get { return nextAvailableAtSeconds; }
        }

        public EnemyCommittedAttackPatternResult Commit(
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
                        EnemyCommittedAttackPatternStatus.ConflictingDuplicate,
                        operationStableId,
                        "enemy-pattern-commit-operation-conflict");
                }
                return new EnemyCommittedAttackPatternResult(
                    EnemyCommittedAttackPatternStatus.ExactReplay,
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
                        EnemyCommittedAttackPatternStatus.Rejected,
                        operationStableId,
                        "enemy-pattern-commit-invalid"));
            }
            if (runTime.CurrentTimeSeconds < nextAvailableAtSeconds)
            {
                // Cooldown observation is not terminal operation history. The caller may retry the
                // same immutable operation after authoritative Run Session time advances.
                return Rejected(
                    EnemyCommittedAttackPatternStatus.CooldownActive,
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
            var execution = new EnemyAttackExecutionRequest(
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
            EnemyAttackPatternStartResult start = authority.Start(execution);
            if (start == null || !start.IsAccepted || start.Sequence == null)
            {
                return Remember(
                    operationStableId,
                    fingerprint,
                    Rejected(
                        EnemyCommittedAttackPatternStatus.Rejected,
                        operationStableId,
                        start == null
                            ? "enemy-pattern-authority-null"
                            : "enemy-pattern-authority-" + start.Rejection));
            }

            var dispatch = new EnemyAttackSequenceDispatch(
                execution,
                start.Sequence,
                start.Emissions);
            EnemyAttackPatternDispatchResult dispatched =
                effectPort.Dispatch(dispatch);
            if (dispatched == null || !dispatched.IsAccepted)
            {
                var rejected = new EnemyCommittedAttackPatternResult(
                    EnemyCommittedAttackPatternStatus.Rejected,
                    operationStableId,
                    execution,
                    start.Sequence,
                    dispatched,
                    dispatched == null
                        ? "enemy-pattern-dispatch-null"
                        : "enemy-pattern-dispatch-" + dispatched.Rejection);
                bool terminalConflict = dispatched != null
                    && dispatched.Rejection
                        == EnemyAttackPatternDispatchRejectionCode
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
                new EnemyCommittedAttackPatternResult(
                    start.Status == EnemyAttackPatternOperationStatus.ExactReplay
                        || dispatched.Status
                            == EnemyAttackPatternOperationStatus.ExactReplay
                        ? EnemyCommittedAttackPatternStatus.ExactReplay
                        : EnemyCommittedAttackPatternStatus.Applied,
                    operationStableId,
                    execution,
                    start.Sequence,
                    dispatched,
                    string.Empty));
        }

        public EnemyAttackPatternCancellationResult CancelLifecycle(
            StableId operationStableId,
            double occurredAtSeconds)
        {
            EnsureAuthority();
            var command = new EnemyAttackLifecycleCancellationCommand(
                operationStableId,
                identity.EntityInstanceId,
                authorityLifecycleGeneration,
                occurredAtSeconds);
            EnemyAttackPatternCancellationResult cancellation =
                authority.CancelLifecycle(command);
            if (cancellation == null
                || !cancellation.IsAuthorityAccepted
                || cancellation.Fact == null)
            {
                return cancellation;
            }
            EnemyAttackPatternDispatchResult dispatch =
                effectPort.Cancel(cancellation.Fact);
            return new EnemyAttackPatternCancellationResult(
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
            authority = new EnemyAttackPatternState(identity, lifecycle);
            nextAvailableAtSeconds = 0d;
            commitReplay.Clear();
        }

        private EnemyCommittedAttackPatternResult Remember(
            StableId operationStableId,
            string fingerprint,
            EnemyCommittedAttackPatternResult result)
        {
            if (operationStableId != null)
            {
                commitReplay.Add(
                    operationStableId,
                    new CommitReplay(fingerprint, result));
            }
            return result;
        }

        private static EnemyAttackExecutionKind ResolveExecutionKind(
            EnemyAttackCapabilityDescriptor attack)
        {
            if (attack.MeleePattern != null)
            {
                return attack.MeleePattern.LungeDistance > 0d
                    ? EnemyAttackExecutionKind.Pounce
                    : EnemyAttackExecutionKind.Contact;
            }
            return attack.ProjectilePayload != null
                && attack.ProjectilePayload.AreaPayload != null
                ? EnemyAttackExecutionKind.Area
                : EnemyAttackExecutionKind.Projectile;
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

        private static EnemyCommittedAttackPatternResult Rejected(
            EnemyCommittedAttackPatternStatus status,
            StableId operationStableId,
            string rejectionCode)
        {
            return new EnemyCommittedAttackPatternResult(
                status,
                operationStableId,
                null,
                null,
                null,
                rejectionCode);
        }
    }
}
