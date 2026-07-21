using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;

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
        private readonly Func<long> runLifecycleGenerationExporter;
        private readonly Func<ProductionCharacterRuntimeGraphV1,
            IStrongboxMissionResultApplicationAuthorityPortV1>
                authorityPortFactory;
        private readonly Dictionary<StableId, ReplayRecord> replay =
            new Dictionary<StableId, ReplayRecord>();

        public StrongboxMissionResultApplicationCoordinatorV1(
            CharacterCompositionCoordinatorV1 composition,
            Func<long> runLifecycleGenerationExporter,
            Func<ProductionCharacterRuntimeGraphV1,
                IStrongboxMissionResultApplicationAuthorityPortV1>
                    authorityPortFactory = null)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.runLifecycleGenerationExporter =
                runLifecycleGenerationExporter
                ?? throw new ArgumentNullException(
                    nameof(runLifecycleGenerationExporter));
            this.authorityPortFactory = authorityPortFactory
                ?? (graph =>
                    new ExistingStrongboxMissionResultApplicationAuthorityPortV1(
                        graph));
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
                if (prior.Result.Succeeded)
                {
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
                if (!prior.Result.ExactRetryAllowed)
                {
                    return prior.Result;
                }
            }

            StrongboxMissionResultApplicationResultV1 result =
                ApplyFirst(command);
            replay[command.OperationStableId] =
                new ReplayRecord(command.Fingerprint, result);
            return result;
        }

        private StrongboxMissionResultApplicationResultV1 ApplyFirst(
            StrongboxMissionResultApplicationCommandV1 command)
        {
            try
            {
                TransferPlan plan;
                string rejection;
                if (!TryCreatePlan(command, out plan, out rejection))
                {
                    return Reject(command, rejection);
                }
                return ExecutePlan(command, plan);
            }
            catch (Exception exception)
            {
                return RejectRetryable(
                    command,
                    "box-transfer-preflight-exception-"
                        + exception.GetType().Name.ToLowerInvariant());
            }
        }
    }
}
