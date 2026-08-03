using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class Pr288WeaponMigrationTests
    {
        private static readonly string[] ExpectedFamilyIds =
        {
            "shotgun",
            "sniper",
            "fast_sniper",
            "heavy_gatling",
            "blaster",
            "arc_rifle",
            "pulse_shotgun",
            "inferno_scattergun",
            "bio_needler",
            "corrosive_scattergun",
            "acid_rifle",
        };

        [Test]
        public void MigratedFamiliesReachCanonicalStrongboxAndEquipmentViews()
        {
            GunCatalogueView catalogue = GunCatalogue.Current;
            int migratedMarkCount = 0;

            for (int familyIndex = 0;
                 familyIndex < ExpectedFamilyIds.Length;
                 familyIndex++)
            {
                GunFamily family = FindFamily(
                    catalogue,
                    ExpectedFamilyIds[familyIndex]);
                Assert.That(family.Marks.Count, Is.EqualTo(3));

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    Gun blueprint = mark.Blueprint;
                    string definitionId = blueprint.DefinitionId.ToString();

                    Assert.That(
                        blueprint.DropMetadata.BaseSelectionWeight,
                        Is.EqualTo(1d),
                        definitionId + " must use the current neutral base weight.");

                    Gun resolvedBlueprint;
                    Assert.That(
                        catalogue.TryGetBlueprint(
                            definitionId,
                            out resolvedBlueprint),
                        Is.True,
                        definitionId + " must reach the canonical lookup.");
                    Assert.That(resolvedBlueprint, Is.SameAs(blueprint));

                    GunDefinitionData flatDefinition;
                    Assert.That(
                        catalogue.GunCatalog.TryGetDefinition(
                            definitionId,
                            out flatDefinition),
                        Is.True,
                        definitionId + " must reach the Strongbox catalogue.");

                    Assert.That(
                        catalogue.EquipmentCatalog.FindEquipmentDefinition(
                            mark.EquipmentDefinitionId),
                        Is.Not.Null,
                        definitionId + " must reach the equipment catalogue.");
                    migratedMarkCount += 1;
                }
            }

            Assert.That(migratedMarkCount, Is.EqualTo(33));
        }

        private static GunFamily FindFamily(
            GunCatalogueView catalogue,
            string familyId)
        {
            for (int index = 0;
                 index < catalogue.Families.Count;
                 index++)
            {
                GunFamily family = catalogue.Families[index];
                if (family.FamilyId == familyId)
                {
                    return family;
                }
            }

            Assert.Fail("Missing migrated gun family: " + familyId);
            return null;
        }
    }
}
