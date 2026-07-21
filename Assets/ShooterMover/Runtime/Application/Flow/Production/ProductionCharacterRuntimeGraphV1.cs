using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// One selected-character graph over existing production authorities. This graph
    /// coordinates lifecycle only; XP, holdings, money, scrap, skills, loadout and BOX
    /// remain the sole owners of their respective state.
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
            StrongboxDefinitionCatalogV1 strongboxCatalog,
            StrongboxOpeningServiceV1 strongboxAuthority,
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
            StrongboxCatalog = strongboxCatalog
                ?? throw new ArgumentNullException(nameof(strongboxCatalog));
            StrongboxAuthority = strongboxAuthority
                ?? throw new ArgumentNullException(nameof(strongboxAuthority));
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

        public StrongboxDefinitionCatalogV1 StrongboxCatalog { get; }

        public StrongboxOpeningServiceV1 StrongboxAuthority { get; }

        public IReadOnlyList<ISaveComponentAdapterV1> SaveAdapters { get; }

        public bool IsDisposed { get; private set; }

        public void MarkPersisted(
            CharacterInstanceSnapshotV1 persistedCharacter)
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
    /// Builds fresh instances of the merged XP, holdings, money, scrap, ranked-skill,
    /// exact-loadout and BOX authorities, then exposes their SAVE-ADAPTERS-001 bindings.
    /// No subsystem authority is reimplemented here.
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

            InventoryLoadoutAuthoritySnapshotV1 loadout =
                ProductionCharacterAuthorityAdaptersV1.DecodeRequired(
                    character,
                    KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                    KnownSaveComponentCodecsV1.ExactInstanceLoadout);
            RankedSkillAllocationSnapshotV2 skills =
                ProductionCharacterAuthorityAdaptersV1.DecodeRequired(
                    character,
                    KnownSaveComponentDefinitionsV1.RankedSkillAllocation(),
                    KnownSaveComponentCodecsV1.RankedSkillAllocation);
            ScrapSnapshotV1 scrap =
                ProductionCharacterAuthorityAdaptersV1.DecodeRequired(
                    character,
                    KnownSaveComponentDefinitionsV1.ScrapWallet(),
                    KnownSaveComponentCodecsV1.ScrapWallet);

            return CreateGraph(
                character,
                RouteFromLoadout(
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    loadout),
                skills.ProfileId,
                skills.ClassId,
                StableId.Parse(scrap.AuthorityStableId),
                StableId.Parse(scrap.CurrencyStableId));
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

            PlayerRouteProfilePayloadV1 exactRoute =
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
                exactRoute,
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
                    50,
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
            ProductionCharacterStrongboxRuntimeV1 strongboxes =
                ProductionCharacterStrongboxCompositionV1.Create(
                    loadout,
                    money,
                    scrap);
            List<ISaveComponentAdapterV1> adapters =
                ProductionCharacterAuthorityAdaptersV1.Create(
                    loadout,
                    experience,
                    experienceCurve,
                    progressionContext,
                    money,
                    scrap,
                    scrapAuthorityId,
                    scrapCurrencyId,
                    skills,
                    skillProfileId,
                    strongboxes);

            var core = new ProductionCharacterRuntimeGraphV1(
                character,
                route,
                loadout,
                experience,
                money,
                scrap,
                skills,
                skillProfileId,
                strongboxes.Catalog,
                strongboxes.Authority,
                adapters);
            if (additionalAdapterFactory == null)
            {
                return core;
            }

            IEnumerable<ISaveComponentAdapterV1> additional =
                additionalAdapterFactory(core);
            if (additional == null)
            {
                return core;
            }
            adapters.AddRange(additional);
            return new ProductionCharacterRuntimeGraphV1(
                character,
                route,
                loadout,
                experience,
                money,
                scrap,
                skills,
                skillProfileId,
                strongboxes.Catalog,
                strongboxes.Authority,
                adapters);
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
            if (value.IndexOf(
                    "combat-medic",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf(
                    "healer",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "combat_medic";
            }
            if (value.IndexOf(
                    "juggernaut",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf(
                    "defensive",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "juggernaut";
            }
            return "striker";
        }
    }
}
