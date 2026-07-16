using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;

namespace ShooterMover.Tests.EditMode.Progression.Random
{
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void Version1_SeedZero_MatchesFrozenSplitMix64Vectors()
        {
            ulong[] expected =
            {
                0xE220A8397B1DCDAFUL,
                0x6E789E6AA1B965F4UL,
                0x06C45D188009454FUL,
                0xF88BB8A8724C81ECUL,
                0x1B39896A51A8749BUL
            };

            DeterministicRandom random = DeterministicRandom.Create(0UL);
            for (int index = 0; index < expected.Length; index++)
            {
                random = random.NextUInt64(out ulong actual);
                Assert.That(actual, Is.EqualTo(expected[index]), $"Frozen vector {index} changed.");
            }
        }

        [Test]
        public void NamedSubstream_MatchesFrozenVersion1SeedAndSequence()
        {
            StableId purpose = StableId.Parse("rng.eligibility");
            DeterministicRandom random = DeterministicRandom.CreateSubstream(
                0x0123456789ABCDEFUL,
                DeterministicRandom.AlgorithmVersion1,
                purpose,
                0UL);

            Assert.That(random.StreamSeed, Is.EqualTo(0x590F362AC2071C8BUL));

            ulong[] expected =
            {
                0xE4A794A9D2AC195DUL,
                0x32A6EFF2A46A2C71UL,
                0x812979C0565FC18EUL
            };

            for (int index = 0; index < expected.Length; index++)
            {
                random = random.NextUInt64(out ulong actual);
                Assert.That(actual, Is.EqualTo(expected[index]));
            }

            Assert.That(random.GetTrace().Fingerprint, Is.EqualTo("7ae44635008fa1ae"));
        }

        [Test]
        public void EqualInputs_ProduceEqualSequencesAndTraces()
        {
            StableId purpose = StableId.Parse("rng.quality");
            DeterministicRandom left = DeterministicRandom.CreateSubstream(42UL, 1, purpose, 7UL);
            DeterministicRandom right = DeterministicRandom.CreateSubstream(42UL, 1, purpose, 7UL);

            for (int index = 0; index < 128; index++)
            {
                left = left.NextUInt64(out ulong leftValue);
                right = right.NextUInt64(out ulong rightValue);
                Assert.That(leftValue, Is.EqualTo(rightValue));
            }

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.GetTrace(), Is.EqualTo(right.GetTrace()));
        }

        [Test]
        public void NamedSubstreams_AreIsolatedAndDoNotConsumeParent()
        {
            DeterministicRandom parent = DeterministicRandom.Create(0xBADC0FFEE0DDF00DUL);
            DeterministicRandom originalParent = parent;
            StableId eligibilityPurpose = StableId.Parse("rng.eligibility");
            StableId cosmeticPurpose = StableId.Parse("rng.cosmetic");

            DeterministicRandom eligibilityBefore = parent.Fork(eligibilityPurpose, 3UL);
            DeterministicRandom cosmetic = parent.Fork(cosmeticPurpose, 99UL);
            for (int index = 0; index < 256; index++)
            {
                cosmetic = cosmetic.NextUInt64(out _);
            }

            parent = parent.NextUInt64(out _);
            parent = parent.NextUInt64(out _);
            DeterministicRandom eligibilityAfter = parent.Fork(eligibilityPurpose, 3UL);

            Assert.That(originalParent.SamplesConsumed, Is.Zero);
            Assert.That(eligibilityBefore, Is.EqualTo(eligibilityAfter));
            Assert.That(cosmetic.StreamSeed, Is.Not.EqualTo(eligibilityBefore.StreamSeed));
        }

        [Test]
        public void BoundedSampling_RemainsInsideRangeAndCoversRepresentativeValues()
        {
            const int bound = 7;
            bool[] seen = new bool[bound];
            DeterministicRandom random = DeterministicRandom.Create(123456789UL);

            for (int index = 0; index < 4096; index++)
            {
                random = random.NextInt32(bound, out int value);
                Assert.That(value, Is.GreaterThanOrEqualTo(0).And.LessThan(bound));
                seen[value] = true;
            }

            Assert.That(seen, Has.All.EqualTo(true));
        }

        [Test]
        public void Int32Range_SupportsNegativeLowerBounds()
        {
            DeterministicRandom random = DeterministicRandom.Create(991UL);
            for (int index = 0; index < 512; index++)
            {
                random = random.NextInt32(-12, 19, out int value);
                Assert.That(value, Is.GreaterThanOrEqualTo(-12).And.LessThan(19));
            }
        }

        [Test]
        public void UnitInterval_UsesDocumentedHigh53BitMapping()
        {
            DeterministicRandom random = DeterministicRandom.Create(0UL);
            random = random.NextUInt64(out ulong frozenWideSample);

            DeterministicRandom second = DeterministicRandom.Create(0UL);
            second = second.NextUnitInterval(out double unitSample);

            double expected = (frozenWideSample >> 11) * (1.0 / 9007199254740992.0);
            Assert.That(unitSample, Is.EqualTo(expected));
            Assert.That(unitSample, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            Assert.That(second, Is.EqualTo(random));
        }

        [Test]
        public void RationalChance_IsDeterministicAndRejectsInvalidFractions()
        {
            DeterministicRandom left = DeterministicRandom.Create(555UL);
            DeterministicRandom right = DeterministicRandom.Create(555UL);

            for (int index = 0; index < 64; index++)
            {
                left = left.NextChance(3UL, 11UL, out bool leftResult);
                right = right.NextChance(3UL, 11UL, out bool rightResult);
                Assert.That(leftResult, Is.EqualTo(rightResult));
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeterministicRandom.Create(1UL).NextChance(0UL, 0UL, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeterministicRandom.Create(1UL).NextChance(2UL, 1UL, out _));
        }

        [Test]
        public void TraceObservation_IsDeterministicAndConsumesNoValues()
        {
            DeterministicRandom random = DeterministicRandom.Create(0UL);
            DeterministicRandomTrace firstTrace = random.GetTrace();
            DeterministicRandomTrace secondTrace = random.GetTrace();

            Assert.That(firstTrace, Is.EqualTo(secondTrace));
            Assert.That(random.SamplesConsumed, Is.Zero);

            DeterministicRandom afterTrace = random.NextUInt64(out ulong afterTraceValue);
            DeterministicRandom direct = DeterministicRandom.Create(0UL).NextUInt64(out ulong directValue);
            Assert.That(afterTraceValue, Is.EqualTo(directValue));
            Assert.That(afterTrace, Is.EqualTo(direct));

            random = random.NextUInt64(out _);
            random = random.NextUInt64(out _);
            random = random.NextUInt64(out _);
            Assert.That(random.GetTrace().Fingerprint, Is.EqualTo("a06f983ab418b31f"));
        }

        [Test]
        public void InvalidRangesVersionsAndDefaultState_FailClosed()
        {
            Assert.Throws<NotSupportedException>(() => DeterministicRandom.Create(1UL, 0));
            Assert.Throws<NotSupportedException>(() => DeterministicRandom.Create(1UL, 2));
            Assert.Throws<NotSupportedException>(() => default(DeterministicRandom).NextUInt64(out _));
            Assert.Throws<ArgumentNullException>(
                () => DeterministicRandom.CreateSubstream(1UL, 1, null, 0UL));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeterministicRandom.Create(1UL).NextBoundedUInt64(0UL, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeterministicRandom.Create(1UL).NextInt32(0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DeterministicRandom.Create(1UL).NextInt32(5, 5, out _));
        }
    }
}
