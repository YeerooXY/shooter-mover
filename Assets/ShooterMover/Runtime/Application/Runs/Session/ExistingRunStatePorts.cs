using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class ExistingStatusEffectRunPort :
        IRunStatusEffectLivePort
    {
        private readonly StatusEffectState authority;

        public ExistingStatusEffectRunPort(StatusEffectState authority)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
        }

        public string PortId
        {
            get { return "status-effect-runtime"; }
        }

        public long LifecycleGeneration
        {
            get { return authority.LifecycleGeneration; }
        }

        public string SnapshotFingerprint
        {
            get { return authority.Snapshot.Fingerprint; }
        }

        public int ActiveEffectCount
        {
            get { return authority.Snapshot.ActiveEffects.Count; }
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            if (retiringLifecycleGeneration != authority.LifecycleGeneration)
            {
                return retiringLifecycleGeneration < authority.LifecycleGeneration
                    ? "status-effect-stale-generation"
                    : "status-effect-future-generation";
            }
            if (replacementLifecycleGeneration
                != retiringLifecycleGeneration + 1L
                || replacementLifecycleGeneration > int.MaxValue)
            {
                return "status-effect-generation-invalid";
            }
            if (authoritativeTick < authority.Snapshot.LatestAcceptedTick)
            {
                return "status-effect-stale-tick";
            }
            return string.Empty;
        }

        public RunLivePortRestartResult Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunLivePortRestartResult(
                    false,
                    rejection,
                    authority.LifecycleGeneration,
                    authority.Snapshot.Fingerprint);
            }

            StatusEffectCommandResult result = authority.Restart(
                new RestartStatusEffectLifecycleCommand(
                    operationStableId.ToString(),
                    authority.Snapshot.SubjectId,
                    (int)retiringLifecycleGeneration,
                    (int)replacementLifecycleGeneration,
                    authoritativeTick));
            return new RunLivePortRestartResult(
                result != null && result.IsAccepted,
                result == null ? "status-effect-null-result" : result.RejectionCode,
                authority.LifecycleGeneration,
                authority.Snapshot.Fingerprint);
        }
    }

    public sealed class ExistingMissionResultRunPort :
        IRunMissionResultPort,
        IRunMissionStrongboxSnapshotSource
    {
        private readonly MissionRunResultState authority;
        private readonly IPlayerHoldingsState holdings;
        private readonly Func<StrongboxOpeningSnapshot> openingExporter;

        public ExistingMissionResultRunPort(
            MissionRunResultState authority,
            IPlayerHoldingsState holdings,
            Func<StrongboxOpeningSnapshot> openingExporter)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            this.openingExporter = openingExporter
                ?? throw new ArgumentNullException(nameof(openingExporter));
        }

        public long Sequence
        {
            get { return authority.Sequence; }
        }

        public bool TryGetRun(
            StableId runStableId,
            out MissionRunPayload runPayload)
        {
            return authority.TryGetRun(runStableId, out runPayload);
        }

        public PlayerHoldingsSnapshot ExportCollectedStrongboxHoldings()
        {
            return holdings.ExportSnapshot();
        }

        public StrongboxOpeningSnapshot
            ExportCollectedStrongboxRegistrations()
        {
            return openingExporter();
        }

        public MissionRunStateResult RecordCollectedStrongbox(
            RunStrongboxCollectionRequest request,
            PlayerRouteProfilePayload routePayload)
        {
            if (request == null || routePayload == null)
            {
                return Invalid(
                    request == null ? null : request.OperationStableId,
                    request == null ? string.Empty : request.Fingerprint,
                    "run-mission-collection-input-null");
            }
            PlayerHoldingsSnapshot snapshot = holdings.ExportSnapshot();
            if (snapshot == null)
            {
                return Invalid(
                    request.OperationStableId,
                    request.Fingerprint,
                    "run-mission-holdings-snapshot-null");
            }
            return authority.RecordCollectedStrongbox(
                MissionRunCollectStrongboxCommand.Create(
                    request.OperationStableId,
                    request.RunStableId,
                    routePayload,
                    request.DefinitionStableId,
                    request.InstanceStableId,
                    request.GrantStableId,
                    request.SourceStableId,
                    authority.Sequence,
                    holdings.Sequence,
                    snapshot.Fingerprint));
        }

        public MissionRunStateResult EndRun(
            EndRunSessionCommand command,
            PlayerRouteProfilePayload routePayload)
        {
            if (command == null || routePayload == null)
            {
                return Invalid(
                    command == null ? null : command.OperationStableId,
                    command == null ? string.Empty : command.Fingerprint,
                    "run-mission-end-input-null");
            }
            PlayerHoldingsSnapshot holdingsSnapshot = holdings.ExportSnapshot();
            StrongboxOpeningSnapshot openingSnapshot = openingExporter();
            if (holdingsSnapshot == null || openingSnapshot == null)
            {
                return Invalid(
                    command.OperationStableId,
                    command.Fingerprint,
                    "run-mission-external-snapshot-null");
            }
            return authority.EndRun(
                EndMissionRunCommand.Create(
                    command.OperationStableId,
                    command.RunStableId,
                    routePayload,
                    command.CompletionState,
                    authority.Sequence,
                    holdings.Sequence,
                    holdingsSnapshot.Fingerprint,
                    openingSnapshot.Sequence,
                    openingSnapshot.Fingerprint));
        }

        private MissionRunStateResult Invalid(
            StableId operationStableId,
            string requestFingerprint,
            string rejectionCode)
        {
            return new MissionRunStateResult(
                MissionRunStateStatus.InvalidRequest,
                authority.Sequence,
                authority.Sequence,
                operationStableId,
                requestFingerprint,
                null,
                null,
                null,
                rejectionCode);
        }
    }

    public abstract class DelegatedRunLifecyclePort :
        IRunLifecycleLivePort
    {
        private readonly string portId;
        private readonly Func<long> generationExporter;
        private readonly Func<string> fingerprintExporter;
        private readonly Func<long, long, long, string> restartValidator;
        private readonly Func<StableId, long, long, long, bool> restartCommit;

        protected DelegatedRunLifecyclePort(
            string portId,
            Func<long> generationExporter,
            Func<string> fingerprintExporter,
            Func<long, long, long, string> restartValidator,
            Func<StableId, long, long, long, bool> restartCommit)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                throw new ArgumentException(
                    "A delegated runtime-port identity is required.",
                    nameof(portId));
            }
            this.portId = portId.Trim();
            this.generationExporter = generationExporter
                ?? throw new ArgumentNullException(nameof(generationExporter));
            this.fingerprintExporter = fingerprintExporter
                ?? throw new ArgumentNullException(nameof(fingerprintExporter));
            this.restartValidator = restartValidator
                ?? throw new ArgumentNullException(nameof(restartValidator));
            this.restartCommit = restartCommit
                ?? throw new ArgumentNullException(nameof(restartCommit));
        }

        public string PortId
        {
            get { return portId; }
        }

        public long LifecycleGeneration
        {
            get { return generationExporter(); }
        }

        public string SnapshotFingerprint
        {
            get { return fingerprintExporter(); }
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            return restartValidator(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick)
                ?? string.Empty;
        }

        public RunLivePortRestartResult Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunLivePortRestartResult(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
            bool succeeded = restartCommit(
                operationStableId,
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            return new RunLivePortRestartResult(
                succeeded,
                succeeded ? string.Empty : portId + "-restart-rejected",
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    public sealed class DelegatedConditionalFactRunPort :
        DelegatedRunLifecyclePort,
        IRunConditionalFactLivePort
    {
        public DelegatedConditionalFactRunPort(
            Func<long> generationExporter,
            Func<string> fingerprintExporter,
            Func<long, long, long, string> restartValidator,
            Func<StableId, long, long, long, bool> restartCommit)
            : base(
                "conditional-fact-runtime",
                generationExporter,
                fingerprintExporter,
                restartValidator,
                restartCommit)
        {
        }
    }

    public sealed class DelegatedRoomRunPort :
        DelegatedRunLifecyclePort,
        IRunRoomLivePort
    {
        private readonly Func<StableId> roomExporter;

        public DelegatedRoomRunPort(
            Func<StableId> roomExporter,
            Func<long> generationExporter,
            Func<string> fingerprintExporter,
            Func<long, long, long, string> restartValidator,
            Func<StableId, long, long, long, bool> restartCommit)
            : base(
                "room-runtime",
                generationExporter,
                fingerprintExporter,
                restartValidator,
                restartCommit)
        {
            this.roomExporter = roomExporter
                ?? throw new ArgumentNullException(nameof(roomExporter));
        }

        public StableId CurrentRoomStableId
        {
            get { return roomExporter(); }
        }
    }

    public sealed class EmptyActiveAbilityRunPort :
        IRunActiveAbilityLivePort
    {
        private readonly string subjectFingerprint;
        private long lifecycleGeneration;

        public EmptyActiveAbilityRunPort(
            long lifecycleGeneration,
            string subjectFingerprint)
        {
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (string.IsNullOrWhiteSpace(subjectFingerprint))
            {
                throw new ArgumentException(
                    "The ability placeholder requires a frozen subject fingerprint.",
                    nameof(subjectFingerprint));
            }
            this.lifecycleGeneration = lifecycleGeneration;
            this.subjectFingerprint = subjectFingerprint.Trim();
        }

        public string PortId
        {
            get { return "active-ability-runtime-placeholder"; }
        }

        public long LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }

        public string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprint.Hash(
                    PortId
                    + "|"
                    + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + subjectFingerprint
                    + "|active-cast-count=0");
            }
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            if (retiringLifecycleGeneration != lifecycleGeneration)
            {
                return retiringLifecycleGeneration < lifecycleGeneration
                    ? "ability-placeholder-stale-generation"
                    : "ability-placeholder-future-generation";
            }
            return replacementLifecycleGeneration
                == retiringLifecycleGeneration + 1L
                ? string.Empty
                : "ability-placeholder-generation-invalid";
        }

        public RunLivePortRestartResult Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return new RunLivePortRestartResult(
                    false,
                    rejection,
                    lifecycleGeneration,
                    SnapshotFingerprint);
            }
            lifecycleGeneration = replacementLifecycleGeneration;
            return new RunLivePortRestartResult(
                true,
                string.Empty,
                lifecycleGeneration,
                SnapshotFingerprint);
        }
    }
}
