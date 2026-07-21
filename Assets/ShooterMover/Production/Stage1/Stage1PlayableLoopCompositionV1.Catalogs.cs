using System;
using System.Collections.Generic;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using ShooterMover.Application.Weapons.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private RoomPresentationCatalog2D BuildRoomPresentationCatalog()
        {
            RoomPresentationCatalog2D catalog =
                ScriptableObject.CreateInstance<RoomPresentationCatalog2D>();
            runtimeAssets.Add(catalog);

            GameObject droid = MarkerPrefab(
                "Room Runtime Moving Droid Fact",
                true,
                false);
            GameObject turret = MarkerPrefab(
                "Room Runtime Turret Fact",
                true,
                false);
            GameObject cover = MarkerPrefab(
                "Room Runtime Cover Projection",
                false,
                false);
            GameObject door = MarkerPrefab(
                "Room Runtime Door Projection",
                false,
                true);
            runtimeAssets.Add(droid);
            runtimeAssets.Add(turret);
            runtimeAssets.Add(cover);
            runtimeAssets.Add(door);

            catalog.ConfigureForTests(
                Entry(
                    Level1AuthorableRoomDefinitionV1.MovingDroidPresentationStableId,
                    droid),
                Entry(
                    Level1AuthorableRoomDefinitionV1.TurretPresentationStableId,
                    turret),
                Entry(
                    Level1AuthorableRoomDefinitionV1.CoverPresentationStableId,
                    cover),
                Entry(
                    Level1AuthorableRoomDefinitionV1.DoorPresentationStableId,
                    door));
            return catalog;
        }

        private static RoomPresentationCatalogEntry2D Entry(
            StableId id,
            GameObject prefab)
        {
            var entry = new RoomPresentationCatalogEntry2D();
            entry.ConfigureForTests(id.ToString(), prefab);
            return entry;
        }

        private static GameObject MarkerPrefab(
            string name,
            bool enemy,
            bool door)
        {
            GameObject marker = new GameObject(name);
            marker.SetActive(false);
            if (enemy)
            {
                marker.AddComponent<Stage1RoomEnemyAuthorityProjection2D>();
            }
            if (door)
            {
                BoxCollider2D collider = marker.AddComponent<BoxCollider2D>();
                collider.enabled = false;
            }
            return marker;
        }

        private static EnemyExperienceRewardDefinitionV1 Reward(
            StableId definitionStableId,
            long experienceAmount)
        {
            return new EnemyExperienceRewardDefinitionV1(
                definitionStableId,
                new[]
                {
                    new EnemyExperienceRewardBandV1(
                        1,
                        100,
                        experienceAmount),
                });
        }

        private sealed class ProjectilePresentation
        {
            public ProjectilePresentation(Color color, Vector3 scale)
            {
                Color = color;
                Scale = scale;
            }

            public Color Color { get; }
            public Vector3 Scale { get; }
        }

        private sealed class EnemyBinding
        {
            public EnemyBinding(
                IEnemyActor2DAuthority authority,
                StableId definitionStableId,
                StableId roomInstanceStableId)
            {
                Authority = authority;
                DefinitionStableId = definitionStableId;
                RoomInstanceStableId = roomInstanceStableId;
            }

            public IEnemyActor2DAuthority Authority { get; }
            public StableId DefinitionStableId { get; }
            public StableId RoomInstanceStableId { get; }
        }

        private sealed class PendingEnemyReward
        {
            public PendingEnemyReward(
                StableId participantStableId,
                StableId enemyDefinitionStableId,
                EnemyDestroyedNotification destruction)
            {
                ParticipantStableId = participantStableId;
                EnemyDefinitionStableId = enemyDefinitionStableId;
                Destruction = destruction;
            }

            public StableId ParticipantStableId { get; }
            public StableId EnemyDefinitionStableId { get; }
            public EnemyDestroyedNotification Destruction { get; }
        }

        private sealed class ParticipantRunStats
        {
            public ParticipantRunStats(StableId participantStableId)
            {
                ParticipantStableId = participantStableId;
            }

            public StableId ParticipantStableId { get; }
            public int Kills { get; set; }
            public long Experience { get; set; }
        }

        private sealed class PlayerWeaponActorStateSourceV1 :
            IInventoryWeaponActorStateSource
        {
            private readonly Stage1VisibleSliceController owner;

            public PlayerWeaponActorStateSourceV1(
                Stage1VisibleSliceController owner)
            {
                this.owner = owner;
            }

            public bool TryResolveActorState(
                out WeaponActorInstanceId actorId,
                out LifecycleGeneration lifecycleGeneration)
            {
                actorId = new WeaponActorInstanceId(
                    StableId.Parse(PlayerActorStableIdText));
                lifecycleGeneration = new LifecycleGeneration(
                    owner.RestartGeneration);
                return owner.PlayerLiveAuthority != null
                    && owner.PlayerLiveAuthority.IsInitialized
                    && !owner.IsPlayerDead;
            }
        }

        private sealed class PlayerWeaponOwnershipResolverV1 :
            IWeaponActorOwnershipResolver
        {
            private readonly Stage1VisibleSliceController owner;

            public PlayerWeaponOwnershipResolverV1(
                Stage1VisibleSliceController owner)
            {
                this.owner = owner;
            }

            public bool TryResolveParticipant(
                WeaponActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out RunParticipantId participantId)
            {
                bool valid = actorId != null
                    && lifecycleGeneration != null
                    && actorId.Value == StableId.Parse(PlayerActorStableIdText)
                    && lifecycleGeneration.Value == owner.RestartGeneration;
                participantId = valid
                    ? new RunParticipantId(owner.PlayerRunParticipantId)
                    : null;
                return valid;
            }
        }

        private sealed class EmptyStrongboxMissionPortV1 :
            IMissionRunExistingAuthorityPortV1
        {
            private readonly PlayerHoldingsService holdings;

            public EmptyStrongboxMissionPortV1(PlayerHoldingsService holdings)
            {
                this.holdings = holdings;
                OpeningFingerprint = MissionRunCanonicalV1.Fingerprint(
                    "production-strongbox-openings-empty-v1");
            }

            public long OpeningSequence { get { return 0L; } }
            public string OpeningFingerprint { get; }

            public MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommandV1 command)
            {
                return MissionRunCollectionVerificationV1.Reject(
                    "production-physical-strongbox-port-not-connected");
            }

            public MissionRunStrongboxProjectionV1 ProjectStrongboxStates(
                EndMissionRunCommandV1 command,
                IReadOnlyList<MissionRunStrongboxCollectionV1> collected)
            {
                if (command == null || collected == null || collected.Count != 0)
                {
                    return MissionRunStrongboxProjectionV1.Reject(
                        "production-strongbox-projection-invalid");
                }

                PlayerHoldingsSnapshotV1 snapshot = holdings.ExportSnapshot();
                return MissionRunStrongboxProjectionV1.Accept(
                    Array.Empty<MissionRunStrongboxResultV1>(),
                    holdings.Sequence,
                    snapshot.Fingerprint,
                    OpeningSequence,
                    OpeningFingerprint);
            }
        }
    }
}
