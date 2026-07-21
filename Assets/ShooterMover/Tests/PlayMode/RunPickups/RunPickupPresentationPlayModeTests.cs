using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.RunPickups
{
    public sealed class RunPickupPresentationPlayModeTests
    {
        private static readonly StableId RunId = Id("run", "pickup-playmode");
        private static readonly StableId RoomId = Id("room", "pickup-playmode");
        private static readonly StableId OtherRoomId = Id("room", "other-room");
        private static readonly StableId PlayerActorId = Id("actor", "player");
        private static readonly StableId PlayerParticipantId = Id("participant", "player");

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
                    2d,
                    3d,
                    "playmode-source-position");
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class FakeRunSessionPort : IRunPickupRunSessionPortV1
        {
            private readonly Dictionary<StableId, string> replay =
                new Dictionary<StableId, string>();

            public StableId RunStableId { get { return RunId; } }
            public long LifecycleGeneration { get { return 1L; } }
            public long AuthoritativeTick { get { return 10L; } }
            public bool IsActive { get { return true; } }
            public StableId PlayerActorStableId { get { return PlayerActorId; } }
            public StableId PlayerParticipantStableId { get { return PlayerParticipantId; } }
            public int CollectionRecordCount { get; private set; }
            public int PermanentGrantCount { get; private set; }

            public RunPickupSessionRecordResultV1 RecordCollection(
                RunPickupCollectionFactV1 fact)
            {
                string existing;
                if (replay.TryGetValue(
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
                replay.Add(
                    fact.Command.CollectionOperationStableId,
                    fact.Fingerprint);
                CollectionRecordCount++;
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Accepted,
                    fact,
                    string.Empty);
            }
        }

        private sealed class SceneFixture
        {
            public FakeRunSessionPort Session;
            public RunLocalPickupAuthorityV1 Authority;
            public RunPickupAuthorityHost2D Host;
            public RunPickupPresentationRegistry2D Registry;
            public RunPickupPresenter2D Presenter;
            public GameObject Prefab;
            public RunPickupCollector2D Collector;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = 0; index < objects.Count; index++)
            {
                if (objects[index] != null)
                    UnityEngine.Object.Destroy(objects[index]);
            }
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedReward_AppearsAsOnePhysicalPickup()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKindV1.Money);
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("money", RewardGrantKindV1.Money, "credits", 5L)).Single();

            RunPickupPresentationSyncResultV1 sync = fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;

            Assert.That(sync.Succeeded, Is.True);
            Assert.That(sync.CreatedCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.TryGetView(pickup.PickupStableId, out view), Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(view.transform.position.y, Is.EqualTo(3f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerTrigger_CollectsOnceAndHidesOnlyAfterAcceptance()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKindV1.Money);
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("money", RewardGrantKindV1.Money, "credits", 5L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            Assert.That(fixture.Presenter.TryGetView(pickup.PickupStableId, out view), Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);

            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(view.LastCollectionResult.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Collected));
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Session.CollectionRecordCount, Is.EqualTo(1));
            yield return null;
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator RepeatedTriggerCallbacks_DoNotDuplicateCollection()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKindV1.Money);
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("money", RewardGrantKindV1.Money, "credits", 5L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out view);

            view.HandleTriggerForTests(fixture.Collector);
            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(fixture.Session.CollectionRecordCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingAndReturning_DoesNotRespawnCollectedPickup()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKindV1.Strongbox);
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("box", RewardGrantKindV1.Strongbox, "emerald", 1L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out view);
            view.HandleTriggerForTests(fixture.Collector);
            yield return null;

            fixture.Presenter.Synchronize(OtherRoomId);
            RunPickupPresentationSyncResultV1 returned =
                fixture.Presenter.Synchronize(RoomId);

            Assert.That(returned.AvailableCount, Is.EqualTo(0));
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PresenterReconstruction_RestoresUncollectedPickupWithSameIdentity()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKindV1.Scrap);
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("scrap", RewardGrantKindV1.Scrap, "scrap", 8L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D firstView;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out firstView);

            GameObject oldPresenterObject = fixture.Presenter.gameObject;
            UnityEngine.Object.Destroy(oldPresenterObject);
            yield return null;
            GameObject presenterObject = Track(new GameObject("RebuiltPickupPresenter"));
            fixture.Presenter = presenterObject.AddComponent<RunPickupPresenter2D>();
            fixture.Presenter.Configure(fixture.Host, fixture.Registry, presenterObject.transform);

            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D rebuiltView;

            Assert.That(fixture.Presenter.TryGetView(
                pickup.PickupStableId,
                out rebuiltView), Is.True);
            Assert.That(rebuiltView.PickupStableId, Is.EqualTo(pickup.PickupStableId));
            Assert.That(rebuiltView.Pickup.Fingerprint, Is.EqualTo(pickup.Fingerprint));
        }

        [UnityTest]
        public IEnumerator StrongboxMoneyAndScrap_UseOneGenericPresentationPath()
        {
            SceneFixture fixture = CreateSceneFixture(
                RewardGrantKindV1.Money,
                RewardGrantKindV1.Scrap,
                RewardGrantKindV1.Strongbox);
            IReadOnlyList<RunPickupSnapshotV1> pickups = Realize(
                fixture,
                Child("money", RewardGrantKindV1.Money, "credits", 5L, 0),
                Child("scrap", RewardGrantKindV1.Scrap, "scrap", 7L, 1),
                Child("box", RewardGrantKindV1.Strongbox, "emerald", 1L, 2));

            RunPickupPresentationSyncResultV1 sync = fixture.Presenter.Synchronize(RoomId);

            Assert.That(sync.CreatedCount, Is.EqualTo(3));
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(3));
            Assert.That(pickups.Select(item => item.Reward.Kind),
                Is.EquivalentTo(new[]
                {
                    RewardGrantKindV1.Money,
                    RewardGrantKindV1.Scrap,
                    RewardGrantKindV1.Strongbox
                }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingPresentation_IsRetryableAndDoesNotCollectOrDiscard()
        {
            SceneFixture fixture = CreateSceneFixture();
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("money", RewardGrantKindV1.Money, "credits", 5L)).Single();

            RunPickupPresentationSyncResultV1 failed = fixture.Presenter.Synchronize(RoomId);

            Assert.That(failed.FailedCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.ExportAvailablePickups().Single().PickupStableId,
                Is.EqualTo(pickup.PickupStableId));
            Assert.That(fixture.Session.CollectionRecordCount, Is.EqualTo(0));
            fixture.Registry.ConfigureForTests(new[]
            {
                Presentation(RewardGrantKindV1.Money, fixture.Prefab)
            });

            RunPickupPresentationSyncResultV1 retry = fixture.Presenter.Synchronize(RoomId);

            Assert.That(retry.CreatedCount, Is.EqualTo(1));
            Assert.That(retry.FailedCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PickupCollection_DoesNotGrantPermanentCharacterReward()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKindV1.Strongbox);
            RunPickupSnapshotV1 pickup = Realize(
                fixture,
                Child("box", RewardGrantKindV1.Strongbox, "emerald", 1L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out view);

            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(fixture.Session.PermanentGrantCount, Is.EqualTo(0));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
            yield return null;
        }

        private SceneFixture CreateSceneFixture(
            params RewardGrantKindV1[] presentationKinds)
        {
            var session = new FakeRunSessionPort();
            var authority = new RunLocalPickupAuthorityV1(
                session,
                new FixedPositionPort());

            GameObject hostObject = Track(new GameObject("PickupAuthorityHost"));
            RunPickupAuthorityHost2D host =
                hostObject.AddComponent<RunPickupAuthorityHost2D>();
            host.Configure(authority);

            GameObject registryObject = Track(new GameObject("PickupPresentationRegistry"));
            RunPickupPresentationRegistry2D registry =
                registryObject.AddComponent<RunPickupPresentationRegistry2D>();

            GameObject prefab = Track(new GameObject("GenericPickupPrefab"));
            prefab.SetActive(false);
            var entries = new List<RunPickupPresentationEntryV1>();
            for (int index = 0; index < presentationKinds.Length; index++)
                entries.Add(Presentation(presentationKinds[index], prefab));
            registry.ConfigureForTests(entries);

            GameObject presenterObject = Track(new GameObject("PickupPresenter"));
            RunPickupPresenter2D presenter =
                presenterObject.AddComponent<RunPickupPresenter2D>();
            presenter.Configure(host, registry, presenterObject.transform);

            GameObject collectorObject = Track(new GameObject("PlayerCollector"));
            RunPickupCollector2D collector =
                collectorObject.AddComponent<RunPickupCollector2D>();
            collector.ConfigureForTests(PlayerActorId.ToString(), PlayerParticipantId.ToString());

            return new SceneFixture
            {
                Session = session,
                Authority = authority,
                Host = host,
                Registry = registry,
                Presenter = presenter,
                Prefab = prefab,
                Collector = collector
            };
        }

        private static RunPickupPresentationEntryV1 Presentation(
            RewardGrantKindV1 kind,
            GameObject prefab)
        {
            var entry = new RunPickupPresentationEntryV1();
            entry.ConfigureForTests(
                kind,
                string.Empty,
                prefab,
                null,
                Vector3.one,
                0.75f,
                kind.ToString());
            return entry;
        }

        private static IReadOnlyList<RunPickupSnapshotV1> Realize(
            SceneFixture fixture,
            params RunPickupGeneratedRewardV1[] children)
        {
            return fixture.Authority.Realize(new RunPickupGeneratedBatchV1(
                Id("terminaldropoperation", "playmode-drop"),
                Id("terminal", "playmode-terminal"),
                Id("trigger", "playmode-trigger"),
                RunId,
                1L,
                Id("entity", "source"),
                Id("placement", "source"),
                1L,
                Id("definition", "source"),
                PlayerParticipantId,
                "playmode-batch-fingerprint",
                children)).Pickups;
        }

        private static RunPickupGeneratedRewardV1 Child(
            string instance,
            RewardGrantKindV1 kind,
            string content,
            long quantity,
            int ordinal = 0)
        {
            return new RunPickupGeneratedRewardV1(
                Id("terminaldropchild", instance),
                ordinal,
                Id("grant", "grant-" + instance),
                kind,
                Id("content", content),
                quantity,
                "playmode-child-fingerprint:" + instance);
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
