using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class FlowSessionTests
    {
        [Test]
        public void ProfileStoreRetainsExactExistingPayload()
        {
            PlayerRouteProfilePayload payload = Route("stored");
            var store = new InMemoryFlowProfileStore();
            store.Save(new FlowProfileRecord("Nemo", payload));

            FlowProfileRecord loaded;
            Assert.That(store.TryLoad(out loaded), Is.True);
            Assert.That(loaded.DisplayName, Is.EqualTo("Nemo"));
            Assert.That(loaded.Payload, Is.SameAs(payload));
        }

        [Test]
        public void SecondDestinationAndBackRejectWhileLoadIsPending()
        {
            var loader = new RecordingLoader();
            HubNavigationActions navigation = AtHub(Route("pending"));
            var transitions = new SceneTransitionFlow(
                navigation,
                loader);

            Assert.That(
                transitions.TryNavigateTo(HubRoute.Inventory),
                Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRoute.Inventory));
            Assert.That(
                transitions.TryNavigateTo(HubRoute.Skills),
                Is.False);
            Assert.That(transitions.TryNavigateBack(), Is.False);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRoute.Inventory));
            Assert.That(loader.Paths, Has.Count.EqualTo(1));
            Assert.That(
                transitions.RejectedWhilePendingCount,
                Is.EqualTo(2));
        }


        [Test]
        public void EveryHubDestinationStartsExactlyOneSceneLoad()
        {
            HubRoute[] routes =
            {
                HubRoute.Inventory,
                HubRoute.Skills,
                HubRoute.Shop,
                HubRoute.Crafting,
                HubRoute.Play,
            };

            for (int index = 0; index < routes.Length; index++)
            {
                var loader = new RecordingLoader();
                var transitions =
                    new SceneTransitionFlow(
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
                        FlowScenePaths.ForHubRoute(
                            routes[index])));
            }
        }

        [Test]
        public void MismatchedSceneCompletionReissuesAcceptedTarget()
        {
            var loader = new RecordingLoader();
            var transitions = new SceneTransitionFlow(
                AtHub(Route("mismatch")),
                loader);

            Assert.That(
                transitions.TryNavigateTo(HubRoute.Shop),
                Is.True);
            Assert.That(
                transitions.CompleteSceneLoad(
                    FlowScenePaths.Inventory),
                Is.False);
            Assert.That(loader.Paths, Has.Count.EqualTo(2));
            Assert.That(
                loader.Paths[1],
                Is.EqualTo(FlowScenePaths.Shop));
            Assert.That(
                transitions.CompleteSceneLoad(
                    FlowScenePaths.Shop),
                Is.True);
            Assert.That(transitions.IsTransitionPending, Is.False);
        }

        [Test]
        public void ReturnedLoadoutPayloadReplacesSessionAtHub()
        {
            var loader = new RecordingLoader();
            var transitions = new SceneTransitionFlow(
                AtHub(Route("before")),
                loader);
            Assert.That(
                transitions.TryNavigateTo(HubRoute.Inventory),
                Is.True);
            transitions.CompleteSceneLoad(
                FlowScenePaths.Inventory);

            PlayerRouteProfilePayload updated = Route("after");
            Assert.That(transitions.TryReturnToHub(updated), Is.True);
            Assert.That(
                transitions.Navigation.CurrentRoute,
                Is.EqualTo(HubRoute.InventoryLoadoutHub));
            Assert.That(transitions.Navigation.Payload, Is.SameAs(updated));
        }

        [Test]
        public void ResultsContextAcceptsOnlyExactUnopenedStrongboxObject()
        {
            MissionRunStrongboxCollection collection = Collection("box-a");
            var exact = new MissionRunStrongboxResult(
                collection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            MissionResultPayload result = MissionResultPayload.Create(
                StableId.Parse("run.flow-results"),
                Route("results"),
                MissionRunCompletionState.Completed,
                new[] { exact },
                1L,
                2L,
                MissionRun.Fingerprint("holdings"),
                3L,
                MissionRun.Fingerprint("opening"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningActions)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningActions));
            var command = (StrongboxOpenCommand)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommand));
