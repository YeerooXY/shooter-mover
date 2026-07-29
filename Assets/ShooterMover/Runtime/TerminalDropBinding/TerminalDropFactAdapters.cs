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
    public sealed class TerminalDropFactBridgeRegistry
    {
        private readonly Dictionary<Type, ITerminalDropFactBridge> byType;

        public TerminalDropFactBridgeRegistry(
            IEnumerable<ITerminalDropFactBridge> adapters)
        {
            if (adapters == null) throw new ArgumentNullException(nameof(adapters));
            var ordered = new List<ITerminalDropFactBridge>();
            foreach (ITerminalDropFactBridge adapter in adapters)
            {
                if (adapter == null)
                    throw new ArgumentException("Adapter registrations cannot contain null.", nameof(adapters));
                ordered.Add(adapter);
            }
            ordered.Sort((left, right) => left.FactKindStableId.CompareTo(right.FactKindStableId));

            byType = new Dictionary<Type, ITerminalDropFactBridge>();
            var kinds = new HashSet<StableId>();
            var canonical = new StringBuilder("schema=terminal-drop-adapter-registry-v1");
            for (int index = 0; index < ordered.Count; index++)
            {
                ITerminalDropFactBridge adapter = ordered[index];
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
                TerminalDrop.Append(canonical, "kind-" + index, adapter.FactKindStableId);
                TerminalDrop.Append(
                    canonical,
                    "type-" + index,
                    adapter.FactType.AssemblyQualifiedName);
            }
            Fingerprint = TerminalDrop.Hash(canonical.ToString());
        }

        public string Fingerprint { get; }

        public TerminalDropAdaptationResult Adapt(object terminalFact)
        {
            if (terminalFact == null)
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.NullFact,
                    "terminal-drop-fact-null");
            }
            ITerminalDropFactBridge adapter;
            if (!byType.TryGetValue(terminalFact.GetType(), out adapter))
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.UnsupportedFactType,
                    "terminal-drop-unsupported-fact-type:" + terminalFact.GetType().FullName);
            }
            return adapter.Adapt(terminalFact)
                ?? TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.InvalidTerminalFact,
                    "terminal-drop-adapter-returned-null");
        }
    }

    public sealed class RewardProfileCatalogResolver : IRewardProfileResolver
    {
        private readonly Dictionary<StableId, RewardProfile> profiles;

        public RewardProfileCatalogResolver(IEnumerable<RewardProfile> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var ordered = new List<RewardProfile>();
            foreach (RewardProfile profile in values)
            {
                if (profile == null)
                    throw new ArgumentException("Reward-profile catalogs cannot contain null.", nameof(values));
                ordered.Add(profile);
            }
            ordered.Sort((left, right) => left.ProfileStableId.CompareTo(right.ProfileStableId));
            profiles = new Dictionary<StableId, RewardProfile>();
            var canonical = new StringBuilder("schema=terminal-drop-profile-catalog-v1");
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardProfile profile = ordered[index];
                if (profiles.ContainsKey(profile.ProfileStableId))
                    throw new ArgumentException(
                        "Duplicate reward profile: " + profile.ProfileStableId,
                        nameof(values));
                profiles.Add(profile.ProfileStableId, profile);
                TerminalDrop.Append(canonical, "profile-id-" + index, profile.ProfileStableId);
                TerminalDrop.Append(canonical, "profile-fingerprint-" + index, profile.Fingerprint);
            }
            Fingerprint = TerminalDrop.Hash(canonical.ToString());
        }

        public string Fingerprint { get; }

        public bool TryResolve(StableId profileStableId, out RewardProfile profile)
        {
            profile = null;
            return profileStableId != null
                && profiles.TryGetValue(profileStableId, out profile)
                && profile != null;
        }
    }

    internal sealed class EnemyDeathTerminalDropDefinitionView
    {
        public EnemyDeathTerminalDropDefinitionView(
            EnemyDeathFact fact,
            StableId declaredDropProfileStableId,
            string definitionFingerprint,
            string upstreamFactFingerprint)
        {
            Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            DeclaredDropProfileStableId = declaredDropProfileStableId;
            DefinitionFingerprint = definitionFingerprint
                ?? throw new ArgumentNullException(nameof(definitionFingerprint));
            UpstreamFactFingerprint = upstreamFactFingerprint
                ?? throw new ArgumentNullException(nameof(upstreamFactFingerprint));
        }

        public EnemyDeathFact Fact { get; }
        public StableId DeclaredDropProfileStableId { get; }
        public string DefinitionFingerprint { get; }
        public string UpstreamFactFingerprint { get; }
    }

    internal sealed class EnemyDeathTerminalDropDefinitionViewResult
    {
        private EnemyDeathTerminalDropDefinitionViewResult(
            EnemyDeathTerminalDropDefinitionView projection,
            TerminalDropRejectionCode rejectionCode,
            string diagnostic)
        {
            Projection = projection;
            RejectionCode = rejectionCode;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public EnemyDeathTerminalDropDefinitionView Projection { get; }
        public TerminalDropRejectionCode RejectionCode { get; }
        public string Diagnostic { get; }
        public bool Succeeded { get { return Projection != null; } }

        public static EnemyDeathTerminalDropDefinitionViewResult Accepted(
            EnemyDeathTerminalDropDefinitionView projection)
        {
            return new EnemyDeathTerminalDropDefinitionViewResult(
                projection ?? throw new ArgumentNullException(nameof(projection)),
                TerminalDropRejectionCode.None,
                string.Empty);
        }

        public static EnemyDeathTerminalDropDefinitionViewResult Rejected(
            TerminalDropRejectionCode code,
            string diagnostic)
        {
            return new EnemyDeathTerminalDropDefinitionViewResult(
                null,
                code,
                diagnostic);
        }
    }

    /// <summary>
    /// Internal catalog-only projection. It validates definition/profile ownership but
    /// deliberately cannot construct a complete terminal-drop source fact because it
    /// does not own Run Session lifecycle context.
    /// </summary>
    internal sealed class EnemyDeathTerminalDropDefinitionProjector
    {
        private readonly EnemyCatalog catalog;

        public EnemyDeathTerminalDropDefinitionProjector(EnemyCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public EnemyDeathTerminalDropDefinitionViewResult Project(
            EnemyDeathFact fact)
        {
            if (fact == null)
            {
                return EnemyDeathTerminalDropDefinitionViewResult.Rejected(
                    TerminalDropRejectionCode.InvalidTerminalFact,
                    "enemy-death-fact-type-mismatch");
            }

            EnemyDefinition definition;
            if (!catalog.TryGetDefinition(fact.DefinitionStableId, out definition))
            {
                return EnemyDeathTerminalDropDefinitionViewResult.Rejected(
                    TerminalDropRejectionCode.MissingDefinition,
                    "enemy-definition-missing:" + fact.DefinitionStableId);
            }
            if (fact.Identity == null
                || fact.Identity.RunStableId == null
                || fact.Identity.EntityInstanceId == null
                || fact.Identity.PlacementStableId == null)
            {
                return EnemyDeathTerminalDropDefinitionViewResult.Rejected(
                    TerminalDropRejectionCode.InvalidTerminalFact,
                    "enemy-death-identity-incomplete");
            }
            if (fact.DropProfileStableId != null
                && definition.DropProfileId != fact.DropProfileStableId)
            {
                return EnemyDeathTerminalDropDefinitionViewResult.Rejected(
                    TerminalDropRejectionCode.DropProfileMismatch,
                    "enemy-drop-profile-mismatch:fact=" + fact.DropProfileStableId
                    + ";definition=" + (definition.DropProfileId == null
                        ? "none"
                        : definition.DropProfileId.ToString()));
            }

            var upstream = new StringBuilder("schema=enemy-death-fact-drop-projection-v2");
            TerminalDrop.Append(upstream, "death-event", fact.DeathEventStableId);
            TerminalDrop.Append(upstream, "trigger", fact.TriggeringEventStableId);
            TerminalDrop.Append(upstream, "definition", fact.DefinitionStableId);
            TerminalDrop.Append(upstream, "level", fact.Level);
            TerminalDrop.Append(upstream, "source-generation", fact.LifecycleGeneration);
            TerminalDrop.Append(upstream, "killer-entity", fact.KillerEntityStableId);
            TerminalDrop.Append(upstream, "killer-participant", fact.KillerRunParticipantStableId);
            TerminalDrop.Append(upstream, "experience-profile", fact.ExperienceProfileStableId);
            TerminalDrop.Append(upstream, "drop-profile", fact.DropProfileStableId);
            TerminalDrop.Append(upstream, "death-cause", (int)fact.DeathCause);
            TerminalDrop.Append(upstream, "run", fact.Identity.RunStableId);
            TerminalDrop.Append(upstream, "room-runtime", fact.Identity.RoomRuntimeInstanceStableId);
            TerminalDrop.Append(upstream, "room", fact.Identity.RoomStableId);
            TerminalDrop.Append(upstream, "placement", fact.Identity.PlacementStableId);
            TerminalDrop.Append(upstream, "entity", fact.Identity.EntityInstanceId);
            TerminalDrop.Append(upstream, "source-participant", fact.Identity.RunParticipantId);

            return EnemyDeathTerminalDropDefinitionViewResult.Accepted(
                new EnemyDeathTerminalDropDefinitionView(
                    fact,
                    definition.DropProfileId,
                    definition.Fingerprint,
                    TerminalDrop.Hash(upstream.ToString())));
        }
    }

    public sealed class PropTerminalSourceContext
    {
        public PropTerminalSourceContext(
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

    public interface IPropTerminalSourceContextResolver
    {
        bool TryResolve(
            PropTerminalFact terminalFact,
            out PropTerminalSourceContext context,
            out string diagnostic);
    }

    public sealed class PropDestructionTerminalDropFactBridge : ITerminalDropFactBridge
    {
        private readonly PropCatalog catalog;
        private readonly IPropTerminalSourceContextResolver sourceContextResolver;

        public PropDestructionTerminalDropFactBridge(
            PropCatalog catalog,
            IPropTerminalSourceContextResolver sourceContextResolver)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.sourceContextResolver = sourceContextResolver
                ?? throw new ArgumentNullException(nameof(sourceContextResolver));
        }

        public StableId FactKindStableId { get { return TerminalDropFactKindIds.PropDestruction; } }
        public Type FactType { get { return typeof(PropFactBatch); } }

        public TerminalDropAdaptationResult Adapt(object terminalFact)
        {
            PropFactBatch batch = terminalFact as PropFactBatch;
            if (batch == null || batch.Terminal == null)
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.InvalidTerminalFact,
                    "prop-terminal-fact-missing");
            }
            PropTerminalFact terminal = batch.Terminal;
            PropDefinition definition;
            if (!catalog.TryGet(terminal.PropDefinitionId, out definition))
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.MissingDefinition,
                    "prop-definition-missing:" + terminal.PropDefinitionId);
            }

            StableId definitionProfile;
            string profileDiagnostic;
            if (!TryResolveDropProfile(definition, out definitionProfile, out profileDiagnostic))
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.DropProfileMismatch,
                    profileDiagnostic);
            }
            if (batch.DropRequest != null)
            {
                if (batch.DropRequest.KindId != PropFactKindIds.DropRequest
                    || batch.DropRequest.PropParticipantId != terminal.PropParticipantId
                    || batch.DropRequest.SourceParticipantId != terminal.SourceParticipantId)
                {
                    return TerminalDropAdaptationResult.Rejected(
                        TerminalDropRejectionCode.InvalidTerminalFact,
                        "prop-drop-request-does-not-belong-to-terminal-fact");
                }
                if (definitionProfile == null
                    || batch.DropRequest.ProfileOrFactId != definitionProfile)
                {
                    return TerminalDropAdaptationResult.Rejected(
                        TerminalDropRejectionCode.DropProfileMismatch,
                        "prop-drop-profile-mismatch:fact=" + batch.DropRequest.ProfileOrFactId
                        + ";definition=" + (definitionProfile == null
                            ? "none"
                            : definitionProfile.ToString()));
                }
            }

            PropTerminalSourceContext sourceContext;
            string sourceDiagnostic;
            bool resolved;
            try
            {
                resolved = sourceContextResolver.TryResolve(
                    terminal,
                    out sourceContext,
                    out sourceDiagnostic);
            }
            catch (Exception exception)
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.MissingSourceContext,
                    "prop-source-context-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
            }
            if (!resolved || sourceContext == null)
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.MissingSourceContext,
                    string.IsNullOrWhiteSpace(sourceDiagnostic)
                        ? "prop-source-context-missing"
                        : sourceDiagnostic);
            }
            if (sourceContext.SourceEntityStableId != terminal.PropParticipantId)
            {
                return TerminalDropAdaptationResult.Rejected(
                    TerminalDropRejectionCode.MissingSourceContext,
                    "prop-source-context-entity-mismatch:terminal="
                        + terminal.PropParticipantId + ";resolved="
                        + sourceContext.SourceEntityStableId);
            }

            var upstream = new StringBuilder("schema=prop-destruction-drop-projection-v1");
            TerminalDrop.Append(upstream, "terminal-fact", terminal.FactId);
            TerminalDrop.Append(upstream, "terminal-kind", terminal.KindId);
            TerminalDrop.Append(upstream, "prop", terminal.PropParticipantId);
            TerminalDrop.Append(upstream, "definition", terminal.PropDefinitionId);
            TerminalDrop.Append(upstream, "source", terminal.SourceParticipantId);
            TerminalDrop.Append(upstream, "source-faction", terminal.SourceFactionId);
            TerminalDrop.Append(upstream, "damage-channel", terminal.DamageChannelId);
            TerminalDrop.Append(upstream, "terminal-fingerprint", terminal.Fingerprint);
            TerminalDrop.Append(
                upstream,
                "drop-request-fingerprint",
                batch.DropRequest == null ? "none" : batch.DropRequest.Fingerprint);

            return TerminalDropAdaptationResult.Accepted(
                new TerminalDropSourceFact(
                    FactKindStableId,
                    terminal.FactId,
                    batch.DropRequest == null ? null : batch.DropRequest.FactId,
                    sourceContext.RunStableId,
                    sourceContext.RunLifecycleGeneration,
                    terminal.PropParticipantId,
                    sourceContext.SourcePlacementStableId,
                    sourceContext.SourceLifecycleGeneration,
                    terminal.PropDefinitionId,
                    terminal.SourceParticipantId,
                    terminal.SourceParticipantId,
                    terminal.DamageChannelId,
                    definitionProfile,
                    sourceContext.Fingerprint,
                    definition.Fingerprint,
                    TerminalDrop.Hash(upstream.ToString())));
        }

        private static bool TryResolveDropProfile(
            PropDefinition definition,
            out StableId profileId,
            out string diagnostic)
        {
            profileId = null;
            diagnostic = string.Empty;
            PropCapability capability;
            if (!definition.TryGet(PropCapabilityIds.DropOnDestroy, out capability))
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
