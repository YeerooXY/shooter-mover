using System;
using NUnit.Framework;
using ShooterMover.Contracts.Progression.Curves;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Tests.EditMode.Progression.Curves
{
    public sealed class ProgressionCurveMathTests
    {
        [Test]
        public void SoftActivation_HasConfigurableNonZeroEarlyTail()
        {
            SoftActivationCurveParameters parameters = ActivationFixture();

            double farBelow = ProgressionCurveMath.EvaluateSoftActivation(0L, 100L, parameters);
            double transitionStart = ProgressionCurveMath.EvaluateSoftActivation(90L, 100L, parameters);

            Assert.That(farBelow, Is.EqualTo(0.02).Within(1e-12));
            Assert.That(transitionStart, Is.EqualTo(0.02).Within(1e-12));
            Assert.That(farBelow, Is.GreaterThan(0.0));
        }

        [Test]
        public void SoftActivation_TransitionsSmoothlyAndRemainsAvailableAboveActivation()
        {
            SoftActivationCurveParameters parameters = ActivationFixture();
            double atStart = ProgressionCurveMath.EvaluateSoftActivation(90L, 100L, parameters);
            double justAfterStart = ProgressionCurveMath.EvaluateSoftActivation(91L, 100L, parameters);
            double atNominal = ProgressionCurveMath.EvaluateSoftActivation(100L, 100L, parameters);
            double justBeforeEnd = ProgressionCurveMath.EvaluateSoftActivation(109L, 100L, parameters);
            double atEnd = ProgressionCurveMath.EvaluateSoftActivation(110L, 100L, parameters);
            double farAbove = ProgressionCurveMath.EvaluateSoftActivation(10000L, 100L, parameters);

            Assert.That(justAfterStart, Is.GreaterThan(atStart));
            Assert.That(atNominal, Is.GreaterThan(justAfterStart));
            Assert.That(justBeforeEnd, Is.GreaterThan(atNominal));
            Assert.That(atEnd, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(farAbove, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(justAfterStart - atStart, Is.LessThan(atNominal - justAfterStart));
        }

        [Test]
        public void OldItems_DecayExponentiallyButRetainConfiguredFloor()
        {
            ObsolescenceCurveParameters parameters = new ObsolescenceCurveParameters(
                decayStartsAfterLevels: 10L,
                halfLifeLevels: 20.0,
                minimumRetention: 0.10);

            double beforeDecay = ProgressionCurveMath.EvaluateOldItemRetention(60L, 50L, parameters);
            double oneHalfLife = ProgressionCurveMath.EvaluateOldItemRetention(80L, 50L, parameters);
            double veryOld = ProgressionCurveMath.EvaluateOldItemRetention(1000000000L, 1L, parameters);

            Assert.That(beforeDecay, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(oneHalfLife, Is.EqualTo(0.55).Within(1e-12));
            Assert.That(veryOld, Is.GreaterThanOrEqualTo(0.10));
            Assert.That(veryOld, Is.EqualTo(0.10).Within(1e-12));
        }

        [Test]
        public void SourceBias_ChangesWeightWithoutCreatingHardGate()
        {
            double lowBias = ProgressionCurveMath.ApplySourceBias(0.02, 0.25);
            double neutralBias = ProgressionCurveMath.ApplySourceBias(0.02, 1.0);
            double highBias = ProgressionCurveMath.ApplySourceBias(0.02, 8.0);

            Assert.That(lowBias, Is.GreaterThan(0.0));
            Assert.That(neutralBias, Is.GreaterThan(lowBias));
            Assert.That(highBias, Is.GreaterThan(neutralBias));
        }

        [Test]
        public void ItemEligibility_ComposesActivationRetentionBaseWeightAndSourceBias()
        {
            ItemEligibilityCurveParameters parameters = new ItemEligibilityCurveParameters(
                ActivationFixture(),
                new ObsolescenceCurveParameters(10L, 20.0, 0.10),
                baseWeight: 2.0,
                sourceBias: 3.0);

            double result = ProgressionCurveMath.EvaluateItemEligibilityWeight(
                currentLevel: 100L,
                itemLevel: 70L,
                nominalActivationLevel: 100L,
                parameters: parameters);

            double expectedActivation = 0.51;
            double expectedRetention = 0.55;
            Assert.That(result, Is.EqualTo(expectedActivation * expectedRetention * 6.0).Within(1e-12));
        }

        [Test]
        public void QualityAvailability_GrowsThroughSameSoftCurveFamily()
        {
            SoftActivationCurveParameters parameters = ActivationFixture();
            double early = ProgressionCurveMath.EvaluateQualityAvailability(20L, 100L, parameters);
            double middle = ProgressionCurveMath.EvaluateQualityAvailability(100L, 100L, parameters);
            double mature = ProgressionCurveMath.EvaluateQualityAvailability(120L, 100L, parameters);

            Assert.That(early, Is.EqualTo(0.02).Within(1e-12));
            Assert.That(middle, Is.GreaterThan(early).And.LessThan(1.0));
            Assert.That(mature, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void CraftingAvailability_IsStrictlyDelayedFromNaturalAvailability()
        {
            SoftActivationCurveParameters activation = ActivationFixture();
            CraftingAvailabilityCurveParameters crafting =
                new CraftingAvailabilityCurveParameters(activation, delayLevels: 20L);

            double naturalAtNominal = ProgressionCurveMath.EvaluateSoftActivation(100L, 100L, activation);
            double craftingAtNominal = ProgressionCurveMath.EvaluateCraftingAvailability(100L, 100L, crafting);
            double naturalLater = ProgressionCurveMath.EvaluateSoftActivation(120L, 100L, activation);
            double craftingLater = ProgressionCurveMath.EvaluateCraftingAvailability(120L, 100L, crafting);

            Assert.That(craftingAtNominal, Is.LessThan(naturalAtNominal));
            Assert.That(craftingLater, Is.LessThan(naturalLater));
            Assert.That(craftingAtNominal, Is.GreaterThan(0.0));
        }

        [Test]
        public void VeryHighLevels_RemainValidWithoutBuiltInCaps()
        {
            SoftActivationCurveParameters activation = ActivationFixture();
            CraftingAvailabilityCurveParameters crafting =
                new CraftingAvailabilityCurveParameters(activation, 250000L);
            ObsolescenceCurveParameters obsolescence =
                new ObsolescenceCurveParameters(100L, 50000.0, 0.03);

            Assert.DoesNotThrow(() =>
            {
                long current = long.MaxValue;
                long nominal = long.MaxValue - 1000000L;
                double natural = ProgressionCurveMath.EvaluateSoftActivation(
                    current,
                    nominal,
                    activation);
                double crafted = ProgressionCurveMath.EvaluateCraftingAvailability(
                    current,
                    nominal,
                    crafting);
                double retained = ProgressionCurveMath.EvaluateOldItemRetention(
                    current,
                    1L,
                    obsolescence);

                Assert.That(natural, Is.InRange(activation.EarlyTailWeight, 1.0));
                Assert.That(crafted, Is.InRange(activation.EarlyTailWeight, 1.0));
                Assert.That(retained, Is.InRange(obsolescence.MinimumRetention, 1.0));
            });
        }

        [Test]
        public void ParametersAndInputs_RejectZeroInvalidAndNonFiniteValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SoftActivationCurveParameters(0.0, 10L, 10L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SoftActivationCurveParameters(0.02, 0L, 10L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SoftActivationCurveParameters(0.02, 10L, 0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ObsolescenceCurveParameters(0L, 0.0, 0.10));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ObsolescenceCurveParameters(0L, 10.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ItemEligibilityCurveParameters(
                    ActivationFixture(),
                    new ObsolescenceCurveParameters(0L, 10.0, 0.10),
                    1.0,
                    0.0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CraftingAvailabilityCurveParameters(ActivationFixture(), 0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ProgressionCurveMath.EvaluateSoftActivation(-1L, 1L, ActivationFixture()));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ProgressionCurveMath.ApplySourceBias(double.NaN, 1.0));
        }

        [Test]
        public void EvaluationContract_IsImmutableAndRejectsInvalidValues()
        {
            ProgressionCurveEvaluation evaluation = new ProgressionCurveEvaluation(
                currentLevel: 100L,
                nominalActivationLevel: 120L,
                naturalAvailability: 0.4,
                oldItemRetention: 0.8,
                sourceBiasedWeight: 2.5,
                craftingAvailability: 0.2);

            Assert.That(evaluation.CurrentLevel, Is.EqualTo(100L));
            Assert.That(evaluation.CraftingAvailability, Is.EqualTo(0.2));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ProgressionCurveEvaluation(0L, 0L, 1.1, 1.0, 1.0, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ProgressionCurveEvaluation(0L, 0L, 0.5, 1.0, -1.0, 0.5));
        }

        private static SoftActivationCurveParameters ActivationFixture()
        {
            return new SoftActivationCurveParameters(
                earlyTailWeight: 0.02,
                earlyTailLevels: 10L,
                postNominalActivationLevels: 10L);
        }
    }
}
