using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionFlowSessionV1Tests
    {
        [Test]
        public void ProfileStoreRetainsExactExistingPayload()
        {
            PlayerRouteProfilePayloadV1 payload = Route("stored");
            var store = new InMemoryProductionFlowProfileStoreV1();
            store.Save(new ProductionFlowProfileRecordV1("Nemo", payload));

            ProductionFlowProfileRecordV1 loaded;
            Assert.That(store.TryLoad(out loaded), Is.True);
            Assert.That(loaded.DisplayName, Is.EqualTo("Nemo"));
            Assert.That(loaded.Payload, Is.SameAs(payload));
        }

        [Test]
        public void SecondDestinationAndBackRejectWhileLoadIsPending()
        {
            var loader = new RecordingLoader();
            HubNavigationServiceV1 navigation = AtHub(Route("pending"));
            var transitions = new ProductionSceneTransitionCoordinatorV1(
                navigation,
                loader);

            Assert.That(
                transitions.TryNavigateTo(HubRouteV1.Inventory),
                Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRouteV1.Inventory));
            Assert.That(
                transitions.TryNavigateTo(HubRouteV1.Skills),
                Is.False);
            Assert.That(transitions.TryNavigateBack(), Is.False);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRouteV1.Inventory));
            Assert.That(loader.Paths, Has.Count.EqualTo(1));
            Assert.That(
                transitions.RejectedWhilePendingCount,
                Is.EqualTo(2));
        }


        [Test]
        public void EveryHubDestinationStartsExactlyOneSceneLoad()
        {
            HubRouteV1[] routes =
            {
                HubRouteV1.Inventory,
                HubRouteV1.Skills,
                HubRouteV1.Shop,
                HubRouteV1.Crafting,
                HubRouteV1.Play,
            };

            for (int index = 0; index < routes.Length; index++)
            {
                var loader = new RecordingLoader();
                var transitions =
                    new ProductionSceneTransitionCoordinatorV1(
                        AtHub(Route("destination-" + index)),
                        loader);
                Assert.That(
                    transitions.TryNavigateTo(routes[index]),
                    Is.True,
                    routes[index].ToString());
                Assert.That(
                    transitions.TryNavigateTo(routes[index]),
                    Is.False,
                    routes[index].ToString());
                Assert.That(loader.Paths, Has.Count.EqualTo(1));
                Assert.That(
                    loader.Paths[0],
                    Is.EqualTo(
                        ProductionFlowScenePathsV1.ForHubRoute(
                            routes[index])));
            }
        }

        [Test]
        public void MismatchedSceneCompletionReissuesAcceptedTarget()
        {
            var loader = new RecordingLoader();
            var transitions = new ProductionSceneTransitionCoordinatorV1(
                AtHub(Route("mismatch")),
                loader);

            Assert.That(
                transitions.TryNavigateTo(HubRouteV1.Shop),
                Is.True);
            Assert.That(
                transitions.CompleteSceneLoad(
                    ProductionFlowScenePathsV1.Inventory),
                Is.False);
            Assert.That(loader.Paths, Has.Count.EqualTo(2));
            Assert.That(
                loader.Paths[1],
                Is.EqualTo(ProductionFlowScenePathsV1.Shop));
            Assert.That(
                transitions.CompleteSceneLoad(
                    ProductionFlowScenePathsV1.Shop),
                Is.True);
            Assert.That(transitions.IsTransitionPending, Is.False);
        }

        [Test]
        public void ReturnedLoadoutPayloadReplacesSessionAtHub()
        {
            var loader = new RecordingLoader();
            var transitions = new ProductionSceneTransitionCoordinatorV1(
                AtHub(Route("before")),
                loader);
            Assert.That(
                transitions.TryNavigateTo(HubRouteV1.Inventory),
                Is.True);
            transitions.CompleteSceneLoad(
                ProductionFlowScenePathsV1.Inventory);

            PlayerRouteProfilePayloadV1 updated = Route("after");
            Assert.That(transitions.TryReturnToHub(updated), Is.True);
            Assert.That(
                transitions.Navigation.CurrentRoute,
                Is.EqualTo(HubRouteV1.InventoryLoadoutHub));
            Assert.That(transitions.Navigation.Payload, Is.SameAs(updated));
        }

        [Test]
        public void ResultsContextAcceptsOnlyExactUnopenedStrongboxObject()
        {
            MissionRunStrongboxCollectionV1 collection = Collection("box-a");
            var exact = new MissionRunStrongboxResultV1(
                collection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            MissionResultPayloadV1 result = MissionResultPayloadV1.Create(
                StableId.Parse("run.flow-results"),
                Route("results"),
                MissionRunCompletionStateV1.Completed,
                new[] { exact },
                1L,
                2L,
                MissionRunCanonicalV1.Fingerprint("holdings"),
                3L,
                MissionRunCanonicalV1.Fingerprint("opening"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningServiceV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningServiceV1));
            var command = (StrongboxOpenCommandV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommandV1));
#pragma warning restore SYSLIB0050

            MissionRunStrongboxResultV1 received = null;
            var context = new ProductionResultsContextV1(
                result,
                service,
                delegate(MissionRunStrongboxResultV1 value)
                {
                    received = value;
                    return command;
                },
                (EquipmentCatalog)null,
                delegate { return result; });

            ProductionStrongboxOpeningBindingV1 binding =
                context.BindExact(exact);
            Assert.That(received, Is.SameAs(exact));
            Assert.That(binding.SelectedStrongbox, Is.SameAs(exact));
            Assert.That(binding.OpeningService, Is.SameAs(service));
            Assert.That(binding.Command, Is.SameAs(command));

            var equalButNotExact = new MissionRunStrongboxResultV1(
                collection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            Assert.Throws<ArgumentException>(
                delegate { context.BindExact(equalButNotExact); });
        }



        [Test]
        public void SuccessfulOpeningChangesOnlyTheExactSelectedStrongbox()
        {
            MissionRunStrongboxCollectionV1 selectedCollection =
                Collection("selected");
            MissionRunStrongboxCollectionV1 untouchedCollection =
                Collection("untouched");
            var selected = new MissionRunStrongboxResultV1(
                selectedCollection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            var untouched = new MissionRunStrongboxResultV1(
                untouchedCollection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            PlayerRouteProfilePayloadV1 route = Route("refresh");
            MissionResultPayloadV1 before = MissionResultPayloadV1.Create(
                StableId.Parse("run.flow-refresh"),
                route,
                MissionRunCompletionStateV1.Completed,
                new[] { selected, untouched },
                1L,
                2L,
                MissionRunCanonicalV1.Fingerprint("holdings-before"),
                3L,
                MissionRunCanonicalV1.Fingerprint("opening-before"));
            var openedSelected = new MissionRunStrongboxResultV1(
                selectedCollection,
                MissionRunStrongboxStateV1.Opened,
                StableId.Parse("opening.flow-refresh-selected"),
                MissionRunCanonicalV1.Fingerprint("selected-open-result"));
            MissionResultPayloadV1 after = MissionResultPayloadV1.Create(
                before.RunStableId,
                route,
                before.CompletionState,
                new[] { openedSelected, untouched },
                2L,
                3L,
                MissionRunCanonicalV1.Fingerprint("holdings-after"),
                4L,
                MissionRunCanonicalV1.Fingerprint("opening-after"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningServiceV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningServiceV1));
            var command = (StrongboxOpenCommandV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommandV1));
#pragma warning restore SYSLIB0050

            var context = new ProductionResultsContextV1(
                before,
                service,
                delegate { return command; },
                (EquipmentCatalog)null,
                delegate { return after; });

            ProductionResultsContextV1 refreshed =
                context.RefreshAfterExactOpening(selected, true);

            Assert.That(
                refreshed.Result.OpenedStrongboxes[0].InstanceStableId,
                Is.EqualTo(selected.InstanceStableId));
            Assert.That(
                refreshed.Result.UnopenedStrongboxes[0],
                Is.SameAs(untouched));
        }

        [Test]
        public void SuccessfulOpeningRejectsMutationOfAnotherStrongbox()
        {
            MissionRunStrongboxCollectionV1 selectedCollection =
                Collection("selected-conflict");
            MissionRunStrongboxCollectionV1 otherCollection =
                Collection("other-conflict");
            var selected = new MissionRunStrongboxResultV1(
                selectedCollection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            var other = new MissionRunStrongboxResultV1(
                otherCollection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            PlayerRouteProfilePayloadV1 route = Route("refresh-conflict");
            MissionResultPayloadV1 before = MissionResultPayloadV1.Create(
                StableId.Parse("run.flow-refresh-conflict"),
                route,
                MissionRunCompletionStateV1.Completed,
                new[] { selected, other },
                1L,
                2L,
                MissionRunCanonicalV1.Fingerprint("holdings-conflict-before"),
                3L,
                MissionRunCanonicalV1.Fingerprint("opening-conflict-before"));
            var openedSelected = new MissionRunStrongboxResultV1(
                selectedCollection,
                MissionRunStrongboxStateV1.Opened,
                StableId.Parse("opening.flow-refresh-selected-conflict"),
                MissionRunCanonicalV1.Fingerprint("selected-conflict-result"));
            var incorrectlyOpenedOther = new MissionRunStrongboxResultV1(
                otherCollection,
                MissionRunStrongboxStateV1.Opened,
                StableId.Parse("opening.flow-refresh-other-conflict"),
                MissionRunCanonicalV1.Fingerprint("other-conflict-result"));
            MissionResultPayloadV1 invalidAfter = MissionResultPayloadV1.Create(
                before.RunStableId,
                route,
                before.CompletionState,
                new[] { openedSelected, incorrectlyOpenedOther },
                2L,
                3L,
                MissionRunCanonicalV1.Fingerprint("holdings-conflict-after"),
                4L,
                MissionRunCanonicalV1.Fingerprint("opening-conflict-after"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningServiceV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningServiceV1));
            var command = (StrongboxOpenCommandV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommandV1));
#pragma warning restore SYSLIB0050

            var context = new ProductionResultsContextV1(
                before,
                service,
                delegate { return command; },
                (EquipmentCatalog)null,
                delegate { return invalidAfter; });

            Assert.Throws<InvalidOperationException>(
                delegate
                {
                    context.RefreshAfterExactOpening(selected, true);
                });
        }

        private static HubNavigationServiceV1 AtHub(
            PlayerRouteProfilePayloadV1 payload)
        {
            var navigation = new HubNavigationServiceV1(payload);
            navigation.TryNavigateTo(HubRouteV1.CharacterSelect);
            navigation.TryNavigateTo(HubRouteV1.InventoryLoadoutHub);
            return navigation;
        }

        private static PlayerRouteProfilePayloadV1 Route(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse("loadout-profile." + suffix),
                new[]
                {
                    StableId.Parse("equipment-instance." + suffix + "-1"),
                    StableId.Parse("equipment-instance." + suffix + "-2"),
                    StableId.Parse("equipment-instance." + suffix + "-3"),
                    StableId.Parse("equipment-instance." + suffix + "-4"),
                });
        }

        private static MissionRunStrongboxCollectionV1 Collection(
            string suffix)
        {
            return new MissionRunStrongboxCollectionV1(
                StableId.Parse("strongbox-definition." + suffix),
                StableId.Parse("strongbox-instance." + suffix),
                StableId.Parse("grant." + suffix),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                1L,
                MissionRunCanonicalV1.Fingerprint("collection-" + suffix));
        }

        private sealed class RecordingLoader :
            IProductionSceneLoadPortV1
        {
            public readonly List<string> Paths = new List<string>();

            public bool BeginLoad(string scenePath)
            {
                Paths.Add(scenePath);
                return true;
            }
        }
    }
}
