using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1CanonicalEnemyTerminalFactV1
    {
        public Stage1CanonicalEnemyTerminalFactV1(
            EnemyDeathFactV1 fact,
            Transform sourceTransform,
            StableId roomStableId,
            StableId placementStableId,
            EnemyDestroyedNotification notification)
        {
            Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            SourceTransform = sourceTransform
                ?? throw new ArgumentNullException(nameof(sourceTransform));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            PlacementStableId = placementStableId
                ?? throw new ArgumentNullException(nameof(placementStableId));
            Notification = notification
                ?? throw new ArgumentNullException(nameof(notification));
        }

        public EnemyDeathFactV1 Fact { get; }
        public Transform SourceTransform { get; }
        public StableId RoomStableId { get; }
        public StableId PlacementStableId { get; }
        public EnemyDestroyedNotification Notification { get; }
    }

    internal sealed class Stage1CanonicalPropTerminalSourceV1
    {
        public Stage1CanonicalPropTerminalSourceV1(
            PropDefinitionV1 definition,
            StableId dropProfileStableId,
            string definitionFingerprint,
            StableId roomStableId,
            StableId placementStableId)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            DropProfileStableId = dropProfileStableId
                ?? throw new ArgumentNullException(nameof(dropProfileStableId));
            if (string.IsNullOrWhiteSpace(definitionFingerprint))
            {
                throw new ArgumentException(
                    "A canonical prop-definition fingerprint is required.",
                    nameof(definitionFingerprint));
            }
            DefinitionFingerprint = definitionFingerprint.Trim();
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            PlacementStableId = placementStableId
                ?? throw new ArgumentNullException(nameof(placementStableId));
        }

        public PropDefinitionV1 Definition { get; }
        public StableId DropProfileStableId { get; }
        public string DefinitionFingerprint { get; }
        public StableId RoomStableId { get; }
        public StableId PlacementStableId { get; }
    }

    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private EnemyCatalogV1 terminalEnemyCatalog;

        internal bool TryResolveCanonicalTerminalDropContent(
            out EnemyCatalogV1 enemyCatalog,
            out PropCatalogV1 propCatalog,
            out IRewardProfileResolverV1 rewardProfiles,
            out string diagnostic)
        {
            enemyCatalog = null;
            propCatalog = null;
            rewardProfiles = null;
            diagnostic = string.Empty;
            try
            {
                if (terminalEnemyCatalog == null)
                    terminalEnemyCatalog = LoadProductionEnemyCatalog();
                enemyCatalog = terminalEnemyCatalog;
                propCatalog = Stage1TerminalDropContentV1.PropCatalog;
                rewardProfiles = Stage1TerminalDropContentV1.RewardProfiles;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-terminal-content-unavailable:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }

        internal bool TryResolveTerminalParticipant(
            StableId actorStableId,
            out StableId participantStableId)
        {
            participantStableId = null;
            if (actorStableId == null) return false;

            RunSessionAggregateV1 run;
            if (TryResolveSharedRunSession(out run) && run != null)
            {
                try
                {
                    RunPlayerRuntimeSnapshotV1 player =
                        run.RuntimePorts.Player.ExportSnapshot();
                    if (player != null
                        && player.ActorInstanceStableId == actorStableId)
                    {
                        participantStableId = player.ParticipantStableId;
                        return participantStableId != null;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            EnemyAttackPatternUnitySourceBindingV1 enemySource;
            if (enemyPatternSources != null
                && enemyPatternSources.TryGet(actorStableId, out enemySource)
                && enemySource != null)
            {
                participantStableId = enemySource.SourceRunParticipantStableId;
                return participantStableId != null;
            }
            return false;
        }

        internal bool TryExportCanonicalEnemyTerminalFacts(
            out IReadOnlyList<Stage1CanonicalEnemyTerminalFactV1> facts,
            out string diagnostic)
        {
            facts = Array.Empty<Stage1CanonicalEnemyTerminalFactV1>();
            diagnostic = string.Empty;
            RunSessionAggregateV1 run;
            if (!TryResolveSharedRunSession(out run) || run == null)
            {
                diagnostic = "stage1-enemy-terminal-shared-run-unavailable";
                return false;
            }
            if (enemyPatternSources == null)
            {
                diagnostic =
                    "stage1-enemy-terminal-live-source-registry-unavailable";
                return false;
            }

            EnemyCatalogV1 enemyCatalog;
            PropCatalogV1 ignoredProps;
            IRewardProfileResolverV1 ignoredProfiles;
            if (!TryResolveCanonicalTerminalDropContent(
                    out enemyCatalog,
                    out ignoredProps,
                    out ignoredProfiles,
                    out diagnostic))
            {
                return false;
            }

            var exported = new List<Stage1CanonicalEnemyTerminalFactV1>();
            for (int index = 0; index < pendingEnemyRewards.Count; index++)
            {
                PendingEnemyReward pending = pendingEnemyRewards[index];
                if (pending == null || pending.Destruction == null) continue;

                EnemyDestroyedNotification notification = pending.Destruction;
                EnemyAttackPatternUnitySourceBindingV1 liveSource;
                if (!enemyPatternSources.TryGet(
                        notification.TargetId,
                        out liveSource)
                    || liveSource == null
                    || liveSource.SourceEntityStableId != notification.TargetId
                    || !liveSource.HasCanonicalPlacementIdentity)
                {
                    diagnostic =
                        "stage1-enemy-terminal-exact-live-source-unavailable:"
                        + notification.TargetId;
                    return false;
                }

                EnemyDefinitionV1 definition = enemyCatalog.GetDefinition(
                    pending.EnemyDefinitionStableId);
                var identity = new EnemyRuntimeIdentityV1(
                    liveSource.SourceEntityStableId,
                    liveSource.SourceRunParticipantStableId,
                    run.RunStableId,
                    liveSource.RoomRuntimeInstanceStableId,
                    liveSource.RoomStableId,
                    liveSource.PlacementStableId);
                var fact = new EnemyDeathFactV1(
                    notification.EventId,
                    notification.EventId,
                    identity,
                    definition.DefinitionId,
                    liveSource.SourceLevel,
                    liveSource.LifecycleGeneration,
                    notification.SourceId,
                    pending.ParticipantStableId,
                    definition.ExperienceProfileId,
                    definition.DropProfileId,
                    notification.DeathCause);
                exported.Add(new Stage1CanonicalEnemyTerminalFactV1(
                    fact,
                    liveSource.SourceRoot.transform,
                    liveSource.RoomStableId,
                    liveSource.PlacementStableId,
                    notification));
            }

            exported.Sort((left, right) =>
                left.Fact.DeathEventStableId.CompareTo(
                    right.Fact.DeathEventStableId));
            facts = new ReadOnlyCollection<Stage1CanonicalEnemyTerminalFactV1>(
                exported);
            return true;
        }

        internal bool TryResolveCanonicalPropTerminalSource(
            DestructibleProp2D prop,
            out Stage1CanonicalPropTerminalSourceV1 source,
            out string diagnostic)
        {
            source = null;
            diagnostic = string.Empty;
            if (prop == null || prop.PropId == null)
            {
                diagnostic = "stage1-prop-terminal-runtime-incomplete";
                return false;
            }
            DestructiblePropTerminalProvenanceV1 provenance =
                prop.TerminalProvenance;
            if (provenance == null)
            {
                diagnostic = "stage1-prop-terminal-provenance-missing";
                return false;
            }
            if (!provenance.HasPlacementProvenance)
            {
                diagnostic =
                    "stage1-prop-terminal-placement-provenance-missing:" + prop.PropId;
                return false;
            }

            EnemyCatalogV1 ignoredEnemies;
            PropCatalogV1 propCatalog;
            IRewardProfileResolverV1 ignoredProfiles;
            if (!TryResolveCanonicalTerminalDropContent(
                    out ignoredEnemies,
                    out propCatalog,
                    out ignoredProfiles,
                    out diagnostic))
            {
                return false;
            }

            PropDefinitionV1 definition;
            StableId catalogDropProfile;
            if (!propCatalog.TryGet(
                    provenance.DefinitionStableId,
                    out definition)
                || definition == null
                || !Stage1TerminalDropContentV1.TryReadDropProfile(
                    definition,
                    out catalogDropProfile)
                || catalogDropProfile != provenance.DropProfileStableId
                || !string.Equals(
                    definition.Fingerprint,
                    provenance.DefinitionFingerprint,
                    StringComparison.Ordinal))
            {
                diagnostic =
                    "stage1-prop-terminal-canonical-provenance-mismatch:"
                    + provenance.DefinitionStableId;
                return false;
            }

            source = new Stage1CanonicalPropTerminalSourceV1(
                definition,
                catalogDropProfile,
                definition.Fingerprint,
                provenance.RoomStableId,
                provenance.PlacementStableId);
            return true;
        }
    }
}
