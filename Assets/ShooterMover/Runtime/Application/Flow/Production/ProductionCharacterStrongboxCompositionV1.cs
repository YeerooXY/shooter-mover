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
    internal sealed class ProductionCharacterStrongboxRuntimeV1
    {
        public ProductionCharacterStrongboxRuntimeV1(
            StrongboxDefinitionCatalogV1 catalog,
            StrongboxOpeningServiceV1 authority,
            IStrongboxOpeningRecoveryPortV1 recovery,
            GeneratedEquipmentAugmentSignatureAuthorityV1 augmentSignatures)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Authority = authority ?? throw new ArgumentNullException(nameof(authority));
            Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            AugmentSignatures = augmentSignatures
                ?? throw new ArgumentNullException(nameof(augmentSignatures));
        }

        public StrongboxDefinitionCatalogV1 Catalog { get; }
        public StrongboxOpeningServiceV1 Authority { get; }
        public IStrongboxOpeningRecoveryPortV1 Recovery { get; }

        /// <summary>
        /// Exact-instance generated capacity/shared-level truth. Installed augments remain
        /// empty on fresh equipment and are owned by the equipment/augment authority.
        /// </summary>
        public GeneratedEquipmentAugmentSignatureAuthorityV1 AugmentSignatures
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
    internal static class ProductionCharacterStrongboxCompositionV1
    {
        public static readonly StableId GenerationPolicyStableId =
            StableId.Parse("generation-policy.production-character-strongbox");

        private static readonly StableId RewardApplicationAuthorityStableId =
            StableId.Parse("authority.production-character-reward-application");

        public static ProductionCharacterStrongboxRuntimeV1 Create(
            ProductionPlayerLoadoutRuntimeV1 loadout,
            MoneyWalletService money,
            ScrapWalletServiceV1 scrap)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (money == null) throw new ArgumentNullException(nameof(money));
            if (scrap == null) throw new ArgumentNullException(nameof(scrap));

            var definitions = new List<StrongboxDefinitionV1>();
            for (int index = 0;
                 index < ProductionStrongboxCatalogV1.Tiers.Count;
                 index++)
            {
                definitions.Add(
                    ProductionStrongboxCatalogV1.Tiers[index].CreateDefinition(
                        GenerationPolicyStableId));
            }

            var catalog = new StrongboxDefinitionCatalogV1(definitions);
            var generator = new RewardGenerationServiceV1();
            var augmentSignatures =
                new GeneratedEquipmentAugmentSignatureAuthorityV1();
            var equipmentResolver =
                new StrongboxHybridEquipmentGenerationResolverV1(
                    loadout.EquipmentCatalog,
                    loadout.WeaponCatalog,
                    augmentSignatures);
            var rewardApplication = new RewardApplicationServiceV1(
                RewardApplicationAuthorityStableId,
                new MoneyRewardChildAuthorityV1(money),
                new ScrapRewardChildAuthorityV1(scrap),
                new PlayerHoldingsRewardChildAuthorityV1(
                    loadout.Holdings,
                    loadout.CatalogAdapter));

            ProductionCollectedRunRewardTransferRuntimeRegistry
                .BindRewardApplication(
                    loadout.RoutePayload.SelectedCharacterStableId,
                    rewardApplication);

            var authority = new StrongboxOpeningServiceV1(
                catalog,
                new SharedStrongboxRewardGeneratorV1(generator),
                loadout.Holdings,
                rewardApplication,
                new DeterministicStrongboxGrantPayloadResolverV1(
                    equipmentResolver));
            return new ProductionCharacterStrongboxRuntimeV1(
                catalog,
                authority,
                new ExistingStrongboxOpeningRecoveryPortV1(
                    authority,
                    rewardApplication),
                augmentSignatures);
        }
    }
}
