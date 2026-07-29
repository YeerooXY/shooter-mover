using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.LootDropBinding
{
    public sealed class LootDropFactBridgeRegistry
    {
        private readonly Dictionary<Type, ILootDropFactBridge> byType;

        public LootDropFactBridgeRegistry(
            IEnumerable<ILootDropFactBridge> adapters)
        {
            if (adapters == null) throw new ArgumentNullException(nameof(adapters));
            var ordered = new List<ILootDropFactBridge>();
            foreach (ILootDropFactBridge adapter in adapters)
            {
                if (adapter == null)
                    throw new ArgumentException("Adapter registrations cannot contain null.", nameof(adapters));
                ordered.Add(adapter);
            }
            ordered.Sort((left, right) => left.FactKindStableId.CompareTo(right.FactKindStableId));

            byType = new Dictionary<Type, ILootDropFactBridge>();
            var kinds = new HashSet<StableId>();
            var canonical = new StringBuilder("schema=terminal-drop-adapter-registry-v1");
            for (int index = 0; index < ordered.Count; index++)
            {
                ILootDropFactBridge adapter = ordered[index];
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
                LootDrop.Append(canonical, "kind-" + index, adapter.FactKindStableId);
                LootDrop.Append(
                    canonical,
                    "type-" + index,
                    adapter.FactType.AssemblyQualifiedName);
            }
            Fingerprint = LootDrop.Hash(canonical.ToString());
        }

        public string Fingerprint { get; }

        public LootDropAdaptationResult Adapt(object terminalFact)
        {
            if (terminalFact == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.NullFact,
                    "terminal-drop-fact-null");
            }
            ILootDropFactBridge adapter;
            if (!byType.TryGetValue(terminalFact.GetType(), out adapter))
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.UnsupportedFactType,
                    "terminal-drop-unsupported-fact-type:" + terminalFact.GetType().FullName);
            }
            return adapter.Adapt(terminalFact)
                ?? LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
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
                LootDrop.Append(canonical, "profile-id-" + index, profile.ProfileStableId);
                LootDrop.Append(canonical, "profile-fingerprint-" + index, profile.Fingerprint);
            }
            Fingerprint = LootDrop.Hash(canonical.ToString());
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

    internal sealed class EnemyDeathLootDropDefinitionView
    {
        public EnemyDeathLootDropDefinitionView(
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

    internal sealed class EnemyDeathLootDropDefinitionViewResult
    {
        private EnemyDeathLootDropDefinitionViewResult(
            EnemyDeathLootDropDefinitionView projection,
            LootDropRejectionCode rejectionCode,
            string diagnostic)
        {
            Projection = projection;
            RejectionCode = rejectionCode;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public EnemyDeathLootDropDefinitionView Projection { get; }
        public LootDropRejectionCode RejectionCode { get; }
        public string Diagnostic { get; }
        public bool Succeeded { get { return Projection != null; } }

        public static EnemyDeathLootDropDefinitionViewResult Accepted(
            EnemyDeathLootDropDefinitionView projection)
        {
            return new EnemyDeathLootDropDefinitionViewResult(
                projection ?? throw new ArgumentNullException(nameof(projection)),
                LootDropRejectionCode.None,
                string.Empty);
        }

        public static EnemyDeathLootDropDefinitionViewResult Rejected(
            LootDropRejectionCode code,
            string diagnostic)
        {
            return new EnemyDeathLootDropDefinitionViewResult(
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
    internal sealed class EnemyDeathLootDropDefinitionProjector
    {
        private readonly EnemyCatalog catalog;

        public EnemyDeathLootDropDefinitionProjector(EnemyCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public EnemyDeathLootDropDefinitionViewResult Project(
            EnemyDeathFact fact)
        {
            if (fact == null)
            {
                return EnemyDeathLootDropDefinitionViewResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "enemy-death-fact-type-mismatch");
            }

            EnemyDefinition definition;
            if (!catalog.TryGetDefinition(fact.DefinitionStableId, out definition))
            {
                return EnemyDeathLootDropDefinitionViewResult.Rejected(
                    LootDropRejectionCode.MissingDefinition,
                    "enemy-definition-missing:" + fact.DefinitionStableId);
            }
            if (fact.Identity == null
                || fact.Identity.RunStableId == null
                || fact.Identity.EntityInstanceId == null
                || fact.Identity.PlacementStableId == null)
            {
                return EnemyDeathLootDropDefinitionViewResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "enemy-death-identity-incomplete");
            }
            if (fact.DropProfileStableId != null
                && definition.DropProfileId != fact.DropProfileStableId)
            {
                return EnemyDeathLootDropDefinitionViewResult.Rejected(
                    LootDropRejectionCode.DropProfileMismatch,
                    "enemy-drop-profile-mismatch:fact=" + fact.DropProfileStableId
                    + ";definition=" + (definition.DropProfileId == null
                        ? "none"
                        : definition.DropProfileId.ToString()));
            }

            var upstream = new StringBuilder("schema=enemy-death-fact-drop-projection-v2");
            LootDrop.Append(upstream, "death-event", fact.DeathEventStableId);
            LootDrop.Append(upstream, "trigger", fact.TriggeringEventStableId);
            LootDrop.Append(upstream, "definition", fact.DefinitionStableId);
            LootDrop.Append(upstream, "level", fact.Level);
            LootDrop.Append(upstream, "source-generation", fact.LifecycleGeneration);
            LootDrop.Append(upstream, "killer-entity", fact.KillerEntityStableId);
            LootDrop.Append(upstream, "killer-participant", fact.KillerRunParticipantStableId);
            LootDrop.Append(upstream, "experience-profile", fact.ExperienceProfileStableId);
            LootDrop.Append(upstream, "drop-profile", fact.DropProfileStableId);
            LootDrop.Append(upstream, "death-cause", (int)fact.DeathCause);
            LootDrop.Append(upstream, "run", fact.Identity.RunStableId);
            LootDrop.Append(upstream, "room-runtime", fact.Identity.RoomRuntimeInstanceStableId);
            LootDrop.Append(upstream, "room", fact.Identity.RoomStableId);
            LootDrop.Append(upstream, "placement", fact.Identity.PlacementStableId);
            LootDrop.Append(upstream, "entity", fact.Identity.EntityInstanceId);
            LootDrop.Append(upstream, "source-participant", fact.Identity.RunParticipantId);

            return EnemyDeathLootDropDefinitionViewResult.Accepted(
                new EnemyDeathLootDropDefinitionView(
                    fact,
                    definition.DropProfileId,
                    definition.Fingerprint,
                    LootDrop.Hash(upstream.ToString())));
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

    public sealed class PropDestructionLootDropFactBridge : ILootDropFactBridge
    {
        private readonly PropCatalog catalog;
        private readonly IPropTerminalSourceContextResolver sourceContextResolver;

        public PropDestructionLootDropFactBridge(
            PropCatalog catalog,
            IPropTerminalSourceContextResolver sourceContextResolver)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.sourceContextResolver = sourceContextResolver
                ?? throw new ArgumentNullException(nameof(sourceContextResolver));
        }

        public StableId FactKindStableId { get { return LootDropFactKindIds.PropDestruction; } }
        public Type FactType { get { return typeof(PropFactBatch); } }

        public LootDropAdaptationResult Adapt(object terminalFact)
        {
            PropFactBatch batch = terminalFact as PropFactBatch;
            if (batch == null || batch.Terminal == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "prop-terminal-fact-missing");
            }
            PropTerminalFact terminal = batch.Terminal;
            PropDefinition definition;
            if (!catalog.TryGet(terminal.PropDefinitionId, out definition))
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingDefinition,
                    "prop-definition-missing:" + terminal.PropDefinitionId);
            }

            StableId definitionProfile;
            string profileDiagnostic;
            if (!TryResolveDropProfile(definition, out definitionProfile, out profileDiagnostic))
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.DropProfileMismatch,
                    profileDiagnostic);
            }
            if (batch.DropRequest != null)
            {
                if (batch.DropRequest.KindId != PropFactKindIds.DropRequest
                    || batch.DropRequest.PropParticipantId != terminal.PropParticipantId
                    || batch.DropRequest.SourceParticipantId != terminal.SourceParticipantId)
                {
                    return LootDropAdaptationResult.Rejected(
                        LootDropRejectionCode.InvalidTerminalFact,
                        "prop-drop-request-does-not-belong-to-terminal-fact");
                }
                if (definitionProfile == null
                    || batch.DropRequest.ProfileOrFactId != definitionProfile)
                {
                    return LootDropAdaptationResult.Rejected(
                        LootDropRejectionCode.DropProfileMismatch,
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
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingSourceContext,
                    "prop-source-context-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
            }
            if (!resolved || sourceContext == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingSourceContext,
                    string.IsNullOrWhiteSpace(sourceDiagnostic)
                        ? "prop-source-context-missing"
                        : sourceDiagnostic);
            }
            if (sourceContext.SourceEntityStableId != terminal.PropParticipantId)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingSourceContext,
                    "prop-source-context-entity-mismatch:terminal="
                        + terminal.PropParticipantId + ";resolved="
                        + sourceContext.SourceEntityStableId);
            }

            var upstream = new StringBuilder("schema=prop-destruction-drop-projection-v1");
            LootDrop.Append(upstream, "terminal-fact", terminal.FactId);
            LootDrop.Append(upstream, "terminal-kind", terminal.KindId);
            LootDrop.Append(upstream, "prop", terminal.PropParticipantId);
            LootDrop.Append(upstream, "definition", terminal.PropDefinitionId);
            LootDrop.Append(upstream, "source", terminal.SourceParticipantId);
            LootDrop.Append(upstream, "source-faction", terminal.SourceFactionId);
            LootDrop.Append(upstream, "damage-channel", terminal.DamageChannelId);
            LootDrop.Append(upstream, "terminal-fingerprint", terminal.Fingerprint);
            LootDrop.Append(
                upstream,
                "drop-request-fingerprint",
                batch.DropRequest == null ? "none" : batch.DropRequest.Fingerprint);

            return LootDropAdaptationResult.Accepted(
                new LootDropSourceFact(
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
                    LootDrop.Hash(upstream.ToString())));
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
