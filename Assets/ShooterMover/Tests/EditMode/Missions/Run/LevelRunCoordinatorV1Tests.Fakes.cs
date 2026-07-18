using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1Tests
    {
        private sealed class CatalogValidator : IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public CatalogValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog;
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    request == null ? null : request.Instance,
                    catalog.ValidateInstance(
                        request == null ? null : request.Instance));
            }
        }

        private sealed class FixedRunIdFactory :
            ILevelRunStableIdFactoryV1
        {
            private readonly StableId runStableId;

            public FixedRunIdFactory(StableId runStableId)
            {
                this.runStableId = runStableId;
            }

            public StableId CreateRunStableId()
            {
                return runStableId;
            }
        }

        private sealed class FakeExistingAuthorityPort :
            IMissionRunExistingAuthorityPortV1
        {
            public FakeExistingAuthorityPort()
            {
                HoldingsFingerprint =
                    MissionRunCanonicalV1.Fingerprint("holdings-v1");
                OpeningFingerprint =
                    MissionRunCanonicalV1.Fingerprint("openings-v1");
            }

            public long HoldingsSequence = 4L;
            public string HoldingsFingerprint;
            public long OpeningSequence = 7L;
            public string OpeningFingerprint;
            public int ProjectCalls;

            public MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommandV1 command)
            {
                return MissionRunCollectionVerificationV1.Reject(
                    "level-run-test-does-not-collect-boxes");
            }

            public MissionRunStrongboxProjectionV1 ProjectStrongboxStates(
                EndMissionRunCommandV1 command,
                IReadOnlyList<MissionRunStrongboxCollectionV1> collected)
            {
                ProjectCalls++;
                return MissionRunStrongboxProjectionV1.Accept(
                    Array.Empty<MissionRunStrongboxResultV1>(),
                    command.ExpectedHoldingsSequence,
                    command.ExpectedHoldingsFingerprint,
                    command.ExpectedStrongboxOpeningSequence,
                    command.ExpectedStrongboxOpeningFingerprint);
            }
        }
    }
}
