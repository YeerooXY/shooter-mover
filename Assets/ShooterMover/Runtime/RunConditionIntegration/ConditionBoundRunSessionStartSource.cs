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
    /// ConditionLiveState. Terminal rewards are persisted by the collected-run atomic
    /// transfer after Run Session produces its immutable accepted result.
    /// </summary>
    public sealed class ConditionBoundRunSessionStartSource :
        IRunSessionStartSource
    {
        private readonly CharacterRunSessionStartSource inner;

        public ConditionBoundRunSessionStartSource(
            CharacterSetupFlow composition,
            IRunStatInputResolver statInputResolver,
            IRunSessionNonConditionLivePortFactory baseRuntimeFactory,
            IRunConditionDefinitionProvider definitionProvider,
            IRunConditionParticipantSeedProvider participantProvider = null,
            IEnumerable<IAcceptedGameplayFactBridge> adapters = null,
            IDerivedCharacterStatComposer statComposer = null)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }
            if (baseRuntimeFactory == null)
            {
                throw new ArgumentNullException(nameof(baseRuntimeFactory));
            }
            var conditionFactory =
                new ConditionBoundRunSessionLivePortFactory(
                    baseRuntimeFactory,
                    definitionProvider,
                    participantProvider,
                    adapters);
            inner = new CharacterRunSessionStartSource(
                composition,
                statInputResolver,
                conditionFactory,
                statComposer);
        }

        public RunSessionStartMaterial Resolve(
            StartRunSessionCommand command,
            StableId resolvedRunStableId)
        {
            RunSessionStartMaterial material = inner.Resolve(
                command,
                resolvedRunStableId);
            if (material == null || !material.Succeeded)
            {
                return material ?? RunSessionStartMaterial.Reject(
                    "run-condition-production-source-null");
            }

            var condition = material.RuntimePorts.ConditionalFacts
                as ExistingConditionLiveRunPort;
            var status = material.RuntimePorts.StatusEffects
                as ConditionOwnedStatusEffectRunPort;
            if (condition == null)
            {
                return RunSessionStartMaterial.Reject(
                    "run-condition-production-port-not-authoritative");
            }
            if (status == null
                || !ReferenceEquals(status.ConditionRuntime, condition))
            {
                return RunSessionStartMaterial.Reject(
                    "run-condition-production-status-owner-split");
            }
            if (condition.LifecycleGeneration
                != material.RuntimePorts.Player.LifecycleGeneration)
            {
                return RunSessionStartMaterial.Reject(
                    "run-condition-production-lifecycle-mismatch");
            }
            return material;
        }
    }
}
