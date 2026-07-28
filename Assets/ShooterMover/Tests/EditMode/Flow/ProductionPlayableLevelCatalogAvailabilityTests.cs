#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Levels.Selection;

namespace ShooterMover.Tests.EditMode.Flow
{
    public sealed class ProductionPlayableLevelCatalogAvailabilityTests
    {
        [Test]
        public void DefaultCatalog_AdvertisesOnlyResolvableProductionContent()
        {
            Assert.That(
                ProductionPlayableLevelCatalogV1.All.Select(
                    value => value.LevelStableId),
                Is.EquivalentTo(new[]
                {
                    ProductionPlayableLevelCatalogV1.FirstLevelStableId,
                }));

            ProductionPlayableLevelDefinitionV1 definition;
            Assert.That(
                ProductionPlayableLevelCatalogV1.TryResolve(
                    ProductionPlayableLevelCatalogV1.FirstLevelStableId,
                    out definition),
                Is.True);
            Assert.That(definition, Is.Not.Null);

            Assert.That(
                ProductionPlayableLevelCatalogV1.TryResolve(
                    ProductionPlayableLevelCatalogV1
                        .AuthoredCombatLoopTestLevelStableId,
                    out definition),
                Is.False);
            Assert.That(definition, Is.Null);
        }
    }
}
#endif
