using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.TerminalDropBinding
{
    public sealed class TerminalDropFactAdapterRegistryV1
    {
        private readonly Dictionary<Type, ITerminalDropFactAdapterV1> byType;

        public TerminalDropFactAdapterRegistryV1(
            IEnumerable<ITerminalDropFactAdapterV1> adapters)
        {
            if (adapters == null) throw new ArgumentNullException(nameof(adapters));
            var ordered = new List<ITerminalDropFactAdapterV1>();
            foreach (ITerminalDropFactAdapterV1 adapter in adapters)
            {
                if (adapter == null)
                    throw new ArgumentException("Adapter registrations cannot contain null.", nameof(adapters));
                ordered.Add(adapter);
            }
            ordered.Sort((left, right) => left.FactKindStableId.CompareTo(right.FactKindStableId));

            byType = new Dictionary<Type, ITerminalDropFactAdapterV1>();
            var kinds = new HashSet<StableId>();
            var canonical = new StringBuilder("schema=terminal-drop-adapter-registry-v1");
            for (int index = 0; index < ordered.Count; index++)
            {
                ITerminalDropFactAdapterV1 adapter = ordered[index];
                if (adapter.FactType == null || adapter.FactKindStableId == null)
                    throw new ArgumentException("Adapters require a fact type and kind identity.", nameof(adapters));
                if (byType.ContainsKey(adapter.FactType))
                    throw new ArgumentException(
                        "Duplicate terminal-drop adapter type: " + adapter.FactType.FullName,
                        nameof(adapters));
                if (!kinds.Add(adapter.FactKindStableId))
                    throw new ArgumentException(
                        "Duplicate terminal-drop fact kind: " + adapter.FactKindStableId,
                        nameof(adapters));
                byType.Add(adapter.FactType, adapter);
                TerminalDropCanonicalV1.Append(canonical, "kind-" + index, adapter.FactKindStableId);
                TerminalDropCanonicalV1.Append(
                    canonical,
                    "type-" + index,
                    adapter.FactType.AssemblyQualifiedName);
            }
            Fingerprint = TerminalDropCanonicalV1.Hash(canonical.ToString());
        }

        public string Fingerprint { get; }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            if (terminalFact == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.NullFact,
                    "terminal-drop-fact-null");
            }
            ITerminalDropFactAdapterV1 adapter;
            if (!byType.TryGetValue(terminalFact.GetType(), out adapter))
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.UnsupportedFactType,
                    "terminal-drop-unsupported-fact-type:" + terminalFact.GetType().FullName);
            }
            return adapter.Adapt(terminalFact)
                ?? TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "terminal-drop-adapter-returned-null");
        }
    }

    public sealed class RewardProfileCatalogResolverV1 : IRewardProfileResolverV1
    {
        private readonly Dictionary<StableId, RewardProfileV1> profiles;

        public RewardProfileCatalogResolverV1(IEnumerable<RewardProfileV1> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var ordered = new List<RewardProfileV1>();
            foreach (RewardProfileV1 profile in values)
            {
                if (profile == null)
                    throw new ArgumentException("Reward-profile catalogs cannot contain null.", nameof(values));
                ordered.Add(profile);
            }
            ordered.Sort((left, right) => left.ProfileStableId.CompareTo(right.ProfileStableId));
            profiles = new Dictionary<StableId, RewardProfileV1>();
            var canonical = new StringBuilder("schema=terminal-drop-profile-catalog-v1");
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardProfileV1 profile = ordered[index];
                if (profiles.ContainsKey(profile.ProfileStableId))
                    throw new ArgumentException(
                        "Duplicate reward profile: " + profile.ProfileStableId,
                        nameof(values));
                profiles.Add(profile.ProfileStableId, profile);
                TerminalDropCanonicalV1.Append(canonical, "profile-id-" + index, profile.ProfileStableId);
                TerminalDropCanonicalV1.Append(canonical, "profile-fingerprint-" + index, profile.Fingerprint);
            }
            Fingerprint = TerminalDropCanonicalV1.Hash(canonical.ToString());
        }

        public string Fingerprint { get; }

        public bool TryResolve(StableId profileStableId, out RewardProfileV1 profile)
        {
            profile = null;
            return profileStableId != null
                && profiles.TryGetValue(profileStableId, out profile)
                && profile != null;
        }
    }

    public sealed class EnemyDeathTerminalDropFactAdapterV1 : ITerminalDropFactAdapterV1
    {
        private readonly EnemyCatalogV1 catalog;

        public EnemyDeathTerminalDropFactAdapterV1(EnemyCatalogV1 catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public StableId FactKindStableId { get { return TerminalDropFactKindIdsV1.EnemyDeath; } }
        public Type FactType { get { return typeof(EnemyDeathFactV1); } }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            EnemyDeathFactV1 fact = terminalFact as EnemyDeathFactV1;
            if (fact == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "enemy-death-fact-type-mismatch");
            }

            EnemyDefinitionV1 definition;
            if (!catalog.TryGetDefinition(fact.DefinitionStableId, out definition))
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.MissingDefinition,
                    "enemy-definition-missing:" + fact.DefinitionStableId);
            }
            if (fact.Identity == null
                || fact.Identity.RunStableId == null
                || fact.Identity.EntityInstanceId == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "enemy-death-identity-incomplete");
            }
            if (fact.DropProfileStableId != null
                && definition.DropProfileId != fact.DropProfileStableId)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.DropProfileMismatch,
                    "enemy-drop-profile-mismatch:fact=" + fact.DropProfileStableId
                    + ";definition=" + (definition.DropProfileId == null
                        ? "none"
                        : definition.DropProfileId.ToString()));
            }

            var context = new StringBuilder("schema=enemy-terminal-drop-context-v1");
            TerminalDropCanonicalV1.Append(context, "run", fact.Identity.RunStableId);
            TerminalDropCanonicalV1.Append(context, "room-runtime", fact.Identity.RoomRuntimeInstanceStableId);
            TerminalDropCanonicalV1.Append(context, "room", fact.Identity.RoomStableId);
            TerminalDropCanonicalV1.Append(context, "placement", fact.Identity.PlacementStableId);
            TerminalDropCanonicalV1.Append(context, "entity", fact.Identity.EntityInstanceId);
            TerminalDropCanonicalV1.Append(context, "participant", fact.Identity.RunParticipantId);
            TerminalDropCanonicalV1.Append(context, "level", fact.Level);

            var upstream = new StringBuilder("schema=enemy-death-fact-drop-projection-v1");
            TerminalDropCanonicalV1.Append(upstream, "death-event", fact.DeathEventStableId);
            TerminalDropCanonicalV1.Append(upstream, "trigger", fact.TriggeringEventStableId);
            TerminalDropCanonicalV1.Append(upstream, "definition", fact.DefinitionStableId);
            TerminalDropCanonicalV1.Append(upstream, "level", fact.Level);
            TerminalDropCanonicalV1.Append(upstream, "generation", fact.LifecycleGeneration);
            TerminalDropCanonicalV1.Append(upstream, "killer-entity", fact.KillerEntityStableId);
            TerminalDropCanonicalV1.Append(upstream, "killer-participant", fact.KillerRunParticipantStableId);
            TerminalDropCanonicalV1.Append(upstream, "experience-profile", fact.ExperienceProfileStableId);
            TerminalDropCanonicalV1.Append(upstream, "drop-profile", fact.DropProfileStableId);
            TerminalDropCanonicalV1.Append(upstream, "death-cause", (int)fact.DeathCause);
            TerminalDropCanonicalV1.Append(upstream, "identity-context", context.ToString());

            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    FactKindStableId,
                    fact.DeathEventStableId,
                    fact.TriggeringEventStableId,
                    fact.Identity.RunStableId,
                    fact.LifecycleGeneration,
                    fact.Identity.EntityInstanceId,
                    fact.Identity.PlacementStableId,
                    fact.LifecycleGeneration,
                    fact.DefinitionStableId,
                    fact.KillerRunParticipantStableId,
                    fact.KillerEntityStableId,
                    null,
                    definition.DropProfileId,
                    TerminalDropCanonicalV1.Hash(context.ToString()),
                    definition.Fingerprint,
                    TerminalDropCanonicalV1.Hash(upstream.ToString())));
        }
    }

    public sealed class PropTerminalSourceContextV1
    {
        public PropTerminalSourceContextV1(
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            long sourceLifecycleGeneration,
            string fingerprint)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException("A source-context fingerprint is required.", nameof(fingerprint));
            RunLifecycleGeneration = runLifecycleGeneration;
            SourcePlacementStableId = sourcePlacementStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            Fingerprint = fingerprint.Trim();
        }

        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourcePlacementStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public string Fingerprint { get; }
    }

    public interface IPropTerminalSourceContextResolverV1
    {
        bool TryResolve(
            PropTerminalFactV1 terminalFact,
            out PropTerminalSourceContextV1 context,
            out string diagnostic);
    }

    public sealed class PropDestructionTerminalDropFactAdapterV1 : ITerminalDropFactAdapterV1
    {
        private readonly PropCatalogV1 catalog;
        private readonly IPropTerminalSourceContextResolverV1 sourceContextResolver;

        public PropDestructionTerminalDropFactAdapterV1(
            PropCatalogV1 catalog,
            IPropTerminalSourceContextResolverV1 sourceContextResolver)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.sourceContextResolver = sourceContextResolver
                ?? throw new ArgumentNullException(nameof(sourceContextResolver));
        }

        public StableId FactKindStableId { get { return TerminalDropFactKindIdsV1.PropDestruction; } }
        public Type FactType { get { return typeof(PropFactBatchV1); } }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            PropFactBatchV1 batch = terminalFact as PropFactBatchV1;
            if (batch == null || batch.Terminal == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "prop-terminal-fact-missing");
            }
            PropTerminalFactV1 terminal = batch.Terminal;
            PropDefinitionV1 definition;
            if (!catalog.TryGet(terminal.PropDefinitionId, out definition))
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.MissingDefinition,
                    "prop-definition-missing:" + terminal.PropDefinitionId);
            }

            StableId definitionProfile;
            string profileDiagnostic;
            if (!TryResolveDropProfile(definition, out definitionProfile, out profileDiagnostic))
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.DropProfileMismatch,
                    profileDiagnostic);
            }
            if (batch.DropRequest != null)
            {
                if (batch.DropRequest.KindId != PropFactKindIdsV1.DropRequest
                    || batch.DropRequest.PropParticipantId != terminal.PropParticipantId
                    || batch.DropRequest.SourceParticipantId != terminal.SourceParticipantId)
                {
                    return TerminalDropAdaptationResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidTerminalFact,
                        "prop-drop-request-does-not-belong-to-terminal-fact");
                }
                if (definitionProfile == null
                    || batch.DropRequest.ProfileOrFactId != definitionProfile)
                {
                    return TerminalDropAdaptationResultV1.Rejected(
                        TerminalDropRejectionCodeV1.DropProfileMismatch,
                        "prop-drop-profile-mismatch:fact=" + batch.DropRequest.ProfileOrFactId
                        + ";definition=" + (definitionProfile == null
                            ? "none"
                            : definitionProfile.ToString()));
                }
            }

            PropTerminalSourceContextV1 sourceContext;
            string sourceDiagnostic;
            if (!sourceContextResolver.TryResolve(terminal, out sourceContext, out sourceDiagnostic)
                || sourceContext == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.MissingSourceContext,
                    string.IsNullOrWhiteSpace(sourceDiagnostic)
                        ? "prop-source-context-missing"
                        : sourceDiagnostic);
            }

            var upstream = new StringBuilder("schema=prop-destruction-drop-projection-v1");
            TerminalDropCanonicalV1.Append(upstream, "terminal-fact", terminal.FactId);
            TerminalDropCanonicalV1.Append(upstream, "terminal-kind", terminal.KindId);
            TerminalDropCanonicalV1.Append(upstream, "prop", terminal.PropParticipantId);
            TerminalDropCanonicalV1.Append(upstream, "definition", terminal.PropDefinitionId);
            TerminalDropCanonicalV1.Append(upstream, "source", terminal.SourceParticipantId);
            TerminalDropCanonicalV1.Append(upstream, "source-faction", terminal.SourceFactionId);
            TerminalDropCanonicalV1.Append(upstream, "damage-channel", terminal.DamageChannelId);
            TerminalDropCanonicalV1.Append(upstream, "terminal-fingerprint", terminal.Fingerprint);
            TerminalDropCanonicalV1.Append(
                upstream,
                "drop-request-fingerprint",
                batch.DropRequest == null ? "none" : batch.DropRequest.Fingerprint);

            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    FactKindStableId,
                    terminal.FactId,
                    batch.DropRequest == null ? null : batch.DropRequest.FactId,
                    sourceContext.RunStableId,
                    sourceContext.RunLifecycleGeneration,
                    sourceContext.SourceEntityStableId,
                    sourceContext.SourcePlacementStableId,
                    sourceContext.SourceLifecycleGeneration,
                    terminal.PropDefinitionId,
                    terminal.SourceParticipantId,
                    terminal.SourceParticipantId,
                    terminal.DamageChannelId,
                    definitionProfile,
                    sourceContext.Fingerprint,
                    definition.Fingerprint,
                    TerminalDropCanonicalV1.Hash(upstream.ToString())));
        }

        private static bool TryResolveDropProfile(
            PropDefinitionV1 definition,
            out StableId profileId,
            out string diagnostic)
        {
            profileId = null;
            diagnostic = string.Empty;
            PropCapabilityV1 capability;
            if (!definition.TryGet(PropCapabilityIdsV1.DropOnDestroy, out capability))
                return true;
            string text;
            if (capability == null
                || !capability.TryGet("profile-id", out text)
                || string.IsNullOrWhiteSpace(text))
            {
                diagnostic = "prop-drop-capability-missing-profile:" + definition.DefinitionId;
                return false;
            }
            try
            {
                profileId = StableId.Parse(text.Trim());
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "prop-drop-capability-invalid-profile:"
                    + definition.DefinitionId + ":" + exception.Message;
                return false;
            }
        }
    }
}
