using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class LowLevelStrongboxTestPoolTests
    {
        private static readonly string[] ExpectedFamilyIds =
        {
            "hv_finch",
            "hv_buckler",
            "teknova_flicker",
            "teknova_vector",
            "ronsen_ember",
            "ronsen_ashmaker",
            "virex_thorn",
            "virex_crown",
            "hv_paragon",
            "ronsen_warden",
            "teknova_singularity",
        };

        [Test]
        public void LowLevelPoolKeepsEveryMarkWithinLevelsOneThroughTen()
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
                        Assert.Fail("Unexpected low-level pool rarity: "
                            + family.CatalogRarity);
                        break;
                }

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    Assert.That(mark.DropAnchorLevel, Is.InRange(1, 10));
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
            Assert.That(epic, Is.EqualTo(2));
            Assert.That(legendary, Is.EqualTo(2));
            Assert.That(artifact, Is.EqualTo(1));
            Assert.That(marks, Is.EqualTo(33));
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

            Assert.Fail("Missing low-level Strongbox test family: " + familyId);
            return null;
        }
    }
}
