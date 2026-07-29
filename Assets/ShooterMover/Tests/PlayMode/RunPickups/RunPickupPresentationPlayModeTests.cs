using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
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

        private sealed class FixedPositionPort : IRunPickupSourcePositionPort
        {
            public bool TryResolve(
                StableId runStableId,
                long runLifecycleGeneration,
                StableId sourceEntityStableId,
                StableId sourcePlacementStableId,
                out RunPickupWorldSpawnContext worldSpawnContext,
                out string diagnostic)
            {
                worldSpawnContext = new RunPickupWorldSpawnContext(
                    RoomId,
                    2d,
                    3d,
                    "playmode-source-position");
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class FakeRunSessionPort : IRunPickupRunSessionPort
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

            public bool TryReadContext(
                out RunPickupRunSessionContext context,
                out string diagnostic)
            {
                context = new RunPickupRunSessionContext(
                    RunStableId,
                    LifecycleGeneration,
                    AuthoritativeTick,
                    IsActive,
                    PlayerActorStableId,
                    PlayerParticipantStableId,
                    checked(replay.Count + 1L));
                diagnostic = string.Empty;
                return true;
            }

            public RunPickupSessionRecordResult RecordCollection(
                RunPickupCollectionFact fact)
            {
                string existing;
                if (replay.TryGetValue(
                    fact.Command.CollectionOperationStableId,
                    out existing))
                {
                    return new RunPickupSessionRecordResult(
                        string.Equals(existing, fact.Fingerprint, StringComparison.Ordinal)
                            ? RunPickupSessionRecordStatus.ExactReplay
                            : RunPickupSessionRecordStatus.ConflictingDuplicate,
                        fact,
                        string.Empty);
                }
                replay.Add(
                    fact.Command.CollectionOperationStableId,
                    fact.Fingerprint);
                CollectionRecordCount++;
                return new RunPickupSessionRecordResult(
                    RunPickupSessionRecordStatus.Accepted,
                    fact,
                    string.Empty);
            }
        }

        private sealed class SceneFixture
        {
            public FakeRunSessionPort Session;
            public RunLocalPickupState Authority;
            public RunPickupStateHost2D Host;
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
            SceneFixture fixture = CreateSceneFixture(RewardGrantKind.Money);
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("money", RewardGrantKind.Money, "credits", 5L)).Single();

            RunPickupPresentationSyncResult sync = fixture.Presenter.Synchronize(RoomId);
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
        public IEnumerator PlayerTrigger_CollectsImmediatelyAndDestroysAfterFeedback()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKind.Money);
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("money", RewardGrantKind.Money, "credits", 5L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            Assert.That(fixture.Presenter.TryGetView(pickup.PickupStableId, out view), Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);

            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(
                view.LastCollectionResult.Status,
                Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.IsRetirementFeedbackPending, Is.True);
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(1));
            Assert.That(fixture.Session.CollectionRecordCount, Is.EqualTo(1));

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(view == null, Is.True);
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator RepeatedTriggerCallbacks_DoNotDuplicateCollection()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKind.Money);
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("money", RewardGrantKind.Money, "credits", 5L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out view);

            view.HandleTriggerForTests(fixture.Collector);
            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(fixture.Session.CollectionRecordCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(fixture.Presenter.RetiringPickupCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LeavingAndReturning_DoesNotRespawnCollectedPickup()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKind.Strongbox);
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("box", RewardGrantKind.Strongbox, "emerald", 1L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out view);
            view.HandleTriggerForTests(fixture.Collector);
            yield return null;

            fixture.Presenter.Synchronize(OtherRoomId);
            RunPickupPresentationSyncResult returned =
                fixture.Presenter.Synchronize(RoomId);

            Assert.That(returned.AvailableCount, Is.EqualTo(0));
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PresenterReconstruction_RestoresUncollectedPickupWithSameIdentity()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKind.Scrap);
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("scrap", RewardGrantKind.Scrap, "scrap", 8L)).Single();
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
                RewardGrantKind.Money,
                RewardGrantKind.Scrap,
                RewardGrantKind.Strongbox);
            IReadOnlyList<RunPickupSnapshot> pickups = Realize(
                fixture,
                Child("money", RewardGrantKind.Money, "credits", 5L, 0),
                Child("scrap", RewardGrantKind.Scrap, "scrap", 7L, 1),
                Child("box", RewardGrantKind.Strongbox, "emerald", 1L, 2));

            RunPickupPresentationSyncResult sync = fixture.Presenter.Synchronize(RoomId);

            Assert.That(sync.CreatedCount, Is.EqualTo(3));
            Assert.That(fixture.Presenter.VisiblePickupCount, Is.EqualTo(3));
            Assert.That(pickups.Select(item => item.Reward.Kind),
                Is.EquivalentTo(new[]
                {
                    RewardGrantKind.Money,
                    RewardGrantKind.Scrap,
                    RewardGrantKind.Strongbox
                }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingPresentation_IsRetryableAndDoesNotCollectOrDiscard()
        {
            SceneFixture fixture = CreateSceneFixture();
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("money", RewardGrantKind.Money, "credits", 5L)).Single();

            RunPickupPresentationSyncResult failed = fixture.Presenter.Synchronize(RoomId);

            Assert.That(failed.FailedCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.ExportAvailablePickups().Single().PickupStableId,
                Is.EqualTo(pickup.PickupStableId));
            Assert.That(fixture.Session.CollectionRecordCount, Is.EqualTo(0));
            fixture.Registry.ConfigureForTests(new[]
            {
                Presentation(RewardGrantKind.Money, fixture.Prefab)
            });

            RunPickupPresentationSyncResult retry = fixture.Presenter.Synchronize(RoomId);

            Assert.That(retry.CreatedCount, Is.EqualTo(1));
            Assert.That(retry.FailedCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PickupCollection_DoesNotGrantPermanentCharacterReward()
        {
            SceneFixture fixture = CreateSceneFixture(RewardGrantKind.Strongbox);
            RunPickupSnapshot pickup = Realize(
                fixture,
                Child("box", RewardGrantKind.Strongbox, "emerald", 1L)).Single();
            fixture.Presenter.Synchronize(RoomId);
            RunRewardPickup2D view;
            fixture.Presenter.TryGetView(pickup.PickupStableId, out view);

            view.HandleTriggerForTests(fixture.Collector);

            Assert.That(fixture.Session.PermanentGrantCount, Is.EqualTo(0));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
            yield return null;
        }

        private SceneFixture CreateSceneFixture(
            params RewardGrantKind[] presentationKinds)
        {
            var session = new FakeRunSessionPort();
            var authority = new RunLocalPickupState(
                session,
                new FixedPositionPort());

            GameObject hostObject = Track(new GameObject("PickupAuthorityHost"));
            RunPickupStateHost2D host =
                hostObject.AddComponent<RunPickupStateHost2D>();
            host.Configure(authority);

            GameObject registryObject = Track(new GameObject("PickupPresentationRegistry"));
            RunPickupPresentationRegistry2D registry =
                registryObject.AddComponent<RunPickupPresentationRegistry2D>();

            GameObject prefab = Track(new GameObject("GenericPickupPrefab"));
            prefab.SetActive(false);
            var entries = new List<RunPickupPresentationEntry>();
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
            collector.ConfigureForTests(
                PlayerActorId.ToString(),
                PlayerParticipantId.ToString());

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

        private static RunPickupPresentationEntry Presentation(
            RewardGrantKind kind,
            GameObject prefab)
        {
            var entry = new RunPickupPresentationEntry();
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

        private static IReadOnlyList<RunPickupSnapshot> Realize(
            SceneFixture fixture,
            params RunPickupGeneratedReward[] children)
        {
            return fixture.Authority.Realize(new RunPickupGeneratedBatch(
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

        private static RunPickupGeneratedReward Child(
            string instance,
            RewardGrantKind kind,
            string content,
            long quantity,
            int ordinal = 0)
        {
            return new RunPickupGeneratedReward(
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
