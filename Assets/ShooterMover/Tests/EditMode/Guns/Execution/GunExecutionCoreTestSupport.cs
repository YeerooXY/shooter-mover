using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Tests.EditMode.Guns.Execution
{
    public sealed partial class GunExecutionCoreTests
    {
        private static Harness HarnessFor(
            GunDefinitionData definition,
            IEnumerable<EquipmentInstance> equippedInstances,
            StableId runtimeReferenceId = null,
            IGunBehaviorSelector selector = null,
            GunBehaviorRegistry registry = null,
            RecordingSink sink = null)
        {
            StableId runtimeReference = runtimeReferenceId
                ?? new GunDefinitionId(definition.DefinitionId)
                    .ToRuntimeReference();
            EquipmentQualityTier quality = EquipmentQualityTier.Create(
                QualityStableId,
                "Common",
                1);
            EquipmentDefinition equipmentDefinition = EquipmentDefinition.Create(
                EquipmentDefinitionStableId,
                EquipmentCategoryIds.Gun,
                StableId.Parse("equipment-family.test"),
                "Test Gun",
                runtimeReference,
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { quality },
                new StableId[0]);
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { equipmentDefinition },
                new AugmentDefinition[0]);
            Assert.That(build.IsValid, Is.True);

            GunCatalogLiveProfileResolver profiles =
                new GunCatalogLiveProfileResolver(
                    build.Catalog,
                    Catalog(definition),
                    selector ?? new DefaultGunBehaviorSelector(),
                    60);
            RecordingSink actualSink = sink ?? new RecordingSink();
            TestEquippedResolver equippedResolver =
                new TestEquippedResolver(equippedInstances);
            GunExecutionCore core = new GunExecutionCore(
                new TestOwnershipResolver(),
                equippedResolver,
                profiles,
                registry ?? GunBehaviorRegistry.CreateWithBuiltIns(),
                actualSink);
            return new Harness(core, actualSink);
        }

        private static GunCatalog Catalog(GunDefinitionData definition)
        {
            GunCatalogRules rules = new GunCatalogRules(
                true,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Thermal", "Energized" },
                10,
                true,
                true,
                true);
            GunCatalogInputs inputs = new GunCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, GunRarityInput>(StringComparer.Ordinal)
                {
                    { "Common", new GunRarityInput("Common", 1000d, 0, 4d, 13d) },
                });
            GunArchetypeDefinition archetype = new GunArchetypeDefinition(
                "Test",
                "Test",
                1d,
                Math.Max(1d, definition.FireRate),
                Math.Max(1, definition.ProjectilesPerTrigger),
                1,
                Math.Max(0d, definition.SpreadDegrees),
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
            GunFamilyDefinition family = new GunFamilyDefinition(
                definition.FamilyId,
                "Test Family",
                "Test",
                definition.DamageType,
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
                GunCatalogAvailability.Live,
                new string[0]);
            return new GunCatalog(
                "0.1",
                "test",
                rules,
                inputs,
                new Dictionary<string, GunArchetypeDefinition>(StringComparer.Ordinal)
                {
                    { "Test", archetype },
                },
                new[] { family },
                new[] { definition });
        }

        private static GunDefinitionData Definition(
            string id,
            int projectileCount,
            double spread,
            double fireRate,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            int chainTargets = 0,
            double chainRange = 0d,
            GunCatalogAvailability availability = GunCatalogAvailability.Live,
            double dotDps = 0d,
            int burstCount = 1)
        {
            int markSeparator = id.LastIndexOf(".mk", StringComparison.Ordinal);
            string familyId = markSeparator > 0
                ? id.Substring(0, markSeparator)
                : id;
            return new GunDefinitionData(
                id,
                id,
                familyId,
                1,
                "Kinetic",
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
                areaDamage > 0d ? 0.5d : 1d,
                areaDamage > 0d ? 0.5d : 0d,
                dotDps > 0d ? 1d : 0d,
                fireRate,
                projectileCount,
                burstCount,
                5d,
                spread,
                30d,
                30d,
                0,
                explosionRadius,
                areaDamage,
                dotDps,
                dotDps > 0d ? 2d : 0d,
                0d,
                0d,
                chainTargets,
                chainRange,
                0.5d,
                1d,
                0d,
                "Test",
                "Test",
                availability,
                new string[0]);
        }

        private static EquipmentInstance Equipment(string id)
        {
            return EquipmentInstance.Create(
                StableId.Parse(id),
                EquipmentDefinitionStableId,
                1,
                QualityStableId,
                new AugmentInstance[0]);
        }

        private static GunFireCommand Command(
            EquipmentInstance equipment,
            string operation,
            long tick,
            long generation = 0L,
            ulong seed = 123UL,
            GunVector2 aim = null,
            GunVector2 origin = null)
        {
            return new GunFireCommand(
                new GunActorInstanceId(ActorStableId),
                new EquipmentInstanceId(equipment.InstanceId),
                new FireOperationId(StableId.Parse(operation)),
                new LifecycleGeneration(generation),
                tick,
                seed,
                origin ?? new GunVector2(2d, 3d),
                aim ?? new GunVector2(1d, 0d));
        }

        private sealed class Harness
        {
            public Harness(GunExecutionCore core, RecordingSink sink)
            {
                Core = core;
                Sink = sink;
            }

            public GunExecutionCore Core { get; }
            public RecordingSink Sink { get; }
        }

        private sealed class TestOwnershipResolver : IGunActorOwnershipResolver
        {
            public bool TryResolveParticipant(
                GunActorInstanceId actor,
                LifecycleGeneration generation,
                out RunParticipantId participant)
            {
                participant = actor != null && generation != null
                    ? new RunParticipantId(ParticipantStableId)
                    : null;
                return participant != null;
            }
        }

        private sealed class TestEquippedResolver : IEquippedGunInstanceResolver
        {
            private readonly Dictionary<StableId, EquipmentInstance> instances =
                new Dictionary<StableId, EquipmentInstance>();

            public TestEquippedResolver(IEnumerable<EquipmentInstance> values)
            {
                foreach (EquipmentInstance value in values ?? new EquipmentInstance[0])
                {
                    instances[value.InstanceId] = value;
                }
            }

            public bool TryResolveEquippedGun(
                GunActorInstanceId actor,
                EquipmentInstanceId requested,
                out EquipmentInstance instance)
            {
                if (actor == null || requested == null)
                {
                    instance = null;
                    return false;
                }

                return instances.TryGetValue(requested.Value, out instance);
            }
        }

        private sealed class RecordingSink : IGunEffectBatchSink
        {
            public bool Reject { get; set; }
            public List<GunEffectBatch> Batches { get; } = new List<GunEffectBatch>();
            public List<int> ValidatedCounts { get; } = new List<int>();

            public GunEffectBatchSinkResult TryAccept(GunEffectBatch batch)
            {
                int validated = 0;
                foreach (IGunEffectDescription effect in batch.Effects)
                {
                    Assert.That(effect, Is.Not.Null);
                    Assert.That(effect.Identity, Is.Not.Null);
                    validated++;
                }

                Batches.Add(batch);
                ValidatedCounts.Add(validated);
                return Reject
                    ? GunEffectBatchSinkResult.Reject("test-sink-rejected")
                    : GunEffectBatchSinkResult.Accept();
            }
        }

        private sealed class ExactDefinitionSelector : IGunBehaviorSelector
        {
            private readonly string definitionId;
            private readonly GunBehaviorId behaviorId;
            private readonly DefaultGunBehaviorSelector fallback =
                new DefaultGunBehaviorSelector();

            public ExactDefinitionSelector(string id, GunBehaviorId behavior)
            {
                definitionId = id;
                behaviorId = behavior;
            }

            public bool TrySelect(
                GunDefinitionData definition,
                out GunBehaviorId selected)
            {
                if (definition != null
                    && string.Equals(
                        definition.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    selected = behaviorId;
                    return true;
                }

                return fallback.TrySelect(definition, out selected);
            }
        }

        private sealed class ThreeProjectileTestBehavior : IGunBehavior
        {
            public ThreeProjectileTestBehavior(GunBehaviorId id)
            {
                BehaviorId = id;
            }

            public GunBehaviorId BehaviorId { get; }

            public GunBehaviorBuildResult Build(GunBehaviorContext context)
            {
                List<IGunEffectDescription> effects =
                    new List<IGunEffectDescription>();
                for (int index = 0; index < 3; index++)
                {
                    effects.Add(
                        new DirectProjectileEffect(
                            context.IdentityFor(index),
                            context.Command.Origin,
                            context.Command.AimDirection.Normalized,
                            context.Profile.ProjectileSpeed,
                            context.Profile.ProjectileRange,
                            context.Profile.DirectDamage,
                            context.Profile.Pierce,
                            context.Profile.Knockback,
                            context.Profile.DamageType));
                }

                return GunBehaviorBuildResult.Accept(new GunEffectBatch(effects));
            }
        }
    }
}
