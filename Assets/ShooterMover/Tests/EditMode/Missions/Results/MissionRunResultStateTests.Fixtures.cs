using System.Collections.Generic;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Missions.Results
{
    public sealed partial class MissionRunResultStateTests
    {
        internal static PlayerRouteProfilePayload CreateRoute(string suffix)
        {
            return PlayerRouteProfilePayload.Create(
                Id("character", suffix),
                Id("loadout", suffix),
                new[]
                {
                    Id("equipment-instance", suffix + "-slot-1"),
                    Id("equipment-instance", suffix + "-slot-2"),
                    Id("equipment-instance", suffix + "-slot-3"),
                    Id("equipment-instance", suffix + "-slot-4"),
                });
        }

        internal static MissionRunCollectStrongboxCommand CreateCollection(
            string operation,
            string run,
            PlayerRouteProfilePayload route,
            string definition,
            string instance,
            long expectedRunSequence,
            FakeExistingStatePort port)
        {
            return MissionRunCollectStrongboxCommand.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                Id("strongbox", definition),
                Id("box-instance", instance),
                Id("reward-grant", operation),
                Id("reward-source", operation),
                expectedRunSequence);
        }

        internal static EndMissionRunCommand CreateEnd(
            string operation,
            string run,
            PlayerRouteProfilePayload route,
            MissionRunCompletionState completionState,
            long expectedRunSequence,
            FakeExistingStatePort port)
        {
            return EndMissionRunCommand.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                completionState,
                expectedRunSequence);
        }

        internal static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        internal sealed class FakeExistingStatePort : IMissionRunExistingStatePort
        {
            public FakeExistingStatePort()
            {
                HoldingsFingerprint = MissionRun.Fingerprint("holdings-v1");
                OpeningFingerprint = MissionRun.Fingerprint("openings-v1");
            }

            public long HoldingsSequence = 17L;
            public string HoldingsFingerprint;
            public long OpeningSequence = 23L;
            public string OpeningFingerprint;
            public int VerifyCalls;
            public int ProjectCalls;
            public int RewardGrantCalls;
            public readonly Dictionary<StableId, MissionRunStrongboxState> States =
                new Dictionary<StableId, MissionRunStrongboxState>();
            public readonly List<EquipmentInstance> OpenedEquipmentRewards =
                new List<EquipmentInstance>();

            public MissionRunCollectionVerification VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommand command)
            {
                VerifyCalls++;
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
                IReadOnlyList<MissionRunStrongboxCollection> collectedStrongboxes)
            {
                ProjectCalls++;
                List<MissionRunStrongboxResult> results =
                    new List<MissionRunStrongboxResult>();
                for (int index = 0; index < collectedStrongboxes.Count; index++)
                {
                    MissionRunStrongboxCollection collection = collectedStrongboxes[index];
                    MissionRunStrongboxState state;
                    if (!States.TryGetValue(collection.InstanceStableId, out state))
                    {
                        state = MissionRunStrongboxState.Unopened;
                    }
                    results.Add(state == MissionRunStrongboxState.Unopened
                        ? new MissionRunStrongboxResult(collection, state, null, null)
                        : new MissionRunStrongboxResult(
                            collection,
                            state,
                            Id("box-opening", collection.InstanceStableId.Value),
                            MissionRun.Fingerprint(
                                "opened:" + collection.InstanceStableId)));
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
