using NUnit.Framework;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Crafting.Presentation;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Crafting.Presentation
{
    public sealed class CraftingScreenPresentationTests
    {
        [Test]
        public void ProjectsLevelsBalanceAvailabilityAndStablePreview()
        {
            CraftingScreenFixture f = CraftingScreenFixture.Create(7, 73, true);
            CraftingScreenServiceV1 s = f.Service();
            CraftingRecipeProjectionV1 available = s.Snapshot.FindRecipe(StableId.Parse("recipe.available"));
            CraftingRecipeProjectionV1 locked = s.Snapshot.FindRecipe(StableId.Parse("recipe.locked"));
            string preview = available.PreviewEquipment.Fingerprint;
            string command = available.Command.Fingerprint;

            s.Refresh();

            Assert.That(available.NaturalDiscoveryLevel, Is.EqualTo(2));
            Assert.That(available.CraftingUnlockLevel, Is.EqualTo(5));
            Assert.That(available.ScrapCost, Is.EqualTo(25));
            Assert.That(available.ScrapBalance, Is.EqualTo(73));
            Assert.That(available.Availability, Is.EqualTo(CraftingRecipeAvailabilityV1.Available));
            Assert.That(locked.CraftingUnlockLevel, Is.EqualTo(11));
            Assert.That(locked.Availability, Is.EqualTo(CraftingRecipeAvailabilityV1.Locked));
            Assert.That(locked.PreviewEquipment, Is.Null);
            Assert.That(s.Snapshot.FindRecipe(available.RecipeStableId).PreviewEquipment.Fingerprint, Is.EqualTo(preview));
            Assert.That(s.Snapshot.FindRecipe(available.RecipeStableId).Command.Fingerprint, Is.EqualTo(command));
            Assert.That(f.Authority.PreviewCalls, Is.EqualTo(1));
        }

        [Test]
        public void SuccessSpendsAndGrantsExactlyOnce()
        {
            CraftingScreenFixture f = CraftingScreenFixture.Create(10, 100);
            CraftingScreenServiceV1 s = f.Service();
            string preview = s.Snapshot.SelectedRecipe.PreviewEquipment.Fingerprint;

            CraftingScreenResultV1 first = s.CraftSelected();
            CraftingScreenResultV1 duplicateClick = s.CraftSelected();

            Assert.That(first.Status, Is.EqualTo(CraftingScreenStatusV1.Crafted));
            Assert.That(first.AuthorityResult.Equipment.Fingerprint, Is.EqualTo(preview));
            Assert.That(duplicateClick.Status, Is.EqualTo(CraftingScreenStatusV1.AlreadyResolved));
            Assert.That(f.Authority.ScrapBalance, Is.EqualTo(75));
            Assert.That(f.Authority.CraftCalls, Is.EqualTo(1));
            Assert.That(f.Authority.Granted.Count, Is.EqualTo(1));
        }

        [Test]
        public void InsufficientScrapDoesNotCallAuthority()
        {
            CraftingScreenFixture f = CraftingScreenFixture.Create(10, 24);
            CraftingScreenResultV1 result = f.Service().CraftSelected();

            Assert.That(result.Status, Is.EqualTo(CraftingScreenStatusV1.InsufficientScrap));
            Assert.That(f.Authority.CraftCalls, Is.Zero);
            Assert.That(f.Authority.ScrapBalance, Is.EqualTo(24));
            Assert.That(f.Authority.Granted, Is.Empty);
        }

        [Test]
        public void RetryUsesExactSameOperationAndCommand()
        {
            CraftingScreenFixture f = CraftingScreenFixture.Create(10, 100);
            f.Authority.ReturnRetryOnce = true;
            CraftingScreenServiceV1 s = f.Service();
            string fingerprint = s.Snapshot.SelectedRecipe.Command.Fingerprint;
            StableId operation = s.Snapshot.SelectedRecipe.Command.CraftTransactionStableId;

            Assert.That(s.CraftSelected().Status, Is.EqualTo(CraftingScreenStatusV1.RetryRequired));
            Assert.That(s.RetrySelected().Status, Is.EqualTo(CraftingScreenStatusV1.Crafted));
            Assert.That(f.Authority.Commands.Count, Is.EqualTo(2));
            Assert.That(f.Authority.Commands[0].Fingerprint, Is.EqualTo(fingerprint));
            Assert.That(f.Authority.Commands[1].Fingerprint, Is.EqualTo(fingerprint));
            Assert.That(f.Authority.Commands[0].CraftTransactionStableId, Is.EqualTo(operation));
            Assert.That(f.Authority.Commands[1].CraftTransactionStableId, Is.EqualTo(operation));
            Assert.That(f.Authority.ScrapBalance, Is.EqualTo(75));
            Assert.That(f.Authority.Granted.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitNextAttemptAllowsDuplicateDefinitionAsSeparateInstance()
        {
            CraftingScreenFixture f = CraftingScreenFixture.Create(10, 100);
            CraftingScreenServiceV1 s = f.Service();
            CraftingScreenResultV1 first = s.CraftSelected();
            Assert.That(s.BeginNextAttempt().Status, Is.EqualTo(CraftingScreenStatusV1.PreviewReady));
            CraftingScreenResultV1 second = s.CraftSelected();

            Assert.That(first.AuthorityResult.Equipment.DefinitionId, Is.EqualTo(second.AuthorityResult.Equipment.DefinitionId));
            Assert.That(first.AuthorityResult.Equipment.InstanceId, Is.Not.EqualTo(second.AuthorityResult.Equipment.InstanceId));
            Assert.That(f.Authority.Granted.Count, Is.EqualTo(2));
            Assert.That(f.Authority.ScrapBalance, Is.EqualTo(50));
        }

        [Test]
        public void ReplayAndBackPreserveAuthorityAndRouteIdentity()
        {
            CraftingScreenFixture f = CraftingScreenFixture.Create(10, 100);
            CraftingScreenServiceV1 first = f.Service();
            CraftingScreenResultV1 applied = first.CraftSelected();
            Assert.That(first.Back().RoutePayload, Is.SameAs(f.Route));

            CraftingScreenServiceV1 revisit = f.Service();
            CraftingScreenResultV1 replay = revisit.CraftSelected();

            Assert.That(replay.Status, Is.EqualTo(CraftingScreenStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.AuthorityResult.Equipment.InstanceId, Is.EqualTo(applied.AuthorityResult.Equipment.InstanceId));
            Assert.That(revisit.Snapshot.ScrapBalance, Is.EqualTo(75));
            Assert.That(revisit.Snapshot.HoldingsSequence, Is.EqualTo(1));
            Assert.That(revisit.IncomingRoutePayload, Is.SameAs(f.Route));
            Assert.That(f.Authority.Granted.Count, Is.EqualTo(1));
        }
    }
}
