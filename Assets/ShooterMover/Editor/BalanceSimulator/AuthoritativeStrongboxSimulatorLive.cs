using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class AuthoritativeStrongboxPreparedOpen
    {
        private readonly string canonicalText;

        public AuthoritativeStrongboxPreparedOpen(
            StrongboxTier tier,
            string committedSourceDefinitionId,
            StrongboxInstanceContext context,
            StrongboxOpenCommand command,
            int queueOrdinal)
        {
            Tier = tier ?? throw new ArgumentNullException(nameof(tier));
            CommittedSourceDefinitionId = committedSourceDefinitionId
                ?? throw new ArgumentNullException(
                    nameof(committedSourceDefinitionId));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (queueOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queueOrdinal));
            }
            QueueOrdinal = queueOrdinal;

            var builder = new StringBuilder();
            Strongbox.AppendToken(
                builder,
                "schema",
                "authoritative-strongbox-prepared-open-v1");
            Strongbox.AppendToken(
                builder,
                "queue_ordinal",
                queueOrdinal.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "production_tier",
                tier.TierStableId.ToString());
            Strongbox.AppendToken(
                builder,
                "committed_source_definition",
                CommittedSourceDefinitionId);
            Strongbox.AppendToken(
                builder,
                "context",
                context.ToCanonicalString());
            Strongbox.AppendToken(
                builder,
                "command",
                command.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StrongboxTier Tier { get; }
        public string CommittedSourceDefinitionId { get; }
        public StrongboxInstanceContext Context { get; }
        public StrongboxOpenCommand Command { get; }
        public int QueueOrdinal { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    /// <summary>
    /// Editor-only production integration runtime. It registers real tier definitions
    /// into one BOX authority and delegates equipment selection, item level and generated
    /// augment signature to StrongboxHybridEquipmentGenerationResolver. The same RAP
    /// equipment child used by production commits signatures only after holdings applies.
    /// Payload preparation also shares the production signature rollback boundary.
    /// No item is preselected and no simulator-only probability table exists.
    /// </summary>
    public sealed class AuthoritativeStrongboxSimulatorLive
    {
        private static readonly StableId DifficultyNormal =
            StableId.Parse("difficulty.normal");
        private static readonly StableId ScrapAuthority =
            StableId.Parse("authority.lootbox-simulator-scrap");
        private static readonly StableId ScrapCurrency =
            StableId.Parse("currency.scrap");
        private static readonly StableId HoldingsAuthority =
            StableId.Parse("holdings.lootbox-authoritative-simulator");
        private static readonly StableId RapAuthority =
            StableId.Parse("authority.lootbox-authoritative-rap");
        private static readonly StableId Claimant =
            StableId.Parse("player.lootbox-authoritative-simulator");
        private static readonly StableId Source =
            StableId.Parse("source.lootbox-authoritative-simulator");
        private static readonly StableId GenerationPolicyId =
            StableId.Parse("generation-policy.authoritative-hybrid-simulator");

        private readonly LootboxSimulatorLive contentRuntime;
        private readonly MoneyWalletActions money;
        private readonly ScrapWalletActions scrap;
        private readonly PlayerHoldingsActions holdings;
        private readonly RewardApplicationActions rewardApplication;
        private readonly GeneratedEquipmentAugmentSignatureState
            augmentSignatures =
                new GeneratedEquipmentAugmentSignatureState();
        private readonly Dictionary<StableId, AuthoritativeStrongboxPreparedOpen>
            preparedByBox =
                new Dictionary<StableId, AuthoritativeStrongboxPreparedOpen>();
        private StrongboxOpeningActions opening;

        private AuthoritativeStrongboxSimulatorLive(
            LootboxSimulatorLive contentRuntime)
        {
            this.contentRuntime = contentRuntime
                ?? throw new ArgumentNullException(nameof(contentRuntime));
            var validator = new SimulatorEquipmentValidator(
                contentRuntime.EquipmentCatalog);
            money = new MoneyWalletActions();
            scrap = new ScrapWalletActions(
                ScrapAuthority,
                ScrapCurrency);
            holdings = new PlayerHoldingsActions(
                HoldingsAuthority,
                1000000L,
                validator);
            rewardApplication = new RewardApplicationActions(
                RapAuthority,
                new MoneyRewardChildState(money),
                new ScrapRewardChildState(scrap),
                new GeneratedAugmentSignaturePlayerHoldingsRewardChildState(
                    holdings,
                    validator,
                    augmentSignatures));
        }

        public GunCatalog GunCatalog
        {
            get { return contentRuntime.GunCatalog; }
        }
        public EquipmentCatalog EquipmentCatalog
        {
            get { return contentRuntime.EquipmentCatalog; }
        }
        public GeneratedEquipmentAugmentSignatureState AugmentSignatures
        {
            get { return augmentSignatures; }
        }
        public long MoneyBalance { get { return money.Balance; } }
        public long ScrapBalance { get { return scrap.Balance; } }
        public long HoldingsSequence { get { return holdings.Sequence; } }
        public long OpeningSequence
        {
            get { return opening == null ? 0L : opening.Sequence; }
        }

        public static bool TryCreate(
            string gunCatalogJson,
            out AuthoritativeStrongboxSimulatorLive runtime,
            out string diagnostic)
        {
            runtime = null;
            LootboxSimulatorLive content;
            if (!LootboxSimulatorLive.TryCreate(
                    gunCatalogJson,
                    out content,
                    out diagnostic))
            {
                return false;
            }

            runtime = new AuthoritativeStrongboxSimulatorLive(content);
            diagnostic = string.Empty;
            return true;
        }

        public IReadOnlyList<AuthoritativeStrongboxPreparedOpen> PrepareBatch(
            IReadOnlyList<int> tierNumbers,
            int playerLevel,
            ulong rootSeed)
        {
            if (tierNumbers == null)
            {
                throw new ArgumentNullException(nameof(tierNumbers));
            }
            if (tierNumbers.Count == 0)
            {
                throw new ArgumentException(
                    "At least one box is required.",
                    nameof(tierNumbers));
            }
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (opening != null)
            {
                throw new InvalidOperationException(
                    "This authoritative runtime already owns a frozen opening batch.");
            }

            var definitionsByTier =
                new Dictionary<StableId, StrongboxDefinition>();
            var values = new List<AuthoritativeStrongboxPreparedOpen>();
            for (int index = 0; index < tierNumbers.Count; index++)
            {
                StrongboxTier tier =
                    StrongboxCatalog.GetByNumber(
                        tierNumbers[index]);
                StrongboxDefinition definition;
                if (!definitionsByTier.TryGetValue(
                        tier.TierStableId,
                        out definition))
                {
                    definition = tier.CreateDefinition(GenerationPolicyId);
                    definitionsByTier.Add(tier.TierStableId, definition);
                }

                StableId boxId = DerivedId("visualbox", rootSeed, index);
                ProgressionContext progression = ProgressionContext.Create(
                    playerLevel,
                    playerLevel,
                    DifficultyNormal,
                    1,
                    Array.Empty<StableId>());
                StrongboxInstanceContext context =
                    StrongboxInstanceContext.Create(
                        boxId,
                        tier.TierStableId,
                        DeriveSeed(rootSeed, index),
                        DeterministicRandom.AlgorithmVersion1,
                        progression,
                        Source,
                        DerivedId("visualcollection", rootSeed, index),
                        definition.Fingerprint);
                StrongboxOpenCommand command = StrongboxOpenCommand.Create(
                    DerivedId("visualopening", rootSeed, index),
                    DerivedId("visualrun", rootSeed, 0),
                    boxId,
                    Claimant,
                    MoneyWalletIds.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority);
                StrongboxHybridLootPolicy policy =
                    StrongboxHybridLootCatalog.GetByTierNumber(
                        tier.TierNumber);
                values.Add(new AuthoritativeStrongboxPreparedOpen(
                    tier,
                    "hybrid-policy:" + policy.PolicyId,
                    context,
                    command,
                    index));
            }

            var definitionCatalog = new StrongboxDefinitionCatalog(
                definitionsByTier.Values);
            var equipmentResolver =
                new StrongboxHybridEquipmentGenerationResolver(
                    EquipmentCatalog,
                    GunCatalog,
                    augmentSignatures);
            opening = new StrongboxOpeningActions(
                definitionCatalog,
                new SharedStrongboxRewardGenerator(
                    new RewardGenerationActions()),
                holdings,
                rewardApplication,
                new TransactionalStrongboxGrantPayloadResolver(
                    new DeterministicStrongboxGrantPayloadResolver(
                        equipmentResolver),
                    augmentSignatures));

            for (int index = 0; index < values.Count; index++)
            {
                AuthoritativeStrongboxPreparedOpen prepared = values[index];
                AddAndRegister(prepared, rootSeed);
                preparedByBox.Add(
                    prepared.Context.InstanceStableId,
                    prepared);
            }
            return new ReadOnlyCollection<
                AuthoritativeStrongboxPreparedOpen>(values);
        }

        public StrongboxOpeningResultLive OpenOrRetry(
            AuthoritativeStrongboxPreparedOpen prepared)
        {
            if (prepared == null)
            {
                throw new ArgumentNullException(nameof(prepared));
            }
            if (opening == null)
            {
                throw new InvalidOperationException(
                    "No authoritative batch has been prepared.");
            }
            AuthoritativeStrongboxPreparedOpen known;
            if (!preparedByBox.TryGetValue(
                    prepared.Context.InstanceStableId,
                    out known)
                || !string.Equals(
                    known.Fingerprint,
                    prepared.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Prepared strongbox is not owned by this simulator runtime.");
            }
            return opening.Open(prepared.Command);
        }

        public bool IsBoxOwned(AuthoritativeStrongboxPreparedOpen prepared)
        {
            if (prepared == null) return false;
            UniqueHoldingSnapshot ignored;
            return holdings.TryGetUnique(
                prepared.Context.InstanceStableId,
                out ignored);
        }

        public IReadOnlyList<EquipmentInstance> EquipmentFrom(
            StrongboxOpeningResultLive result)
        {
            if (result == null || result.GeneratedOutcome == null)
            {
                return Array.Empty<EquipmentInstance>();
            }

            var equipment = new List<EquipmentInstance>();
            for (int payloadIndex = 0;
                 payloadIndex < result.GeneratedOutcome.Payloads.Count;
                 payloadIndex++)
            {
                RewardGrantApplicationPayload payload =
                    result.GeneratedOutcome.Payloads[payloadIndex];
                for (int itemIndex = 0;
                     itemIndex < payload.EquipmentInstances.Count;
                     itemIndex++)
                {
                    equipment.Add(payload.EquipmentInstances[itemIndex]);
                }
            }
            return new ReadOnlyCollection<EquipmentInstance>(equipment);
        }

        public bool TryGetAugmentSignature(
            StableId equipmentInstanceStableId,
            out GeneratedEquipmentAugmentSignature signature)
        {
            return augmentSignatures.TryGet(
                equipmentInstanceStableId,
                out signature);
        }

        private void AddAndRegister(
            AuthoritativeStrongboxPreparedOpen prepared,
            ulong rootSeed)
        {
            int ordinal = prepared.QueueOrdinal;
            PlayerHoldingsMutationResult add = holdings.Apply(
                PlayerHoldingsCommand.AddStrongbox(
                    DerivedId("visualaddtx", rootSeed, ordinal),
                    DerivedId("visualaddop", rootSeed, ordinal),
                    HoldingsAuthority,
                    prepared.Context.TierStableId,
                    prepared.Context.InstanceStableId,
                    HoldingProvenance.Create(
                        DerivedId("visualaddgrant", rootSeed, ordinal),
                        Source)));
            if (add.Status != PlayerHoldingsMutationStatus.Applied
                && add.Status
                    != PlayerHoldingsMutationStatus.ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to add simulator strongbox to holdings: "
                    + add.Status + " / " + add.RejectionCode);
            }

            StrongboxRegistrationResult registration =
                opening.RegisterInstance(prepared.Context);
            if (registration.Status
                    != StrongboxRegistrationStatus.Registered
                && registration.Status
                    != StrongboxRegistrationStatus.ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to register simulator strongbox: "
                    + registration.Status
                    + " / "
                    + registration.RejectionCode);
            }
        }

        private static ulong DeriveSeed(ulong rootSeed, int ordinal)
        {
            DeterministicRandom random = DeterministicRandom.Create(rootSeed)
                .Fork(
                    StableId.Parse("lootbox-simulator.open"),
                    checked((ulong)ordinal));
            ulong value;
            random.NextUInt64(out value);
            return value;
        }

        private static StableId DerivedId(
            string purpose,
            ulong seed,
            int ordinal)
        {
            return Strongbox.DeriveId(
                "lootboxsimulator",
                purpose,
                seed.ToString("x16", CultureInfo.InvariantCulture),
                ordinal.ToString("D6", CultureInfo.InvariantCulture));
        }

        private sealed class SimulatorEquipmentValidator :
            IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public SimulatorEquipmentValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog
                    ?? throw new ArgumentNullException(nameof(catalog));
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null
                    ? null
                    : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }
    }
}
