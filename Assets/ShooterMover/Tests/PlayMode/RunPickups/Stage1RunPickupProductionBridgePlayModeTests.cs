#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UnityAdapters.Production.Stage1;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.RunPickups
{
    public sealed class Stage1RunPickupProductionBridgePlayModeTests
    {
        private static readonly StableId RunId = Id("run", "stage1-pickup-bridge");
        private static readonly StableId RoomId = Id("room", "stage1-entry");
        private static readonly StableId SourceEntityId = Id("enemy-entity", "mobile-droid");
        private static readonly StableId SourcePlacementId = Id("placement", "mobile-droid");
        private static readonly StableId PlayerActorId = Id("actor", "player");
        private static readonly StableId PlayerParticipantId = Id("participant", "player");

        private readonly List<UnityEngine.Object> objects =
            new List<UnityEngine.Object>();

        private sealed class RunSessionPort : IRunPickupRunSessionPortV1
        {
            private readonly Dictionary<StableId, string> records =
                new Dictionary<StableId, string>();

            public StableId RunStableId { get { return RunId; } }
            public long LifecycleGeneration { get { return 1L; } }
            public long AuthoritativeTick { get { return 42L; } }
            public bool IsActive { get { return true; } }
            public StableId PlayerActorStableId { get { return PlayerActorId; } }
            public StableId PlayerParticipantStableId
            {
                get { return PlayerParticipantId; }
            }
            public int RecordCount { get; private set; }

            public bool TryReadContext(
                out RunPickupRunSessionContextV1 context,
                out string diagnostic)
            {
                context = new RunPickupRunSessionContextV1(
                    RunStableId,
                    LifecycleGeneration,
                    AuthoritativeTick,
                    IsActive,
                    PlayerActorStableId,
                    PlayerParticipantStableId,
                    checked(records.Count + 1L));
                diagnostic = string.Empty;
                return true;
            }

            public RunPickupSessionRecordResultV1 RecordCollection(
                RunPickupCollectionFactV1 fact)
            {
                string existing;
                if (records.TryGetValue(
                    fact.Command.CollectionOperationStableId,
                    out existing))
                {
                    return new RunPickupSessionRecordResultV1(
                        string.Equals(existing, fact.Fingerprint, StringComparison.Ordinal)
                            ? RunPickupSessionRecordStatusV1.ExactReplay
                            : RunPickupSessionRecordStatusV1.ConflictingDuplicate,
                        fact,
                        string.Empty);
                }

                records.Add(
                    fact.Command.CollectionOperationStableId,
                    fact.Fingerprint);
                RecordCount++;
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Accepted,
                    fact,
                    string.Empty);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                    UnityEngine.Object.Destroy(objects[index]);
            }
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExactAdmission_UsesTerminalTransform_AndPhysicsCollectsOnce()
        {
            var session = new RunSessionPort();
            GameObject runtime = Track(new GameObject("Stage1 Pickup Production Bridge"));
            RunPickupSourcePositionRegistry2D positions =
                runtime.AddComponent<RunPickupSourcePositionRegistry2D>();
            var authority = new RunLocalPickupAuthorityV1(session, positions);
            var pendingConsumer = new PendingTerminalDropPickupConsumerV1(authority);

            RunPickupAuthorityHost2D host =
                runtime.AddComponent<RunPickupAuthorityHost2D>();
            host.Configure(authority);
            RunPickupPresentationRegistry2D registry =
                runtime.AddComponent<RunPickupPresentationRegistry2D>();
            GameObject prefab = Track(new GameObject("Money Pickup Prefab"));
            prefab.SetActive(false);
            var presentation = new RunPickupPresentationEntryV1();
            presentation.Configure(
                RewardGrantKindV1.Money,
                null,
                prefab,
                null,
                Vector3.one,
                0.75f,
                "Money");
            registry.Configure(new[] { presentation });
            RunPickupPresenter2D presenter =
                runtime.AddComponent<RunPickupPresenter2D>();
            presenter.Configure(host, registry, runtime.transform);

            var bridge = new Stage1PendingAdmissionPickupBridgeV1(
                positions,
                pendingConsumer,
                presenter);
            GameObject terminalSource = Track(new GameObject("Destroyed Mobile Droid"));
            terminalSource.transform.position = new Vector3(8.25f, -3.5f, 0f);
            bridge.RegisterSource(
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                RoomId,
                terminalSource.transform);

            PendingTerminalDropAdmissionResultV1 admission =
                new PendingTerminalDropAdmissionAuthorityV1().Admit(GeneratedDrop());
            Assert.That(admission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));

            bridge.Consume(admission);

            Assert.That(bridge.LastDiagnostic, Is.Empty);
            RunPickupSnapshotV1 pickup = authority.ExportAvailablePickups().Single();
            Assert.That(pickup.Reward.RewardInstanceStableId,
                Is.EqualTo(Id("terminaldropchild", "exact-money-child")));
            Assert.That(pickup.WorldSpawnContext.PositionX, Is.EqualTo(8.25d));
            Assert.That(pickup.WorldSpawnContext.PositionY, Is.EqualTo(-3.5d));
            RunRewardPickup2D view;
            Assert.That(presenter.TryGetView(pickup.PickupStableId, out view), Is.True);
            Assert.That(view.transform.position.x, Is.EqualTo(8.25f).Within(0.001f));
            Assert.That(view.transform.position.y, Is.EqualTo(-3.5f).Within(0.001f));

            GameObject player = Track(new GameObject("Production Player Collector"));
            player.transform.position = view.transform.position;
            RunPickupCollector2D collector = player.AddComponent<RunPickupCollector2D>();
            collector.Configure(PlayerActorId, PlayerParticipantId);
            CircleCollider2D playerCollider = player.AddComponent<CircleCollider2D>();
            playerCollider.radius = 0.5f;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(session.RecordCount, Is.EqualTo(1));
            Assert.That(authority.CollectedPickupCount, Is.EqualTo(1));
            Assert.That(view.IsRetired, Is.True);
            Assert.That(view.LastCollectionResult.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Collected));
        }

        [Test]
        public void Stage1Bootstrap_ContainsTheProductionAuthorityChain()
        {
            Type type = typeof(Stage1RunPickupBootstrap2D);
            const System.Reflection.BindingFlags fields =
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;

            Assert.That(type.GetField("run", fields).FieldType,
                Is.EqualTo(typeof(ShooterMover.Application.Runs.Session.RunSessionAggregateV1)));
            Assert.That(type.GetField("terminalDrops", fields).FieldType,
                Is.EqualTo(typeof(TerminalDropBindingCompositionV1)));
            Assert.That(type.GetField("sourcePositions", fields).FieldType,
                Is.EqualTo(typeof(RunPickupSourcePositionRegistry2D)));
            Assert.That(type.GetField("pickups", fields).FieldType,
                Is.EqualTo(typeof(RunPickupLiveCompositionV1)));
            Assert.That(type.GetField("presenter", fields).FieldType,
                Is.EqualTo(typeof(RunPickupPresenter2D)));
            Assert.That(type.GetMethod("EmitEnemyDeath", fields), Is.Not.Null);
        }

        private static GeneratedTerminalDropResultV1 GeneratedDrop()
        {
            StableId operationId = Id("terminaldropoperation", "exact-admitted-drop");
            StableId profileId = Id("drop-profile", "stage1-money");
            var source = new TerminalDropSourceFactV1(
                TerminalDropFactKindIdsV1.EnemyDeath,
                Id("terminal", "mobile-droid-death"),
                Id("trigger", "mobile-droid-final-hit"),
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                1L,
                Id("enemy", "mobile-blaster-droid"),
                PlayerParticipantId,
                PlayerActorId,
                Id("damage", "kinetic"),
                profileId,
                "stage1-source-context-fingerprint",
                "stage1-definition-fingerprint",
                "stage1-upstream-fingerprint");
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                RunId,
                SourceEntityId,
                operationId,
                Id("commitment", "stage1-money"),
                profileId,
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var child = new GeneratedTerminalDropRewardV1(
                Id("terminaldropchild", "exact-money-child"),
                0,
                Id("grant", "stage1-money"),
                RewardGrantKindV1.Money,
                Id("currency", "credits"),
                5L);
            return new GeneratedTerminalDropResultV1(
                TerminalDropBindingStatusV1.Accepted,
                TerminalDropRejectionCodeV1.None,
                source,
                profileId,
                operation,
                123UL,
                null,
                new[] { child },
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                string.Empty);
        }

        private GameObject Track(GameObject value)
        {
            objects.Add(value);
            return value;
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
