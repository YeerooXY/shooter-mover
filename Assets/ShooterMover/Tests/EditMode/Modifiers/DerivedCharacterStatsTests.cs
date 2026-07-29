using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Characters.Stats
{
    public sealed class DerivedCharacterStatsTests
    {
        private readonly IDerivedCharacterStatComposer composer =
            new DefaultDerivedCharacterStatComposer();

        [Test]
        public void SameInputsAndDifferentInsertionOrder_ProduceSameFingerprint()
        {
            var firstBase = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIds.MaximumHealth, 100m },
                { DerivedStatTargetIds.MovementSpeed, 5m },
            };
            var secondBase = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIds.MovementSpeed, 5m },
                { DerivedStatTargetIds.MaximumHealth, 100m },
            };
            DerivedStatModifierSource equipment = Source(
                "equipment.loadout",
                DerivedStatSourcePriorities.Equipment,
                "equipment-fingerprint",
                new LiveModifierDefinition(
                    "equipment.armor-one",
                    DerivedStatTargetIds.MaximumHealth,
                    LiveModifierOperation.Flat,
                    20m));
            DerivedStatModifierSource skills = Source(
                "skills.allocation",
                DerivedStatSourcePriorities.Skills,
                "skill-fingerprint",
                new LiveModifierDefinition(
                    "skill.mobility",
                    DerivedStatTargetIds.MovementSpeed,
                    LiveModifierOperation.Percentage,
                    0.10m));

            DerivedCharacterStatsSnapshot left = composer.DeriveCharacter(
                Input(firstBase, new[] { skills, equipment }));
            DerivedCharacterStatsSnapshot right = composer.DeriveCharacter(
                Input(secondBase, new[] { equipment, skills }));

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(left.InputFingerprint, Is.EqualTo(right.InputFingerprint));
            Assert.That(left.Values, Is.EqualTo(right.Values));
        }

        [Test]
        public void PermanentSources_CombineCoreCombatAndRewardStats()
        {
            DerivedStatModifierSource equipment = Source(
                "equipment.loadout",
                DerivedStatSourcePriorities.Equipment,
                "equipment-fingerprint",
                new LiveModifierDefinition(
                    "equipment.chassis",
                    DerivedStatTargetIds.MaximumHealth,
                    LiveModifierOperation.Flat,
                    20m),
                new LiveModifierDefinition(
                    "equipment.servos",
                    DerivedStatTargetIds.MovementSpeed,
                    LiveModifierOperation.Percentage,
                    0.20m));
            DerivedStatModifierSource skills = Source(
                "skills.allocation",
                DerivedStatSourcePriorities.Skills,
                "skill-fingerprint",
                new LiveModifierDefinition(
                    "skill.vitality",
                    DerivedStatTargetIds.MaximumHealth,
                    LiveModifierOperation.Percentage,
                    0.10m),
                new LiveModifierDefinition(
                    "skill.precision",
                    DerivedStatTargetIds.CriticalChance,
                    LiveModifierOperation.Flat,
                    0.15m),
                new LiveModifierDefinition(
                    "skill.damage",
                    DerivedStatTargetIds.OutgoingDamageMultiplier,
                    LiveModifierOperation.Multiplicative,
                    1.25m));
            DerivedStatModifierSource account = Source(
                "account.progression",
                DerivedStatSourcePriorities.Account,
                "account-fingerprint",
                new LiveModifierDefinition(
                    "account.reward-bonus",
                    DerivedStatTargetIds.RewardMultiplier,
                    LiveModifierOperation.Multiplicative,
                    1.5m));

            DerivedCharacterStatsSnapshot result = composer.DeriveCharacter(
                Input(
                    BaseValues(
                        100m,
                        5m,
                        new Dictionary<string, decimal>
                        {
                            { DerivedStatTargetIds.CriticalChance, 0.05m },
                        }),
                    new[] { account, skills, equipment }));

            Assert.That(result.MaximumHealth, Is.EqualTo(132m));
            Assert.That(result.MovementSpeed, Is.EqualTo(6m));
            Assert.That(result.CriticalChance, Is.EqualTo(0.20m));
            Assert.That(result.OutgoingDamageMultiplier, Is.EqualTo(1.25m));
            Assert.That(
                result.GetValue(DerivedStatTargetIds.RewardMultiplier),
                Is.EqualTo(1.5m));
        }

        [Test]
        public void RunOnlyCondition_AppliesAfterPermanentCharacterComposition()
        {
            DerivedCharacterStatsSnapshot character = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), Array.Empty<DerivedStatModifierSource>()));
            DerivedStatModifierSource eventSource = Source(
                "event.double-drops",
                DerivedStatSourcePriorities.Events,
                "event-calendar-fingerprint",
                new LiveModifierDefinition(
                    "event.double-drops-2026",
                    DerivedStatTargetIds.StrongboxDropWeight,
                    LiveModifierOperation.Multiplicative,
                    2m,
                    "event.double-drops-active"));

            RunCombatProfile inactive = composer.BuildRunProfile(
                RunInput(character, new[] { eventSource }, Array.Empty<string>()));
            RunCombatProfile active = composer.BuildRunProfile(
                RunInput(
                    character,
                    new[] { eventSource },
                    new[] { "event.double-drops-active" }));

            Assert.That(
                inactive.GetValue(
                    DerivedStatTargetIds.StrongboxDropWeight),
                Is.EqualTo(1m));
            Assert.That(
                active.GetValue(
                    DerivedStatTargetIds.StrongboxDropWeight),
                Is.EqualTo(2m));
            Assert.That(active.Fingerprint, Is.Not.EqualTo(inactive.Fingerprint));
            Assert.That(
                active.CharacterStatsFingerprint,
                Is.EqualTo(character.Fingerprint));
        }

        [Test]
        public void ExplicitPolicy_ClampsImpossibleValues()
        {
            DerivedStatModifierSource source = Source(
                "equipment.loadout",
                DerivedStatSourcePriorities.Equipment,
                "equipment-fingerprint",
                new LiveModifierDefinition(
                    "equipment.broken-health",
                    DerivedStatTargetIds.MaximumHealth,
                    LiveModifierOperation.Flat,
                    -1000m),
                new LiveModifierDefinition(
                    "equipment.guaranteed-crit",
                    DerivedStatTargetIds.CriticalChance,
                    LiveModifierOperation.Flat,
                    5m),
                new LiveModifierDefinition(
                    "equipment.weapon-rack",
                    DerivedStatTargetIds.WeaponCapacity,
                    LiveModifierOperation.Flat,
                    100m));

            DerivedCharacterStatsSnapshot result = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { source }));

            Assert.That(result.MaximumHealth, Is.EqualTo(1m));
            Assert.That(result.CriticalChance, Is.EqualTo(1m));
            Assert.That(result.WeaponCapacity, Is.EqualTo(64));
        }

        [Test]
        public void NonIntegralCapacity_IsRejectedByExplicitRule()
        {
            DerivedStatModifierSource source = Source(
                "skills.allocation",
                DerivedStatSourcePriorities.Skills,
                "skill-fingerprint",
                new LiveModifierDefinition(
                    "skill.invalid-capacity",
                    DerivedStatTargetIds.WeaponCapacity,
                    LiveModifierOperation.Flat,
                    0.5m));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => composer.DeriveCharacter(
                    Input(BaseValues(100m, 5m), new[] { source })));

            Assert.That(error.Message, Does.Contain("whole number"));
        }

        [Test]
        public void ChangedEquipmentInputFingerprint_ChangesDerivedFingerprint()
        {
            LiveModifierSnapshot modifiers = new LiveModifierSnapshot(
                new[]
                {
                    new LiveModifierDefinition(
                        "equipment.armor",
                        DerivedStatTargetIds.Armor,
                        LiveModifierOperation.Flat,
                        10m),
                });
            var firstSource = new DerivedStatModifierSource(
                "equipment.loadout",
                DerivedStatSourcePriorities.Equipment,
                "equipment-instance-a",
                modifiers);
            var secondSource = new DerivedStatModifierSource(
                "equipment.loadout",
                DerivedStatSourcePriorities.Equipment,
                "equipment-instance-b",
                modifiers);

            DerivedCharacterStatsSnapshot first = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { firstSource }));
            DerivedCharacterStatsSnapshot second = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { secondSource }));

            Assert.That(first.Values, Is.EqualTo(second.Values));
            Assert.That(first.Fingerprint, Is.Not.EqualTo(second.Fingerprint));
            Assert.That(
                first.InputFingerprint,
                Is.Not.EqualTo(second.InputFingerprint));
        }

        [Test]
        public void RemovingSkillSource_RebuildsWithoutStaleContribution()
        {
            DerivedStatModifierSource skills = Source(
                "skills.allocation",
                DerivedStatSourcePriorities.Skills,
                "skill-before-respec",
                new LiveModifierDefinition(
                    "skill.mobility",
                    DerivedStatTargetIds.MovementSpeed,
                    LiveModifierOperation.Percentage,
                    0.20m));

            DerivedCharacterStatsSnapshot before = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { skills }));
            DerivedCharacterStatsSnapshot after = composer.DeriveCharacter(
                Input(
                    BaseValues(100m, 5m),
                    Array.Empty<DerivedStatModifierSource>()));

            Assert.That(before.MovementSpeed, Is.EqualTo(6m));
            Assert.That(after.MovementSpeed, Is.EqualTo(5m));
            Assert.That(after.Fingerprint, Is.Not.EqualTo(before.Fingerprint));
        }

        [Test]
        public void SkillAuthorityProjection_PreservesClassSpecificRankCurve()
        {
            var armorSkill = new RankedSkillDefinition(
                "skill.armor",
                "defense",
                1,
                new[] { "juggernaut" },
                null,
                null,
                new[]
                {
                    new SkillClassOverride(
                        "juggernaut",
                        2,
                        new[] { 5m, 9m }),
                },
                new[] { 2m },
                new[]
                {
                    new SkillEffectDescriptor(
                        DerivedStatTargetIds.Armor,
                        SkillModifierKind.Flat,
                        1m),
                },
                null);
            var catalog = new RankedSkillCatalog(
                "skills.schema.v2",
                "content.fixture",
                new[] { armorSkill },
                null);
            var authority = new RankedSkillAllocationState(catalog);
            authority.Seed(RankedSkillAllocationSnapshot.Empty(
                "profile.juggernaut-one",
                "juggernaut",
                catalog));
            SkillAllocationResult first = authority.Allocate(
                new AllocateSkillRankCommand(
                    "allocate.armor.1",
                    "profile.juggernaut-one",
                    armorSkill.Id,
                    0L,
                    2));
            SkillAllocationResult second = authority.Allocate(
                new AllocateSkillRankCommand(
                    "allocate.armor.2",
                    "profile.juggernaut-one",
                    armorSkill.Id,
                    1L,
                    2));
            LiveModifierSnapshot projected =
                SkillEffectModifierBridge.Adapt(second.Effects);
            var source = new DerivedStatModifierSource(
                "skills.allocation",
                DerivedStatSourcePriorities.Skills,
                second.Effects.Fingerprint,
                projected);

            DerivedCharacterStatsSnapshot result = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { source }));

            Assert.That(first.Accepted, Is.True);
            Assert.That(second.Accepted, Is.True);
            Assert.That(second.Snapshot.RankOf(armorSkill.Id), Is.EqualTo(2));
            Assert.That(armorSkill.EffectiveMaximumRank("juggernaut"), Is.EqualTo(2));
            Assert.That(result.Armor, Is.EqualTo(14m));
            Assert.That(result.SourceFingerprints, Has.Count.EqualTo(1));
        }

        [Test]
        public void ConditionalPermanentModifier_IsRejectedAtBoundary()
        {
            DerivedStatModifierSource source = Source(
                "skills.allocation",
                DerivedStatSourcePriorities.Skills,
                "skill-fingerprint",
                new LiveModifierDefinition(
                    "skill.killing-spree",
                    DerivedStatTargetIds.OutgoingDamageMultiplier,
                    LiveModifierOperation.Multiplicative,
                    1.25m,
                    "condition.killing-spree"));

            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Input(BaseValues(100m, 5m), new[] { source }));

            Assert.That(error.Message, Does.Contain("run profile input"));
        }

        [Test]
        public void UnknownTargetWithoutPolicyRule_FailsClosed()
        {
            DerivedStatModifierSource source = Source(
                "equipment.loadout",
                DerivedStatSourcePriorities.Equipment,
                "equipment-fingerprint",
                new LiveModifierDefinition(
                    "equipment.future-stat",
                    "combat.future-stat",
                    LiveModifierOperation.Flat,
                    1m));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => composer.DeriveCharacter(
                    Input(BaseValues(100m, 5m), new[] { source })));

            Assert.That(error.Message, Does.Contain("policy rule"));
        }

        private static DerivedCharacterStatInput Input(
            IDictionary<string, decimal> baseValues,
            IEnumerable<DerivedStatModifierSource> sources)
        {
            return new DerivedCharacterStatInput(
                "character.instance.one",
                new CharacterBaseStatProfile(
                    "class-profile.striker.level-10",
                    "striker",
                    10,
                    "class-definition-fingerprint",
                    baseValues),
                sources,
                DerivedStatPolicy.CreateDefault());
        }

        private static RunCombatProfileInput RunInput(
            DerivedCharacterStatsSnapshot character,
            IEnumerable<DerivedStatModifierSource> sources,
            IEnumerable<string> activeConditionIds)
        {
            return new RunCombatProfileInput(
                "run.one",
                "run-context-fingerprint",
                character,
                sources,
                activeConditionIds,
                DerivedStatPolicy.CreateDefault());
        }

        private static IDictionary<string, decimal> BaseValues(
            decimal maximumHealth,
            decimal movementSpeed,
            IDictionary<string, decimal> additional = null)
        {
            var result = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIds.MaximumHealth, maximumHealth },
                { DerivedStatTargetIds.MovementSpeed, movementSpeed },
            };
            foreach (KeyValuePair<string, decimal> pair in additional
                ?? new Dictionary<string, decimal>())
            {
                result.Add(pair.Key, pair.Value);
            }

            return result;
        }

        private static DerivedStatModifierSource Source(
            string sourceId,
            int priority,
            string inputFingerprint,
            params LiveModifierDefinition[] modifiers)
        {
            return new DerivedStatModifierSource(
                sourceId,
                priority,
                inputFingerprint,
                new LiveModifierSnapshot(modifiers));
        }
    }
}