#pragma warning restore SYSLIB0050

            MissionRunStrongboxResult received = null;
            var context = new ResultsContext(
                result,
                service,
                delegate(MissionRunStrongboxResult value)
                {
                    received = value;
                    return command;
                },
                (EquipmentCatalog)null,
                delegate { return result; });

            StrongboxOpeningBinding binding =
                context.BindExact(exact);
            Assert.That(received, Is.SameAs(exact));
            Assert.That(binding.SelectedStrongbox, Is.SameAs(exact));
            Assert.That(binding.OpeningService, Is.SameAs(service));
            Assert.That(binding.Command, Is.SameAs(command));

            var equalButNotExact = new MissionRunStrongboxResult(
                collection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            Assert.Throws<ArgumentException>(
                delegate { context.BindExact(equalButNotExact); });
        }



        [Test]
        public void SuccessfulOpeningChangesOnlyTheExactSelectedStrongbox()
        {
            MissionRunStrongboxCollection selectedCollection =
                Collection("selected");
            MissionRunStrongboxCollection untouchedCollection =
                Collection("untouched");
            var selected = new MissionRunStrongboxResult(
                selectedCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            var untouched = new MissionRunStrongboxResult(
                untouchedCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            PlayerRouteProfilePayload route = Route("refresh");
            MissionResultPayload before = MissionResultPayload.Create(
                StableId.Parse("run.flow-refresh"),
                route,
                MissionRunCompletionState.Completed,
                new[] { selected, untouched },
                1L,
                2L,
                MissionRun.Fingerprint("holdings-before"),
                3L,
                MissionRun.Fingerprint("opening-before"));
            var openedSelected = new MissionRunStrongboxResult(
                selectedCollection,
                MissionRunStrongboxState.Opened,
                StableId.Parse("opening.flow-refresh-selected"),
                MissionRun.Fingerprint("selected-open-result"));
            MissionResultPayload after = MissionResultPayload.Create(
                before.RunStableId,
                route,
                before.CompletionState,
                new[] { openedSelected, untouched },
                2L,
                3L,
                MissionRun.Fingerprint("holdings-after"),
                4L,
                MissionRun.Fingerprint("opening-after"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningActions)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningActions));
            var command = (StrongboxOpenCommand)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommand));
#pragma warning restore SYSLIB0050

            var context = new ResultsContext(
                before,
                service,
                delegate { return command; },
                (EquipmentCatalog)null,
                delegate { return after; });

            ResultsContext refreshed =
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
            MissionRunStrongboxCollection selectedCollection =
                Collection("selected-conflict");
            MissionRunStrongboxCollection otherCollection =
                Collection("other-conflict");
            var selected = new MissionRunStrongboxResult(
                selectedCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            var other = new MissionRunStrongboxResult(
                otherCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            PlayerRouteProfilePayload route = Route("refresh-conflict");
            MissionResultPayload before = MissionResultPayload.Create(
                StableId.Parse("run.flow-refresh-conflict"),
                route,
                MissionRunCompletionState.Completed,
                new[] { selected, other },
                1L,
                2L,
                MissionRun.Fingerprint("holdings-conflict-before"),
                3L,
                MissionRun.Fingerprint("opening-conflict-before"));
            var openedSelected = new MissionRunStrongboxResult(
                selectedCollection,
                MissionRunStrongboxState.Opened,
                StableId.Parse("opening.flow-refresh-selected-conflict"),
                MissionRun.Fingerprint("selected-conflict-result"));
            var incorrectlyOpenedOther = new MissionRunStrongboxResult(
                otherCollection,
                MissionRunStrongboxState.Opened,
                StableId.Parse("opening.flow-refresh-other-conflict"),
                MissionRun.Fingerprint("other-conflict-result"));
            MissionResultPayload invalidAfter = MissionResultPayload.Create(
                before.RunStableId,
                route,
                before.CompletionState,
                new[] { openedSelected, incorrectlyOpenedOther },
                2L,
                3L,
                MissionRun.Fingerprint("holdings-conflict-after"),
                4L,
                MissionRun.Fingerprint("opening-conflict-after"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningActions)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningActions));
            var command = (StrongboxOpenCommand)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommand));
#pragma warning restore SYSLIB0050

            var context = new ResultsContext(
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

        private static HubNavigationActions AtHub(
            PlayerRouteProfilePayload payload)
        {
            var navigation = new HubNavigationActions(payload);
            navigation.TryNavigateTo(HubRoute.CharacterSelect);
            navigation.TryNavigateTo(HubRoute.InventoryLoadoutHub);
            return navigation;
        }

        private static PlayerRouteProfilePayload Route(string suffix)
        {
            return PlayerRouteProfilePayload.Create(
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

        private static MissionRunStrongboxCollection Collection(
            string suffix)
        {
            return new MissionRunStrongboxCollection(
                StableId.Parse("strongbox-definition." + suffix),
                StableId.Parse("strongbox-instance." + suffix),
                StableId.Parse("grant." + suffix),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                1L,
                MissionRun.Fingerprint("collection-" + suffix));
        }

        private sealed class RecordingLoader :
            ISceneLoadPort
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
