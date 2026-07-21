using System;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.ConditionRuntime
{
    public sealed class EnemyDeathConditionFactAdapterV1 : IAcceptedGameplayFactAdapterV1
    {
        public Type SourceFactRuntimeType
        {
            get { return typeof(EnemyDeathFactV1); }
        }

        public string SourceFactTypeId
        {
            get { return "enemy-runtime.death-v1"; }
        }

        public bool TryAdapt(
            AcceptedGameplayFactDeliveryV1 delivery,
            out ConditionObservedGameplayFactV1 observedFact,
            out string diagnosticCode)
        {
            observedFact = null;
            diagnosticCode = string.Empty;
            if (delivery == null)
            {
                diagnosticCode = "condition-enemy-death-delivery-null";
                return false;
            }

            EnemyDeathFactV1 death = delivery.SourceFact as EnemyDeathFactV1;
            if (death == null)
            {
                diagnosticCode = "condition-enemy-death-fact-invalid";
                return false;
            }
            if (death.KillerEntityStableId == null
                || death.KillerRunParticipantStableId == null)
            {
                diagnosticCode = "condition-enemy-death-killer-unattributed";
                return false;
            }
            if (!ConditionRuntimeHashV1.SameId(delivery.RunId, death.Identity.RunStableId))
            {
                diagnosticCode = "condition-enemy-death-run-mismatch";
                return false;
            }
            if (!ConditionRuntimeHashV1.SameId(delivery.SourceActorId, death.KillerEntityStableId))
            {
                diagnosticCode = "condition-enemy-death-source-actor-mismatch";
                return false;
            }
            if (!ConditionRuntimeHashV1.SameId(
                delivery.SubjectParticipantId,
                death.KillerRunParticipantStableId))
            {
                diagnosticCode = "condition-enemy-death-killer-participant-mismatch";
                return false;
            }

            observedFact = new ConditionObservedGameplayFactV1(
                death.DeathEventStableId.ToString(),
                SourceFactTypeId,
                death.TriggeringEventStableId.ToString(),
                ConditionRuntimeFactTypeIdsV1.EnemyKilled,
                death.Identity.RunStableId,
                delivery.RunLifecycleGeneration,
                death.KillerEntityStableId,
                death.KillerRunParticipantStableId,
                delivery.SourceCharacterId,
                death.Identity.EntityInstanceId,
                death.Identity.RunParticipantId,
                delivery.SourceActorLifecycleGeneration,
                death.LifecycleGeneration,
                delivery.AuthoritativeTick);
            return true;
        }
    }
}
