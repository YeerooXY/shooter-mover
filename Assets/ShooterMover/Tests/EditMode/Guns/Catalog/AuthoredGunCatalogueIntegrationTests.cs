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
            Assert.That(production, Is.Not.SameAs(GunCatalogue.Current));
            Assert.That(production.Families.Count, Is.EqualTo(36));

            GunFamily rattler = FindFamily(production, "rattler");
            Assert.That(rattler.Marks.Count, Is.EqualTo(3));
            GunMark markOne = rattler.Marks[0];
            Gun blueprint = markOne.Blueprint;

            Assert.That(blueprint.DefinitionId.ToString(), Is.EqualTo("rattler.mk1"));
            Assert.That(blueprint.FireSettings.Mode, Is.EqualTo(GunFireMode.Automatic));
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
