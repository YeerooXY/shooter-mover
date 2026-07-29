using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.RunConditionIntegration
{
    internal static class RunMissionStrongboxSnapshotSourceResolver
    {
        public static bool TryResolve(
            IRunMissionResultPort port,
            out PlayerHoldingsSnapshot holdings,
            out StrongboxOpeningSnapshot strongboxes,
            out string rejectionCode,
            out bool retryable)
        {
            holdings = null;
            strongboxes = null;
            rejectionCode = string.Empty;
            retryable = false;
            var source = port as IRunMissionStrongboxSnapshotSource;
            if (source == null)
            {
                rejectionCode =
                    "box-transfer-source-snapshot-port-unavailable";
                return false;
            }

            try
            {
                holdings = source.ExportCollectedStrongboxHoldings();
                strongboxes =
                    source.ExportCollectedStrongboxRegistrations();
            }
            catch (Exception exception)
            {
                rejectionCode = "box-transfer-source-snapshot-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                retryable = true;
                return false;
            }
            if (holdings == null || strongboxes == null)
            {
                rejectionCode = "box-transfer-source-snapshot-unavailable";
                retryable = true;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Decorates the existing mission-result port. The immutable result is accepted by
    /// Run Session only after the complete selected-character transfer is durably saved.
    /// Compensated transient failures remain exact-retryable even after the inner RUN
    /// authority has frozen its terminal mission result.
    /// </summary>
    public sealed class PersistentMissionResultRunPort :
        IRunMissionResultPort,
        IRunMissionResultEndRetryPolicy,
        IRunMissionResultLifecycleBinding
    {
        private readonly IRunMissionResultPort inner;
        private readonly CharacterSetupFlow composition;
        private readonly FrozenCharacterRunInputs frozenInputs;
        private readonly long expectedAccountRevision;
        private readonly StrongboxMissionResultApplicationFlow
            coordinator;
        private readonly Dictionary<StableId, string> retryableEndFailures =
            new Dictionary<StableId, string>();
        private StableId boundRunStableId;
        private Func<long> runLifecycleGenerationExporter;

        public PersistentMissionResultRunPort(
            IRunMissionResultPort inner,
            CharacterSetupFlow composition,
            FrozenCharacterRunInputs frozenInputs,
            long expectedAccountRevision)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.frozenInputs = frozenInputs
                ?? throw new ArgumentNullException(nameof(frozenInputs));
            if (expectedAccountRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedAccountRevision));
            }
            this.expectedAccountRevision = expectedAccountRevision;
            coordinator = new StrongboxMissionResultApplicationFlow(
                composition,
                ExportBoundRunLifecycleGeneration);
        }

        public long Sequence { get { return inner.Sequence; } }

        public void BindRunLifecycle(
            StableId runStableId,
            Func<long> lifecycleGenerationExporter)
        {
            if (runStableId == null)
            {
                throw new ArgumentNullException(nameof(runStableId));
            }
            if (lifecycleGenerationExporter == null)
            {
                throw new ArgumentNullException(
                    nameof(lifecycleGenerationExporter));
            }
            if (boundRunStableId != null
                && boundRunStableId != runStableId)
            {
                throw new InvalidOperationException(
                    "The mission-result persistence port is already bound to another run.");
            }
            boundRunStableId = runStableId;
            runLifecycleGenerationExporter = lifecycleGenerationExporter;
        }

        private long ExportBoundRunLifecycleGeneration()
        {
            if (boundRunStableId == null
                || runLifecycleGenerationExporter == null)
            {
                throw new InvalidOperationException(
                    "The mission-result persistence port is not bound to Run Session.");
            }
            return runLifecycleGenerationExporter();
        }

        public bool TryGetRun(
            StableId runStableId,
            out MissionRunPayload runPayload)
        {
            return inner.TryGetRun(runStableId, out runPayload);
        }

        public MissionRunStateResult RecordCollectedStrongbox(
            RunStrongboxCollectionRequest request,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayload
                routePayload)
        {
            return inner.RecordCollectedStrongbox(request, routePayload);
        }

        public MissionRunStateResult EndRun(
            EndRunSessionCommand command,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayload
                routePayload)
        {
            long authoritativeRunGeneration;
            try
            {
                authoritativeRunGeneration =
                    ExportBoundRunLifecycleGeneration();
            }
            catch (Exception exception)
            {
                return InvalidEnd(
                    command,
                    "box-transfer-run-lifecycle-unbound-"
                        + exception.GetType().Name.ToLowerInvariant());
            }
            if (command != null
                && (command.RunStableId != boundRunStableId
                    || command.LifecycleGeneration
                        != authoritativeRunGeneration))
            {
                retryableEndFailures.Remove(command.OperationStableId);
                return new MissionRunStateResult(
                    MissionRunStateStatus.InvalidRequest,
                    inner.Sequence,
                    inner.Sequence,
                    command.OperationStableId,
                    command.Fingerprint,
                    null,
                    null,
                    null,
                    command.RunStableId != boundRunStableId
                        ? "box-transfer-run-identity-mismatch"
                        : (command.LifecycleGeneration
                                < authoritativeRunGeneration
                            ? "box-transfer-run-generation-stale"
                            : "box-transfer-run-generation-future"));
            }

            MissionRunStateResult result =
                inner.EndRun(command, routePayload);
            if (result == null || !result.Succeeded
                || result.ResultPayload == null)
            {
                if (command != null)
                {
                    retryableEndFailures.Remove(command.OperationStableId);
                }
                return result;
            }

            PlayerHoldingsSnapshot sourceHoldings;
            StrongboxOpeningSnapshot sourceStrongboxes;
            string sourceError;
            bool sourceRetryable;
            if (!RunMissionStrongboxSnapshotSourceResolver.TryResolve(
                inner,
                out sourceHoldings,
                out sourceStrongboxes,
                out sourceError,
                out sourceRetryable))
            {
                RememberRetryable(command, sourceRetryable);
                return ExternalReject(result, command, sourceError);
            }

            StableId applicationOperation = Strongbox.DeriveId(
                "boxresultapply",
                command.OperationStableId.ToString(),
                command.RunStableId.ToString(),
                result.ResultPayload.Fingerprint,
                frozenInputs.Character.CharacterInstanceStableId.ToString(),
                command.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            var application =
                new StrongboxMissionResultApplicationCommand(
                    applicationOperation,
                    command.RunStableId,
                    command.LifecycleGeneration,
                    result.ResultPayload,
                    frozenInputs.Character.CharacterInstanceStableId,
                    frozenInputs.Character.Revision,
                    frozenInputs.Character.Fingerprint,
                    expectedAccountRevision,
                    sourceHoldings,
                    sourceStrongboxes);
            StrongboxMissionResultApplicationResult applied =
                coordinator.Apply(application);
            bool succeeded = applied != null && applied.Succeeded;
            RememberRetryable(
                command,
                !succeeded
                    && (applied == null || applied.ExactRetryAllowed));
            return succeeded
                ? result
                : ExternalReject(
                    result,
                    command,
                    applied == null
                        ? "box-transfer-result-null"
                        : applied.RejectionCode);
        }

        public bool IsRetryableEndFailure(
            EndRunSessionCommand command,
            MissionRunStateResult result)
        {
            if (command == null || result == null
                || result.Status
                    != MissionRunStateStatus.ExternalAuthorityRejected)
            {
                return false;
            }
            string fingerprint;
            return retryableEndFailures.TryGetValue(
                    command.OperationStableId,
                    out fingerprint)
                && string.Equals(
                    fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal);
        }

        private void RememberRetryable(
            EndRunSessionCommand command,
            bool retryable)
        {
            if (command == null)
            {
                return;
            }
            if (retryable)
            {
                retryableEndFailures[command.OperationStableId] =
                    command.Fingerprint;
            }
            else
            {
                retryableEndFailures.Remove(command.OperationStableId);
            }
        }

        private MissionRunStateResult InvalidEnd(
            EndRunSessionCommand command,
            string rejection)
        {
            if (command != null)
            {
                retryableEndFailures.Remove(command.OperationStableId);
            }
            return new MissionRunStateResult(
                MissionRunStateStatus.InvalidRequest,
                inner.Sequence,
                inner.Sequence,
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint,
                null,
                null,
                null,
                rejection);
        }

        private static MissionRunStateResult ExternalReject(
            MissionRunStateResult source,
            EndRunSessionCommand command,
            string rejection)
        {
            return new MissionRunStateResult(
                MissionRunStateStatus.ExternalAuthorityRejected,
                source == null ? 0L : source.PreviousSequence,
                source == null ? 0L : source.CurrentSequence,
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint,
                source == null ? null : source.RunPayload,
                null,
                source == null ? null : source.ResultPayload,
                string.IsNullOrWhiteSpace(rejection)
                    ? "box-transfer-rejected"
                    : rejection);
        }
    }

    public sealed class StrongboxPersistentNonConditionLivePortFactory :
        IRunSessionNonConditionLivePortFactory
    {
        private readonly CharacterSetupFlow composition;
        private readonly IRunSessionNonConditionLivePortFactory inner;

        public StrongboxPersistentNonConditionLivePortFactory(
            CharacterSetupFlow composition,
            IRunSessionNonConditionLivePortFactory inner)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public RunSessionNonConditionLivePorts Create(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputs frozenInputs)
        {
            RunSessionNonConditionLivePorts ports =
                inner.Create(command, resolvedRunStableId, frozenInputs);
            if (ports == null)
            {
                throw new InvalidOperationException(
                    "The non-condition runtime factory returned null.");
            }
            PlayerAccountSnapshot account = composition.Account;
            if (account == null)
            {
                throw new InvalidOperationException(
                    "The selected character account is unavailable.");
            }
            return new RunSessionNonConditionLivePorts(
                ports.Player,
                ports.Guns,
                ports.ActiveAbilities,
                ports.Rooms,
                new PersistentMissionResultRunPort(
                    ports.MissionResults,
                    composition,
                    frozenInputs,
                    account.Revision));
        }
    }
}
