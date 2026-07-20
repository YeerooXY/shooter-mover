using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Characters.Stats
{
    public sealed class DerivedCharacterStatsV1Tests
    {
        private readonly IDerivedCharacterStatComposerV1 composer =
            new DefaultDerivedCharacterStatComposerV1();

        [Test]
        public void SameInputsAndDifferentInsertionOrder_ProduceSameFingerprint()
        {
            var firstBase = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                { DerivedStatTargetIdsV1.MovementSpeed, 5m },
            };
            var secondBase = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIdsV1.MovementSpeed, 5m },
                { DerivedStatTargetIdsV1.MaximumHealth, 100m },
            };
            DerivedStatModifierSourceV1 equipment = Source(
                "equipment.loadout",
                DerivedStatSourcePrioritiesV1.Equipment,
                "equipment-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "equipment.armor-one",
                    DerivedStatTargetIdsV1.MaximumHealth,
                    RuntimeModifierOperationV1.Flat,
                    20m));
            DerivedStatModifierSourceV1 skills = Source(
                "skills.allocation",
                DerivedStatSourcePrioritiesV1.Skills,
                "skill-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "skill.mobility",
                    DerivedStatTargetIdsV1.MovementSpeed,
                    RuntimeModifierOperationV1.Percentage,
                    0.10m));

            DerivedCharacterStatsSnapshotV1 left = composer.DeriveCharacter(
                Input(firstBase, new[] { skills, equipment }));
            DerivedCharacterStatsSnapshotV1 right = composer.DeriveCharacter(
                Input(secondBase, new[] { equipment, skills }));

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(left.InputFingerprint, Is.EqualTo(right.InputFingerprint));
            Assert.That(left.Values, Is.EqualTo(right.Values));
        }

        [Test]
        public void PermanentSources_CombineCoreCombatAndRewardStats()
        {
            DerivedStatModifierSourceV1 equipment = Source(
                "equipment.loadout",
                DerivedStatSourcePrioritiesV1.Equipment,
                "equipment-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "equipment.chassis",
                    DerivedStatTargetIdsV1.MaximumHealth,
                    RuntimeModifierOperationV1.Flat,
                    20m),
                new RuntimeModifierDefinitionV1(
                    "equipment.servos",
                    DerivedStatTargetIdsV1.MovementSpeed,
                    RuntimeModifierOperationV1.Percentage,
                    0.20m));
            DerivedStatModifierSourceV1 skills = Source(
                "skills.allocation",
                DerivedStatSourcePrioritiesV1.Skills,
                "skill-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "skill.vitality",
                    DerivedStatTargetIdsV1.MaximumHealth,
                    RuntimeModifierOperationV1.Percentage,
                    0.10m),
                new RuntimeModifierDefinitionV1(
                    "skill.precision",
                    DerivedStatTargetIdsV1.CriticalChance,
                    RuntimeModifierOperationV1.Flat,
                    0.15m),
                new RuntimeModifierDefinitionV1(
                    "skill.damage",
                    DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m));
            DerivedStatModifierSourceV1 account = Source(
                "account.progression",
                DerivedStatSourcePrioritiesV1.Account,
                "account-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "account.reward-bonus",
                    DerivedStatTargetIdsV1.RewardMultiplier,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.5m));

            DerivedCharacterStatsSnapshotV1 result = composer.DeriveCharacter(
                Input(
                    BaseValues(
                        100m,
                        5m,
                        new Dictionary<string, decimal>
                        {
                            { DerivedStatTargetIdsV1.CriticalChance, 0.05m },
                        }),
                    new[] { account, skills, equipment }));

            Assert.That(result.MaximumHealth, Is.EqualTo(132m));
            Assert.That(result.MovementSpeed, Is.EqualTo(6m));
            Assert.That(result.CriticalChance, Is.EqualTo(0.20m));
            Assert.That(result.OutgoingDamageMultiplier, Is.EqualTo(1.25m));
            Assert.That(
                result.GetValue(DerivedStatTargetIdsV1.RewardMultiplier),
                Is.EqualTo(1.5m));
        }

        [Test]
        public void RunOnlyCondition_AppliesAfterPermanentCharacterComposition()
        {
            DerivedCharacterStatsSnapshotV1 character = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), Array.Empty<DerivedStatModifierSourceV1>()));
            DerivedStatModifierSourceV1 eventSource = Source(
                "event.double-drops",
                DerivedStatSourcePrioritiesV1.Events,
                "event-calendar-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "event.double-drops-2026",
                    DerivedStatTargetIdsV1.StrongboxDropWeight,
                    RuntimeModifierOperationV1.Multiplicative,
                    2m,
                    "event.double-drops-active"));

            RunCombatProfileV1 inactive = composer.BuildRunProfile(
                RunInput(character, new[] { eventSource }, Array.Empty<string>()));
            RunCombatProfileV1 active = composer.BuildRunProfile(
                RunInput(
                    character,
                    new[] { eventSource },
                    new[] { "event.double-drops-active" }));

            Assert.That(
                inactive.GetValue(
                    DerivedStatTargetIdsV1.StrongboxDropWeight),
                Is.EqualTo(1m));
            Assert.That(
                active.GetValue(
                    DerivedStatTargetIdsV1.StrongboxDropWeight),
                Is.EqualTo(2m));
            Assert.That(active.Fingerprint, Is.Not.EqualTo(inactive.Fingerprint));
            Assert.That(
                active.CharacterStatsFingerprint,
                Is.EqualTo(character.Fingerprint));
        }

        [Test]
        public void ExplicitPolicy_ClampsImpossibleValues()
        {
            DerivedStatModifierSourceV1 source = Source(
                "equipment.loadout",
                DerivedStatSourcePrioritiesV1.Equipment,
                "equipment-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "equipment.broken-health",
                    DerivedStatTargetIdsV1.MaximumHealth,
                    RuntimeModifierOperationV1.Flat,
                    -1000m),
                new RuntimeModifierDefinitionV1(
                    "equipment.guaranteed-crit",
                    DerivedStatTargetIdsV1.CriticalChance,
                    RuntimeModifierOperationV1.Flat,
                    5m),
                new RuntimeModifierDefinitionV1(
                    "equipment.weapon-rack",
                    DerivedStatTargetIdsV1.WeaponCapacity,
                    RuntimeModifierOperationV1.Flat,
                    100m));

            DerivedCharacterStatsSnapshotV1 result = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { source }));

            Assert.That(result.MaximumHealth, Is.EqualTo(1m));
            Assert.That(result.CriticalChance, Is.EqualTo(1m));
            Assert.That(result.WeaponCapacity, Is.EqualTo(64));
        }

        [Test]
        public void NonIntegralCapacity_IsRejectedByExplicitRule()
        {
            DerivedStatModifierSourceV1 source = Source(
                "skills.allocation",
                DerivedStatSourcePrioritiesV1.Skills,
                "skill-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "skill.invalid-capacity",
                    DerivedStatTargetIdsV1.WeaponCapacity,
                    RuntimeModifierOperationV1.Flat,
                    0.5m));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => composer.DeriveCharacter(
                    Input(BaseValues(100m, 5m), new[] { source })));

            Assert.That(error.Message, Does.Contain("whole number"));
        }

        [Test]
        public void ChangedEquipmentInputFingerprint_ChangesDerivedFingerprint()
        {
            RuntimeModifierSnapshotV1 modifiers = new RuntimeModifierSnapshotV1(
                new[]
                {
                    new RuntimeModifierDefinitionV1(
                        "equipment.armor",
                        DerivedStatTargetIdsV1.Armor,
                        RuntimeModifierOperationV1.Flat,
                        10m),
                });
            var firstSource = new DerivedStatModifierSourceV1(
                "equipment.loadout",
                DerivedStatSourcePrioritiesV1.Equipment,
                "equipment-instance-a",
                modifiers);
            var secondSource = new DerivedStatModifierSourceV1(
                "equipment.loadout",
                DerivedStatSourcePrioritiesV1.Equipment,
                "equipment-instance-b",
                modifiers);

            DerivedCharacterStatsSnapshotV1 first = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { firstSource }));
            DerivedCharacterStatsSnapshotV1 second = composer.DeriveCharacter(
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
            DerivedStatModifierSourceV1 skills = Source(
                "skills.allocation",
                DerivedStatSourcePrioritiesV1.Skills,
                "skill-before-respec",
                new RuntimeModifierDefinitionV1(
                    "skill.mobility",
                    DerivedStatTargetIdsV1.MovementSpeed,
                    RuntimeModifierOperationV1.Percentage,
                    0.20m));

            DerivedCharacterStatsSnapshotV1 before = composer.DeriveCharacter(
                Input(BaseValues(100m, 5m), new[] { skills }));
            DerivedCharacterStatsSnapshotV1 after = composer.DeriveCharacter(
                Input(
                    BaseValues(100m, 5m),
                    Array.Empty<DerivedStatModifierSourceV1>()));

            Assert.That(before.MovementSpeed, Is.EqualTo(6m));
            Assert.That(after.MovementSpeed, Is.EqualTo(5m));
            Assert.That(after.Fingerprint, Is.Not.EqualTo(before.Fingerprint));
        }

        [Test]
        public void SkillAuthorityProjection_PreservesClassSpecificRankCurve()
        {
            var armorSkill = new RankedSkillDefinitionV2(
                "skill.armor",
                "defense",
                1,
                new[] { "juggernaut" },
                null,
                null,
                new[]
                {
                    new SkillClassOverrideV2(
                        "juggernaut",
                        2,
                        new[] { 5m, 9m }),
                },
                new[] { 2m },
                new[]
                {
                    new SkillEffectDescriptorV2(
                        DerivedStatTargetIdsV1.Armor,
                        SkillModifierKindV2.Flat,
                        1m),
                },
                null);
            var catalog = new RankedSkillCatalogV2(
                "skills.schema.v2",
                "content.fixture",
                new[] { armorSkill },
                null);
            var authority = new RankedSkillAllocationAuthorityV2(catalog);
            authority.Seed(RankedSkillAllocationSnapshotV2.Empty(
                "profile.juggernaut-one",
                "juggernaut",
                catalog));
            SkillAllocationResultV2 first = authority.Allocate(
                new AllocateSkillRankCommandV2(
                    "allocate.armor.1",
                    "profile.juggernaut-one",
                    armorSkill.Id,
                    0L,
                    2));
            SkillAllocationResultV2 second = authority.Allocate(
                new AllocateSkillRankCommandV2(
                    "allocate.armor.2",
                    "profile.juggernaut-one",
                    armorSkill.Id,
                    1L,
                    2));
            RuntimeModifierSnapshotV1 projected =
                SkillEffectModifierAdapterV1.Adapt(second.Effects);
            var source = new DerivedStatModifierSourceV1(
                "skills.allocation",
                DerivedStatSourcePrioritiesV1.Skills,
                second.Effects.Fingerprint,
                projected);

            DerivedCharacterStatsSnapshotV1 result = composer.DeriveCharacter(
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
            DerivedStatModifierSourceV1 source = Source(
                "skills.allocation",
                DerivedStatSourcePrioritiesV1.Skills,
                "skill-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "skill.killing-spree",
                    DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m,
                    "condition.killing-spree"));

            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Input(BaseValues(100m, 5m), new[] { source }));

            Assert.That(error.Message, Does.Contain("run profile input"));
        }

        [Test]
        public void UnknownTargetWithoutPolicyRule_FailsClosed()
        {
            DerivedStatModifierSourceV1 source = Source(
                "equipment.loadout",
                DerivedStatSourcePrioritiesV1.Equipment,
                "equipment-fingerprint",
                new RuntimeModifierDefinitionV1(
                    "equipment.future-stat",
                    "combat.future-stat",
                    RuntimeModifierOperationV1.Flat,
                    1m));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => composer.DeriveCharacter(
                    Input(BaseValues(100m, 5m), new[] { source })));

            Assert.That(error.Message, Does.Contain("policy rule"));
        }

        private static DerivedCharacterStatInputV1 Input(
            IDictionary<string, decimal> baseValues,
            IEnumerable<DerivedStatModifierSourceV1> sources)
        {
            return new DerivedCharacterStatInputV1(
                "character.instance.one",
                new CharacterBaseStatProfileV1(
                    "class-profile.striker.level-10",
                    "striker",
                    10,
                    "class-definition-fingerprint",
                    baseValues),
                sources,
                DerivedStatPolicyV1.CreateDefault());
        }

        private static RunCombatProfileInputV1 RunInput(
            DerivedCharacterStatsSnapshotV1 character,
            IEnumerable<DerivedStatModifierSourceV1> sources,
            IEnumerable<string> activeConditionIds)
        {
            return new RunCombatProfileInputV1(
                "run.one",
                "run-context-fingerprint",
                character,
                sources,
                activeConditionIds,
                DerivedStatPolicyV1.CreateDefault());
        }

        private static IDictionary<string, decimal> BaseValues(
            decimal maximumHealth,
            decimal movementSpeed,
            IDictionary<string, decimal> additional = null)
        {
            var result = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIdsV1.MaximumHealth, maximumHealth },
                { DerivedStatTargetIdsV1.MovementSpeed, movementSpeed },
            };
            foreach (KeyValuePair<string, decimal> pair in additional
                ?? new Dictionary<string, decimal>())
            {
                result.Add(pair.Key, pair.Value);
            }

            return result;
        }

        private static DerivedStatModifierSourceV1 Source(
            string sourceId,
            int priority,
            string inputFingerprint,
            params RuntimeModifierDefinitionV1[] modifiers)
        {
            return new DerivedStatModifierSourceV1(
                sourceId,
                priority,
                inputFingerprint,
                new RuntimeModifierSnapshotV1(modifiers));
        }
    }
}
