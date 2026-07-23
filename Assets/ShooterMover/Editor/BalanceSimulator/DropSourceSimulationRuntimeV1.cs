using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    /// <summary>
    /// Engine-neutral simulator consumer of the production profile resolver, personal
    /// generation service, pacing authority and tier catalog. It owns no copied reward
    /// probabilities, pity formula, saturation formula or tier-selection weights.
    /// </summary>
    public sealed class DropSourceSimulationRuntimeV1
    {
        private sealed class ParticipantAccumulator
        {
            public ParticipantAccumulator(StableId participantStableId)
            {
                ParticipantStableId = participantStableId;
            }

            public StableId ParticipantStableId;
            public long SourceAttempts;
            public long NoDropCount;
            public long MoneyQuantity;
            public long ScrapQuantity;
            public long MiscQuantity;
            public long RandomStrongboxCount;
            public long GuaranteedStrongboxCount;
            public long PityActivations;
            public long RoomSaturationActivations;
            public long RunSaturationActivations;
            public long RunMinimumGrants;
            public long RawStrongboxProbabilityMillionthsTotal;
            public long EffectiveStrongboxProbabilityMillionthsTotal;
            public readonly Dictionary<StableId, long> TierCounts =
                new Dictionary<StableId, long>();
            public ParticipantDropPacingStateV1 FinalPacingState;
        }

        private readonly RewardProfileResolverV1 profileResolver;

        public DropSourceSimulationRuntimeV1()
            : this(new RewardProfileResolverV1())
        {
        }

        public DropSourceSimulationRuntimeV1(
            RewardProfileResolverV1 profileResolver)
        {
            this.profileResolver = profileResolver
                ?? throw new ArgumentNullException(nameof(profileResolver));
        }

        public RewardSimulationReportV1 Run(
            DropSourceSimulationRequestV1 request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RewardSourceProfileV1 source =
                ProductionRewardSourceCatalogV1.Get(
                    request.SourceProfileReferenceId);
            RewardProfileResolutionV1 resolution = profileResolver.Resolve(
                request.SourceProfileReferenceId,
                source,
                request.GameModeOverride,
                request.MissionOverride,
                request.DifficultyOverride,
                request.EventOverrides,
                request.PlacementOverride);
            var totals = new Dictionary<StableId, ParticipantAccumulator>();
            for (int index = 0; index < request.Participants.Count; index++)
            {
                RewardSimulationParticipantInputV1 participant =
                    request.Participants[index];
                totals.Add(
                    participant.ParticipantStableId,
                    new ParticipantAccumulator(
                        participant.ParticipantStableId));
            }

            long rejected = 0L;
            for (int sampleOrdinal = 0;
                sampleOrdinal < request.SampleCount;
                sampleOrdinal++)
            {
                StableId runStableId = StableId.Create(
                    "simulation-run",
                    "drop-source-" + sampleOrdinal);
                var pacingAuthority = new ParticipantDropPacingAuthorityV1();
                var generation = new PersonalRewardGenerationServiceV1(
                    pacingAuthority);

                for (int roomOrdinal = 0;
                    roomOrdinal < request.RoomCount;
                    roomOrdinal++)
                {
                    StableId roomStableId = StableId.Create(
                        "simulation-room",
                        "sample-" + sampleOrdinal + "-room-" + roomOrdinal);
                    for (int sourceOrdinal = 0;
                        sourceOrdinal < request.SourcesPerRoom;
                        sourceOrdinal++)
                    {
                        StableId terminalStableId = StableId.Create(
                            "simulation-terminal",
                            "sample-" + sampleOrdinal
                                + "-room-" + roomOrdinal
                                + "-source-" + sourceOrdinal);
                        StableId placementStableId = StableId.Create(
                            "simulation-placement",
                            "sample-" + sampleOrdinal
                                + "-room-" + roomOrdinal
                                + "-source-" + sourceOrdinal);
                        string terminalFingerprint =
                            RewardGenerationFingerprintV1.Compute(
                                terminalStableId
                                    + "|"
                                    + roomStableId
                                    + "|"
                                    + placementStableId
                                    + "|"
                                    + resolution.Fingerprint);
                        var contexts = new List<PersonalRewardRollContextV1>(
                            request.Participants.Count);
                        for (int participantIndex = 0;
                            participantIndex < request.Participants.Count;
                            participantIndex++)
                        {
                            RewardSimulationParticipantInputV1 participant =
                                request.Participants[participantIndex];
                            contexts.Add(BuildContext(
                                request,
                                resolution,
                                participant,
                                runStableId,
                                roomStableId,
                                terminalStableId,
                                placementStableId,
                                terminalFingerprint,
                                sampleOrdinal,
                                roomOrdinal,
                                sourceOrdinal));
                        }

                        IReadOnlyList<PersonalRewardGenerationResultV1> results =
                            generation.GenerateForParticipants(contexts);
                        for (int resultIndex = 0;
                            resultIndex < results.Count;
                            resultIndex++)
                        {
                            PersonalRewardGenerationResultV1 result =
                                results[resultIndex];
                            ParticipantAccumulator accumulator =
                                totals[result.Context.ParticipantStableId];
                            accumulator.SourceAttempts++;
                            if (!result.IsSuccess)
                            {
                                rejected++;
                                continue;
                            }
                            Accumulate(accumulator, result, false);
                        }
                    }
                }

                for (int participantIndex = 0;
                    participantIndex < request.Participants.Count;
                    participantIndex++)
                {
                    RewardSimulationParticipantInputV1 participant =
                        request.Participants[participantIndex];
                    PersonalRewardRollContextV1 completionContext =
                        BuildCompletionContext(
                            request,
                            resolution,
                            participant,
                            runStableId,
                            sampleOrdinal);
                    PersonalRewardGenerationResultV1 completion =
                        generation.GenerateRunMinimum(completionContext);
                    ParticipantAccumulator accumulator =
                        totals[participant.ParticipantStableId];
                    if (!completion.IsSuccess)
                    {
                        rejected++;
                    }
                    else
                    {
                        Accumulate(accumulator, completion, true);
                    }
                    ParticipantDropPacingStateV1 finalState;
                    if (pacingAuthority.TryExport(
                            runStableId,
                            1,
                            participant.ParticipantStableId,
                            out finalState))
                    {
                        accumulator.FinalPacingState = finalState;
                    }
                }
            }

            var participantReports =
                new List<RewardSimulationParticipantReportV1>(totals.Count);
            foreach (ParticipantAccumulator accumulator in totals.Values)
            {
                participantReports.Add(
                    new RewardSimulationParticipantReportV1(
                        accumulator.ParticipantStableId,
                        accumulator.SourceAttempts,
                        accumulator.NoDropCount,
                        accumulator.MoneyQuantity,
                        accumulator.ScrapQuantity,
                        accumulator.MiscQuantity,
                        accumulator.RandomStrongboxCount,
                        accumulator.GuaranteedStrongboxCount,
                        accumulator.PityActivations,
                        accumulator.RoomSaturationActivations,
                        accumulator.RunSaturationActivations,
                        accumulator.RunMinimumGrants,
                        accumulator.RawStrongboxProbabilityMillionthsTotal,
                        accumulator.EffectiveStrongboxProbabilityMillionthsTotal,
                        accumulator.TierCounts,
                        accumulator.FinalPacingState));
            }
            participantReports.Sort();
            return new RewardSimulationReportV1(
                request.SourceProfileReferenceId,
                resolution.EffectiveProfile.Fingerprint,
                request.PacingPolicy.Fingerprint,
                request.MissionLevel,
                request.DifficultyStableId,
                request.GameModeStableId,
                request.SourcesPerRoom,
                request.RoomCount,
                request.SampleCount,
                request.Seed,
                participantReports,
                rejected,
                string.Empty);
        }

        private static PersonalRewardRollContextV1 BuildContext(
            DropSourceSimulationRequestV1 request,
            RewardProfileResolutionV1 resolution,
            RewardSimulationParticipantInputV1 participant,
            StableId runStableId,
            StableId roomStableId,
            StableId terminalStableId,
            StableId placementStableId,
            string terminalFingerprint,
            int sampleOrdinal,
            int roomOrdinal,
            int sourceOrdinal)
        {
            StableId seedIdentity = StableId.Create(
                "simulation-seed",
                "sample-" + sampleOrdinal
                    + "-room-" + roomOrdinal
                    + "-source-" + sourceOrdinal
                    + "-participant-"
                    + participant.ParticipantStableId.Value);
            ulong personalSeed = request.Seed
                ^ RewardGenerationFingerprintV1.StableOrdinal(seedIdentity);
            return new PersonalRewardRollContextV1(
                runStableId,
                1,
                terminalStableId,
                1,
                roomStableId,
                1,
                placementStableId,
                participant.ParticipantStableId,
                true,
                participant.PlayerLevel,
                request.MissionLevel,
                request.DifficultyStableId,
                request.GameModeStableId,
                request.EventModifierIds,
                request.MoneyQuantityMultiplierPermille,
                request.ScrapQuantityMultiplierPermille,
                resolution,
                request.PacingPolicy,
                terminalFingerprint,
                personalSeed,
                1);
        }

        private static PersonalRewardRollContextV1 BuildCompletionContext(
            DropSourceSimulationRequestV1 request,
            RewardProfileResolutionV1 resolution,
            RewardSimulationParticipantInputV1 participant,
            StableId runStableId,
            int sampleOrdinal)
        {
            StableId terminalStableId = StableId.Create(
                "simulation-terminal",
                "sample-" + sampleOrdinal + "-run-completion");
            StableId roomStableId = StableId.Create(
                "simulation-room",
                "sample-" + sampleOrdinal + "-run-completion");
            StableId placementStableId = StableId.Create(
                "simulation-placement",
                "sample-" + sampleOrdinal + "-run-completion");
            string fingerprint = RewardGenerationFingerprintV1.Compute(
                terminalStableId + "|completion|" + resolution.Fingerprint);
            return new PersonalRewardRollContextV1(
                runStableId,
                1,
                terminalStableId,
                1,
                roomStableId,
                1,
                placementStableId,
                participant.ParticipantStableId,
                true,
                participant.PlayerLevel,
                request.MissionLevel,
                request.DifficultyStableId,
                request.GameModeStableId,
                request.EventModifierIds,
                request.MoneyQuantityMultiplierPermille,
                request.ScrapQuantityMultiplierPermille,
                resolution,
                request.PacingPolicy,
                fingerprint,
                request.Seed
                    ^ RewardGenerationFingerprintV1.StableOrdinal(
                        terminalStableId)
                    ^ RewardGenerationFingerprintV1.StableOrdinal(
                        participant.ParticipantStableId),
                1);
        }

        private static void Accumulate(
            ParticipantAccumulator accumulator,
            PersonalRewardGenerationResultV1 result,
            bool runMinimum)
        {
            if (result.Grants.Count == 0 && !runMinimum)
            {
                accumulator.NoDropCount++;
            }
            for (int decisionIndex = 0;
                decisionIndex < result.Decisions.Count;
                decisionIndex++)
            {
                PersonalRewardDecisionV1 decision = result.Decisions[decisionIndex];
                accumulator.RawStrongboxProbabilityMillionthsTotal +=
                    decision.RawStrongboxProbabilityMillionths;
                accumulator.EffectiveStrongboxProbabilityMillionthsTotal +=
                    decision.EffectiveStrongboxProbabilityMillionths;
                if (decision.PityApplied) accumulator.PityActivations++;
                if (decision.RoomSaturationApplied)
                    accumulator.RoomSaturationActivations++;
                if (decision.RunSaturationApplied)
                    accumulator.RunSaturationActivations++;
                accumulator.RandomStrongboxCount +=
                    decision.GeneratedRandomBoxCount;
                accumulator.GuaranteedStrongboxCount +=
                    decision.GeneratedGuaranteedBoxCount;
            }

            for (int grantIndex = 0;
                grantIndex < result.Grants.Count;
                grantIndex++)
            {
                RewardGrantV1 grant = result.Grants[grantIndex];
                switch (grant.Kind)
                {
                    case RewardGrantKindV1.Money:
                        accumulator.MoneyQuantity += grant.Quantity;
                        break;
                    case RewardGrantKindV1.Scrap:
                        accumulator.ScrapQuantity += grant.Quantity;
                        break;
                    case RewardGrantKindV1.Miscellaneous:
                        accumulator.MiscQuantity += grant.Quantity;
                        break;
                    case RewardGrantKindV1.Strongbox:
                        long current;
                        accumulator.TierCounts.TryGetValue(
                            grant.ContentStableId,
                            out current);
                        accumulator.TierCounts[grant.ContentStableId] =
                            checked(current + grant.Quantity);
                        if (runMinimum)
                        {
                            accumulator.RunMinimumGrants += grant.Quantity;
                            accumulator.GuaranteedStrongboxCount += grant.Quantity;
                        }
                        break;
                }
            }
        }
    }
}
