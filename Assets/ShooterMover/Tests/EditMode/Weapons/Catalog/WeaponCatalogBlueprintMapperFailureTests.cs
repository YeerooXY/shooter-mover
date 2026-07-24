using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Tests.EditMode.Weapons.Catalog
{
    public sealed partial class WeaponCatalogBlueprintMapperTests
    {
        [Test]
        public void Map_IntentMustMatchRequestedDefinitionIdentity()
        {
            WeaponCatalog catalog = BuildCatalog();

            WeaponBlueprintMappingResult missing = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(expectedDefinitionId: null));
            AssertIssue(missing, WeaponBlueprintMappingIssueCode.MissingIntentDefinitionId);

            WeaponBlueprintMappingResult mismatched = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(expectedDefinitionId: "other_weapon.mk1"));
            AssertIssue(mismatched, WeaponBlueprintMappingIssueCode.MismatchedIntentDefinitionId);
        }

        [Test]
        public void Map_EffectDataRequiresExplicitMissingSemantics()
        {
            WeaponCatalog catalog = BuildCatalog(
                areaDamage: 8d,
                explosionRadius: 2d,
                dotDps: 3d,
                dotDuration: 4d,
                chainTargets: 2,
                chainRange: 5d);

            WeaponBlueprintMappingResult result = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent());

            AssertIssue(result, WeaponBlueprintMappingIssueCode.MissingExplosionMapping);
            AssertIssue(result, WeaponBlueprintMappingIssueCode.MissingExplosionTrigger);
            AssertIssue(result, WeaponBlueprintMappingIssueCode.MissingDamageOverTimeMapping);
            AssertIssue(result, WeaponBlueprintMappingIssueCode.MissingChainMapping);
        }

        [Test]
        public void Map_ExplosionTriggerMustMatchAuthoredExplosionData()
        {
            WeaponCatalog explosionCatalog = BuildCatalog(
                areaDamage: 8d,
                explosionRadius: 2d);
            WeaponBlueprintMappingResult missingTrigger = WeaponCatalogBlueprintMapper.Map(
                explosionCatalog,
                "test_weapon.mk1",
                Intent(explosion: new WeaponCatalogExplosionMapping(0.25d)));
            AssertIssue(missingTrigger, WeaponBlueprintMappingIssueCode.MissingExplosionTrigger);

            WeaponImpactSpec triggeredImpact = WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                new WeaponExplosionTriggerSpec(true, false, false, false));
            WeaponBlueprintMappingResult unexpectedTrigger = WeaponCatalogBlueprintMapper.Map(
                BuildCatalog(),
                "test_weapon.mk1",
                Intent(impact: triggeredImpact));
            AssertIssue(unexpectedTrigger, WeaponBlueprintMappingIssueCode.UnexpectedExplosionTrigger);
        }

        [Test]
        public void Map_PresentationSelectionMustBeAuthoredAndUnambiguous()
        {
            WeaponCatalog catalog = BuildCatalog(
                definitionArt: new[] { "art/a.png", "art/b.png" });

            WeaponBlueprintMappingResult ambiguous = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent());
            AssertIssue(ambiguous, WeaponBlueprintMappingIssueCode.AmbiguousPresentationReference);

            WeaponBlueprintMappingResult unauthored = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(presentationReference: "art/c.png"));
            AssertIssue(unauthored, WeaponBlueprintMappingIssueCode.UnauthoredPresentationReference);

            WeaponBlueprintMappingResult selected = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent(presentationReference: "art/b.png"));
            Assert.That(selected.Succeeded, Is.True, JoinIssues(selected));
            Assert.That(selected.Blueprint.PresentationReference, Is.EqualTo("art/b.png"));
        }

        [Test]
        public void Map_PersistentPoolAndHealingFailWithoutDroppingValues()
        {
            WeaponCatalog poolCatalog = BuildCatalog(poolRadius: 2d, poolDuration: 4d);
            WeaponBlueprintMappingResult pool = WeaponCatalogBlueprintMapper.Map(
                poolCatalog,
                "test_weapon.mk1",
                Intent());
            AssertIssue(pool, WeaponBlueprintMappingIssueCode.UnsupportedPersistentPool);

            WeaponCatalog healingCatalog = BuildCatalog(healingPerSecond: 3d);
            WeaponBlueprintMappingResult healing = WeaponCatalogBlueprintMapper.Map(
                healingCatalog,
                "test_weapon.mk1",
                Intent());
            AssertIssue(healing, WeaponBlueprintMappingIssueCode.UnsupportedHealing);
        }

        [Test]
        public void PierceBoundaryNeverLosesFractionalInformation()
        {
            WeaponCatalog catalog = BuildCatalog(pierce: 2);
            WeaponBlueprintMappingResult mapped = WeaponCatalogBlueprintMapper.Map(
                catalog,
                "test_weapon.mk1",
                Intent());
            Assert.That(mapped.Succeeded, Is.True, JoinIssues(mapped));
            Assert.That(mapped.Blueprint.Projectile.Pierce.Tenths, Is.EqualTo(20));

            int legacy;
            Assert.That(new PierceValue(15).TryToLegacyInteger(out legacy), Is.False);
            Assert.That(new PierceValue(20).TryToLegacyInteger(out legacy), Is.True);
            Assert.That(legacy, Is.EqualTo(2));
        }

    }
}
