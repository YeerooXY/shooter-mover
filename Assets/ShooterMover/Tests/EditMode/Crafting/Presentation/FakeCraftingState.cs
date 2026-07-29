using System;
using System.Collections.Generic;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Crafting.Presentation;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Crafting.Presentation
{
    internal sealed class FakeCraftingState : ICraftingPresentationStatePort
    {
        private readonly CraftingRecipeCatalog recipes;
        private readonly EquipmentCatalog equipment;
        private readonly Dictionary<StableId, EquipmentInstance> applied = new Dictionary<StableId, EquipmentInstance>();
        private bool retryReturned;

        public FakeCraftingState(long scrap, CraftingRecipeCatalog recipes, EquipmentCatalog equipment)
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
        public List<CraftEquipmentCommand> Commands { get; } = new List<CraftEquipmentCommand>();

        public CraftingPresentationStateSnapshot ExportSnapshot()
        {
            return new CraftingPresentationStateSnapshot(
                ScrapBalance, ScrapSequence, HoldingsSequence, recipes, equipment,
                "fake|" + ScrapSequence + "|" + HoldingsSequence + "|" + ScrapBalance);
        }

        public CraftingPresentationStateResult Preview(CraftEquipmentCommand command)
        {
            PreviewCalls++;
            return Result(command, CraftingResultStatus.Crafted, PreviewEquipment(command), string.Empty);
        }

        public CraftingPresentationStateResult Craft(CraftEquipmentCommand command)
        {
            CraftCalls++;
            Commands.Add(command);
            EquipmentInstance existing;
            if (applied.TryGetValue(command.CraftTransactionStableId, out existing))
            {
                return Result(command, CraftingResultStatus.ExactDuplicateNoChange, existing, string.Empty);
            }
            if (ReturnRetryOnce && !retryReturned)
            {
                retryReturned = true;
                return Result(command, CraftingResultStatus.RewardApplicationRetryRequired,
                    PreviewEquipment(command), "reward-application-pending");
            }

            CraftingRecipe recipe = recipes.Find(command.RecipeStableId);
            if (ScrapBalance < recipe.ScrapCost)
            {
                return Result(command, CraftingResultStatus.InsufficientScrap,
                    PreviewEquipment(command), "insufficient-scrap");
            }

            EquipmentInstance generated = PreviewEquipment(command);
            ScrapBalance -= recipe.ScrapCost;
            ScrapSequence++;
            HoldingsSequence++;
            applied.Add(command.CraftTransactionStableId, generated);
            Granted.Add(generated);
            return Result(command, CraftingResultStatus.Crafted, generated, string.Empty);
        }

        private CraftingPresentationStateResult Result(
            CraftEquipmentCommand command,
            CraftingResultStatus status,
            EquipmentInstance instance,
            string rejection)
        {
            CraftingRecipe recipe = recipes.Find(command.RecipeStableId);
            return new CraftingPresentationStateResult(
                status, command.RecipeStableId, recipe.ResolveUnlockLevel(command.RootSeed),
                recipe.ScrapCost, instance, command.Fingerprint, rejection);
        }

        private static EquipmentInstance PreviewEquipment(CraftEquipmentCommand command)
        {
            return EquipmentInstance.Create(
                CraftingFormat.DeriveStableId("craftitem", command.CraftTransactionStableId.ToString()),
                StableId.Parse("gun.shared"), 7, StableId.Parse("quality.standard"),
                Array.Empty<AugmentInstance>());
        }
    }
}
