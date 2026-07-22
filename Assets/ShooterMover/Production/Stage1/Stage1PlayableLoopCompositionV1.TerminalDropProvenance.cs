using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
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
            string definitionFingerprint)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            DropProfileStableId = dropProfileStableId
                ?? throw new ArgumentNullException(nameof(dropProfileStableId));
            if (string.IsNullOrWhiteSpace(definitionFingerprint))
                throw new ArgumentException(
                    "A canonical prop-definition fingerprint is required.",
                    nameof(definitionFingerprint));
            DefinitionFingerprint = definitionFingerprint.Trim();
        }

        public PropDefinitionV1 Definition { get; }
        public StableId DropProfileStableId { get; }
        public string DefinitionFingerprint { get; }
    }

    internal static class Stage1ProductionTerminalDropContentV1
    {
        internal static readonly StableId CrateDefinitionStableId =
            StableId.Parse("prop.stage1-crate");
        internal static readonly StableId ExplosiveDefinitionStableId =
            StableId.Parse("prop.stage1-explosive");
        internal static readonly StableId CrateDropProfileStableId =
            StableId.Parse("drop.prop-stage1-ordinary");
        internal static readonly StableId ExplosiveDropProfileStableId =
            StableId.Parse("drop.prop-stage1-explosive");

        public static PropCatalogV1 CreatePropCatalog()
        {
            return new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[]
                {
                    new PropDefinitionV1(
                        CrateDefinitionStableId,
                        StableId.Parse("presentation.prop.stage1-crate"),
                        new[]
                        {
                            PropCapabilitiesV1.HealthBased(
                                Stage1DestructiblePropIntegration.CrateMaximumHealth),
                            PropCapabilitiesV1.DamageBehavior(
                                PropDamageAlignmentV1.Neutral,
                                StableId.Parse("damage-policy.prop-stage1")),
                            PropCapabilitiesV1.DropOnDestroy(
                                CrateDropProfileStableId),
                        }),
                    new PropDefinitionV1(
                        ExplosiveDefinitionStableId,
                        StableId.Parse("presentation.prop.stage1-explosive"),
                        new[]
                        {
                            PropCapabilitiesV1.HealthBased(
                                Stage1DestructiblePropIntegration.ExplosiveMaximumHealth),
                            PropCapabilitiesV1.DamageBehavior(
                                PropDamageAlignmentV1.Neutral,
                                StableId.Parse("damage-policy.prop-stage1")),
                            PropCapabilitiesV1.ExplodeOnDestroy(
                                StableId.Parse("explosion-profile.prop-stage1")),
                            PropCapabilitiesV1.DropOnDestroy(
                                ExplosiveDropProfileStableId),
                        }),
                });
        }

        public static IRewardProfileResolverV1 CreateRewardProfiles()
        {
            RewardProfileV1 enemyCommon = RewardProfileV1.Create(
                StableId.Parse("drop.enemy-common"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-enemy-common-money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.credits"),
                        5L),
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-enemy-common-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 enemyTurret = RewardProfileV1.Create(
                StableId.Parse("drop.enemy-turret"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-enemy-turret-money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.credits"),
                        15L),
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-enemy-turret-box"),
                        RewardGrantKindV1.Strongbox,
                        StableId.Parse("strongbox.tier-common"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 crate = RewardProfileV1.Create(
                CrateDropProfileStableId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-prop-ordinary-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        2L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 explosive = RewardProfileV1.Create(
                ExplosiveDropProfileStableId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-prop-explosive-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        4L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return new RewardProfileCatalogResolverV1(new[]
            {
                enemyCommon,
                enemyTurret,
                crate,
                explosive,
                RewardProfileV1.CreateExplicitNoDrop(
                    StableId.Parse("drop.enemy-none")),
            });
        }

        public static bool TryReadDropProfile(
            PropDefinitionV1 definition,
            out StableId profileStableId)
        {
            profileStableId = null;
            PropCapabilityV1 capability;
            string value;
            return definition != null
                && definition.TryGet(
                    PropCapabilityIdsV1.DropOnDestroy,
                    out capability)
                && capability != null
                && capability.TryGet("profile-id", out value)
                && StableId.TryParse(value, out profileStableId)
                && profileStableId != null;
        }
    }

    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private EnemyCatalogV1 terminalEnemyCatalog;
        private PropCatalogV1 terminalPropCatalog;
        private IRewardProfileResolverV1 terminalRewardProfiles;

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
                if (terminalPropCatalog == null)
                    terminalPropCatalog =
                        Stage1ProductionTerminalDropContentV1.CreatePropCatalog();
                if (terminalRewardProfiles == null)
                    terminalRewardProfiles =
                        Stage1ProductionTerminalDropContentV1.CreateRewardProfiles();
                enemyCatalog = terminalEnemyCatalog;
                propCatalog = terminalPropCatalog;
                rewardProfiles = terminalRewardProfiles;
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
                diagnostic = "stage1-enemy-terminal-live-source-registry-unavailable";
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
                    || liveSource.SourceEntityStableId != notification.TargetId)
                {
                    diagnostic =
                        "stage1-enemy-terminal-exact-live-source-unavailable:"
                        + notification.TargetId;
                    return false;
                }

                StableId roomStableId;
                StableId placementStableId;
                if (!TryResolveEnemyPlacement(
                        notification.TargetId,
                        pending.EnemyDefinitionStableId,
                        out roomStableId,
                        out placementStableId,
                        out diagnostic))
                {
                    return false;
                }

                EnemyDefinitionV1 definition = enemyCatalog.GetDefinition(
                    pending.EnemyDefinitionStableId);
                var identity = new EnemyRuntimeIdentityV1(
                    liveSource.SourceEntityStableId,
                    liveSource.SourceRunParticipantStableId,
                    run.RunStableId,
                    RoomRuntimeStableId,
                    roomStableId,
                    placementStableId);
                var fact = new EnemyDeathFactV1(
                    notification.EventId,
                    notification.EventId,
                    identity,
                    definition.DefinitionId,
                    1,
                    liveSource.LifecycleGeneration,
                    notification.SourceId,
                    pending.ParticipantStableId,
                    definition.ExperienceProfileId,
                    definition.DropProfileId,
                    notification.DeathCause);
                exported.Add(new Stage1CanonicalEnemyTerminalFactV1(
                    fact,
                    liveSource.SourceRoot.transform,
                    roomStableId,
                    placementStableId,
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
            if (prop == null || prop.PropId == null || prop.BlockingCollider == null)
            {
                diagnostic = "stage1-prop-terminal-runtime-incomplete";
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

            DestructiblePropAuthoring2D[] authored = controller
                .GetComponentsInChildren<DestructiblePropAuthoring2D>(true);
            DestructiblePropAuthoring2D match = null;
            float bestDistance = float.PositiveInfinity;
            Vector2 runtimePosition = prop.BlockingCollider.bounds.center;
            for (int index = 0; index < authored.Length; index++)
            {
                DestructiblePropAuthoring2D candidate = authored[index];
                if (candidate == null) continue;
                Vector2 expected = (Vector2)candidate.transform.position
                    + candidate.ColliderOffset;
                float distance = (expected - runtimePosition).sqrMagnitude;
                if (distance > 0.000001f) continue;
                if (match != null && Math.Abs(distance - bestDistance) < 0.0000001f)
                {
                    diagnostic = "stage1-prop-terminal-authoring-ambiguous";
                    return false;
                }
                match = candidate;
                bestDistance = distance;
            }
            if (match == null)
            {
                diagnostic = "stage1-prop-terminal-authoring-missing";
                return false;
            }

            StableId definitionStableId;
            if (match.name.StartsWith("Crate_", StringComparison.Ordinal))
            {
                definitionStableId =
                    Stage1ProductionTerminalDropContentV1.CrateDefinitionStableId;
            }
            else if (match.name.StartsWith("Explosive_", StringComparison.Ordinal))
            {
                definitionStableId =
                    Stage1ProductionTerminalDropContentV1.ExplosiveDefinitionStableId;
            }
            else
            {
                diagnostic =
                    "stage1-prop-terminal-legacy-definition-binding-missing";
                return false;
            }

            PropDefinitionV1 definition;
            StableId dropProfileStableId;
            if (!propCatalog.TryGet(definitionStableId, out definition)
                || definition == null
                || !Stage1ProductionTerminalDropContentV1.TryReadDropProfile(
                    definition,
                    out dropProfileStableId))
            {
                diagnostic =
                    "stage1-prop-terminal-canonical-definition-invalid:"
                    + definitionStableId;
                return false;
            }
            source = new Stage1CanonicalPropTerminalSourceV1(
                definition,
                dropProfileStableId,
                definition.Fingerprint);
            return true;
        }

        private static bool TryResolveEnemyPlacement(
            StableId actorStableId,
            StableId definitionStableId,
            out StableId roomStableId,
            out StableId placementStableId,
            out string diagnostic)
        {
            roomStableId = null;
            placementStableId = null;
            diagnostic = string.Empty;
            StableId resolvedPlacement = null;
            foreach (KeyValuePair<StableId, IEnemyActor2DAuthority> pair in
                projectedRoomEnemies)
            {
                EnemyActorState state;
                if (pair.Value != null
                    && pair.Value.TryReadState(out state)
                    && state != null
                    && state.ActorId == actorStableId)
                {
                    if (resolvedPlacement != null && resolvedPlacement != pair.Key)
                    {
                        diagnostic =
                            "stage1-enemy-terminal-placement-identity-ambiguous";
                        return false;
                    }
                    resolvedPlacement = pair.Key;
                }
            }
            if (resolvedPlacement == null)
            {
                diagnostic = "stage1-enemy-terminal-placement-unavailable:"
                    + actorStableId;
                return false;
            }

            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            for (int roomIndex = 0; roomIndex < graph.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinitionV1 room = graph.Rooms[roomIndex];
                RoomPlacedEntityDefinitionV1 placement;
                if (!room.TryGetPlacement(resolvedPlacement, out placement)
                    || placement == null)
                {
                    continue;
                }
                if (placement.DefinitionStableId != definitionStableId
                    || placement.PlacementKind != RoomLivePlacementKindV1.Enemy)
                {
                    diagnostic =
                        "stage1-enemy-terminal-placement-definition-mismatch";
                    return false;
                }
                roomStableId = room.RoomStableId;
                placementStableId = placement.InstanceStableId;
                return true;
            }

            diagnostic = "stage1-enemy-terminal-room-unavailable:"
                + resolvedPlacement;
            return false;
        }
    }
}
