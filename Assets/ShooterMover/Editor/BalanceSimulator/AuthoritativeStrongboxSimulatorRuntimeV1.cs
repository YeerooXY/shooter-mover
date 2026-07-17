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
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
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
                ?? throw new ArgumentNullException(nameof(committedSourceDefinitionId));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (queueOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(queueOrdinal));
            QueueOrdinal = queueOrdinal;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "authoritative-strongbox-prepared-open-v1");
            StrongboxCanonicalV1.AppendToken(builder, "queue_ordinal", queueOrdinal.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "production_tier", tier.TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "committed_source_definition", CommittedSourceDefinitionId);
            StrongboxCanonicalV1.AppendToken(builder, "context", context.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "command", command.ToCanonicalString());
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
    /// Editor-only, production-shaped integration runtime. Queue preparation first
    /// commits an exact weapon definition using the existing BOX/GEN composition.
    /// The whole frozen batch is then registered into one real BOX authority whose
    /// generated grants carry exact definition identities accepted by RAP/INV.
    /// </summary>
    public sealed class AuthoritativeStrongboxSimulatorRuntimeV1
    {
        private static readonly StableId DifficultyNormal = StableId.Parse("difficulty.normal");
        private static readonly StableId ScrapAuthority = StableId.Parse("authority.lootbox-simulator-scrap");
        private static readonly StableId ScrapCurrency = StableId.Parse("currency.scrap");
        private static readonly StableId HoldingsAuthority = StableId.Parse("holdings.lootbox-authoritative-simulator");
        private static readonly StableId RapAuthority = StableId.Parse("authority.lootbox-authoritative-rap");
        private static readonly StableId Claimant = StableId.Parse("player.lootbox-authoritative-simulator");
        private static readonly StableId Source = StableId.Parse("source.lootbox-authoritative-simulator");

        private readonly LootboxSimulatorRuntimeV1 contentRuntime;
        private readonly MoneyWalletService money;
        private readonly ScrapWalletServiceV1 scrap;
        private readonly PlayerHoldingsService holdings;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly Dictionary<StableId, AuthoritativeStrongboxPreparedOpenV1> preparedByBox =
            new Dictionary<StableId, AuthoritativeStrongboxPreparedOpenV1>();
        private StrongboxOpeningServiceV1 opening;

        private AuthoritativeStrongboxSimulatorRuntimeV1(
            LootboxSimulatorRuntimeV1 contentRuntime)
        {
            this.contentRuntime = contentRuntime ?? throw new ArgumentNullException(nameof(contentRuntime));
            var validator = new SimulatorEquipmentValidator(contentRuntime.EquipmentCatalog);
            money = new MoneyWalletService();
            scrap = new ScrapWalletServiceV1(ScrapAuthority, ScrapCurrency);
            holdings = new PlayerHoldingsService(HoldingsAuthority, 1000000L, validator);
            rewardApplication = new RewardApplicationServiceV1(
                RapAuthority,
                new MoneyRewardChildAuthorityV1(money),
                new ScrapRewardChildAuthorityV1(scrap),
                new PlayerHoldingsRewardChildAuthorityV1(holdings, validator));
        }

        public WeaponCatalog WeaponCatalog { get { return contentRuntime.WeaponCatalog; } }
        public EquipmentCatalog EquipmentCatalog { get { return contentRuntime.EquipmentCatalog; } }
        public long MoneyBalance { get { return money.Balance; } }
        public long ScrapBalance { get { return scrap.Balance; } }
        public long HoldingsSequence { get { return holdings.Sequence; } }
        public long OpeningSequence { get { return opening == null ? 0L : opening.Sequence; } }

        public static bool TryCreate(
            string weaponCatalogJson,
            out AuthoritativeStrongboxSimulatorRuntimeV1 runtime,
            out string diagnostic)
        {
            runtime = null;
            LootboxSimulatorRuntimeV1 content;
            if (!LootboxSimulatorRuntimeV1.TryCreate(
                weaponCatalogJson,
                out content,
                out diagnostic))
            {
                return false;
            }

            runtime = new AuthoritativeStrongboxSimulatorRuntimeV1(content);
            diagnostic = string.Empty;
            return true;
        }

        public IReadOnlyList<AuthoritativeStrongboxPreparedOpenV1> PrepareBatch(
            IReadOnlyList<int> tierNumbers,
            int playerLevel,
            ulong rootSeed)
        {
            if (tierNumbers == null) throw new ArgumentNullException(nameof(tierNumbers));
            if (tierNumbers.Count == 0) throw new ArgumentException("At least one box is required.", nameof(tierNumbers));
            if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (opening != null) throw new InvalidOperationException("This authoritative runtime already owns a frozen opening batch.");

            IReadOnlyList<WeaponDefinitionData> live =
                WeaponCatalog.GetDefinitions(WeaponCatalogContentFilter.LiveOnly);
            var definitions = new List<StrongboxDefinitionV1>();
            var bindings = new List<StrongboxEquipmentGenerationDefinitionV1>();
            var values = new List<AuthoritativeStrongboxPreparedOpenV1>();

            for (int index = 0; index < tierNumbers.Count; index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.GetByNumber(tierNumbers[index]);
                LootboxGeneratedItemV1 commitment =
                    contentRuntime.Generate(tier.TierNumber, playerLevel, rootSeed, index);
                WeaponDefinitionData sourceDefinition = FindSourceDefinition(
                    live,
                    commitment.SourceDefinitionId);
                StableId bindingTierId = DerivedId("visualtierbinding", rootSeed, index);
                EquipmentGenerationPolicyV1 policy = BuildExactPolicy(
                    tier,
                    bindingTierId,
                    sourceDefinition);
                StrongboxDefinitionV1 definition = CreateExactDefinition(
                    tier,
                    bindingTierId,
                    policy.PolicyId,
                    commitment.Equipment.DefinitionId);
                definitions.Add(definition);
                bindings.Add(new StrongboxEquipmentGenerationDefinitionV1(
                    bindingTierId,
                    tier.CreatePowerBudgetPolicy(),
                    policy,
                    EquipmentCatalog));

                StableId boxId = DerivedId("visualbox", rootSeed, index);
                int effectiveLevel = tier.ResolveEffectivePlayerLevel(playerLevel);
                ProgressionContext progression = ProgressionContext.Create(
                    effectiveLevel,
                    effectiveLevel,
                    DifficultyNormal,
                    1,
                    Array.Empty<StableId>());
                StrongboxInstanceContextV1 context = StrongboxInstanceContextV1.Create(
                    boxId,
                    bindingTierId,
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
                values.Add(new AuthoritativeStrongboxPreparedOpenV1(
                    tier,
                    commitment.SourceDefinitionId,
                    context,
                    command,
                    index));
            }

            var definitionCatalog = new StrongboxDefinitionCatalogV1(definitions);
            var equipmentResolver = new StrongboxEquipmentGenerationResolverV1(
                new RewardGenerationServiceV1(),
                new StrongboxEquipmentGenerationDefinitionCatalogV1(bindings));
            opening = new StrongboxOpeningServiceV1(
                definitionCatalog,
                new SharedStrongboxRewardGeneratorV1(new RewardGenerationServiceV1()),
                holdings,
                rewardApplication,
                new DeterministicStrongboxGrantPayloadResolverV1(equipmentResolver));

            for (int index = 0; index < values.Count; index++)
            {
                AuthoritativeStrongboxPreparedOpenV1 prepared = values[index];
                AddAndRegister(prepared, rootSeed);
                preparedByBox.Add(prepared.Context.InstanceStableId, prepared);
            }

            return new ReadOnlyCollection<AuthoritativeStrongboxPreparedOpenV1>(values);
        }

        public StrongboxOpeningResultRuntimeV1 OpenOrRetry(
            AuthoritativeStrongboxPreparedOpenV1 prepared)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            if (opening == null) throw new InvalidOperationException("No authoritative batch has been prepared.");
            AuthoritativeStrongboxPreparedOpenV1 known;
            if (!preparedByBox.TryGetValue(prepared.Context.InstanceStableId, out known)
                || !string.Equals(known.Fingerprint, prepared.Fingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Prepared strongbox is not owned by this simulator runtime.");
            }
            return opening.Open(prepared.Command);
        }

        public bool IsBoxOwned(AuthoritativeStrongboxPreparedOpenV1 prepared)
        {
            if (prepared == null) return false;
            UniqueHoldingSnapshotV1 ignored;
            return holdings.TryGetUnique(prepared.Context.InstanceStableId, out ignored);
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
                for (int itemIndex = 0; itemIndex < payload.EquipmentInstances.Count; itemIndex++)
                {
                    equipment.Add(payload.EquipmentInstances[itemIndex]);
                }
            }
            return new ReadOnlyCollection<EquipmentInstance>(equipment);
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
                && add.Status != PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to add simulator strongbox to holdings: "
                    + add.Status + " / " + add.RejectionCode);
            }

            StrongboxRegistrationResultV1 registration =
                opening.RegisterInstance(prepared.Context);
            if (registration.Status != StrongboxRegistrationStatusV1.Registered
                && registration.Status != StrongboxRegistrationStatusV1.ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to register simulator strongbox: "
                    + registration.Status + " / " + registration.RejectionCode);
            }
        }

        private static StrongboxDefinitionV1 CreateExactDefinition(
            ProductionStrongboxTierV1 tier,
            StableId bindingTierId,
            StableId generationPolicyId,
            StableId exactEquipmentDefinitionId)
        {
            RewardGrantSpecificationV1 equipment = RewardGrantSpecificationV1.CreateFixed(
                StrongboxCanonicalV1.DeriveId(
                    "strongboxgrant",
                    bindingTierId.ToString(),
                    "equipment"),
                RewardGrantKindV1.EquipmentReference,
                exactEquipmentDefinitionId,
                1L);
            RewardProfileV1 profile = RewardProfileV1.Create(
                StrongboxCanonicalV1.DeriveId(
                    "strongboxprofile",
                    bindingTierId.ToString()),
                new[] { equipment },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return StrongboxDefinitionV1.Create(
                bindingTierId,
                tier.TierNumber,
                tier.GenerationBias,
                tier.QualityBias,
                tier.ExceptionalRollBias,
                StrongboxRewardCountPolicyV1.Create(2, 2),
                StrongboxMandatoryScrapPolicyV1.Create(
                    ScrapCurrency,
                    tier.ScrapMinimum,
                    tier.ScrapMaximum),
                generationPolicyId,
                profile,
                StableId.Parse("scaling.source-tier"),
                StableId.Parse("scaling.exceptional"));
        }

        private static EquipmentGenerationPolicyV1 BuildExactPolicy(
            ProductionStrongboxTierV1 tier,
            StableId bindingTierId,
            WeaponDefinitionData weapon)
        {
            StableId equipmentId = StrongboxCanonicalV1.DeriveId(
                "weapondefinition",
                weapon.DefinitionId);
            EquipmentGenerationCandidateV1 candidate = EquipmentGenerationCandidateV1.Create(
                equipmentId,
                0,
                1000,
                0,
                1000,
                Array.Empty<StableId>(),
                Math.Max(1, weapon.PeakDropLevel),
                InclusiveIntRange.Create(
                    Math.Max(1, weapon.FirstAppearance),
                    MaximumItemLevel(weapon)),
                Math.Max(0.000001, weapon.FinalBaseWeight),
                1.0);
            return EquipmentGenerationPolicyV1.Create(
                StrongboxCanonicalV1.DeriveId(
                    "lootboxpolicy",
                    bindingTierId.ToString()),
                new[] { candidate },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(
                        StableId.Parse("quality.common"), 0L, tier.CommonWeight),
                    EquipmentQualityCandidateV1.Create(
                        StableId.Parse("quality.rare"), 0L, tier.RareWeight),
                    EquipmentQualityCandidateV1.Create(
                        StableId.Parse("quality.exceptional"), 0L, tier.ExceptionalWeight),
                },
                new[]
                {
                    AugmentGenerationCandidateV1.Create(
                        StableId.Parse("augment.simulator-1"), 0, 1000, 1UL),
                    AugmentGenerationCandidateV1.Create(
                        StableId.Parse("augment.simulator-2"), 0, 1000, 1UL),
                    AugmentGenerationCandidateV1.Create(
                        StableId.Parse("augment.simulator-3"), 0, 1000, 1UL),
                },
                0,
                3,
                false,
                new SoftActivationCurveParameters(0.08, 12L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.15));
        }

        private static WeaponDefinitionData FindSourceDefinition(
            IReadOnlyList<WeaponDefinitionData> live,
            string sourceDefinitionId)
        {
            for (int index = 0; index < live.Count; index++)
            {
                if (string.Equals(
                    live[index].DefinitionId,
                    sourceDefinitionId,
                    StringComparison.Ordinal))
                {
                    return live[index];
                }
            }
            throw new InvalidOperationException(
                "Committed weapon definition is missing from the live catalog: "
                + sourceDefinitionId + ".");
        }

        private static int MaximumItemLevel(WeaponDefinitionData weapon)
        {
            return Math.Max(
                Math.Max(1, weapon.FirstAppearance),
                Math.Max(200, checked(weapon.PowerAnchor + 50)));
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

        private static StableId DerivedId(string purpose, ulong seed, int ordinal)
        {
            return StrongboxCanonicalV1.DeriveId(
                "lootboxsimulator",
                purpose,
                seed.ToString("x16", CultureInfo.InvariantCulture),
                ordinal.ToString("D6", CultureInfo.InvariantCulture));
        }

        private sealed class SimulatorEquipmentValidator : IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;
            public SimulatorEquipmentValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            }
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null ? null : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }
    }
}
