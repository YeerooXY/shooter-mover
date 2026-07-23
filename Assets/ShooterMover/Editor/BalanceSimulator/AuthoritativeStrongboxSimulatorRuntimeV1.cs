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
using ShooterMover.Application.Weapons.Catalog;
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
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class AuthoritativeStrongboxPreparedOpenV1
    {
        private readonly string canonicalText;

        public AuthoritativeStrongboxPreparedOpenV1(
            ProductionStrongboxTierV1 tier,
            string committedSourceDefinitionId,
            StrongboxInstanceContextV1 context,
            StrongboxOpenCommandV1 command,
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
            StrongboxCanonicalV1.AppendToken(
                builder,
                "schema",
                "authoritative-strongbox-prepared-open-v1");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "queue_ordinal",
                queueOrdinal.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "production_tier",
                tier.TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(
                builder,
                "committed_source_definition",
                CommittedSourceDefinitionId);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "context",
                context.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(
                builder,
                "command",
                command.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public ProductionStrongboxTierV1 Tier { get; }
        public string CommittedSourceDefinitionId { get; }
        public StrongboxInstanceContextV1 Context { get; }
        public StrongboxOpenCommandV1 Command { get; }
        public int QueueOrdinal { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    /// <summary>
    /// Editor-only production integration runtime. It registers real tier definitions
    /// into one BOX authority and delegates equipment selection, item level and generated
    /// augment signature to StrongboxHybridEquipmentGenerationResolverV1. Production and
    /// the simulator receive the same canonical weapon/equipment projection rather than
    /// independently constructing equipment candidates.
    /// </summary>
    public sealed class AuthoritativeStrongboxSimulatorRuntimeV1
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

        private readonly IWeaponCatalogProjectionV1 contentProjection;
        private readonly MoneyWalletService money;
        private readonly ScrapWalletServiceV1 scrap;
        private readonly PlayerHoldingsService holdings;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly GeneratedEquipmentAugmentSignatureAuthorityV1
            augmentSignatures =
                new GeneratedEquipmentAugmentSignatureAuthorityV1();
        private readonly Dictionary<StableId, AuthoritativeStrongboxPreparedOpenV1>
            preparedByBox =
                new Dictionary<StableId, AuthoritativeStrongboxPreparedOpenV1>();
        private StrongboxOpeningServiceV1 opening;

        private AuthoritativeStrongboxSimulatorRuntimeV1(
            IWeaponCatalogProjectionV1 projection)
        {
            contentProjection = projection
                ?? throw new ArgumentNullException(nameof(projection));
            var validator = new SimulatorEquipmentValidator(
                projection.EquipmentCatalog);
            money = new MoneyWalletService();
            scrap = new ScrapWalletServiceV1(
                ScrapAuthority,
                ScrapCurrency);
            holdings = new PlayerHoldingsService(
                HoldingsAuthority,
                1000000L,
                validator);
            rewardApplication = new RewardApplicationServiceV1(
                RapAuthority,
                new MoneyRewardChildAuthorityV1(money),
                new ScrapRewardChildAuthorityV1(scrap),
                new GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1(
                    holdings,
                    validator,
                    augmentSignatures));
        }

        public WeaponCatalog WeaponCatalog
        {
            get { return contentProjection.WeaponCatalog; }
        }
        public EquipmentCatalog EquipmentCatalog
        {
            get { return contentProjection.EquipmentCatalog; }
        }
        public string CatalogFingerprint
        {
            get { return contentProjection.Fingerprint; }
        }
        public GeneratedEquipmentAugmentSignatureAuthorityV1 AugmentSignatures
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
            string weaponCatalogJson,
            out AuthoritativeStrongboxSimulatorRuntimeV1 runtime,
            out string diagnostic)
        {
            runtime = null;
            CanonicalWeaponCatalogProjectionV1 projection;
            if (!CanonicalWeaponCatalogProjectionV1.TryCreate(
                    new StringWeaponCatalogSourceV1(
                        "authoritative-strongbox-simulator",
                        weaponCatalogJson),
                    WeaponRarityNormalizationPolicyV1.CreateBaselineV1(),
                    out projection,
                    out diagnostic))
            {
                return false;
            }

            runtime = new AuthoritativeStrongboxSimulatorRuntimeV1(projection);
            diagnostic = string.Empty;
            return true;
        }

        public IReadOnlyList<AuthoritativeStrongboxPreparedOpenV1> PrepareBatch(
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
                new Dictionary<StableId, StrongboxDefinitionV1>();
            var values = new List<AuthoritativeStrongboxPreparedOpenV1>();
            for (int index = 0; index < tierNumbers.Count; index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.GetByNumber(
                        tierNumbers[index]);
                StrongboxDefinitionV1 definition;
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
                StrongboxInstanceContextV1 context =
                    StrongboxInstanceContextV1.Create(
                        boxId,
                        tier.TierStableId,
                        DeriveSeed(rootSeed, index),
                        DeterministicRandom.AlgorithmVersion1,
                        progression,
                        Source,
                        DerivedId("visualcollection", rootSeed, index),
                        definition.Fingerprint);
                StrongboxOpenCommandV1 command = StrongboxOpenCommandV1.Create(
                    DerivedId("visualopening", rootSeed, index),
                    DerivedId("visualrun", rootSeed, 0),
                    boxId,
                    Claimant,
                    MoneyWalletIdsV1.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority);
                StrongboxHybridLootPolicyV1 policy =
                    ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(
                        tier.TierNumber);
                values.Add(new AuthoritativeStrongboxPreparedOpenV1(
                    tier,
                    "hybrid-policy:" + policy.PolicyId,
                    context,
                    command,
                    index));
            }

            var definitionCatalog = new StrongboxDefinitionCatalogV1(
                definitionsByTier.Values);
            var equipmentResolver =
                new StrongboxHybridEquipmentGenerationResolverV1(
                    EquipmentCatalog,
                    WeaponCatalog,
                    augmentSignatures);
            opening = new StrongboxOpeningServiceV1(
                definitionCatalog,
                new SharedStrongboxRewardGeneratorV1(
                    new RewardGenerationServiceV1()),
                holdings,
                rewardApplication,
                new TransactionalStrongboxGrantPayloadResolverV1(
                    new DeterministicStrongboxGrantPayloadResolverV1(
                        equipmentResolver),
                    augmentSignatures));

            for (int index = 0; index < values.Count; index++)
            {
                AuthoritativeStrongboxPreparedOpenV1 prepared = values[index];
                AddAndRegister(prepared, rootSeed);
                preparedByBox.Add(
                    prepared.Context.InstanceStableId,
                    prepared);
            }
            return new ReadOnlyCollection<
                AuthoritativeStrongboxPreparedOpenV1>(values);
        }

        public StrongboxOpeningResultRuntimeV1 OpenOrRetry(
            AuthoritativeStrongboxPreparedOpenV1 prepared)
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
            AuthoritativeStrongboxPreparedOpenV1 known;
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

        public bool IsBoxOwned(AuthoritativeStrongboxPreparedOpenV1 prepared)
        {
            if (prepared == null) return false;
            UniqueHoldingSnapshotV1 ignored;
            return holdings.TryGetUnique(
                prepared.Context.InstanceStableId,
                out ignored);
        }

        public IReadOnlyList<EquipmentInstance> EquipmentFrom(
            StrongboxOpeningResultRuntimeV1 result)
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
                RewardGrantApplicationPayloadV1 payload =
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
            out GeneratedEquipmentAugmentSignatureV1 signature)
        {
            return augmentSignatures.TryGet(
                equipmentInstanceStableId,
                out signature);
        }

        private void AddAndRegister(
            AuthoritativeStrongboxPreparedOpenV1 prepared,
            ulong rootSeed)
        {
            int ordinal = prepared.QueueOrdinal;
            PlayerHoldingsMutationResultV1 add = holdings.Apply(
                PlayerHoldingsCommandV1.AddStrongbox(
                    DerivedId("visualaddtx", rootSeed, ordinal),
                    DerivedId("visualaddop", rootSeed, ordinal),
                    HoldingsAuthority,
                    prepared.Context.TierStableId,
                    prepared.Context.InstanceStableId,
                    HoldingProvenanceV1.Create(
                        DerivedId("visualaddgrant", rootSeed, ordinal),
                        Source)));
            if (add.Status != PlayerHoldingsMutationStatusV1.Applied
                && add.Status
                    != PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to add simulator strongbox to holdings: "
                    + add.Status + " / " + add.RejectionCode);
            }

            StrongboxRegistrationResultV1 registration =
                opening.RegisterInstance(prepared.Context);
            if (registration.Status
                    != StrongboxRegistrationStatusV1.Registered
                && registration.Status
                    != StrongboxRegistrationStatusV1.ExactDuplicateNoChange)
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
            return StrongboxCanonicalV1.DeriveId(
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
