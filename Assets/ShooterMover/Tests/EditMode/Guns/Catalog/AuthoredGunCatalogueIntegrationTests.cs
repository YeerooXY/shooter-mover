using System;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class AuthoredGunCatalogueIntegrationTests
    {
        [Test]
        public void ProductionProviderUsesGeneratedWeaponMakerCatalogue()
        {
            GunCatalogueView production = GunCatalogProvider.Current;

            Assert.That(AuthoredGunCatalogue.UsesGeneratedSource, Is.True);
            Assert.That(
                AuthoredGunCatalogue.SourceFingerprint,
                Does.StartWith("sha256:"));
            Assert.That(production, Is.SameAs(AuthoredGunCatalogue.Current));
            Assert.That(production.Families.Count, Is.GreaterThan(0));
            Assert.That(
                production.Blueprints.Count,
                Is.EqualTo(production.Families.Count * 3));

            Type retiredCatalogue = typeof(AuthoredGunCatalogue)
                .Assembly
                .GetType(
                    "ShooterMover.Application.Guns.Catalog.GunCatalogue",
                    false);
            Assert.That(
                retiredCatalogue,
                Is.Null,
                "The retired hand-built GunCatalogue authority must not return.");

            GunFamily rattler = FindFamily(production, "rattler");
            Assert.That(rattler.Marks.Count, Is.EqualTo(3));
            GunMark markOne = rattler.Marks[0];
            Gun blueprint = markOne.Blueprint;

            Assert.That(
                blueprint.DefinitionId.ToString(),
                Is.EqualTo("rattler.mk1"));
            Assert.That(
                blueprint.FireSettings.Mode,
                Is.EqualTo(GunFireMode.Automatic));
            Assert.That(blueprint.FireSettings.RateOfFire, Is.EqualTo(12d));
            Assert.That(blueprint.Damage.DirectDamage, Is.EqualTo(2d));
            Assert.That(blueprint.Impact.Ricochet, Is.Null);

            Gun resolved;
            Assert.That(
                production.TryGetBlueprint("rattler.mk1", out resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(blueprint));

            GunDefinitionData flatDefinition;
            Assert.That(
                production.GunCatalog.TryGetDefinition(
                    "rattler.mk1",
                    out flatDefinition),
                Is.True);
            Assert.That(
                production.EquipmentCatalog.FindEquipmentDefinition(
                    markOne.EquipmentDefinitionId),
                Is.Not.Null);
        }

        [Test]
        public void EveryAuthoredMarkReachesStrongboxAndEquipmentViews()
        {
            GunCatalogueView production = GunCatalogProvider.Current;
            int markCount = 0;

            for (int familyIndex = 0;
                 familyIndex < production.Families.Count;
                 familyIndex++)
            {
                GunFamily family = production.Families[familyIndex];
                Assert.That(
                    family.Marks.Count,
                    Is.EqualTo(3),
                    family.FamilyId);

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    string definitionId =
                        mark.Blueprint.DefinitionId.ToString();

                    Gun resolved;
                    Assert.That(
                        production.TryGetBlueprint(
                            definitionId,
                            out resolved),
                        Is.True,
                        definitionId);
                    Assert.That(resolved, Is.SameAs(mark.Blueprint));

                    GunDefinitionData flatDefinition;
                    Assert.That(
                        production.GunCatalog.TryGetDefinition(
                            definitionId,
                            out flatDefinition),
                        Is.True,
                        definitionId);
                    Assert.That(
                        production.EquipmentCatalog.FindEquipmentDefinition(
                            mark.EquipmentDefinitionId),
                        Is.Not.Null,
                        definitionId);
                    Assert.That(
                        mark.Blueprint.DropMetadata.BaseSelectionWeight,
                        Is.GreaterThan(0d),
                        definitionId);
                    markCount += 1;
                }
            }

            Assert.That(markCount, Is.EqualTo(production.Blueprints.Count));
            Assert.That(
                markCount,
                Is.EqualTo(production.EquipmentDefinitionIds.Count));
        }

        private static GunFamily FindFamily(
            GunCatalogueView catalogue,
            string familyId)
        {
            for (int index = 0; index < catalogue.Families.Count; index++)
            {
                GunFamily family = catalogue.Families[index];
                if (family.FamilyId == familyId)
                {
                    return family;
                }
            }

            Assert.Fail("Missing authored gun family: " + familyId);
            return null;
        }
    }
}
