using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxMissionResultApplicationCoordinatorV1
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string fingerprint,
                StrongboxMissionResultApplicationResultV1 result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public StrongboxMissionResultApplicationResultV1 Result { get; }
        }

        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly Dictionary<StableId, ReplayRecord> replay =
            new Dictionary<StableId, ReplayRecord>();

        public StrongboxMissionResultApplicationCoordinatorV1(
            CharacterCompositionCoordinatorV1 composition)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
        }

        public StrongboxMissionResultApplicationResultV1 Apply(
            StrongboxMissionResultApplicationCommandV1 command)
        {
            if (command == null)
            {
                return Reject(
                    null,
                    string.Empty,
                    string.Empty,
                    "box-transfer-command-null");
            }

            ReplayRecord prior;
            if (replay.TryGetValue(command.OperationStableId, out prior))
            {
                if (!string.Equals(
                    prior.Fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new StrongboxMissionResultApplicationResultV1(
                        StrongboxMissionResultApplicationStatusV1
                            .ConflictingDuplicate,
                        command.OperationStableId,
                        command.Fingerprint,
                        command.TerminalResult.Fingerprint,
                        0,
                        string.Empty,
                        string.Empty,
                        composition.Account == null
                            ? string.Empty
                            : composition.Account.Fingerprint,
                        "box-transfer-operation-conflicting-duplicate");
                }
                if (!prior.Result.Succeeded)
                {
                    return prior.Result;
                }
                return new StrongboxMissionResultApplicationResultV1(
                    StrongboxMissionResultApplicationStatusV1.ExactReplay,
                    command.OperationStableId,
                    command.Fingerprint,
                    prior.Result.ResultFingerprint,
                    prior.Result.TransferredCount,
                    prior.Result.HoldingsFingerprint,
                    prior.Result.StrongboxFingerprint,
                    prior.Result.AccountFingerprint,
                    string.Empty);
            }

            StrongboxMissionResultApplicationResultV1 result =
                ApplyFirst(command);
            replay.Add(
                command.OperationStableId,
                new ReplayRecord(command.Fingerprint, result));
            return result;
        }

        private StrongboxMissionResultApplicationResultV1 ApplyFirst(
            StrongboxMissionResultApplicationCommandV1 command)
        {
            TransferPlan plan;
            string rejection;
            if (!TryCreatePlan(command, out plan, out rejection))
            {
                return Reject(command, rejection);
            }
            return ExecutePlan(command, plan);
        }
    }
}
