using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Flow.Production
{
    internal sealed class CharacterStrongboxLive
    {
        public CharacterStrongboxLive(
            StrongboxDefinitionCatalog catalog,
            StrongboxOpeningActions authority,
            IStrongboxOpeningRecoveryPort recovery,
            GeneratedEquipmentAugmentSignatureState augmentSignatures)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Authority = authority ?? throw new ArgumentNullException(nameof(authority));
            Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            AugmentSignatures = augmentSignatures
                ?? throw new ArgumentNullException(nameof(augmentSignatures));
        }

        public StrongboxDefinitionCatalog Catalog { get; }
        public StrongboxOpeningActions Authority { get; }
        public IStrongboxOpeningRecoveryPort Recovery { get; }

        /// <summary>
        /// Exact-instance generated capacity/shared-level state. Payload generation stages
        /// intent; RAP commits it only after the matching equipment holding is applied.
        /// Installed augments remain owned by the equipment/augment authority.
        /// </summary>
        public GeneratedEquipmentAugmentSignatureState AugmentSignatures
        {
            get;
        }
    }

    /// <summary>
    /// Builds the production BOX/RAP authorities over one character graph. Strongbox
    /// equipment payloads are resolved by the same hybrid policy/catalog used by balance
    /// simulation; the older power-budget equipment resolver is not part of this path.
    /// The reward-application authority remains registered with the durable collected-run
    /// transfer boundary introduced by DROP-PERSIST-PROOF-001.
    /// </summary>
    internal static class CharacterStrongboxSetup
    {
        public static readonly StableId GenerationPolicyStableId =
            StableId.Parse("generation-policy.production-character-strongbox");

        private static readonly StableId RewardApplicationAuthorityStableId =
            StableId.Parse("authority.production-character-reward-application");

        public static CharacterStrongboxLive Create(
            PlayerLoadoutLive loadout,
            MoneyWalletActions money,
            ScrapWalletActions scrap)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (money == null) throw new ArgumentNullException(nameof(money));
            if (scrap == null) throw new ArgumentNullException(nameof(scrap));

            var definitions = new List<StrongboxDefinition>();
            for (int index = 0;
                 index < StrongboxCatalog.Tiers.Count;
                 index++)
            {
                definitions.Add(
                    StrongboxCatalog.Tiers[index].CreateDefinition(
                        GenerationPolicyStableId));
            }

            var catalog = new StrongboxDefinitionCatalog(definitions);
            var generator = new RewardGenerationActions();
            var augmentSignatures =
                new GeneratedEquipmentAugmentSignatureState();
            var equipmentResolver =
                new StrongboxHybridEquipmentGenerationResolver(
                    loadout.EquipmentCatalog,
                    loadout.WeaponCatalog,
                    augmentSignatures);
            var rewardApplication = new RewardApplicationActions(
                RewardApplicationAuthorityStableId,
                new MoneyRewardChildState(money),
                new ScrapRewardChildState(scrap),
                new GeneratedAugmentSignaturePlayerHoldingsRewardChildState(
                    loadout.LegacyHoldings,
                    loadout.CatalogBridge,
                    augmentSignatures));

            CollectedRunRewardTransferLiveRegistry
                .BindRewardApplication(
                    loadout.RoutePayload.SelectedCharacterStableId,
                    rewardApplication);

            var authority = new StrongboxOpeningActions(
                catalog,
                new SharedStrongboxRewardGenerator(generator),
                loadout.Holdings,
                rewardApplication,
                new TransactionalStrongboxGrantPayloadResolver(
                    new DeterministicStrongboxGrantPayloadResolver(
                        equipmentResolver),
                    augmentSignatures));
            return new CharacterStrongboxLive(
                catalog,
                authority,
                new ExistingStrongboxOpeningRecoveryPort(
                    authority,
                    rewardApplication),
                augmentSignatures);
        }
    }
}
