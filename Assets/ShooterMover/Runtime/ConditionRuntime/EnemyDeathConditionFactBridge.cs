using System;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.ConditionRuntime
{
    public sealed class EnemyDeathConditionFactBridge :
        IAcceptedGameplayFactBridge,
        IAcceptedGameplayFactSourceFingerprint
    {
        public Type SourceFactRuntimeType
        {
            get { return typeof(EnemyDeathFact); }
        }

        public string SourceFactTypeId
        {
            get { return "enemy-runtime.death-v1"; }
        }

        public string ComputeSourceFactFingerprint(object sourceFact)
        {
            EnemyDeathFact death = sourceFact as EnemyDeathFact;
            if (death == null)
                throw new ArgumentException(
                    "The enemy-death adapter can fingerprint only EnemyDeathFact values.",
                    nameof(sourceFact));
            return ConditionSourceFactFingerprint.Compute(death);
        }

        public bool TryAdapt(
            AcceptedGameplayFactDelivery delivery,
            out ConditionObservedGameplayFact observedFact,
            out string diagnosticCode)
        {
            observedFact = null;
            diagnosticCode = string.Empty;
            if (delivery == null)
            {
                diagnosticCode = "condition-enemy-death-delivery-null";
                return false;
            }

            EnemyDeathFact death = delivery.SourceFact as EnemyDeathFact;
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
            if (!ConditionLiveHash.SameId(
                delivery.RunId,
                death.Identity.RunStableId))
            {
                diagnosticCode = "condition-enemy-death-run-mismatch";
                return false;
            }
            if (!ConditionLiveHash.SameId(
                delivery.SourceActorId,
                death.KillerEntityStableId))
            {
                diagnosticCode = "condition-enemy-death-source-actor-mismatch";
                return false;
            }
            if (!ConditionLiveHash.SameId(
                delivery.SubjectParticipantId,
                death.KillerRunParticipantStableId))
            {
                diagnosticCode =
                    "condition-enemy-death-killer-participant-mismatch";
                return false;
            }

            observedFact = new ConditionObservedGameplayFact(
                death.DeathEventStableId.ToString(),
                SourceFactTypeId,
                death.TriggeringEventStableId.ToString(),
                ConditionLiveFactTypeIds.EnemyKilled,
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
