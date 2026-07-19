using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.Tests.EditMode.Weapons.Live
{
    public sealed class InventoryBackedWeaponExecutionAdapterTests
    {
        private const int TicksPerSecond = 60;

        private static readonly StableId HoldingsAuthorityId =
            StableId.Parse("holdings.test-player");
        private static readonly StableId ActorId =
            StableId.Parse("actor.test-player");
        private static readonly StableId ParticipantId =
            StableId.Parse("participant.test-player");
        private static readonly StableId QualityId =
            StableId.Parse("quality.common");
        private static readonly StableId EquipmentFamilyId =
            StableId.Parse("equipment-family.test-weapons");

        [Test]
        public void CatalogDefinitions_ResolveToExactImmutableExecutionProfiles()
        {
            EquipmentInstance blaster = Equipment(
                "equipment-instance.blaster",
                "equipment-definition.blaster");
            EquipmentInstance shotgun = Equipment(
                "equipment-instance.shotgun",
                "equipment-definition.shotgun");
            EquipmentInstance rocket = Equipment(
                "equipment-instance.rocket",
                "equipment-definition.rocket");
            EquipmentInstance flamethrower = Equipment(
                "equipment-instance.flamethrower",
                "equipment-definition.flamethrower");
            Harness harness = CreateHarness(blaster, shotgun, rocket, flamethrower);

            AssertProfile(
                harness,
                blaster,
                "fire.profile-blaster",
                "weapon.blaster-machine-gun",
                10d,
                1,
                0d,
                40d,
                30d,
                5d,
                1,
                0d,
                0d,
                6);
            AssertProfile(
                harness,
                shotgun,
                "fire.profile-shotgun",
                "weapon.shotgun",
                2d,
                7,
                24d,
                30d,
                15d,
                3d,
                0,
                0d,
                0d,
                30);
            AssertProfile(
                harness,
                rocket,
                "fire.profile-rocket",
                "weapon.rocket-launcher",
                1d,
                1,
                0d,
                12d,
                35d,
                4d,
                0,
                0d,
                0d,
                60);
            AssertProfile(
                harness,
                flamethrower,
                "fire.profile-flamethrower",
                "weapon.flamethrower",
                5d,
                4,
                12d,
                10d,
                8d,
                1d,
                0,
                4d,
                2d,
                12);

            Assert.That(
                harness.Sink.Batches[2].CoreBatch.Effects[0],
                Is.TypeOf<ExplosiveProjectileEffect>());
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(4));
        }

        [Test]
        public void Shotgun_UsesCatalogProjectileCountAndRealSpread()
        {
            EquipmentInstance shotgun = Equipment(
                "equipment-instance.shotgun-spread",
                "equipment-definition.shotgun");
            Harness harness = CreateHarness(shotgun);
            harness.Active.Set(shotgun);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request("fire.shotgun-spread", 0L, seed: 4421UL));

            Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(result.EffectBatch.Profile.ProjectileCount, Is.EqualTo(7));
            Assert.That(result.EffectBatch.Profile.SpreadDegrees, Is.EqualTo(24d));
            Assert.That(result.EffectBatch.EffectCount, Is.EqualTo(7));

            HashSet<string> directions = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < result.EffectBatch.CoreBatch.Effects.Count; index++)
            {
                DirectProjectileEffect effect =
                    (DirectProjectileEffect)result.EffectBatch.CoreBatch.Effects[index];
                directions.Add(effect.Direction.ToString());
            }

            Assert.That(directions.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Blaster_DoesNotAccidentallyUseShotgunBehavior()
        {
            EquipmentInstance blaster = Equipment(
                "equipment-instance.blaster-single",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(blaster);
            harness.Active.Set(blaster);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request("fire.blaster-single", 0L));

            Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(result.EffectBatch.Profile.DefinitionId.Value,
                Is.EqualTo("weapon.blaster-machine-gun"));
            Assert.That(result.EffectBatch.Profile.ProjectileCount, Is.EqualTo(1));
            Assert.That(result.EffectBatch.Profile.SpreadDegrees, Is.Zero);
            Assert.That(result.EffectBatch.EffectCount, Is.EqualTo(1));
            Assert.That(
                result.EffectBatch.CoreBatch.Effects[0],
                Is.TypeOf<DirectProjectileEffect>());
        }

        [Test]
        public void ConcreteEquipmentInstances_HaveIndependentCooldowns()
        {
            EquipmentInstance first = Equipment(
                "equipment-instance.blaster-cooldown-a",
                "equipment-definition.blaster");
            EquipmentInstance second = Equipment(
                "equipment-instance.blaster-cooldown-b",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(first, second);

            harness.Active.Set(first);
            Assert.That(
                harness.Adapter.TryExecute(Request("fire.cooldown-a", 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.Accepted));

            harness.Active.Set(second);
            Assert.That(
                harness.Adapter.TryExecute(Request("fire.cooldown-b", 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.Accepted));

            harness.Active.Set(first);
            Assert.That(
                harness.Adapter.TryExecute(Request("fire.cooldown-a-again", 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.CooldownActive));
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(2));
        }

        [Test]
        public void ExactReplay_DoesNotSubmitOrFireTwice()
        {
            EquipmentInstance blaster = Equipment(
                "equipment-instance.replay",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(blaster);
            harness.Active.Set(blaster);
            InventoryWeaponFireRequest request = Request("fire.exact-replay", 0L);

            InventoryWeaponExecutionResult first = harness.Adapter.TryExecute(request);
            InventoryWeaponExecutionResult replay = harness.Adapter.TryExecute(request);

            Assert.That(first.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(replay.Status, Is.EqualTo(WeaponExecutionStatus.ReplayAccepted));
            Assert.That(replay.IsExactReplay, Is.True);
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
            Assert.That(replay.EffectBatch.Fingerprint, Is.EqualTo(first.EffectBatch.Fingerprint));
        }

        [Test]
        public void ConflictingDuplicateOperation_IsRejectedWithoutSecondSubmission()
        {
            EquipmentInstance blaster = Equipment(
                "equipment-instance.conflict",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(blaster);
            harness.Active.Set(blaster);

            Assert.That(
                harness.Adapter.TryExecute(Request("fire.conflict", 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.Accepted));
            InventoryWeaponExecutionResult conflict = harness.Adapter.TryExecute(
                Request(
                    "fire.conflict",
                    0L,
                    aim: new WeaponVector2(0d, 1d)));

            Assert.That(
                conflict.Status,
                Is.EqualTo(WeaponExecutionStatus.ConflictingDuplicate));
            Assert.That(conflict.EffectBatch, Is.Null);
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownEquipmentInstance_FailsClosed()
        {
            EquipmentInstance known = Equipment(
                "equipment-instance.known",
                "equipment-definition.blaster");
            EquipmentInstance unknown = Equipment(
                "equipment-instance.not-in-inventory",
                "equipment-definition.blaster");
            Harness harness = CreateHarness(known);
            harness.Active.Set(unknown);

            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request("fire.unknown-equipment", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(WeaponExecutionStatus.MissingEquippedEquipment));
            Assert.That(result.EffectBatch, Is.Null);
            Assert.That(harness.Sink.Batches, Is.Empty);
        }

        private static void AssertProfile(
            Harness harness,
            EquipmentInstance equipment,
            string operationId,
            string definitionId,
            double fireRate,
            int projectileCount,
            double spread,
            double speed,
            double range,
            double directDamage,
            int pierce,
            double dotDps,
            double dotDuration,
            int cooldownTicks)
        {
            harness.Active.Set(equipment);
            InventoryWeaponExecutionResult result = harness.Adapter.TryExecute(
                Request(operationId, 0L));

            Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(result.EquipmentInstanceId.Value, Is.EqualTo(equipment.InstanceId));
            Assert.That(result.EffectBatch, Is.Not.Null);
            Assert.That(result.EffectBatch.Profile.DefinitionId.Value, Is.EqualTo(definitionId));
            Assert.That(result.EffectBatch.Profile.FireRate, Is.EqualTo(fireRate));
            Assert.That(result.EffectBatch.Profile.ProjectileCount, Is.EqualTo(projectileCount));
            Assert.That(result.EffectBatch.Profile.SpreadDegrees, Is.EqualTo(spread));
            Assert.That(result.EffectBatch.Profile.ProjectileSpeed, Is.EqualTo(speed));
            Assert.That(result.EffectBatch.Profile.Range, Is.EqualTo(range));
            Assert.That(
                result.EffectBatch.Profile.DirectDamagePerProjectile,
                Is.EqualTo(directDamage));
            Assert.That(result.EffectBatch.Profile.Pierce, Is.EqualTo(pierce));
            Assert.That(
                result.EffectBatch.Profile.DamageOverTimePerSecond,
                Is.EqualTo(dotDps));
            Assert.That(
                result.EffectBatch.Profile.DamageOverTimeDuration,
                Is.EqualTo(dotDuration));
            Assert.That(result.EffectBatch.Profile.CooldownTicks, Is.EqualTo(cooldownTicks));
        }

        private static Harness CreateHarness(params EquipmentInstance[] ownedEquipment)
        {
            EquipmentCatalog equipmentCatalog = CreateEquipmentCatalog();
            PlayerHoldingsService holdings = new PlayerHoldingsService(
                HoldingsAuthorityId,
                1000L,
                new AcceptingEquipmentValidator());
            for (int index = 0; index < ownedEquipment.Length; index++)
            {
                EquipmentInstance equipment = ownedEquipment[index];
                PlayerHoldingsMutationResultV1 add = holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        Id("transaction.add-" + index),
                        Id("operation.add-" + index),
                        HoldingsAuthorityId,
                        equipment,
                        HoldingProvenanceV1.Create(
                            Id("grant.add-" + index),
                            Id("source.test"))));
                Assert.That(add.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            }

            MutableActiveEquipmentSource active = new MutableActiveEquipmentSource();
            RecordingInventorySink sink = new RecordingInventorySink();
            InventoryBackedWeaponExecutionAdapter adapter =
                new InventoryBackedWeaponExecutionAdapter(
                    holdings,
                    active,
                    equipmentCatalog,
                    CreateWeaponCatalog(),
                    new TestOwnershipResolver(),
                    sink,
                    TicksPerSecond);
            return new Harness(adapter, active, sink);
        }

        private static EquipmentCatalog CreateEquipmentCatalog()
        {
            EquipmentQualityTier quality = EquipmentQualityTier.Create(
                QualityId,
                "Common",
                1);
            List<EquipmentDefinition> definitions = new List<EquipmentDefinition>
            {
                EquipmentDefinitionFor(
                    "equipment-definition.blaster",
                    "weapon.blaster-machine-gun",
                    "Blaster"),
                EquipmentDefinitionFor(
                    "equipment-definition.shotgun",
                    "weapon.shotgun",
                    "Shotgun"),
                EquipmentDefinitionFor(
                    "equipment-definition.rocket",
                    "weapon.rocket-launcher",
                    "Rocket Launcher"),
                EquipmentDefinitionFor(
                    "equipment-definition.flamethrower",
                    "weapon.flamethrower",
                    "Flamethrower"),
            };

            for (int index = 0; index < definitions.Count; index++)
            {
                EquipmentDefinition definition = definitions[index];
                definitions[index] = EquipmentDefinition.Create(
                    definition.DefinitionId,
                    definition.CategoryId,
                    definition.FamilyId,
                    definition.DisplayName,
                    definition.RuntimeWeaponReferenceId,
                    definition.ItemLevelRange,
                    definition.MaximumAugmentSlots,
                    new[] { quality },
                    definition.Tags);
            }

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                definitions,
                new AugmentDefinition[0]);
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static EquipmentDefinition EquipmentDefinitionFor(
            string equipmentDefinitionId,
            string weaponDefinitionId,
            string displayName)
        {
            return EquipmentDefinition.Create(
                Id(equipmentDefinitionId),
                EquipmentCategoryIds.Weapon,
                EquipmentFamilyId,
                displayName,
                Id(weaponDefinitionId),
                InclusiveIntRange.Create(1, 100),
                0,
                new EquipmentQualityTier[0],
                new StableId[0]);
        }

        private static WeaponCatalog CreateWeaponCatalog()
        {
            WeaponCatalogRules rules = new WeaponCatalogRules(
                true,
                false,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Thermal" },
                10,
                true,
                true,
                true);
            WeaponCatalogInputs inputs = new WeaponCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, WeaponRarityInput>(StringComparer.Ordinal)
                {
                    { "Common", new WeaponRarityInput("Common", 1000d, 0, 4d, 13d) },
                });
            WeaponArchetypeDefinition archetype = new WeaponArchetypeDefinition(
                "Test",
                "Test",
                1d,
                1d,
                1,
                1,
                0d,
                30d,
                30d,
                1d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0,
                0d,
                0d,
                1d);
            WeaponFamilyDefinition family = new WeaponFamilyDefinition(
                "test-family",
                "Test Family",
                "Test",
                "Kinetic",
                "Universal",
                1,
                20,
                20,
                3,
                "Common",
                "Common",
                "Common",
                1d,
                "Standard",
                "Test",
                "Test",
                WeaponCatalogAvailability.Live,
                new string[0]);

            return new WeaponCatalog(
                "0.1",
                "test",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal)
                {
                    { "Test", archetype },
                },
                new[] { family },
                new[]
                {
                    Definition(
                        "weapon.blaster-machine-gun",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    Definition(
                        "weapon.shotgun",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    Definition(
                        "weapon.rocket-launcher",
                        "Thermal",
                        1d,
                        1,
                        0d,
                        12d,
                        35d,
                        4d,
                        0,
                        areaDamage: 15d,
                        explosionRadius: 3d),
                    Definition(
                        "weapon.flamethrower",
                        "Thermal",
                        5d,
                        4,
                        12d,
                        10d,
                        8d,
                        1d,
                        0,
                        dotDps: 4d,
                        dotDuration: 2d,
                        poolRadius: 1.5d,
                        poolDuration: 3d),
                });
        }

        private static WeaponDefinitionData Definition(
            string id,
            string damageType,
            double fireRate,
            int projectileCount,
            double spread,
            double projectileSpeed,
            double range,
            double directDamage,
            int pierce,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            double dotDps = 0d,
            double dotDuration = 0d,
            double poolRadius = 0d,
            double poolDuration = 0d)
        {
            bool hasDot = dotDps > 0d;
            bool hasArea = areaDamage > 0d;
            return new WeaponDefinitionData(
                id,
                id,
                "test-family",
                1,
                damageType,
                "Test",
                "Universal",
                1,
                1,
                1,
                "Common",
                1000d,
                1d,
                1000d,
                4d,
                13d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                10d,
                hasDot ? 0.5d : hasArea ? 0.5d : 1d,
                hasArea ? 0.5d : 0d,
                hasDot ? 0.5d : 0d,
                fireRate,
                projectileCount,
                1,
                directDamage,
                spread,
                projectileSpeed,
                range,
                pierce,
                explosionRadius,
                areaDamage,
                dotDps,
                dotDuration,
                poolRadius,
                poolDuration,
                0,
                0d,
                0.5d,
                1d,
                0d,
                "Test",
                "Test",
                WeaponCatalogAvailability.Live,
                new string[0]);
        }

        private static EquipmentInstance Equipment(string instanceId, string definitionId)
        {
            return EquipmentInstance.Create(
                Id(instanceId),
                Id(definitionId),
                1,
                QualityId,
                new AugmentInstance[0]);
        }

        private static InventoryWeaponFireRequest Request(
            string operationId,
            long tick,
            ulong seed = 123UL,
            WeaponVector2 aim = null)
        {
            return new InventoryWeaponFireRequest(
                new WeaponActorInstanceId(ActorId),
                new FireOperationId(Id(operationId)),
                new LifecycleGeneration(0L),
                tick,
                seed,
                new WeaponVector2(2d, 3d),
                aim ?? new WeaponVector2(1d, 0d));
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class Harness
        {
            public Harness(
                InventoryBackedWeaponExecutionAdapter adapter,
                MutableActiveEquipmentSource active,
                RecordingInventorySink sink)
            {
                Adapter = adapter;
                Active = active;
                Sink = sink;
            }

            public InventoryBackedWeaponExecutionAdapter Adapter { get; }
            public MutableActiveEquipmentSource Active { get; }
            public RecordingInventorySink Sink { get; }
        }

        private sealed class MutableActiveEquipmentSource : IActiveWeaponEquipmentInstanceSource
        {
            private EquipmentInstanceId active;

            public void Set(EquipmentInstance equipment)
            {
                active = equipment == null ? null : new EquipmentInstanceId(equipment.InstanceId);
            }

            public bool TryResolveActiveEquipmentInstance(
                WeaponActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out EquipmentInstanceId equipmentInstanceId)
            {
                equipmentInstanceId = actorId == null || lifecycleGeneration == null
                    ? null
                    : active;
                return equipmentInstanceId != null;
            }
        }

        private sealed class TestOwnershipResolver : IWeaponActorOwnershipResolver
        {
            public bool TryResolveParticipant(
                WeaponActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out RunParticipantId participantId)
            {
                participantId = actorId == null || lifecycleGeneration == null
                    ? null
                    : new RunParticipantId(ParticipantId);
                return participantId != null;
            }
        }

        private sealed class RecordingInventorySink : IInventoryWeaponEffectBatchSink
        {
            public List<InventoryWeaponEffectBatch> Batches { get; } =
                new List<InventoryWeaponEffectBatch>();

            public WeaponEffectBatchSinkResult TryAccept(InventoryWeaponEffectBatch batch)
            {
                Batches.Add(batch);
                return WeaponEffectBatchSinkResult.Accept();
            }
        }

        private sealed class AcceptingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "catalog-test",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    new List<EquipmentModelIssue>());
            }
        }
    }
}
