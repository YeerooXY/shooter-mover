using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Selected-character graph over existing authorities. This type is a composition
    /// object only: each public authority remains the sole owner of its subsystem state.
    /// </summary>
    public sealed class ProductionCharacterRuntimeGraphV1 :
        ICharacterRuntimeGraphV1
    {
        private CharacterInstanceSnapshotV1 character;

        public ProductionCharacterRuntimeGraphV1(
            CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 routePayload,
            ProductionPlayerLoadoutRuntimeV1 loadoutRuntime,
            PlayerExperienceAuthorityV1 experienceAuthority,
            MoneyWalletService moneyWallet,
            ScrapWalletServiceV1 scrapWallet,
            RankedSkillAllocationAuthorityV2 skillAuthority,
            string skillProfileId,
            IEnumerable<ISaveComponentAdapterV1> saveAdapters)
        {
            this.character = character
                ?? throw new ArgumentNullException(nameof(character));
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            LoadoutRuntime = loadoutRuntime
                ?? throw new ArgumentNullException(nameof(loadoutRuntime));
            ExperienceAuthority = experienceAuthority
                ?? throw new ArgumentNullException(nameof(experienceAuthority));
            MoneyWallet = moneyWallet
                ?? throw new ArgumentNullException(nameof(moneyWallet));
            ScrapWallet = scrapWallet
                ?? throw new ArgumentNullException(nameof(scrapWallet));
            SkillAuthority = skillAuthority
                ?? throw new ArgumentNullException(nameof(skillAuthority));
            if (string.IsNullOrWhiteSpace(skillProfileId))
            {
                throw new ArgumentException(
                    "A ranked-skill profile identity is required.",
                    nameof(skillProfileId));
            }
            SkillProfileId = skillProfileId.Trim();
            SaveAdapters = new ReadOnlyCollection<ISaveComponentAdapterV1>(
                new List<ISaveComponentAdapterV1>(
                    saveAdapters
                    ?? throw new ArgumentNullException(nameof(saveAdapters))));
            if (SaveAdapters.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Character save adapters must be non-null.",
                    nameof(saveAdapters));
            }
        }

        public CharacterInstanceSnapshotV1 Character
        {
            get { return character; }
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }

        public ProductionPlayerLoadoutRuntimeV1 LoadoutRuntime { get; }

        public PlayerExperienceAuthorityV1 ExperienceAuthority { get; }

        public MoneyWalletService MoneyWallet { get; }

        public ScrapWalletServiceV1 ScrapWallet { get; }

        public RankedSkillAllocationAuthorityV2 SkillAuthority { get; }

        public string SkillProfileId { get; }

        public IReadOnlyList<ISaveComponentAdapterV1> SaveAdapters { get; }

        public bool IsDisposed { get; private set; }

        public void MarkPersisted(CharacterInstanceSnapshotV1 persistedCharacter)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(ProductionCharacterRuntimeGraphV1));
            }
            if (persistedCharacter == null
                || persistedCharacter.CharacterInstanceStableId
                    != character.CharacterInstanceStableId
                || persistedCharacter.ClassDefinitionStableId
                    != character.ClassDefinitionStableId
                || persistedCharacter.SlotIndex != character.SlotIndex)
            {
                throw new ArgumentException(
                    "Persisted character identity does not match the runtime graph.",
                    nameof(persistedCharacter));
            }
            character = persistedCharacter;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    /// <summary>
    /// Creates a fresh set of the existing character authorities and exposes only their
    /// SAVE-ADAPTERS-001 bindings. Optional integrations such as BOX may append their real
    /// authority adapter through additionalAdapterFactory; unknown optional components
    /// remain retained by the account aggregate until that authority is available.
    /// </summary>
    public sealed class ProductionCharacterRuntimeGraphFactoryV1 :
        ICharacterRuntimeGraphFactoryV1,
        IStarterCharacterRuntimeGraphFactoryV1
    {
        private readonly PlayerExperienceCurveV1 experienceCurve;
        private readonly ProgressionContext progressionContext;
        private readonly RankedSkillCatalogV2 skillCatalog;
        private readonly Func<StableId, string> skillClassIdResolver;
        private readonly Func<ProductionCharacterRuntimeGraphV1,
            IEnumerable<ISaveComponentAdapterV1>> additionalAdapterFactory;

        public ProductionCharacterRuntimeGraphFactoryV1(
            PlayerExperienceCurveV1 experienceCurve,
            ProgressionContext progressionContext,
            RankedSkillCatalogV2 skillCatalog,
            Func<StableId, string> skillClassIdResolver = null,
            Func<ProductionCharacterRuntimeGraphV1,
                IEnumerable<ISaveComponentAdapterV1>>
                    additionalAdapterFactory = null)
        {
            this.experienceCurve = experienceCurve
                ?? throw new ArgumentNullException(nameof(experienceCurve));
            this.progressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            this.skillCatalog = skillCatalog
                ?? throw new ArgumentNullException(nameof(skillCatalog));
            this.skillClassIdResolver = skillClassIdResolver
                ?? ResolveCurrentSkillClassId;
            this.additionalAdapterFactory = additionalAdapterFactory;
        }

        public ICharacterRuntimeGraphV1 CreateRestoreTarget(
            CharacterInstanceSnapshotV1 character)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            InventoryLoadoutAuthoritySnapshotV1 loadout = DecodeRequired(
                character,
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                KnownSaveComponentCodecsV1.ExactInstanceLoadout);
            RankedSkillAllocationSnapshotV2 skill = DecodeRequired(
                character,
                KnownSaveComponentDefinitionsV1.RankedSkillAllocation(),
                KnownSaveComponentCodecsV1.RankedSkillAllocation);
            ScrapSnapshotV1 scrap = DecodeRequired(
                character,
                KnownSaveComponentDefinitionsV1.ScrapWallet(),
                KnownSaveComponentCodecsV1.ScrapWallet);

            PlayerRouteProfilePayloadV1 route = RouteFromLoadout(
                character.CharacterInstanceStableId,
                character.ClassDefinitionStableId,
                loadout);
            return CreateGraph(
                character,
                route,
                skill.ProfileId,
                skill.ClassId,
                scrap.AuthorityStableId,
                scrap.CurrencyStableId);
        }

        public ICharacterRuntimeGraphV1 CreateStarter(
            int slotIndex,
            StableId exactCharacterInstanceStableId,
            StableId classDefinitionStableId,
            string displayName,
            object legacyContext)
        {
            PlayerAccountSnapshotV1.ValidateSlotIndex(slotIndex);
            if (exactCharacterInstanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(exactCharacterInstanceStableId));
            }
            if (classDefinitionStableId == null)
            {
                throw new ArgumentNullException(nameof(classDefinitionStableId));
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A starter character display name is required.",
                    nameof(displayName));
            }
            PlayerRouteProfilePayloadV1 legacyRoute =
                legacyContext as PlayerRouteProfilePayloadV1;
            if (legacyRoute == null || !legacyRoute.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "Starter migration requires the valid legacy route payload.",
                    nameof(legacyContext));
            }

            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    exactCharacterInstanceStableId,
                    classDefinitionStableId,
                    legacyRoute.WeaponSlots.Select(
                        item => item.EquipmentInstanceStableId));
            var shell = new CharacterInstanceSnapshotV1(
                exactCharacterInstanceStableId,
                classDefinitionStableId,
                slotIndex,
                displayName,
                0L,
                null);
            return CreateGraph(
                shell,
                route,
                exactCharacterInstanceStableId.ToString(),
                skillClassIdResolver(classDefinitionStableId),
                StableId.Parse("authority.production-scrap-wallet"),
                StableId.Parse("currency.scrap"));
        }

        public static ProductionCharacterRuntimeGraphFactoryV1
            CreateVerticalSliceDefaults(
                Func<ProductionCharacterRuntimeGraphV1,
                    IEnumerable<ISaveComponentAdapterV1>>
                        additionalAdapterFactory = null)
        {
            return new ProductionCharacterRuntimeGraphFactoryV1(
                new PlayerExperienceCurveV1(
                    100L,
                    100L,
                    100,
                    new SoftActivationCurveParameters(0.1, 10L, 10L)),
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }),
                RankedSkillSampleCatalogV2.Create(),
                additionalAdapterFactory: additionalAdapterFactory);
        }

        private ProductionCharacterRuntimeGraphV1 CreateGraph(
            CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 route,
            string skillProfileId,
            string skillClassId,
            StableId scrapAuthorityId,
            StableId scrapCurrencyId)
        {
            var loadout = new ProductionPlayerLoadoutRuntimeV1(route);
            var experience = new PlayerExperienceAuthorityV1(
                experienceCurve,
                progressionContext);
            var money = new MoneyWalletService();
            var scrap = new ScrapWalletServiceV1(
                scrapAuthorityId,
                scrapCurrencyId);
            var skills = new RankedSkillAllocationAuthorityV2(skillCatalog);
            skills.Seed(RankedSkillAllocationSnapshotV2.Empty(
                skillProfileId,
                skillClassId,
                skillCatalog));

            var adapters = new List<ISaveComponentAdapterV1>
            {
                ExperienceAdapter(experience),
                HoldingsAdapter(loadout),
                MoneyAdapter(money),
                ScrapAdapter(scrap, scrapAuthorityId, scrapCurrencyId),
                SkillAdapter(skills, skillProfileId),
                LoadoutAdapter(loadout),
            };
            var graph = new ProductionCharacterRuntimeGraphV1(
                character,
                route,
                loadout,
                experience,
                money,
                scrap,
                skills,
                skillProfileId,
                adapters);
            if (additionalAdapterFactory != null)
            {
                IEnumerable<ISaveComponentAdapterV1> additional =
                    additionalAdapterFactory(graph);
                if (additional != null)
                {
                    adapters.AddRange(additional);
                    graph = new ProductionCharacterRuntimeGraphV1(
                        character,
                        route,
                        loadout,
                        experience,
                        money,
                        scrap,
                        skills,
                        skillProfileId,
                        adapters);
                }
            }
            return graph;
        }

        private ISaveComponentAdapterV1 ExperienceAdapter(
            PlayerExperienceAuthorityV1 authority)
        {
            return KnownSaveComponentAdaptersV1.PlayerExperience(
                authority.ExportSnapshot,
                snapshot =>
                {
                    var validator = new PlayerExperienceAuthorityV1(
                        experienceCurve,
                        progressionContext);
                    PlayerExperienceImportResultV1 result =
                        validator.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatusV1.Imported
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerExperienceImportResultV1 result =
                        authority.TryImport(snapshot);
                    return result.Status
                            == PlayerExperienceImportStatusV1.Imported
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 HoldingsAdapter(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return KnownSaveComponentAdaptersV1.PlayerHoldings(
                runtime.Holdings.ExportSnapshot,
                snapshot =>
                {
                    var validator = new PlayerHoldingsService(
                        runtime.Holdings.AuthorityStableId,
                        999L,
                        runtime.CatalogAdapter);
                    PlayerHoldingsImportResultV1 result =
                        validator.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerHoldingsImportResultV1 result =
                        runtime.Holdings.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 MoneyAdapter(
            MoneyWalletService authority)
        {
            return KnownSaveComponentAdaptersV1.MoneyWallet(
                () => authority.CurrentSnapshot,
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        new MoneyWalletService().ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 ScrapAdapter(
            ScrapWalletServiceV1 authority,
            StableId authorityId,
            StableId currencyId)
        {
            return KnownSaveComponentAdaptersV1.ScrapWallet(
                authority.ExportSnapshot,
                snapshot =>
                {
                    ScrapSnapshotImportResultV1 result =
                        new ScrapWalletServiceV1(authorityId, currencyId)
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    ScrapSnapshotImportResultV1 result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 SkillAdapter(
            RankedSkillAllocationAuthorityV2 authority,
            string profileId)
        {
            return KnownSaveComponentAdaptersV1.RankedSkillAllocation(
                () => authority.Get(profileId),
                snapshot => KnownSaveComponentCodecsV1.RankedSkillAllocation
                    .Validate(snapshot),
                snapshot =>
                {
                    authority.Seed(snapshot);
                    return authority.Get(snapshot.ProfileId).Fingerprint
                            == snapshot.Fingerprint
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            "ranked-skill-seed-mismatch");
                });
        }

        private static ISaveComponentAdapterV1 LoadoutAdapter(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return KnownSaveComponentAdaptersV1.ExactInstanceLoadout(
                runtime.LoadoutAuthority.ExportSnapshot,
                snapshot => KnownSaveComponentCodecsV1.ExactInstanceLoadout
                    .Validate(snapshot),
                snapshot => runtime.LoadoutAuthority.ImportSnapshot(snapshot));
        }

        private static TSnapshot DecodeRequired<TSnapshot>(
            CharacterInstanceSnapshotV1 character,
            SaveComponentDefinitionV1 definition,
            ISaveComponentPayloadCodecV1<TSnapshot> codec)
            where TSnapshot : class
        {
            SaveComponentSnapshotV1 component;
            if (!character.TryGetComponent(
                definition.ComponentStableId,
                out component))
            {
                throw new InvalidOperationException(
                    "Required character component is missing: "
                        + definition.ComponentStableId);
            }
            TSnapshot snapshot;
            string rejectionCode;
            if (!codec.TryDecode(
                component.CanonicalPayload,
                out snapshot,
                out rejectionCode))
            {
                throw new InvalidOperationException(
                    "Required character component is corrupt: "
                        + definition.ComponentStableId
                        + ":"
                        + rejectionCode);
            }
            return snapshot;
        }

        private static PlayerRouteProfilePayloadV1 RouteFromLoadout(
            StableId characterInstanceId,
            StableId loadoutProfileId,
            InventoryLoadoutAuthoritySnapshotV1 loadout)
        {
            var instances = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                instances.Add(loadout.GetBinding(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId)
                    .EquipmentInstanceStableId);
            }
            return PlayerRouteProfilePayloadV1.Create(
                characterInstanceId,
                loadoutProfileId,
                instances);
        }

        private static string ResolveCurrentSkillClassId(
            StableId classDefinitionStableId)
        {
            string value = classDefinitionStableId == null
                ? string.Empty
                : classDefinitionStableId.ToString();
            if (value.IndexOf("healer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "combat_medic";
            }
            if (value.IndexOf("defensive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "juggernaut";
            }
            return "striker";
        }
    }
}
