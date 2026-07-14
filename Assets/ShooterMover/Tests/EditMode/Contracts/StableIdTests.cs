using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class StableIdTests
    {
        [TestCase("weapon.blaster-machine-gun")]
        [TestCase("enemy.pursuer-drone")]
        public void Parse_FrozenExamples_RoundTripCanonically(string canonical)
        {
            StableId parsed = StableId.Parse(canonical);

            Assert.That(parsed.ToString(), Is.EqualTo(canonical));
            Assert.That(StableId.Parse(parsed.ToString()), Is.EqualTo(parsed));
        }

        [Test]
        public void Parse_SeparatesNamespaceAndValue()
        {
            StableId parsed = StableId.Parse("weapon.blaster-machine-gun");

            Assert.That(parsed.Namespace, Is.EqualTo("weapon"));
            Assert.That(parsed.Value, Is.EqualTo("blaster-machine-gun"));
        }

        [Test]
        public void Create_FormatsCanonicalValue()
        {
            StableId created = StableId.Create("enemy", "pursuer-drone");

            Assert.That(created.ToString(), Is.EqualTo("enemy.pursuer-drone"));
        }

        [Test]
        public void TryParse_ValidValue_ReturnsCanonicalId()
        {
            StableId parsed;

            bool success = StableId.TryParse("factory.teleport-b-shop", out parsed);

            Assert.That(success, Is.True);
            Assert.That(parsed.ToString(), Is.EqualTo("factory.teleport-b-shop"));
        }

        [Test]
        public void EqualityAndOperators_UseCanonicalOrdinalValue()
        {
            StableId first = StableId.Parse("weapon.blaster-machine-gun");
            StableId second = StableId.Create("weapon", "blaster-machine-gun");
            StableId different = StableId.Parse("weapon.shotgun");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first, Is.Not.EqualTo(different));
        }

        [Test]
        public void HashCode_IsDeterministicForFrozenExamples()
        {
            Assert.That(
                StableId.Parse("weapon.blaster-machine-gun").GetHashCode(),
                Is.EqualTo(899414729));
            Assert.That(
                StableId.Parse("enemy.pursuer-drone").GetHashCode(),
                Is.EqualTo(2054100398));
        }

        [Test]
        public void EqualValues_HaveEqualHashCodesAndWorkAsDictionaryKeys()
        {
            StableId stored = StableId.Parse("enemy.pursuer-drone");
            StableId lookup = StableId.Create("enemy", "pursuer-drone");
            var dictionary = new Dictionary<StableId, string>
            {
                [stored] = "found",
            };

            Assert.That(stored.GetHashCode(), Is.EqualTo(lookup.GetHashCode()));
            Assert.That(dictionary[lookup], Is.EqualTo("found"));
        }

        [Test]
        public void Ordering_IsOrdinalAcrossCanonicalValues()
        {
            var values = new List<StableId>
            {
                StableId.Parse("weapon.shotgun"),
                StableId.Parse("weapon.blaster-machine-gun"),
                StableId.Parse("enemy.pursuer-drone"),
                StableId.Parse("weapon.arc-gun"),
            };

            values.Sort();

            Assert.That(
                values.ConvertAll(value => value.ToString()),
                Is.EqualTo(new[]
                {
                    "enemy.pursuer-drone",
                    "weapon.arc-gun",
                    "weapon.blaster-machine-gun",
                    "weapon.shotgun",
                }));
        }

        [Test]
        public void OrderingOperators_AgreeWithCompareTo()
        {
            StableId lower = StableId.Parse("enemy.pursuer-drone");
            StableId higher = StableId.Parse("weapon.blaster-machine-gun");

            Assert.That(lower.CompareTo(higher), Is.LessThan(0));
            Assert.That(lower < higher, Is.True);
            Assert.That(lower <= higher, Is.True);
            Assert.That(higher > lower, Is.True);
            Assert.That(higher >= lower, Is.True);
        }

        [Test]
        public void BoundaryLengths_AreAcceptedAtTheirLimits()
        {
            StableId maximumNamespace = StableId.Parse(
                new string('n', StableId.MaxNamespaceLength) + ".v");
            StableId maximumValue = StableId.Parse(
                "n." + new string('v', StableId.MaxValueLength));
            StableId maximumCanonical = StableId.Parse(
                new string('n', 31) + "." + new string('v', StableId.MaxValueLength));

            Assert.That(maximumNamespace.Namespace.Length, Is.EqualTo(StableId.MaxNamespaceLength));
            Assert.That(maximumValue.Value.Length, Is.EqualTo(StableId.MaxValueLength));
            Assert.That(maximumCanonical.ToString().Length, Is.EqualTo(StableId.MaxCanonicalLength));
        }

        [Test]
        public void Parse_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => StableId.Parse(null));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase("weapon")]
        [TestCase(".weapon")]
        [TestCase("weapon.")]
        [TestCase("weapon..blaster")]
        [TestCase("weapon.blaster.machine")]
        [TestCase("Weapon.blaster-machine-gun")]
        [TestCase("weapon.Blaster-machine-gun")]
        [TestCase("weapön.blaster")]
        [TestCase("weapon.blaster machine")]
        [TestCase("weapon_blaster.machine-gun")]
        [TestCase("weapon:blaster-machine-gun")]
        [TestCase("weapon.blaster_machine_gun")]
        [TestCase("-weapon.blaster")]
        [TestCase("weapon-.blaster")]
        [TestCase("weapon.-blaster")]
        [TestCase("weapon.blaster-")]
        [TestCase("weapon.blaster--machine-gun")]
        [TestCase("weapon../enemy")]
        [TestCase("weapon.enemy/../boss")]
        [TestCase("weapon.\\enemy")]
        [TestCase("weapon.%2e%2e")]
        public void Parse_MalformedOrAmbiguousValue_ThrowsFormatException(string invalid)
        {
            Assert.Throws<FormatException>(() => StableId.Parse(invalid));
        }

        [Test]
        public void TryParse_InvalidValue_ReturnsFalseAndNull()
        {
            StableId parsed;

            bool success = StableId.TryParse("Weapon.blaster-machine-gun", out parsed);

            Assert.That(success, Is.False);
            Assert.That(parsed, Is.Null);
        }

        [Test]
        public void TryParse_Null_ReturnsFalseAndNull()
        {
            StableId parsed;

            bool success = StableId.TryParse(null, out parsed);

            Assert.That(success, Is.False);
            Assert.That(parsed, Is.Null);
        }

        [Test]
        public void BoundaryLengths_RejectValuesBeyondEachLimit()
        {
            string namespaceTooLong = new string('n', StableId.MaxNamespaceLength + 1) + ".v";
            string valueTooLong = "n." + new string('v', StableId.MaxValueLength + 1);
            string canonicalTooLong =
                new string('n', StableId.MaxNamespaceLength)
                + "."
                + new string('v', StableId.MaxValueLength);

            Assert.Throws<FormatException>(() => StableId.Parse(namespaceTooLong));
            Assert.Throws<FormatException>(() => StableId.Parse(valueTooLong));
            Assert.That(canonicalTooLong.Length, Is.EqualTo(StableId.MaxCanonicalLength + 1));
            Assert.Throws<FormatException>(() => StableId.Parse(canonicalTooLong));
        }

        [Test]
        public void Create_RejectsNullAndMalformedComponents()
        {
            Assert.Throws<ArgumentNullException>(() => StableId.Create(null, "value"));
            Assert.Throws<ArgumentNullException>(() => StableId.Create("namespace", null));
            Assert.Throws<FormatException>(() => StableId.Create("Weapon", "value"));
            Assert.Throws<FormatException>(() => StableId.Create("weapon", "blaster--machine-gun"));
        }
    }
}
