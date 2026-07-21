using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Tests.EditMode.Modifiers.StatusEffects
{
    public sealed partial class StatusEffectAuthorityV1Tests
    {
        [Test]
        public void CatalogFingerprint_IsDefinitionOrderIndependent()
        {
            StatusEffectDefinitionV1 first = Definition(
                "status-effect.a",
                StatusEffectStackingPolicyV1.Refresh,
                1,
                5L,
                RuntimeModifierOperationV1.Flat,
                1m);
            StatusEffectDefinitionV1 second = Definition(
                "status-effect.b",
                StatusEffectStackingPolicyV1.Add,
                3,
                5L,
                RuntimeModifierOperationV1.Percentage,
                0.1m);

            StatusEffectCatalogV1 left =
                new StatusEffectCatalogV1(
                    "status-effects.fixture",
                    "1",
                    new[] { first, second });
            StatusEffectCatalogV1 right =
                new StatusEffectCatalogV1(
                    "status-effects.fixture",
                    "1",
                    new[] { second, first });

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
        }

        private static StatusEffectAuthorityV1 CreateAuthority(
            params StatusEffectDefinitionV1[] definitions)
        {
            return new StatusEffectAuthorityV1(
                SubjectId,
                0,
                Catalog(definitions));
        }

        private static StatusEffectCatalogV1 Catalog(
            params StatusEffectDefinitionV1[] definitions)
        {
            return new StatusEffectCatalogV1(
                "status-effects.fixture",
                "1",
                definitions);
        }

        private static StatusEffectDefinitionV1 Definition(
            string effectId,
            StatusEffectStackingPolicyV1 policy,
            int maximumStacks,
            long durationTicks,
            RuntimeModifierOperationV1 operation,
            decimal value,
            string targetId = "combat.damage-multiplier",
            string dispelCategoryId = "dispel.beneficial")
        {
            return new StatusEffectDefinitionV1(
                effectId,
                "1",
                durationTicks,
                maximumStacks,
                policy,
                dispelCategoryId,
                new[]
                {
                    new RuntimeModifierDefinitionV1(
                        "template." + effectId,
                        targetId,
                        operation,
                        value),
                });
        }

        private static ApplyStatusEffectCommandV1 Apply(
            string operationId,
            string effectId,
            string sourceId,
            long tick,
            int generation = 0)
        {
            return new ApplyStatusEffectCommandV1(
                operationId,
                SubjectId,
                generation,
                tick,
                effectId,
                sourceId);
        }

        private static RuntimeObservedFactV1 Kill(
            string factId,
            long tick)
        {
            return new RuntimeObservedFactV1(
                factId,
                "fact.enemy-killed",
                SubjectId,
                tick);
        }
    }
}
