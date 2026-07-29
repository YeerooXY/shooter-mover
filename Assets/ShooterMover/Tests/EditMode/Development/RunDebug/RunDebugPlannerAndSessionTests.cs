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
                    RunDebugSpawnRequest.Create(
                        Id("run", "invalid"),
                        Route("invalid"),
                        -1,
                        Id("strongbox", "common"),
                        1UL);
                });
            Assert.Throws<ArgumentOutOfRangeException>(
                delegate
                {
                    RunDebugSpawnRequest.Create(
                        Id("run", "invalid"),
                        Route("invalid"),
                        RunDebugSpawnRequest.MaximumStrongboxCount + 1,
                        Id("strongbox", "common"),
                        1UL);
                });
            Assert.Throws<ArgumentNullException>(
                delegate
                {
                    RunDebugSpawnRequest.Create(
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
            RunDebugSpawnRequest first = Request("repeat", 4, 77UL);
            RunDebugSpawnRequest second = Request("repeat", 4, 77UL);
            IReadOnlyList<RunDebugBoxPlan> left = RunDebugPlanner.CreatePlan(first);
            IReadOnlyList<RunDebugBoxPlan> right = RunDebugPlanner.CreatePlan(second);

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
            RunDebugSpawnRequest request = Request("count-" + count, count, 4UL);
            Assert.That(RunDebugPlanner.CreatePlan(request).Count, Is.EqualTo(count));
        }

        [Test]
        public void SnapshotRejectsDuplicateConcreteBoxIdentity()
        {
            RunDebugSpawnRequest request = Request("collision", 2, 8UL);
            IReadOnlyList<RunDebugBoxPlan> plan = RunDebugPlanner.CreatePlan(request);
            StableId duplicate = Id("box-instance", "duplicate");
            var facts = new[]
            {
                Spawned(plan[0], duplicate),
                Spawned(plan[1], duplicate),
            };

            Assert.Throws<ArgumentException>(
                delegate { new RunDebugSnapshot(request, facts, string.Empty); });
        }

        [Test]
        public void ExactInstanceIdentitySurvivesCollectedSnapshot()
        {
            RunDebugSpawnRequest request = Request("identity", 1, 9UL);
            RunDebugBoxPlan plan = RunDebugPlanner.CreatePlan(request)[0];
            StableId instance = Id("box-instance", "identity");
            RunDebugBoxFact fact = Spawned(plan, instance).WithCollection("accepted");
            RunDebugSnapshot snapshot = new RunDebugSnapshot(
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
            var port = new FakeLivePort(Request("end", 0, 1UL));
            var session = new RunDebugPanelSession(port);

            RunDebugEndResult first = session.EndRun(MissionRunCompletionState.Completed);
            RunDebugEndResult replay = session.EndRun(MissionRunCompletionState.Completed);

            Assert.That(port.EndCalls, Is.EqualTo(1));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(replay.ResultsSession.Snapshot.UnopenedStrongboxes.Count, Is.Zero);
        }

        [Test]
        public void BuildGuardRequiresEditorOrDevelopmentBuild()
        {
            Assert.That(RunDebugBuildGuard.Evaluate(false, false), Is.False);
            Assert.That(RunDebugBuildGuard.Evaluate(true, false), Is.True);
            Assert.That(RunDebugBuildGuard.Evaluate(false, true), Is.True);
        }

        private static RunDebugBoxFact Spawned(
            RunDebugBoxPlan plan,
            StableId instance)
        {
            return new RunDebugBoxFact(
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

        private static RunDebugSpawnRequest Request(
            string suffix,
            int count,
            ulong seed)
        {
            return RunDebugSpawnRequest.Create(
                Id("run", suffix),
                Route(suffix),
                count,
                Id("strongbox", "common"),
                seed);
        }

        private static PlayerRouteProfilePayload Route(string suffix)
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

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        private sealed class FakeLivePort : IRunDebugLivePort
        {
            private readonly RunDebugSnapshot snapshot;

            public FakeLivePort(RunDebugSpawnRequest request)
            {
                var facts = new List<RunDebugBoxFact>();
                IReadOnlyList<RunDebugBoxPlan> plan = RunDebugPlanner.CreatePlan(request);
                for (int index = 0; index < plan.Count; index++)
                {
                    facts.Add(new RunDebugBoxFact(
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

                snapshot = new RunDebugSnapshot(request, facts, string.Empty);
            }

            public int EndCalls;

            public RunDebugSpawnBatchResult Spawn(RunDebugSpawnRequest request)
            {
                return new RunDebugSpawnBatchResult(
                    RunDebugSpawnBatchStatus.Spawned,
                    snapshot,
                    string.Empty);
            }

            public RunDebugSnapshot RefreshSnapshot() { return snapshot; }

            public RunDebugEndResult EndRun(MissionRunCompletionState completionState)
            {
                EndCalls++;
                MissionResultPayload payload = MissionResultPayload.Create(
                    snapshot.Request.RunStableId,
                    snapshot.Request.RoutePayload,
                    completionState,
                    Array.Empty<MissionRunStrongboxResult>(),
                    1L,
                    0L,
                    MissionRun.Fingerprint("holdings"),
                    0L,
                    MissionRun.Fingerprint("openings"));
                MissionRunStateResult authority =
                    new MissionRunStateResult(
                        MissionRunStateStatus.RunEnded,
                        0L,
                        1L,
                        Id("run-operation", "end"),
                        MissionRun.Fingerprint("request"),
                        null,
                        null,
                        payload,
                        string.Empty);
                return new RunDebugEndResult(
                    authority,
                    new MissionResultsSession(payload),
                    true,
                    string.Empty);
            }
        }
    }
}
