using System;
using System.Collections.Generic;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Crafting.Presentation;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Crafting.Presentation
{
    internal sealed class FakeCraftingAuthority : ICraftingPresentationAuthorityPortV1
    {
        private readonly CraftingRecipeCatalogV1 recipes;
        private readonly EquipmentCatalog equipment;
        private readonly Dictionary<StableId, EquipmentInstance> applied = new Dictionary<StableId, EquipmentInstance>();
        private bool retryReturned;

        public FakeCraftingAuthority(long scrap, CraftingRecipeCatalogV1 recipes, EquipmentCatalog equipment)
        {
            ScrapBalance = scrap;
            this.recipes = recipes;
            this.equipment = equipment;
        }

        public long ScrapBalance { get; private set; }
        public long ScrapSequence { get; private set; }
        public long HoldingsSequence { get; private set; }
        public int PreviewCalls { get; private set; }
        public int CraftCalls { get; private set; }
        public bool ReturnRetryOnce { get; set; }
        public List<EquipmentInstance> Granted { get; } = new List<EquipmentInstance>();
        public List<CraftEquipmentCommandV1> Commands { get; } = new List<CraftEquipmentCommandV1>();

        public CraftingPresentationAuthoritySnapshotV1 ExportSnapshot()
        {
            return new CraftingPresentationAuthoritySnapshotV1(
                ScrapBalance, ScrapSequence, HoldingsSequence, recipes, equipment,
                "fake|" + ScrapSequence + "|" + HoldingsSequence + "|" + ScrapBalance);
        }

        public CraftingPresentationAuthorityResultV1 Preview(CraftEquipmentCommandV1 command)
        {
            PreviewCalls++;
            return Result(command, CraftingResultStatusV1.Crafted, PreviewEquipment(command), string.Empty);
        }

        public CraftingPresentationAuthorityResultV1 Craft(CraftEquipmentCommandV1 command)
        {
            CraftCalls++;
            Commands.Add(command);
            EquipmentInstance existing;
            if (applied.TryGetValue(command.CraftTransactionStableId, out existing))
            {
                return Result(command, CraftingResultStatusV1.ExactDuplicateNoChange, existing, string.Empty);
            }
            if (ReturnRetryOnce && !retryReturned)
            {
                retryReturned = true;
                return Result(command, CraftingResultStatusV1.RewardApplicationRetryRequired,
                    PreviewEquipment(command), "reward-application-pending");
            }

            CraftingRecipeV1 recipe = recipes.Find(command.RecipeStableId);
            if (ScrapBalance < recipe.ScrapCost)
            {
                return Result(command, CraftingResultStatusV1.InsufficientScrap,
                    PreviewEquipment(command), "insufficient-scrap");
            }

            EquipmentInstance generated = PreviewEquipment(command);
            ScrapBalance -= recipe.ScrapCost;
            ScrapSequence++;
            HoldingsSequence++;
            applied.Add(command.CraftTransactionStableId, generated);
            Granted.Add(generated);
            return Result(command, CraftingResultStatusV1.Crafted, generated, string.Empty);
        }

        private CraftingPresentationAuthorityResultV1 Result(
            CraftEquipmentCommandV1 command,
            CraftingResultStatusV1 status,
            EquipmentInstance instance,
            string rejection)
        {
            CraftingRecipeV1 recipe = recipes.Find(command.RecipeStableId);
            return new CraftingPresentationAuthorityResultV1(
                status, command.RecipeStableId, recipe.ResolveUnlockLevel(command.RootSeed),
                recipe.ScrapCost, instance, command.Fingerprint, rejection);
        }

        private static EquipmentInstance PreviewEquipment(CraftEquipmentCommandV1 command)
        {
            return EquipmentInstance.Create(
                CraftingCanonicalV1.DeriveStableId("craftitem", command.CraftTransactionStableId.ToString()),
                StableId.Parse("weapon.shared"), 7, StableId.Parse("quality.standard"),
                Array.Empty<AugmentInstance>());
        }
    }
}
