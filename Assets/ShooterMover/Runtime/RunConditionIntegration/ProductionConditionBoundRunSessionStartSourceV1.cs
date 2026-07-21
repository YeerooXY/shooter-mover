using System;
using System.Collections.Generic;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ConditionRuntime;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunConditionIntegration
{
    /// <summary>
    /// Canonical production entry point for freezing one selected account-backed
    /// character into a run whose conditional lifecycle is backed by the merged
    /// ConditionRuntimeAuthorityV1. Terminal mission results are decorated downstream
    /// so accepted unopened strongboxes are durable before Run Session reports Ended.
    /// </summary>
    public sealed class ProductionConditionBoundRunSessionStartSourceV1 :
        IRunSessionStartSourceV1
    {
        private readonly ProductionCharacterRunSessionStartSourceV1 inner;

        public ProductionConditionBoundRunSessionStartSourceV1(
            CharacterCompositionCoordinatorV1 composition,
            IProductionRunStatInputResolverV1 statInputResolver,
            IRunSessionNonConditionRuntimePortFactoryV1 baseRuntimeFactory,
            IRunConditionDefinitionProviderV1 definitionProvider,
            IRunConditionParticipantSeedProviderV1 participantProvider = null,
            IEnumerable<IAcceptedGameplayFactAdapterV1> adapters = null,
            IDerivedCharacterStatComposerV1 statComposer = null)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }
            if (baseRuntimeFactory == null)
            {
                throw new ArgumentNullException(nameof(baseRuntimeFactory));
            }
            var persistentFactory =
                new StrongboxPersistentNonConditionRuntimePortFactoryV1(
                    composition,
                    baseRuntimeFactory);
            var conditionFactory =
                new ProductionConditionBoundRunSessionRuntimePortFactoryV1(
                    persistentFactory,
                    definitionProvider,
                    participantProvider,
                    adapters);
            inner = new ProductionCharacterRunSessionStartSourceV1(
                composition,
                statInputResolver,
                conditionFactory,
                statComposer);
        }

        public RunSessionStartMaterialV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId)
        {
            RunSessionStartMaterialV1 material = inner.Resolve(
                command,
                resolvedRunStableId);
            if (material == null || !material.Succeeded)
            {
                return material ?? RunSessionStartMaterialV1.Reject(
                    "run-condition-production-source-null");
            }

            var condition = material.RuntimePorts.ConditionalFacts
                as ExistingConditionRuntimeRunPortV1;
            var status = material.RuntimePorts.StatusEffects
                as ConditionOwnedStatusEffectRunPortV1;
            if (condition == null)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-condition-production-port-not-authoritative");
            }
            if (status == null
                || !ReferenceEquals(status.ConditionRuntime, condition))
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-condition-production-status-owner-split");
            }
            if (condition.LifecycleGeneration
                != material.RuntimePorts.Player.LifecycleGeneration)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-condition-production-lifecycle-mismatch");
            }
            return material;
        }
    }
}
