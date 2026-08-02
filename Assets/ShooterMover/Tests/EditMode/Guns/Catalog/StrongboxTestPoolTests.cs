using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class StrongboxTestPoolTests
    {
        private static readonly string[] ExpectedFamilyIds =
        {
            "hv_kestrel",
            "hv_breacher",
            "hv_vanguard",
            "teknova_spark",
            "teknova_pulse",
            "teknova_sovereign",
            "ronsen_cinder",
            "ronsen_furnace",
            "ronsen_sunspike",
            "virex_needle",
            "virex_corroder",
            "virex_apex",
        };

        [Test]
        public void TestPoolProvidesDeterministicRarityAndLevelDepth()
        {
            GunCatalogueView catalogue = GunCatalogue.Current;
            int common = 0;
            int rare = 0;
            int epic = 0;
            int legendary = 0;
            int artifact = 0;
            int marks = 0;

            for (int familyIndex = 0;
                 familyIndex < ExpectedFamilyIds.Length;
                 familyIndex++)
            {
                GunFamily family = FindFamily(
                    catalogue,
                    ExpectedFamilyIds[familyIndex]);
                Assert.That(family.Marks.Count, Is.EqualTo(3));

                switch (family.CatalogRarity)
                {
                    case "common": common += 1; break;
                    case "rare": rare += 1; break;
                    case "epic": epic += 1; break;
                    case "legendary": legendary += 1; break;
                    case "artifact": artifact += 1; break;
                    default:
                        Assert.Fail("Unexpected test-pool rarity: "
                            + family.CatalogRarity);
                        break;
                }

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    Assert.That(
                        mark.Blueprint.DropMetadata.BaseSelectionWeight,
                        Is.EqualTo(1d));
                    if (markIndex > 0)
                    {
                        Assert.That(
                            mark.DropAnchorLevel,
                            Is.GreaterThan(
                                family.Marks[markIndex - 1]
                                    .DropAnchorLevel));
                    }
                    marks += 1;
                }
            }

            Assert.That(common, Is.EqualTo(3));
            Assert.That(rare, Is.EqualTo(3));
            Assert.That(epic, Is.EqualTo(3));
            Assert.That(legendary, Is.EqualTo(2));
            Assert.That(artifact, Is.EqualTo(1));
            Assert.That(marks, Is.EqualTo(36));
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

            Assert.Fail("Missing Strongbox test family: " + familyId);
            return null;
        }
    }
}
