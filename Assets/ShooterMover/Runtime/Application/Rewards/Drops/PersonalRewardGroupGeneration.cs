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
    internal static class PersonalRewardGroupGeneration
    {
        internal static PersonalRewardGenerationResult Generate(PersonalRewardRollContext context,ParticipantDropPacingState before)
        {
            if(!context.ParticipantEligible)return Result(PersonalRewardGenerationStatus.Ineligible,context,before,before,Array.Empty<RewardGrant>(),Array.Empty<PersonalRewardDecision>(),false,"participant-ineligible");
            LootSourceProfile profile=context.ProfileResolution.EffectiveProfile; if(profile.ExplicitNoDrop)return Result(PersonalRewardGenerationStatus.ExplicitNoDrop,context,before,before,Array.Empty<RewardGrant>(),Array.Empty<PersonalRewardDecision>(),false,string.Empty);
            ParticipantDropPacingState state=before;var grants=new List<RewardGrant>();var decisions=new List<PersonalRewardDecision>();
            for(int groupIndex=0;groupIndex<profile.Groups.Count;groupIndex++)
            {
                RewardRollGroup group=profile.Groups[groupIndex];int raw=PersonalRewardGenerationRandom.RawStrongboxProbabilityMillionths(group);ParticipantDropPacingState decisionState=state;int effective=group.BoxPacingMode==RewardBoxPacingMode.RandomBox?context.PacingPolicy.CalculateEffectiveRandomBoxProbability(raw,decisionState):raw;
                RewardOutcome selected=SelectOutcome(context,group,effective);int first=grants.Count;if(selected!=null&&!selected.IsExplicitNoDrop)AddOutcomeGrants(context,profile,group,selected,grants);
                int randomBoxes=0,guaranteedBoxes=0;for(int grantIndex=first;grantIndex<grants.Count;grantIndex++)if(grants[grantIndex].Kind==RewardGrantKind.Strongbox){if(group.BoxPacingMode==RewardBoxPacingMode.RandomBox)randomBoxes++;else if(group.BoxPacingMode==RewardBoxPacingMode.GuaranteedBox)guaranteedBoxes++;}
                if(group.BoxPacingMode==RewardBoxPacingMode.RandomBox)state=state.RecordRandomAttempt(randomBoxes>0,randomBoxes);else if(guaranteedBoxes>0)state=state.RecordGuaranteedBoxes(guaranteedBoxes,context.PacingPolicy.GuaranteedBoxesResetPity);
                decisions.Add(new PersonalRewardDecision(group.Ordinal,group.GroupStableId,raw,effective,context.PacingPolicy.CalculatePityBonus(decisionState.ConsecutiveEligibleRandomBoxFailures)>0,context.PacingPolicy.GetRoomSaturationMultiplier(decisionState.RandomBoxesInCurrentRoom)<RunDropPacingPolicy.ProbabilityScale,context.PacingPolicy.GetRunSaturationMultiplier(decisionState.RandomBoxesInRun)<RunDropPacingPolicy.ProbabilityScale,selected==null?null:selected.OutcomeStableId,randomBoxes,guaranteedBoxes));
            }
            return Result(grants.Count==0?PersonalRewardGenerationStatus.ExplicitNoDrop:PersonalRewardGenerationStatus.Generated,context,before,state,grants,decisions,false,string.Empty);
        }
        internal static PersonalRewardGenerationResult GenerateRunMinimum(PersonalRewardRollContext context,ParticipantDropPacingState before)
        {
            int missing=Math.Max(0,context.PacingPolicy.MinimumBoxesPerCompletedRun-before.TotalBoxesInRun);if(missing==0)return Result(PersonalRewardGenerationStatus.ExplicitNoDrop,context,before,before,Array.Empty<RewardGrant>(),Array.Empty<PersonalRewardDecision>(),true,"run-minimum-already-satisfied");
            var grants=new List<RewardGrant>(missing);for(int index=0;index<missing;index++)grants.Add(PersonalStrongboxRewardGeneration.CreateGrant(context,null,null,index,StrongboxTierSelectionCatalog.CompletionMinimumProfileId));ParticipantDropPacingState after=before.RecordGuaranteedBoxes(missing,context.PacingPolicy.GuaranteedBoxesResetPity);
            return Result(PersonalRewardGenerationStatus.Generated,context,before,after,grants,Array.Empty<PersonalRewardDecision>(),true,string.Empty);
        }
        private static RewardOutcome SelectOutcome(PersonalRewardRollContext context,RewardRollGroup group,int effectiveBoxProbability)
        {
            switch(group.Behavior)
            {
                case RewardRollGroupBehavior.GuaranteedGrant:return group.Outcomes[0];
                case RewardRollGroupBehavior.WeightedRewardCountRoll:return PersonalRewardGenerationRandom.RollWeighted(context,group,group.Outcomes,2UL);
                case RewardRollGroupBehavior.IndependentProbabilityRoll:int chance=group.BoxPacingMode==RewardBoxPacingMode.RandomBox?effectiveBoxProbability:group.ProbabilityMillionths;return PersonalRewardGenerationRandom.RollChance(context,group,chance,3UL)?group.Outcomes[0]:null;
                case RewardRollGroupBehavior.ExclusiveWeightedOutcome:return group.BoxPacingMode!=RewardBoxPacingMode.RandomBox?PersonalRewardGenerationRandom.RollWeighted(context,group,group.Outcomes,4UL):SelectPacedExclusive(context,group,effectiveBoxProbability);
                default:throw new InvalidOperationException("Unsupported reward roll-group behavior.");
            }
        }
        private static RewardOutcome SelectPacedExclusive(PersonalRewardRollContext context,RewardRollGroup group,int effectiveBoxProbability)
        {
            var boxes=new List<RewardOutcome>();var nonBoxes=new List<RewardOutcome>();for(int index=0;index<group.Outcomes.Count;index++){RewardOutcome outcome=group.Outcomes[index];if(outcome.Grant!=null&&outcome.Grant.Kind==RewardGrantKind.Strongbox)boxes.Add(outcome);else nonBoxes.Add(outcome);}
            bool boxSelected=PersonalRewardGenerationRandom.RollChance(context,group,effectiveBoxProbability,5UL);IReadOnlyList<RewardOutcome> pool=boxSelected?boxes:nonBoxes;return pool.Count==0?null:PersonalRewardGenerationRandom.RollWeighted(context,group,pool,boxSelected?6UL:7UL);
        }
        private static void AddOutcomeGrants(PersonalRewardRollContext context,LootSourceProfile profile,RewardRollGroup group,RewardOutcome outcome,ICollection<RewardGrant> output)
        {
            long quantity=ScaleCurrencyQuantity(context,outcome.Grant.Kind,PersonalRewardGenerationRandom.RollQuantity(context,group,outcome,100UL));
            if(outcome.Grant.Kind==RewardGrantKind.Strongbox){StableId tierProfile=profile.DefaultStrongboxTierSelectionProfileId??outcome.Grant.ContentStableId;for(long unit=0;unit<quantity;unit++)output.Add(PersonalStrongboxRewardGeneration.CreateGrant(context,group,outcome,checked((int)unit),tierProfile));return;}
            output.Add(RewardGrant.Create(DeriveGrantId(context,group,outcome,0),outcome.Grant.Kind,outcome.Grant.ContentStableId,quantity));
        }
        private static StableId DeriveGrantId(PersonalRewardRollContext context,RewardRollGroup group,RewardOutcome outcome,int unitOrdinal){return RewardGenerationFingerprint.DeriveStableId("personalrewardgrant",context.OperationStableId.ToString(),group.GroupStableId.ToString(),outcome.OutcomeStableId.ToString(),unitOrdinal.ToString(CultureInfo.InvariantCulture),context.ParticipantStableId.ToString());}
        private static long ScaleCurrencyQuantity(PersonalRewardRollContext context,RewardGrantKind kind,long quantity){int multiplier=kind==RewardGrantKind.Money?context.MoneyQuantityMultiplierPermille:kind==RewardGrantKind.Scrap?context.ScrapQuantityMultiplierPermille:1000;return Math.Max(1L,checked(quantity*multiplier+500L)/1000L);}
        private static PersonalRewardGenerationResult Result(PersonalRewardGenerationStatus status,PersonalRewardRollContext context,ParticipantDropPacingState before,ParticipantDropPacingState after,IEnumerable<RewardGrant> grants,IEnumerable<PersonalRewardDecision> decisions,bool runMinimum,string diagnostic){return new PersonalRewardGenerationResult(status,context,before,after,grants,decisions,runMinimum,diagnostic);}
    }
}
