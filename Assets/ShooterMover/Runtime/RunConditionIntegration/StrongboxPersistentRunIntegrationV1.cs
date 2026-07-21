using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;

namespace ShooterMover.RunConditionIntegration
{
    public interface IRunMissionStrongboxSnapshotSourceV1
    {
        PlayerHoldingsSnapshotV1 ExportCollectedStrongboxHoldings();
        StrongboxOpeningSnapshotV1 ExportCollectedStrongboxRegistrations();
    }

    internal static class RunMissionStrongboxSnapshotSourceResolverV1
    {
        public static bool TryResolve(
            IRunMissionResultPortV1 port,
            ProductionCharacterRuntimeGraphV1 graph,
            MissionResultPayloadV1 result,
            out PlayerHoldingsSnapshotV1 holdings,
            out StrongboxOpeningSnapshotV1 strongboxes,
            out string rejectionCode)
        {
            holdings = null;
            strongboxes = null;
            rejectionCode = string.Empty;

            var source = port as IRunMissionStrongboxSnapshotSourceV1;
            if (source != null)
            {
                holdings = source.ExportCollectedStrongboxHoldings();
                strongboxes = source.ExportCollectedStrongboxRegistrations();
            }
            else if (port is ExistingMissionResultRunPortV1)
            {
                // Compatibility seam for the merged RUN-SESSION adapter. This does not
                // create authority state; it exposes the two immutable snapshots already
                // captured by that adapter until the port can implement the typed source
                // interface directly. Reflection failure is closed and reported below.
                try
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    FieldInfo holdingsField = typeof(ExistingMissionResultRunPortV1)
                        .GetField("holdings", flags);
                    FieldInfo exporterField = typeof(ExistingMissionResultRunPortV1)
                        .GetField("openingExporter", flags);
                    var authority = holdingsField == null
                        ? null
                        : holdingsField.GetValue(port) as IPlayerHoldingsAuthorityV1;
                    var exporter = exporterField == null
                        ? null
                        : exporterField.GetValue(port)
                            as Func<StrongboxOpeningSnapshotV1>;
                    holdings = authority == null ? null : authority.ExportSnapshot();
                    strongboxes = exporter == null ? null : exporter();
                }
                catch (Exception)
                {
                    holdings = null;
                    strongboxes = null;
                }
            }

            if ((holdings == null || strongboxes == null) && graph != null)
            {
                PlayerHoldingsSnapshotV1 graphHoldings =
                    graph.LoadoutRuntime.Holdings.ExportSnapshot();
                StrongboxOpeningSnapshotV1 graphStrongboxes =
                    graph.StrongboxAuthority.ExportSnapshot();
                if (graphHoldings != null && graphStrongboxes != null
                    && string.Equals(
                        graphHoldings.Fingerprint,
                        result.HoldingsFingerprint,
                        StringComparison.Ordinal)
                    && string.Equals(
                        graphStrongboxes.Fingerprint,
                        result.StrongboxOpeningFingerprint,
                        StringComparison.Ordinal))
                {
                    holdings = graphHoldings;
                    strongboxes = graphStrongboxes;
                }
            }

            if (holdings == null || strongboxes == null)
            {
                rejectionCode = "box-transfer-source-snapshot-unavailable";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Decorates the existing mission-result port. The immutable result is accepted by
    /// Run Session only after the complete selected-character transfer is durably saved.
    /// </summary>

    public sealed class PersistentMissionResultRunPortV1 : IRunMissionResultPortV1
    {
        private readonly IRunMissionResultPortV1 inner;
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly FrozenCharacterRunInputsV1 frozenInputs;
        private readonly long expectedAccountRevision;
        private readonly IRunPlayerRuntimePortV1 player;
        private readonly StrongboxMissionResultApplicationCoordinatorV1 coordinator;

        public PersistentMissionResultRunPortV1(
            IRunMissionResultPortV1 inner,
            CharacterCompositionCoordinatorV1 composition,
            FrozenCharacterRunInputsV1 frozenInputs,
            long expectedAccountRevision,
            IRunPlayerRuntimePortV1 player)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.frozenInputs = frozenInputs
                ?? throw new ArgumentNullException(nameof(frozenInputs));
            if (expectedAccountRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedAccountRevision));
            }
            this.expectedAccountRevision = expectedAccountRevision;
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            coordinator = new StrongboxMissionResultApplicationCoordinatorV1(composition);
        }

        public long Sequence { get { return inner.Sequence; } }

        public bool TryGetRun(StableId runStableId, out MissionRunPayloadV1 runPayload)
        {
            return inner.TryGetRun(runStableId, out runPayload);
        }

        public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            RunStrongboxCollectionRequestV1 request,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1 routePayload)
        {
            return inner.RecordCollectedStrongbox(request, routePayload);
        }

        public MissionRunAuthorityResultV1 EndRun(
            EndRunSessionCommandV1 command,
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1 routePayload)
        {
            MissionRunAuthorityResultV1 result = inner.EndRun(command, routePayload);
            if (result == null || !result.Succeeded || result.ResultPayload == null)
            {
                return result;
            }

            var graph = composition.ActiveRuntime as ProductionCharacterRuntimeGraphV1;
            PlayerHoldingsSnapshotV1 sourceHoldings;
            StrongboxOpeningSnapshotV1 sourceStrongboxes;
            string sourceError;
            if (!RunMissionStrongboxSnapshotSourceResolverV1.TryResolve(
                inner,
                graph,
                result.ResultPayload,
                out sourceHoldings,
                out sourceStrongboxes,
                out sourceError))
            {
                return ExternalReject(result, command, sourceError);
            }

            StableId applicationOperation = StrongboxCanonicalV1.DeriveId(
                "boxresultapply",
                command.OperationStableId.ToString(),
                command.RunStableId.ToString(),
                result.ResultPayload.Fingerprint,
                frozenInputs.Character.CharacterInstanceStableId.ToString(),
                player.LifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            var application = new StrongboxMissionResultApplicationCommandV1(
                applicationOperation,
                command.RunStableId,
                player.LifecycleGeneration,
                result.ResultPayload,
                frozenInputs.Character.CharacterInstanceStableId,
                frozenInputs.Character.Revision,
                frozenInputs.Character.Fingerprint,
                expectedAccountRevision,
                sourceHoldings,
                sourceStrongboxes);
            StrongboxMissionResultApplicationResultV1 applied =
                coordinator.Apply(application);
            return applied != null && applied.Succeeded
                ? result
                : ExternalReject(
                    result,
                    command,
                    applied == null
                        ? "box-transfer-result-null"
                        : applied.RejectionCode);
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
                    account.Revision,
                    ports.Player));
        }
    }
}
