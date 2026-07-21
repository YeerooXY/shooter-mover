using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.RunConditionIntegration
{
    internal static class RunMissionStrongboxSnapshotSourceResolverV1
    {
        public static bool TryResolve(
            IRunMissionResultPortV1 port,
            out PlayerHoldingsSnapshotV1 holdings,
            out StrongboxOpeningSnapshotV1 strongboxes,
            out string rejectionCode,
            out bool retryable)
        {
            holdings = null;
            strongboxes = null;
            rejectionCode = string.Empty;
            retryable = false;
            var source = port as IRunMissionStrongboxSnapshotSourceV1;
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
    public sealed class PersistentMissionResultRunPortV1 :
        IRunMissionResultPortV1,
        IRunMissionResultEndRetryPolicyV1,
        IRunMissionResultLifecycleBindingV1
    {
        private readonly IRunMissionResultPortV1 inner;
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly FrozenCharacterRunInputsV1 frozenInputs;
        private readonly long expectedAccountRevision;
        private readonly StrongboxMissionResultApplicationCoordinatorV1
            coordinator;
        private readonly Dictionary<StableId, string> retryableEndFailures =
            new Dictionary<StableId, string>();
        private StableId boundRunStableId;
        private Func<long> runLifecycleGenerationExporter;

        public PersistentMissionResultRunPortV1(
            IRunMissionResultPortV1 inner,
            CharacterCompositionCoordinatorV1 composition,
            FrozenCharacterRunInputsV1 frozenInputs,
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
            coordinator = new StrongboxMissionResultApplicationCoordinatorV1(
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
            out MissionRunPayloadV1 runPayload)
        {
            return inner.TryGetRun(runStableId, out runPayload);
        }

        public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            RunStrongboxCollectionRequestV1 request,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1
                routePayload)
        {
            return inner.RecordCollectedStrongbox(request, routePayload);
        }

        public MissionRunAuthorityResultV1 EndRun(
            EndRunSessionCommandV1 command,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1
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
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.InvalidRequest,
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

            MissionRunAuthorityResultV1 result =
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

            PlayerHoldingsSnapshotV1 sourceHoldings;
            StrongboxOpeningSnapshotV1 sourceStrongboxes;
            string sourceError;
            bool sourceRetryable;
            if (!RunMissionStrongboxSnapshotSourceResolverV1.TryResolve(
                inner,
                out sourceHoldings,
                out sourceStrongboxes,
                out sourceError,
                out sourceRetryable))
            {
                RememberRetryable(command, sourceRetryable);
                return ExternalReject(result, command, sourceError);
            }

            StableId applicationOperation = StrongboxCanonicalV1.DeriveId(
                "boxresultapply",
                command.OperationStableId.ToString(),
                command.RunStableId.ToString(),
                result.ResultPayload.Fingerprint,
                frozenInputs.Character.CharacterInstanceStableId.ToString(),
                command.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            var application =
                new StrongboxMissionResultApplicationCommandV1(
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
            StrongboxMissionResultApplicationResultV1 applied =
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
            EndRunSessionCommandV1 command,
            MissionRunAuthorityResultV1 result)
        {
            if (command == null || result == null
                || result.Status
                    != MissionRunAuthorityStatusV1.ExternalAuthorityRejected)
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
            EndRunSessionCommandV1 command,
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

        private MissionRunAuthorityResultV1 InvalidEnd(
            EndRunSessionCommandV1 command,
            string rejection)
        {
            if (command != null)
            {
                retryableEndFailures.Remove(command.OperationStableId);
            }
            return new MissionRunAuthorityResultV1(
                MissionRunAuthorityStatusV1.InvalidRequest,
                inner.Sequence,
                inner.Sequence,
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint,
                null,
                null,
                null,
                rejection);
        }

        private static MissionRunAuthorityResultV1 ExternalReject(
            MissionRunAuthorityResultV1 source,
            EndRunSessionCommandV1 command,
            string rejection)
        {
            return new MissionRunAuthorityResultV1(
                MissionRunAuthorityStatusV1.ExternalAuthorityRejected,
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

    public sealed class StrongboxPersistentNonConditionRuntimePortFactoryV1 :
        IRunSessionNonConditionRuntimePortFactoryV1
    {
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly IRunSessionNonConditionRuntimePortFactoryV1 inner;

        public StrongboxPersistentNonConditionRuntimePortFactoryV1(
            CharacterCompositionCoordinatorV1 composition,
            IRunSessionNonConditionRuntimePortFactoryV1 inner)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public RunSessionNonConditionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs)
        {
            RunSessionNonConditionRuntimePortsV1 ports =
                inner.Create(command, resolvedRunStableId, frozenInputs);
            if (ports == null)
            {
                throw new InvalidOperationException(
                    "The non-condition runtime factory returned null.");
            }
            PlayerAccountSnapshotV1 account = composition.Account;
            if (account == null)
            {
                throw new InvalidOperationException(
                    "The selected character account is unavailable.");
            }
            return new RunSessionNonConditionRuntimePortsV1(
                ports.Player,
                ports.Weapons,
                ports.ActiveAbilities,
                ports.Rooms,
                new PersistentMissionResultRunPortV1(
                    ports.MissionResults,
                    composition,
                    frozenInputs,
                    account.Revision));
        }
    }
}
