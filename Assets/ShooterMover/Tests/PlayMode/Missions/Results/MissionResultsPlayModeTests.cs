using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Missions.Results
{
    public sealed class MissionResultsPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResultsDisplay_WithZeroBoxes_IsPureReadOnlyHandoff()
        {
            TestPort port = new TestPort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("play-zero");
            MissionRunStateResult ended = authority.EndRun(CreateEnd(
                "play-zero-end", "play-zero-run", route, 0L, port));
            int callsAtEnd = port.ProjectCalls;

            MissionResultsSession session = new MissionResultsSession(ended.ResultPayload);
            yield return null;
            MissionResultPayload first = session.Snapshot;
            MissionResultPayload second = session.Snapshot;

            Assert.That(first, Is.SameAs(second));
            Assert.That(session.CollectedStrongboxCount, Is.Zero);
            Assert.That(port.ProjectCalls, Is.EqualTo(callsAtEnd));
            Assert.That(port.OpenCalls, Is.Zero);
            Assert.That(port.GrantCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ResultsDisplay_WithOneBox_PreservesExactUnopenedInstance()
        {
            TestPort port = new TestPort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("play-one");
            StableId instance = Id("box-instance", "play-one-box");
            authority.RecordCollectedStrongbox(CreateCollection(
                "play-one-collect", "play-one-run", route, instance, 0L, port));
            MissionRunStateResult ended = authority.EndRun(CreateEnd(
                "play-one-end", "play-one-run", route, 1L, port));

            MissionResultsSession session = new MissionResultsSession(ended.ResultPayload);
            yield return null;

            Assert.That(session.UnopenedStrongboxCount, Is.EqualTo(1));
            Assert.That(session.Snapshot.UnopenedStrongboxes[0].InstanceStableId, Is.EqualTo(instance));
            Assert.That(port.OpenCalls, Is.Zero);
            Assert.That(port.GrantCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ResultsDisplay_WithMultipleBoxes_AndRepeatedEnd_DoesNotConsumeOrReroll()
        {
            TestPort port = new TestPort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("play-many");
            authority.RecordCollectedStrongbox(CreateCollection(
                "play-many-a", "play-many-run", route, Id("box-instance", "play-a"), 0L, port));
            authority.RecordCollectedStrongbox(CreateCollection(
                "play-many-b", "play-many-run", route, Id("box-instance", "play-b"), 1L, port));
            MissionRunStateResult first = authority.EndRun(CreateEnd(
                "play-many-end-a", "play-many-run", route, 2L, port));
            int projectCalls = port.ProjectCalls;
            MissionRunStateResult repeated = authority.EndRun(CreateEnd(
                "play-many-end-b", "play-many-run", route, 999L, port));

            MissionResultsSession session = new MissionResultsSession(repeated.ResultPayload);
            yield return null;

            Assert.That(repeated, Is.SameAs(first));
            Assert.That(session.UnopenedStrongboxCount, Is.EqualTo(2));
            Assert.That(session.RoutePayload, Is.SameAs(route));
            Assert.That(port.ProjectCalls, Is.EqualTo(projectCalls));
            Assert.That(port.OpenCalls, Is.Zero);
            Assert.That(port.ConsumeCalls, Is.Zero);
            Assert.That(port.GrantCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ConflictingReplay_RemainsRejectedAcrossFramesWithoutMutation()
        {
            TestPort port = new TestPort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("play-conflict");
            MissionRunStateResult first = authority.EndRun(CreateEnd(
                "play-conflict-a", "play-conflict-run", route, 0L, port));
            yield return null;
            EndMissionRunCommand conflict = EndMissionRunCommand.Create(
                Id("run-operation", "play-conflict-b"),
                Id("run", "play-conflict-run"),
                route,
                MissionRunCompletionState.Failed,
                1L,
                port.HoldingsSequence,
                port.HoldingsFingerprint,
                port.OpeningSequence,
                port.OpeningFingerprint);
            MissionRunStateResult rejected = authority.EndRun(conflict);

            Assert.That(first.Status, Is.EqualTo(MissionRunStateStatus.RunEnded));
            Assert.That(rejected.Status, Is.EqualTo(MissionRunStateStatus.ConflictingDuplicate));
            Assert.That(authority.Sequence, Is.EqualTo(1L));
            Assert.That(port.OpenCalls, Is.Zero);
            Assert.That(port.GrantCalls, Is.Zero);
        }

        private static PlayerRouteProfilePayload CreateRoute(string suffix)
        {
            return PlayerRouteProfilePayload.Create(
                Id("character", suffix),
                Id("loadout", suffix),
                new[]
                {
                    Id("equipment-instance", suffix + "-1"),
                    Id("equipment-instance", suffix + "-2"),
                    Id("equipment-instance", suffix + "-3"),
                    Id("equipment-instance", suffix + "-4"),
                });
        }

        private static MissionRunCollectStrongboxCommand CreateCollection(
            string operation,
            string run,
            PlayerRouteProfilePayload route,
            StableId instance,
            long expectedRunSequence,
            TestPort port)
        {
            return MissionRunCollectStrongboxCommand.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                Id("strongbox", "standard"),
                instance,
                Id("reward-grant", operation),
                Id("reward-source", operation),
                expectedRunSequence,
                port.HoldingsSequence,
                port.HoldingsFingerprint);
        }

        private static EndMissionRunCommand CreateEnd(
            string operation,
            string run,
            PlayerRouteProfilePayload route,
            long expectedRunSequence,
            TestPort port)
        {
            return EndMissionRunCommand.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                MissionRunCompletionState.Completed,
                expectedRunSequence,
                port.HoldingsSequence,
                port.HoldingsFingerprint,
                port.OpeningSequence,
                port.OpeningFingerprint);
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        private sealed class TestPort : IMissionRunExistingStatePort
        {
            public TestPort()
            {
                HoldingsFingerprint = MissionRun.Fingerprint("play-holdings");
                OpeningFingerprint = MissionRun.Fingerprint("play-openings");
            }

            public long HoldingsSequence = 4L;
            public string HoldingsFingerprint;
            public long OpeningSequence = 8L;
            public string OpeningFingerprint;
            public int ProjectCalls;
            public int OpenCalls;
            public int ConsumeCalls;
            public int GrantCalls;

            public MissionRunCollectionVerification VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommand command)
            {
                return MissionRunCollectionVerification.Accept(
                    new MissionRunStrongboxCollection(
                        command.DefinitionStableId,
                        command.InstanceStableId,
                        command.GrantStableId,
                        command.SourceStableId,
                        command.OperationStableId,
                        HoldingsSequence,
                        HoldingsFingerprint));
            }

            public MissionRunStrongboxView ProjectStrongboxStates(
                EndMissionRunCommand command,
                System.Collections.Generic.IReadOnlyList<MissionRunStrongboxCollection> collectedStrongboxes)
            {
                ProjectCalls++;
                System.Collections.Generic.List<MissionRunStrongboxResult> results =
                    new System.Collections.Generic.List<MissionRunStrongboxResult>();
                for (int index = 0; index < collectedStrongboxes.Count; index++)
                {
                    results.Add(new MissionRunStrongboxResult(
                        collectedStrongboxes[index],
                        MissionRunStrongboxState.Unopened,
                        null,
                        null));
                }
                return MissionRunStrongboxView.Accept(
                    results,
                    HoldingsSequence,
                    HoldingsFingerprint,
                    OpeningSequence,
                    OpeningFingerprint);
            }
        }
    }
}
