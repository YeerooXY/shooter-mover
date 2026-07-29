using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Tests.EditMode.Modifiers.StatusEffects
{
    public sealed partial class StatusEffectStateTests
    {
        [Test]
        public void CatalogFingerprint_IsDefinitionOrderIndependent()
        {
            StatusEffectDefinition first = Definition(
                "status-effect.a",
                StatusEffectStackingPolicy.Refresh,
                1,
                5L,
                LiveModifierOperation.Flat,
                1m);
            StatusEffectDefinition second = Definition(
                "status-effect.b",
                StatusEffectStackingPolicy.Add,
                3,
                5L,
                LiveModifierOperation.Percentage,
                0.1m);

            StatusEffectCatalog left =
                new StatusEffectCatalog(
                    "status-effects.fixture",
                    "1",
                    new[] { first, second });
            StatusEffectCatalog right =
                new StatusEffectCatalog(
                    "status-effects.fixture",
                    "1",
                    new[] { second, first });

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
        }

        private static StatusEffectState CreateAuthority(
            params StatusEffectDefinition[] definitions)
        {
            return new StatusEffectState(
                SubjectId,
                0,
                Catalog(definitions));
        }

        private static StatusEffectCatalog Catalog(
            params StatusEffectDefinition[] definitions)
        {
            return new StatusEffectCatalog(
                "status-effects.fixture",
                "1",
                definitions);
        }

        private static StatusEffectDefinition Definition(
            string effectId,
            StatusEffectStackingPolicy policy,
            int maximumStacks,
            long durationTicks,
            LiveModifierOperation operation,
            decimal value,
            string targetId = "combat.damage-multiplier",
            string dispelCategoryId = "dispel.beneficial")
        {
            return new StatusEffectDefinition(
                effectId,
                "1",
                durationTicks,
                maximumStacks,
                policy,
                dispelCategoryId,
                new[]
                {
                    new LiveModifierDefinition(
                        "template." + effectId,
                        targetId,
                        operation,
                        value),
                });
        }

        private static ApplyStatusEffectCommand Apply(
            string operationId,
            string effectId,
            string sourceId,
            long tick,
            int generation = 0)
        {
            return new ApplyStatusEffectCommand(
                operationId,
                SubjectId,
                generation,
                tick,
                effectId,
                sourceId);
        }

        private static LiveObservedFact Kill(
            string factId,
            long tick)
        {
            return new LiveObservedFact(
                factId,
                "fact.enemy-killed",
                SubjectId,
                tick);
        }
    }
}
