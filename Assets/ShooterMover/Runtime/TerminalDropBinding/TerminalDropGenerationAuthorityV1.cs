using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Atomic engine-neutral boundary from one accepted terminal fact to one immutable
    /// deterministic reward batch. It owns replay records only; DROP/GEN own reward
    /// operation and generation behavior.
    /// </summary>
    public sealed class TerminalDropGenerationAuthorityV1
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(string sourceFingerprint, GeneratedTerminalDropResultV1 result)
            {
                SourceFingerprint = sourceFingerprint;
                Result = result;
            }

            public string SourceFingerprint { get; }
            public GeneratedTerminalDropResultV1 Result { get; }
        }

        private readonly object gate = new object();
        private readonly TerminalDropFactAdapterRegistryV1 adapters;
        private readonly ITerminalDropRunContextResolverV1 runContexts;
        private readonly IRewardProfileResolverV1 profiles;
        private readonly IRewardGenerationExecutorV1 generator;
        private readonly Dictionary<StableId, ReplayRecord> replay =
            new Dictionary<StableId, ReplayRecord>();

        public TerminalDropGenerationAuthorityV1(
            TerminalDropFactAdapterRegistryV1 adapters,
            ITerminalDropRunContextResolverV1 runContexts,
            IRewardProfileResolverV1 profiles,
            IRewardGenerationExecutorV1 generator)
        {
            this.adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
            this.runContexts = runContexts ?? throw new ArgumentNullException(nameof(runContexts));
            this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public int AcceptedBatchCount
        {
            get
            {
                lock (gate)
                {
                    return replay.Count;
                }
            }
        }

        public GeneratedTerminalDropResultV1 Generate(object terminalFact)
        {
            TerminalDropAdaptationResultV1 adaptation = adapters.Adapt(terminalFact);
            if (adaptation == null || !adaptation.Succeeded)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    adaptation == null
                        ? TerminalDropRejectionCodeV1.InvalidTerminalFact
                        : adaptation.RejectionCode,
                    null,
                    adaptation == null
                        ? "terminal-drop-adaptation-returned-null"
                        : adaptation.Diagnostic);
            }

            TerminalDropSourceFactV1 source = adaptation.SourceFact;
            lock (gate)
            {
                ReplayRecord existing;
                if (replay.TryGetValue(source.TerminalEventStableId, out existing))
                {
                    if (string.Equals(
                        existing.SourceFingerprint,
                        source.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return existing.Result.AsExactReplay();
                    }
                    return GeneratedTerminalDropResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidTerminalFact,
                        source,
                        "terminal-drop-event-identity-conflict",
                        true);
                }

                if (source.AttributedParticipantStableId == null)
                {
                    return GeneratedTerminalDropResultV1.Rejected(
                        TerminalDropRejectionCodeV1.UnattributedTerminalFact,
                        source,
                        "terminal-drop-unattributed-source");
                }

                TerminalDropRunGenerationContextV1 runContext;
                TerminalDropRejectionCodeV1 runRejection;
                string runDiagnostic;
                if (!runContexts.TryResolve(
                    source.RunStableId,
                    source.RunLifecycleGeneration,
                    out runContext,
                    out runRejection,
                    out runDiagnostic)
                    || runContext == null)
                {
                    return GeneratedTerminalDropResultV1.Rejected(
                        runRejection == TerminalDropRejectionCodeV1.None
                            ? TerminalDropRejectionCodeV1.MissingRun
                            : runRejection,
                        source,
                        string.IsNullOrWhiteSpace(runDiagnostic)
                            ? "terminal-drop-run-context-resolution-failed"
                            : runDiagnostic);
                }

                RewardProfileV1 profile;
                if (source.DeclaredDropProfileStableId == null)
                {
                    StableId noDropProfileId = RewardApplicationCanonicalV1.DeriveStableId(
                        "terminaldropnoprofile",
                        source.FactKindStableId.ToString(),
                        source.SourceDefinitionStableId.ToString());
                    profile = RewardProfileV1.CreateExplicitNoDrop(noDropProfileId);
                }
                else if (!profiles.TryResolve(source.DeclaredDropProfileStableId, out profile)
                    || profile == null)
                {
                    return GeneratedTerminalDropResultV1.Rejected(
                        TerminalDropRejectionCodeV1.MissingDropProfile,
                        source,
                        "terminal-drop-profile-missing:" + source.DeclaredDropProfileStableId);
                }

                RewardOperationRequestV1 operation = BuildOperation(source, runContext, profile);
                ulong generationSeed = TerminalDropCanonicalV1.DeriveSeed(
                    runContext.RootSeed,
                    operation.Fingerprint + "|" + source.Fingerprint);
                RewardGenerationRequestV1 generationRequest =
                    RewardGenerationRequestV1.Create(
                        operation,
                        profile,
                        runContext.ProgressionContext,
                        generationSeed,
                        runContext.GenerationAlgorithmVersion);

                RewardGenerationResultEnvelopeV1 envelope;
                try
                {
                    envelope = generator.Generate(generationRequest);
                }
                catch (Exception exception)
                {
                    return GeneratedTerminalDropResultV1.Rejected(
                        TerminalDropRejectionCodeV1.GenerationFailed,
                        source,
                        "terminal-drop-generation-exception:" + exception.Message);
                }
                if (envelope == null || !envelope.IsSuccess || envelope.Result == null)
                {
                    return GeneratedTerminalDropResultV1.Rejected(
                        TerminalDropRejectionCodeV1.GenerationFailed,
                        source,
                        envelope == null
                            ? "terminal-drop-generation-returned-null"
                            : "terminal-drop-generation-rejected:" + envelope.FailureReason);
                }

                List<GeneratedTerminalDropRewardV1> children;
                try
                {
                    children = BuildChildren(operation, envelope.Result);
                }
                catch (Exception exception)
                {
                    return GeneratedTerminalDropResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidGeneratedBatch,
                        source,
                        "terminal-drop-child-materialization-failed:" + exception.Message);
                }

                TerminalDropBindingStatusV1 status = envelope.Status
                    == RewardGenerationStatusV1.ExplicitNoDrop
                    ? TerminalDropBindingStatusV1.ExplicitNoDrop
                    : TerminalDropBindingStatusV1.Accepted;
                string fingerprint = BuildBatchFingerprint(
                    source,
                    runContext,
                    profile,
                    operation,
                    generationSeed,
                    envelope,
                    children);
                var accepted = new GeneratedTerminalDropResultV1(
                    status,
                    TerminalDropRejectionCodeV1.None,
                    source,
                    profile.ProfileStableId,
                    operation,
                    generationSeed,
                    envelope,
                    children,
                    fingerprint,
                    string.Empty);

                replay.Add(
                    source.TerminalEventStableId,
                    new ReplayRecord(source.Fingerprint, accepted));
                return accepted;
            }
        }

        private static RewardOperationRequestV1 BuildOperation(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            RewardProfileV1 profile)
        {
            string placement = source.SourcePlacementStableId == null
                ? "none"
                : source.SourcePlacementStableId.ToString();
            StableId operationId = RewardApplicationCanonicalV1.DeriveStableId(
                "terminaldropoperation",
                source.TerminalEventStableId.ToString(),
                source.RunStableId.ToString(),
                source.RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture),
                source.SourceEntityStableId.ToString(),
                placement,
                source.SourceLifecycleGeneration.ToString(CultureInfo.InvariantCulture),
                source.SourceDefinitionStableId.ToString(),
                profile.ProfileStableId.ToString(),
                source.AttributedParticipantStableId.ToString(),
                source.UpstreamFactFingerprint,
                runContext.Fingerprint);
            StableId commitmentId = RewardApplicationCanonicalV1.DeriveStableId(
                "terminaldropcommitment",
                operationId.ToString(),
                source.TerminalEventStableId.ToString());
            return RewardOperationRequestV1.Create(
                source.RunStableId,
                source.SourceEntityStableId,
                operationId,
                commitmentId,
                profile.ProfileStableId,
                profile.Fingerprint);
        }

        private static List<GeneratedTerminalDropRewardV1> BuildChildren(
            RewardOperationRequestV1 operation,
            RewardResultV1 result)
        {
            var children = new List<GeneratedTerminalDropRewardV1>();
            int ordinal = 0;
            for (int grantIndex = 0; grantIndex < result.Grants.Count; grantIndex++)
            {
                RewardGrantV1 grant = result.Grants[grantIndex];
                bool unique = grant.Kind == RewardGrantKindV1.Strongbox
                    || grant.Kind == RewardGrantKindV1.EquipmentReference;
                if (unique)
                {
                    if (grant.Quantity > int.MaxValue)
                        throw new InvalidOperationException(
                            "Unique reward quantity exceeds deterministic child ordinal capacity.");
                    for (long unitIndex = 0L; unitIndex < grant.Quantity; unitIndex++)
                    {
                        children.Add(BuildChild(
                            operation,
                            grant,
                            ordinal,
                            unitIndex,
                            1L));
                        ordinal = checked(ordinal + 1);
                    }
                }
                else
                {
                    children.Add(BuildChild(operation, grant, ordinal, 0L, grant.Quantity));
                    ordinal = checked(ordinal + 1);
                }
            }
            return children;
        }

        private static GeneratedTerminalDropRewardV1 BuildChild(
            RewardOperationRequestV1 operation,
            RewardGrantV1 grant,
            int ordinal,
            long unitIndex,
            long quantity)
        {
            StableId childId = RewardApplicationCanonicalV1.DeriveStableId(
                "terminaldropchild",
                operation.SourceOperationStableId.ToString(),
                grant.GrantStableId.ToString(),
                ((int)grant.Kind).ToString(CultureInfo.InvariantCulture),
                grant.ContentStableId.ToString(),
                unitIndex.ToString(CultureInfo.InvariantCulture));
            return new GeneratedTerminalDropRewardV1(
                childId,
                ordinal,
                grant.GrantStableId,
                grant.Kind,
                grant.ContentStableId,
                quantity);
        }

        private static string BuildBatchFingerprint(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            RewardProfileV1 profile,
            RewardOperationRequestV1 operation,
            ulong generationSeed,
            RewardGenerationResultEnvelopeV1 envelope,
            IReadOnlyList<GeneratedTerminalDropRewardV1> children)
        {
            var builder = new StringBuilder("schema=generated-terminal-drop-batch-v1");
            TerminalDropCanonicalV1.Append(builder, "source", source.Fingerprint);
            TerminalDropCanonicalV1.Append(builder, "run-context", runContext.Fingerprint);
            TerminalDropCanonicalV1.Append(builder, "profile", profile.Fingerprint);
            TerminalDropCanonicalV1.Append(builder, "operation", operation.Fingerprint);
            TerminalDropCanonicalV1.Append(builder, "generation-seed", generationSeed);
            TerminalDropCanonicalV1.Append(builder, "generation-result", envelope.ResultFingerprint);
            TerminalDropCanonicalV1.Append(builder, "child-count", children.Count);
            for (int index = 0; index < children.Count; index++)
                TerminalDropCanonicalV1.Append(builder, "child-" + index, children[index].Fingerprint);
            return TerminalDropCanonicalV1.Hash(builder.ToString());
        }
    }
}
