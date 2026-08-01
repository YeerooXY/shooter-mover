using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class CharacterLiveGraph :
        ICharacterLiveGraph
    {
        private CharacterInstanceSnapshot character;

        public CharacterLiveGraph(
            CharacterInstanceSnapshot character,
            PlayerRouteProfilePayload routePayload,
            PlayerLoadoutLive loadoutRuntime,
            PlayerExperience experienceAuthority,
            MoneyWalletActions moneyWallet,
            ScrapWalletActions scrapWallet,
            RankedSkillAllocationState skillAuthority,
            string skillProfileId,
            StrongboxDefinitionCatalog strongboxCatalog,
            StrongboxOpeningActions strongboxAuthority,
            IStrongboxOpeningRecoveryPort strongboxRecovery,
            IEnumerable<ISavePart> saveAdapters)
        {
            this.character = character
                ?? throw new ArgumentNullException(nameof(character));
            if (routePayload == null)
            {
                throw new ArgumentNullException(nameof(routePayload));
            }
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
            StrongboxRecovery = strongboxRecovery
                ?? throw new ArgumentNullException(nameof(strongboxRecovery));
            if (string.IsNullOrWhiteSpace(skillProfileId))
            {
                throw new ArgumentException(
                    "A ranked-skill profile identity is required.",
                    nameof(skillProfileId));
            }
            SkillProfileId = skillProfileId.Trim();

            SaveAdapters = new ReadOnlyCollection<ISavePart>(
                new List<ISavePart>(
                    saveAdapters
                    ?? throw new ArgumentNullException(nameof(saveAdapters))));
            if (SaveAdapters.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Character save adapters must be non-null.",
                    nameof(saveAdapters));
            }
        }

        public CharacterInstanceSnapshot Character
        {
            get { return character; }
        }

        public PlayerRouteProfilePayload RoutePayload
        {
            get { return LoadoutRuntime.CurrentRoutePayload; }
        }
        public PlayerLoadoutLive LoadoutRuntime { get; }
        public PlayerExperience ExperienceAuthority { get; }
        public MoneyWalletActions MoneyWallet { get; }
        public ScrapWalletActions ScrapWallet { get; }
        public RankedSkillAllocationState SkillAuthority { get; }
        public string SkillProfileId { get; }
        public StrongboxDefinitionCatalog StrongboxCatalog { get; }
        public StrongboxOpeningActions StrongboxAuthority { get; }
        public IStrongboxOpeningRecoveryPort StrongboxRecovery { get; }
        public IReadOnlyList<ISavePart> SaveAdapters { get; }
        public bool IsDisposed { get; private set; }

        public void MarkPersisted(
            CharacterInstanceSnapshot persistedCharacter)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(CharacterLiveGraph));
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

    public sealed class CharacterLiveGraphFactory :
        ICharacterLiveGraphFactory,
        IStarterCharacterLiveGraphFactory
    {
        private readonly PlayerExperienceCurve experienceCurve;
        private readonly ProgressionContext progressionContext;
        private readonly RankedSkillCatalog skillCatalog;
        private readonly Func<StableId, string> skillClassIdResolver;
        private readonly Func<CharacterLiveGraph,
            IEnumerable<ISavePart>> additionalAdapterFactory;

        public CharacterLiveGraphFactory(
            PlayerExperienceCurve experienceCurve,
            ProgressionContext progressionContext,
            RankedSkillCatalog skillCatalog,
            Func<StableId, string> skillClassIdResolver = null,
            Func<CharacterLiveGraph,
                IEnumerable<ISavePart>>
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

        public ICharacterLiveGraph CreateRestoreTarget(
            CharacterInstanceSnapshot character)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            PlayerHoldingsSnapshot holdings =
                CharacterStateAdapters.DecodeRequired(
                    character,
                    GameSaveParts.PlayerHoldings(),
                    GameSaveFormats.PlayerHoldings);
            InventoryLoadoutStateSnapshot loadout =
                CharacterStateAdapters.DecodeRequired(
                    character,
                    GameSaveParts.ExactInstanceLoadout(),
                    GameSaveFormats.ExactInstanceLoadout);
            RankedSkillAllocationSnapshot skills =
                CharacterStateAdapters.DecodeRequired(
                    character,
                    GameSaveParts.RankedSkillAllocation(),
                    GameSaveFormats.RankedSkillAllocation);
            ScrapSnapshot scrap =
                CharacterStateAdapters.DecodeRequired(
                    character,
                    GameSaveParts.ScrapWallet(),
                    GameSaveFormats.ScrapWallet);

            GunInventorySnapshot gunHoldings;
            string gunError;
            bool hasGunInventory = GunInventorySavePart.TryRead(
                character,
                out gunHoldings,
                out gunError);
            if (!hasGunInventory && !string.IsNullOrEmpty(gunError))
            {
                throw new InvalidOperationException(
                    "Canonical gun holdings are corrupt: " + gunError);
            }

            LoadoutSnapshot gunMountLoadout;
            string mountError;
            bool hasLoadout =
                LoadoutSavePart.TryRead(
                    character,
                    out gunMountLoadout,
                    out mountError);
            if (!hasLoadout && !string.IsNullOrEmpty(mountError))
            {
                throw new InvalidOperationException(
                    "Canonical gun mount loadout is corrupt: " + mountError);
            }
            if (hasLoadout && !hasGunInventory)
            {
                throw new InvalidOperationException(
                    "Canonical gun mount loadout requires canonical holdings.");
            }

            PlayerLoadoutLive inventory =
                PlayerLoadoutLive.Restore(
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    holdings,
                    gunHoldings,
                    gunMountLoadout,
                    loadout);
            return CreateGraph(
                character,
                inventory,
                skills.ProfileId,
                skills.ClassId,
                StableId.Parse(scrap.AuthorityStableId),
                StableId.Parse(scrap.CurrencyStableId));
        }

        public ICharacterLiveGraph CreateStarter(
            int slotIndex,
            StableId exactCharacterInstanceStableId,
            StableId classDefinitionStableId,
            string displayName,
            object legacyContext)
        {
            PlayerAccountSnapshot.ValidateSlotIndex(slotIndex);
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

            PlayerRouteProfilePayload onboardingRoute =
                PlayerRouteProfilePayload.Create(
                    exactCharacterInstanceStableId,
                    classDefinitionStableId,
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            var shell = new CharacterInstanceSnapshot(
                exactCharacterInstanceStableId,
                classDefinitionStableId,
                slotIndex,
                displayName,
                0L,
                null);
            return CreateGraph(
                shell,
                new PlayerLoadoutLive(onboardingRoute),
                exactCharacterInstanceStableId.ToString(),
                skillClassIdResolver(classDefinitionStableId),
                StableId.Parse("authority.production-scrap-wallet"),
                StableId.Parse("currency.scrap"));
        }

        public static CharacterLiveGraphFactory
            CreateVerticalSliceDefaults(
                Func<CharacterLiveGraph,
                    IEnumerable<ISavePart>>
                        additionalAdapterFactory = null)
        {
            return new CharacterLiveGraphFactory(
                PlayerExperienceCurve.CreateProduction(),
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }),
                RankedSkillSampleCatalog.Create(),
                additionalAdapterFactory: additionalAdapterFactory);
        }

        private CharacterLiveGraph CreateGraph(
            CharacterInstanceSnapshot character,
            PlayerLoadoutLive loadout,
            string skillProfileId,
            string skillClassId,
            StableId scrapAuthorityId,
            StableId scrapCurrencyId)
        {
            var experience = new PlayerExperience(
                experienceCurve,
                progressionContext);
            var money = new MoneyWalletActions();
            var scrap = new ScrapWalletActions(
                scrapAuthorityId,
                scrapCurrencyId);
            var skills = new RankedSkillAllocationState(skillCatalog);
            skills.Seed(RankedSkillAllocationSnapshot.Empty(
                skillProfileId,
                skillClassId,
                skillCatalog));
            CharacterStrongboxLive strongboxes =
                CharacterStrongboxSetup.Create(
                    loadout,
                    money,
                    scrap);
            List<ISavePart> adapters =
                CharacterStateAdapters.Create(
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

            var core = new CharacterLiveGraph(
                character,
                loadout.CurrentRoutePayload,
                loadout,
                experience,
                money,
                scrap,
                skills,
                skillProfileId,
                strongboxes.Catalog,
                strongboxes.Authority,
                strongboxes.Recovery,
                adapters);
            if (additionalAdapterFactory == null)
            {
                return core;
            }

            IEnumerable<ISavePart> additional =
                additionalAdapterFactory(core);
            if (additional == null)
            {
                return core;
            }

            adapters.AddRange(additional);
            return new CharacterLiveGraph(
                character,
                loadout.CurrentRoutePayload,
                loadout,
                experience,
                money,
                scrap,
                skills,
                skillProfileId,
                strongboxes.Catalog,
                strongboxes.Authority,
                strongboxes.Recovery,
                adapters);
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
