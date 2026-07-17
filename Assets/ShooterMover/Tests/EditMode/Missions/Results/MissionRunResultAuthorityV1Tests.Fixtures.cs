using System.Collections.Generic;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Missions.Results
{
    public sealed partial class MissionRunResultAuthorityV1Tests
    {
        internal static PlayerRouteProfilePayloadV1 CreateRoute(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
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

        internal static MissionRunCollectStrongboxCommandV1 CreateCollection(
            string operation,
            string run,
            PlayerRouteProfilePayloadV1 route,
            string definition,
            string instance,
            long expectedRunSequence,
            FakeExistingAuthorityPort port)
        {
            return MissionRunCollectStrongboxCommandV1.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                Id("strongbox", definition),
                Id("box-instance", instance),
                Id("reward-grant", operation),
                Id("reward-source", operation),
                expectedRunSequence,
                port.HoldingsSequence,
                port.HoldingsFingerprint);
        }

        internal static EndMissionRunCommandV1 CreateEnd(
            string operation,
            string run,
            PlayerRouteProfilePayloadV1 route,
            MissionRunCompletionStateV1 completionState,
            long expectedRunSequence,
            FakeExistingAuthorityPort port)
        {
            return EndMissionRunCommandV1.Create(
                Id("run-operation", operation),
                Id("run", run),
                route,
                completionState,
                expectedRunSequence,
                port.HoldingsSequence,
                port.HoldingsFingerprint,
                port.OpeningSequence,
                port.OpeningFingerprint);
        }

        internal static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        internal sealed class FakeExistingAuthorityPort : IMissionRunExistingAuthorityPortV1
        {
            public FakeExistingAuthorityPort()
            {
                HoldingsFingerprint = MissionRunCanonicalV1.Fingerprint("holdings-v1");
                OpeningFingerprint = MissionRunCanonicalV1.Fingerprint("openings-v1");
            }

            public long HoldingsSequence = 17L;
            public string HoldingsFingerprint;
            public long OpeningSequence = 23L;
            public string OpeningFingerprint;
            public int VerifyCalls;
            public int ProjectCalls;
            public int RewardGrantCalls;
            public readonly Dictionary<StableId, MissionRunStrongboxStateV1> States =
                new Dictionary<StableId, MissionRunStrongboxStateV1>();
            public readonly List<EquipmentInstance> OpenedEquipmentRewards =
                new List<EquipmentInstance>();

            public MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommandV1 command)
            {
                VerifyCalls++;
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
                IReadOnlyList<MissionRunStrongboxCollectionV1> collectedStrongboxes)
            {
                ProjectCalls++;
                List<MissionRunStrongboxResultV1> results =
                    new List<MissionRunStrongboxResultV1>();
                for (int index = 0; index < collectedStrongboxes.Count; index++)
                {
                    MissionRunStrongboxCollectionV1 collection = collectedStrongboxes[index];
                    MissionRunStrongboxStateV1 state;
                    if (!States.TryGetValue(collection.InstanceStableId, out state))
                    {
                        state = MissionRunStrongboxStateV1.Unopened;
                    }
                    results.Add(state == MissionRunStrongboxStateV1.Unopened
                        ? new MissionRunStrongboxResultV1(collection, state, null, null)
                        : new MissionRunStrongboxResultV1(
                            collection,
                            state,
                            Id("box-opening", collection.InstanceStableId.Value),
                            MissionRunCanonicalV1.Fingerprint(
                                "opened:" + collection.InstanceStableId)));
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
