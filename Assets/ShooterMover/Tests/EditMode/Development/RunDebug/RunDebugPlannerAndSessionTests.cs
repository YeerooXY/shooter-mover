using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Development.RunDebug;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Development.RunDebug
{
    public sealed class RunDebugPlannerAndSessionTests
    {
        [Test]
        public void RequestRejectsInvalidCountAndTier()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                delegate
                {
                    RunDebugSpawnRequestV1.Create(
                        Id("run", "invalid"),
                        Route("invalid"),
                        -1,
                        Id("strongbox", "common"),
                        1UL);
                });
            Assert.Throws<ArgumentOutOfRangeException>(
                delegate
                {
                    RunDebugSpawnRequestV1.Create(
                        Id("run", "invalid"),
                        Route("invalid"),
                        RunDebugSpawnRequestV1.MaximumStrongboxCount + 1,
                        Id("strongbox", "common"),
                        1UL);
                });
            Assert.Throws<ArgumentNullException>(
                delegate
                {
                    RunDebugSpawnRequestV1.Create(
                        Id("run", "invalid"),
                        Route("invalid"),
                        1,
                        null,
                        1UL);
                });
        }

        [Test]
        public void SameInputsProduceByteIdenticalPlan()
        {
            RunDebugSpawnRequestV1 first = Request("repeat", 4, 77UL);
            RunDebugSpawnRequestV1 second = Request("repeat", 4, 77UL);
            IReadOnlyList<RunDebugBoxPlanV1> left = RunDebugPlannerV1.CreatePlan(first);
            IReadOnlyList<RunDebugBoxPlanV1> right = RunDebugPlannerV1.CreatePlan(second);

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(right.Count, Is.EqualTo(left.Count));
            for (int index = 0; index < left.Count; index++)
            {
                Assert.That(
                    right[index].SourceInstanceStableId,
                    Is.EqualTo(left[index].SourceInstanceStableId));
                Assert.That(
                    right[index].CollectionOperationStableId,
                    Is.EqualTo(left[index].CollectionOperationStableId));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6)]
        public void PlannerSupportsZeroAndMultipleBoxes(int count)
        {
            RunDebugSpawnRequestV1 request = Request("count-" + count, count, 4UL);
            Assert.That(RunDebugPlannerV1.CreatePlan(request).Count, Is.EqualTo(count));
        }

        [Test]
        public void SnapshotRejectsDuplicateConcreteBoxIdentity()
        {
            RunDebugSpawnRequestV1 request = Request("collision", 2, 8UL);
            IReadOnlyList<RunDebugBoxPlanV1> plan = RunDebugPlannerV1.CreatePlan(request);
            StableId duplicate = Id("box-instance", "duplicate");
            var facts = new[]
            {
                Spawned(plan[0], duplicate),
                Spawned(plan[1], duplicate),
            };

            Assert.Throws<ArgumentException>(
                delegate { new RunDebugSnapshotV1(request, facts, string.Empty); });
        }

        [Test]
        public void ExactInstanceIdentitySurvivesCollectedSnapshot()
        {
            RunDebugSpawnRequestV1 request = Request("identity", 1, 9UL);
            RunDebugBoxPlanV1 plan = RunDebugPlannerV1.CreatePlan(request)[0];
            StableId instance = Id("box-instance", "identity");
            RunDebugBoxFactV1 fact = Spawned(plan, instance).WithCollection("accepted");
            RunDebugSnapshotV1 snapshot = new RunDebugSnapshotV1(
                request,
                new[] { fact },
                string.Empty);

            Assert.That(snapshot.CollectedCount, Is.EqualTo(1));
            Assert.That(snapshot.PendingCount, Is.Zero);
            Assert.That(snapshot.Boxes[0].InstanceStableId, Is.EqualTo(instance));
        }

        [Test]
        public void SessionCallsEndRunExactlyOnceAcrossReplay()
        {
            var port = new FakeRuntimePort(Request("end", 0, 1UL));
            var session = new RunDebugPanelSessionV1(port);

            RunDebugEndResultV1 first = session.EndRun(MissionRunCompletionStateV1.Completed);
            RunDebugEndResultV1 replay = session.EndRun(MissionRunCompletionStateV1.Completed);

            Assert.That(port.EndCalls, Is.EqualTo(1));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(replay.ResultsSession.Snapshot.UnopenedStrongboxes.Count, Is.Zero);
        }

        [Test]
        public void BuildGuardRequiresEditorOrDevelopmentBuild()
        {
            Assert.That(RunDebugBuildGuardV1.Evaluate(false, false), Is.False);
            Assert.That(RunDebugBuildGuardV1.Evaluate(true, false), Is.True);
            Assert.That(RunDebugBuildGuardV1.Evaluate(false, true), Is.True);
        }

        private static RunDebugBoxFactV1 Spawned(
            RunDebugBoxPlanV1 plan,
            StableId instance)
        {
            return new RunDebugBoxFactV1(
                plan,
                true,
                false,
                Id("strongbox", "common"),
                instance,
                Id("reward-grant", "test-" + plan.Index),
                Id("reward-source", "test-" + plan.Index),
                Id("reward-pickup", "test-" + plan.Index),
                string.Empty);
        }

        private static RunDebugSpawnRequestV1 Request(
            string suffix,
            int count,
            ulong seed)
        {
            return RunDebugSpawnRequestV1.Create(
                Id("run", suffix),
                Route(suffix),
                count,
                Id("strongbox", "common"),
                seed);
        }

        private static PlayerRouteProfilePayloadV1 Route(string suffix)
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

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        private sealed class FakeRuntimePort : IRunDebugRuntimePortV1
        {
            private readonly RunDebugSnapshotV1 snapshot;

            public FakeRuntimePort(RunDebugSpawnRequestV1 request)
            {
                var facts = new List<RunDebugBoxFactV1>();
                IReadOnlyList<RunDebugBoxPlanV1> plan = RunDebugPlannerV1.CreatePlan(request);
                for (int index = 0; index < plan.Count; index++)
                {
                    facts.Add(new RunDebugBoxFactV1(
                        plan[index],
                        false,
                        false,
                        null,
                        null,
                        null,
                        null,
                        null,
                        string.Empty));
                }

                snapshot = new RunDebugSnapshotV1(request, facts, string.Empty);
            }

            public int EndCalls;

            public RunDebugSpawnBatchResultV1 Spawn(RunDebugSpawnRequestV1 request)
            {
                return new RunDebugSpawnBatchResultV1(
                    RunDebugSpawnBatchStatusV1.Spawned,
                    snapshot,
                    string.Empty);
            }

            public RunDebugSnapshotV1 RefreshSnapshot() { return snapshot; }

            public RunDebugEndResultV1 EndRun(MissionRunCompletionStateV1 completionState)
            {
                EndCalls++;
                MissionResultPayloadV1 payload = MissionResultPayloadV1.Create(
                    snapshot.Request.RunStableId,
                    snapshot.Request.RoutePayload,
                    completionState,
                    Array.Empty<MissionRunStrongboxResultV1>(),
                    1L,
                    0L,
                    MissionRunCanonicalV1.Fingerprint("holdings"),
                    0L,
                    MissionRunCanonicalV1.Fingerprint("openings"));
                MissionRunAuthorityResultV1 authority =
                    new MissionRunAuthorityResultV1(
                        MissionRunAuthorityStatusV1.RunEnded,
                        0L,
                        1L,
                        Id("run-operation", "end"),
                        MissionRunCanonicalV1.Fingerprint("request"),
                        null,
                        null,
                        payload,
                        string.Empty);
                return new RunDebugEndResultV1(
                    authority,
                    new MissionResultsSessionV1(payload),
                    true,
                    string.Empty);
            }
        }
    }
}
