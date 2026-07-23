using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>Pure deterministic evaluation of ordered groups against one personal pacing snapshot.</summary>
    internal static class PersonalRewardGroupGenerationV1
    {
        internal static PersonalRewardGenerationResultV1 Generate(PersonalRewardRollContextV1 context,ParticipantDropPacingStateV1 before)
        {
            if(!context.ParticipantEligible)return Result(PersonalRewardGenerationStatusV1.Ineligible,context,before,before,Array.Empty<RewardGrantV1>(),Array.Empty<PersonalRewardDecisionV1>(),false,"participant-ineligible");
            RewardSourceProfileV1 profile=context.ProfileResolution.EffectiveProfile; if(profile.ExplicitNoDrop)return Result(PersonalRewardGenerationStatusV1.ExplicitNoDrop,context,before,before,Array.Empty<RewardGrantV1>(),Array.Empty<PersonalRewardDecisionV1>(),false,string.Empty);
            ParticipantDropPacingStateV1 state=before;var grants=new List<RewardGrantV1>();var decisions=new List<PersonalRewardDecisionV1>();
            for(int groupIndex=0;groupIndex<profile.Groups.Count;groupIndex++)
            {
                RewardRollGroupV1 group=profile.Groups[groupIndex];int raw=PersonalRewardGenerationRandomV1.RawStrongboxProbabilityMillionths(group);ParticipantDropPacingStateV1 decisionState=state;int effective=group.BoxPacingMode==RewardBoxPacingModeV1.RandomBox?context.PacingPolicy.CalculateEffectiveRandomBoxProbability(raw,decisionState):raw;
                RewardOutcomeV1 selected=SelectOutcome(context,group,effective);int first=grants.Count;if(selected!=null&&!selected.IsExplicitNoDrop)AddOutcomeGrants(context,profile,group,selected,grants);
                int randomBoxes=0,guaranteedBoxes=0;for(int grantIndex=first;grantIndex<grants.Count;grantIndex++)if(grants[grantIndex].Kind==RewardGrantKindV1.Strongbox){if(group.BoxPacingMode==RewardBoxPacingModeV1.RandomBox)randomBoxes++;else if(group.BoxPacingMode==RewardBoxPacingModeV1.GuaranteedBox)guaranteedBoxes++;}
                if(group.BoxPacingMode==RewardBoxPacingModeV1.RandomBox)state=state.RecordRandomAttempt(randomBoxes>0,randomBoxes);else if(guaranteedBoxes>0)state=state.RecordGuaranteedBoxes(guaranteedBoxes,context.PacingPolicy.GuaranteedBoxesResetPity);
                decisions.Add(new PersonalRewardDecisionV1(group.Ordinal,group.GroupStableId,raw,effective,context.PacingPolicy.CalculatePityBonus(decisionState.ConsecutiveEligibleRandomBoxFailures)>0,context.PacingPolicy.GetRoomSaturationMultiplier(decisionState.RandomBoxesInCurrentRoom)<RunDropPacingPolicyV1.ProbabilityScale,context.PacingPolicy.GetRunSaturationMultiplier(decisionState.RandomBoxesInRun)<RunDropPacingPolicyV1.ProbabilityScale,selected==null?null:selected.OutcomeStableId,randomBoxes,guaranteedBoxes));
            }
            return Result(grants.Count==0?PersonalRewardGenerationStatusV1.ExplicitNoDrop:PersonalRewardGenerationStatusV1.Generated,context,before,state,grants,decisions,false,string.Empty);
        }
        internal static PersonalRewardGenerationResultV1 GenerateRunMinimum(PersonalRewardRollContextV1 context,ParticipantDropPacingStateV1 before)
        {
            int missing=Math.Max(0,context.PacingPolicy.MinimumBoxesPerCompletedRun-before.TotalBoxesInRun);if(missing==0)return Result(PersonalRewardGenerationStatusV1.ExplicitNoDrop,context,before,before,Array.Empty<RewardGrantV1>(),Array.Empty<PersonalRewardDecisionV1>(),true,"run-minimum-already-satisfied");
            var grants=new List<RewardGrantV1>(missing);for(int index=0;index<missing;index++)grants.Add(PersonalStrongboxRewardGenerationV1.CreateGrant(context,null,null,index,ProductionStrongboxTierSelectionCatalogV1.CompletionMinimumProfileId));ParticipantDropPacingStateV1 after=before.RecordGuaranteedBoxes(missing,context.PacingPolicy.GuaranteedBoxesResetPity);
            return Result(PersonalRewardGenerationStatusV1.Generated,context,before,after,grants,Array.Empty<PersonalRewardDecisionV1>(),true,string.Empty);
        }
        private static RewardOutcomeV1 SelectOutcome(PersonalRewardRollContextV1 context,RewardRollGroupV1 group,int effectiveBoxProbability)
        {
            switch(group.Behavior)
            {
                case RewardRollGroupBehaviorV1.GuaranteedGrant:return group.Outcomes[0];
                case RewardRollGroupBehaviorV1.WeightedRewardCountRoll:return PersonalRewardGenerationRandomV1.RollWeighted(context,group,group.Outcomes,2UL);
                case RewardRollGroupBehaviorV1.IndependentProbabilityRoll:int chance=group.BoxPacingMode==RewardBoxPacingModeV1.RandomBox?effectiveBoxProbability:group.ProbabilityMillionths;return PersonalRewardGenerationRandomV1.RollChance(context,group,chance,3UL)?group.Outcomes[0]:null;
                case RewardRollGroupBehaviorV1.ExclusiveWeightedOutcome:return group.BoxPacingMode!=RewardBoxPacingModeV1.RandomBox?PersonalRewardGenerationRandomV1.RollWeighted(context,group,group.Outcomes,4UL):SelectPacedExclusive(context,group,effectiveBoxProbability);
                default:throw new InvalidOperationException("Unsupported reward roll-group behavior.");
            }
        }
        private static RewardOutcomeV1 SelectPacedExclusive(PersonalRewardRollContextV1 context,RewardRollGroupV1 group,int effectiveBoxProbability)
        {
            var boxes=new List<RewardOutcomeV1>();var nonBoxes=new List<RewardOutcomeV1>();for(int index=0;index<group.Outcomes.Count;index++){RewardOutcomeV1 outcome=group.Outcomes[index];if(outcome.Grant!=null&&outcome.Grant.Kind==RewardGrantKindV1.Strongbox)boxes.Add(outcome);else nonBoxes.Add(outcome);}
            bool boxSelected=PersonalRewardGenerationRandomV1.RollChance(context,group,effectiveBoxProbability,5UL);IReadOnlyList<RewardOutcomeV1> pool=boxSelected?boxes:nonBoxes;return pool.Count==0?null:PersonalRewardGenerationRandomV1.RollWeighted(context,group,pool,boxSelected?6UL:7UL);
        }
        private static void AddOutcomeGrants(PersonalRewardRollContextV1 context,RewardSourceProfileV1 profile,RewardRollGroupV1 group,RewardOutcomeV1 outcome,ICollection<RewardGrantV1> output)
        {
            long quantity=ScaleCurrencyQuantity(context,outcome.Grant.Kind,PersonalRewardGenerationRandomV1.RollQuantity(context,group,outcome,100UL));
            if(outcome.Grant.Kind==RewardGrantKindV1.Strongbox){StableId tierProfile=profile.DefaultStrongboxTierSelectionProfileId??outcome.Grant.ContentStableId;for(long unit=0;unit<quantity;unit++)output.Add(PersonalStrongboxRewardGenerationV1.CreateGrant(context,group,outcome,checked((int)unit),tierProfile));return;}
            output.Add(RewardGrantV1.Create(DeriveGrantId(context,group,outcome,0),outcome.Grant.Kind,outcome.Grant.ContentStableId,quantity));
        }
        private static StableId DeriveGrantId(PersonalRewardRollContextV1 context,RewardRollGroupV1 group,RewardOutcomeV1 outcome,int unitOrdinal){return RewardGenerationFingerprintV1.DeriveStableId("personalrewardgrant",context.OperationStableId.ToString(),group.GroupStableId.ToString(),outcome.OutcomeStableId.ToString(),unitOrdinal.ToString(CultureInfo.InvariantCulture),context.ParticipantStableId.ToString());}
        private static long ScaleCurrencyQuantity(PersonalRewardRollContextV1 context,RewardGrantKindV1 kind,long quantity){int multiplier=kind==RewardGrantKindV1.Money?context.MoneyQuantityMultiplierPermille:kind==RewardGrantKindV1.Scrap?context.ScrapQuantityMultiplierPermille:1000;return Math.Max(1L,checked(quantity*multiplier+500L)/1000L);}
        private static PersonalRewardGenerationResultV1 Result(PersonalRewardGenerationStatusV1 status,PersonalRewardRollContextV1 context,ParticipantDropPacingStateV1 before,ParticipantDropPacingStateV1 after,IEnumerable<RewardGrantV1> grants,IEnumerable<PersonalRewardDecisionV1> decisions,bool runMinimum,string diagnostic){return new PersonalRewardGenerationResultV1(status,context,before,after,grants,decisions,runMinimum,diagnostic);}
    }
}
