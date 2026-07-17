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
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("play-zero");
            MissionRunAuthorityResultV1 ended = authority.EndRun(CreateEnd(
                "play-zero-end", "play-zero-run", route, 0L, port));
            int callsAtEnd = port.ProjectCalls;

            MissionResultsSessionV1 session = new MissionResultsSessionV1(ended.ResultPayload);
            yield return null;
            MissionResultPayloadV1 first = session.Snapshot;
            MissionResultPayloadV1 second = session.Snapshot;

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
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("play-one");
            StableId instance = Id("box-instance", "play-one-box");
            authority.RecordCollectedStrongbox(CreateCollection(
                "play-one-collect", "play-one-run", route, instance, 0L, port));
            MissionRunAuthorityResultV1 ended = authority.EndRun(CreateEnd(
                "play-one-end", "play-one-run", route, 1L, port));

            MissionResultsSessionV1 session = new MissionResultsSessionV1(ended.ResultPayload);
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
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("play-many");
            authority.RecordCollectedStrongbox(CreateCollection(
                "play-many-a", "play-many-run", route, Id("box-instance", "play-a"), 0L, port));
            authority.RecordCollectedStrongbox(CreateCollection(
                "play-many-b", "play-many-run", route, Id("box-instance", "play-b"), 1L, port));
            MissionRunAuthorityResultV1 first = authority.EndRun(CreateEnd(
                "play-many-end-a", "play-many-run", route, 2L, port));
            int projectCalls = port.ProjectCalls;
            MissionRunAuthorityResultV1 repeated = authority.EndRun(CreateEnd(
                "play-many-end-b", "play-many-run", route, 999L, port));

            MissionResultsSessionV1 session = new MissionResultsSessionV1(repeated.ResultPayload);
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
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("play-conflict");
            MissionRunAuthorityResultV1 first = authority.EndRun(CreateEnd(
                "play-conflict-a", "play-conflict-run", route, 0L, port));
            yield return null;
            EndMissionRunCommandV1 conflict = EndMissionRunCommandV1.Create(
                Id("run-operation", "play-conflict-b"),
                Id("run", "play-conflict-run"),
                route,
                MissionRunCompletionStateV1.Failed,
                1L,
                port.HoldingsSequence,
                port.HoldingsFingerprint,
                port.OpeningSequence,
                port.OpeningFingerprint);
            MissionRunAuthorityResultV1 rejected = authority.EndRun(conflict);

            Assert.That(first.Status, Is.EqualTo(MissionRunAuthorityStatusV1.RunEnded));
            Assert.That(rejected.Status, Is.EqualTo(MissionRunAuthorityStatusV1.ConflictingDuplicate));
            Assert.That(authority.Sequence, Is.EqualTo(1L));
            Assert.That(port.OpenCalls, Is.Zero);
            Assert.That(port.GrantCalls, Is.Zero);
        }

        private static PlayerRouteProfilePayloadV1 CreateRoute(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
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

        private static MissionRunCollectStrongboxCommandV1 CreateCollection(
            string operation,
            string run,
            PlayerRouteProfilePayloadV1 route,
            StableId instance,
            long expectedRunSequence,
            TestPort port)
        {
            return MissionRunCollectStrongboxCommandV1.Create(
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

        private static EndMissionRunCommandV1 CreateEnd(
            string operation,
            string run,
            PlayerRouteProfilePayloadV1 route,
            long expectedRunSequence,
            TestPort port)
        {
            return EndMissionRunCommandV1.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                MissionRunCompletionStateV1.Completed,
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

        private sealed class TestPort : IMissionRunExistingAuthorityPortV1
        {
            public TestPort()
            {
                HoldingsFingerprint = MissionRunCanonicalV1.Fingerprint("play-holdings");
                OpeningFingerprint = MissionRunCanonicalV1.Fingerprint("play-openings");
            }

            public long HoldingsSequence = 4L;
            public string HoldingsFingerprint;
            public long OpeningSequence = 8L;
            public string OpeningFingerprint;
            public int ProjectCalls;
            public int OpenCalls;
            public int ConsumeCalls;
            public int GrantCalls;

            public MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommandV1 command)
            {
                return MissionRunCollectionVerificationV1.Accept(
                    new MissionRunStrongboxCollectionV1(
                        command.DefinitionStableId,
                        command.InstanceStableId,
                        command.GrantStableId,
                        command.SourceStableId,
                        command.OperationStableId,
                        HoldingsSequence,
                        HoldingsFingerprint));
            }

            public MissionRunStrongboxProjectionV1 ProjectStrongboxStates(
                EndMissionRunCommandV1 command,
                System.Collections.Generic.IReadOnlyList<MissionRunStrongboxCollectionV1> collectedStrongboxes)
            {
                ProjectCalls++;
                System.Collections.Generic.List<MissionRunStrongboxResultV1> results =
                    new System.Collections.Generic.List<MissionRunStrongboxResultV1>();
                for (int index = 0; index < collectedStrongboxes.Count; index++)
                {
                    results.Add(new MissionRunStrongboxResultV1(
                        collectedStrongboxes[index],
                        MissionRunStrongboxStateV1.Unopened,
                        null,
                        null));
                }
                return MissionRunStrongboxProjectionV1.Accept(
                    results,
                    HoldingsSequence,
                    HoldingsFingerprint,
                    OpeningSequence,
                    OpeningFingerprint);
            }
        }
    }
}
