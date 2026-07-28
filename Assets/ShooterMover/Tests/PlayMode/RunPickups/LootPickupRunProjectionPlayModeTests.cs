using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;
using ShooterMover.UI.StrongboxOpening;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.RunPickups
{
    public sealed class LootPickupRunProjectionPlayModeTests
    {
        private static readonly StableId RunId =
            StableId.Parse("run.loot-projection-test");
        private static readonly StableId RoomId =
            StableId.Parse("room.loot-projection-test");
        private static readonly StableId PlayerActorId =
            StableId.Parse("actor.loot-projection-player");
        private static readonly StableId PlayerParticipantId =
            StableId.Parse("participant.loot-projection-player");

        private readonly List<GameObject> objects = new List<GameObject>();

        private sealed class FixedPositionPort : IRunPickupSourcePositionPortV1
        {
            public bool TryResolve(
                StableId runStableId,
                long runLifecycleGeneration,
                StableId sourceEntityStableId,
                StableId sourcePlacementStableId,
                out RunPickupWorldSpawnContextV1 worldSpawnContext,
                out string diagnostic)
            {
                worldSpawnContext = new RunPickupWorldSpawnContextV1(
                    RoomId,
                    3d,
                    1d,
                    "loot-projection-test-position");
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class SessionPort : IRunPickupRunSessionPortV1
        {
            public StableId RunStableId { get { return RunId; } }
            public long LifecycleGeneration { get { return 1L; } }
            public long AuthoritativeTick { get { return 10L; } }
            public bool IsActive { get { return true; } }
            public StableId PlayerActorStableId { get { return PlayerActorId; } }
            public StableId PlayerParticipantStableId
            {
                get { return PlayerParticipantId; }
            }
            public int CollectionCount { get; private set; }

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
                    1L);
                diagnostic = string.Empty;
                return true;
            }

            public RunPickupSessionRecordResultV1 RecordCollection(
                RunPickupCollectionFactV1 fact)
            {
                CollectionCount++;
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Accepted,
                    fact,
                    string.Empty);
            }
        }

        private sealed class Fixture
        {
            public SessionPort Session;
            public RunLocalPickupAuthorityV1 Authority;
            public RunPickupPresenter2D Presenter;
            public RunPickupCollector2D Collector;
            public RunPickupSnapshotV1 Pickup;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = 0; index < objects.Count; index++)
            {
                if (objects[index] != null)
                {
                    UnityEngine.Object.Destroy(objects[index]);
                }
            }
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MoneyPickup_BindsRichVisualAndRetiresAfterCanonicalAcceptance()
        {
            Fixture fixture = CreateFixture(true, false);
            RunRewardPickup2D view;
            Assert.That(
                fixture.Presenter.TryGetView(
                    fixture.Pickup.PickupStableId,
                    out view),
                Is.True);

            LootPickupRunProjection2D bridge =
                view.GetComponent<LootPickupRunProjection2D>();
            LootPickupVisual2D visual =
                view.GetComponent<LootPickupVisual2D>();
            SpriteRenderer legacy = view.GetComponent<SpriteRenderer>();

            Assert.That(bridge, Is.Not.Null);
            Assert.That(bridge.IsBound, Is.True);
            Assert.That(visual, Is.Not.Null);
            Assert.That(
                visual.Projection.PickupStableId,
                Is.EqualTo(fixture.Pickup.PickupStableId));
            Assert.That(visual.Projection.Quantity, Is.EqualTo(25L));
            Assert.That(legacy.enabled, Is.False);

            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(view.LastCollectionResult.IsCollected, Is.True);
            Assert.That(fixture.Session.CollectionCount, Is.EqualTo(1));
            Assert.That(visual.IsPlayingAcceptedCollectionFeedback, Is.True);
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(1));

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(view == null, Is.True);
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator FeedbackFailure_DoesNotRewriteAcceptedCollectionAsRejected()
        {
            Fixture fixture = CreateFixture(false, true);
            RunRewardPickup2D view;
            Assert.That(
                fixture.Presenter.TryGetView(
                    fixture.Pickup.PickupStableId,
                    out view),
                Is.True);

            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(view.LastCollectionResult.IsCollected, Is.True);
            Assert.That(fixture.Session.CollectionCount, Is.EqualTo(1));
            Assert.That(
                view.PresentationDiagnostic,
                Does.Contain("test-feedback-failure"));
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(0));

            yield return null;

            Assert.That(view == null, Is.True);
        }

        private Fixture CreateFixture(
            bool addRichProjection,
            bool addThrowingFeedback)
        {
            var session = new SessionPort();
            var authority = new RunLocalPickupAuthorityV1(
                session,
                new FixedPositionPort());

            GameObject hostObject =
                Track(new GameObject("LootProjectionAuthorityHost"));
            RunPickupAuthorityHost2D host =
                hostObject.AddComponent<RunPickupAuthorityHost2D>();
            host.Configure(authority);

            GameObject prefab = Track(new GameObject("LootProjectionPrefab"));
            prefab.AddComponent<SpriteRenderer>();
            if (addRichProjection)
            {
                prefab.AddComponent<LootPickupRunProjection2D>();
            }
            if (addThrowingFeedback)
            {
                prefab.AddComponent<ThrowingRunPickupAcceptedFeedback2D>();
            }
            prefab.SetActive(false);

            GameObject registryObject =
                Track(new GameObject("LootProjectionRegistry"));
            RunPickupPresentationRegistry2D registry =
                registryObject.AddComponent<RunPickupPresentationRegistry2D>();
            var entry = new RunPickupPresentationEntryV1();
            entry.ConfigureForTests(
                RewardGrantKindV1.Money,
                string.Empty,
                prefab,
                null,
                Vector3.one,
                0.75f,
                "money");
            registry.ConfigureForTests(new[] { entry });

            GameObject presenterObject =
                Track(new GameObject("LootProjectionPresenter"));
            RunPickupPresenter2D presenter =
                presenterObject.AddComponent<RunPickupPresenter2D>();
            presenter.Configure(host, registry, presenterObject.transform);

            GameObject collectorObject =
                Track(new GameObject("LootProjectionCollector"));
            collectorObject.transform.position = Vector3.zero;
            RunPickupCollector2D collector =
                collectorObject.AddComponent<RunPickupCollector2D>();
            collector.ConfigureForTests(
                PlayerActorId.ToString(),
                PlayerParticipantId.ToString());

            RunPickupSnapshotV1 pickup = authority.Realize(
                new RunPickupGeneratedBatchV1(
                    StableId.Parse("terminaldropoperation.loot-projection"),
                    StableId.Parse("terminal.loot-projection"),
                    StableId.Parse("trigger.loot-projection"),
                    RunId,
                    1L,
                    StableId.Parse("entity.loot-projection-source"),
                    StableId.Parse("placement.loot-projection-source"),
                    1L,
                    StableId.Parse("definition.loot-projection-source"),
                    PlayerParticipantId,
                    "loot-projection-batch-fingerprint",
                    new[]
                    {
                        new RunPickupGeneratedRewardV1(
                            StableId.Parse(
                                "terminaldropchild.loot-projection-money"),
                            0,
                            StableId.Parse("grant.loot-projection-money"),
                            RewardGrantKindV1.Money,
                            StableId.Parse("currency.money"),
                            25L,
                            "loot-projection-child-fingerprint"),
                    }))
                .Pickups
                .Single();

            RunPickupPresentationSyncResultV1 sync =
                presenter.Synchronize(RoomId);
            Assert.That(sync.Succeeded, Is.True, sync.Diagnostic);

            return new Fixture
            {
                Session = session,
                Authority = authority,
                Presenter = presenter,
                Collector = collector,
                Pickup = pickup,
            };
        }

        private GameObject Track(GameObject value)
        {
            objects.Add(value);
            return value;
        }
    }
}
