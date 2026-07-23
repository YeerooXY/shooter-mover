using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>Precedence: source, game mode, mission, difficulty, sorted events, placement.</summary>
    public sealed class RewardProfileResolverV1
    {
        public RewardProfileResolutionV1 Resolve(StableId declaredProfileReferenceId, RewardSourceProfileV1 sourceDefault, RewardProfileOverrideV1 gameModeOverride, RewardProfileOverrideV1 missionOverride, RewardProfileOverrideV1 difficultyOverride, IEnumerable<RewardProfileOverrideV1> eventOverrides, RewardProfileOverrideV1 placementOverride)
        {
            if (declaredProfileReferenceId == null) throw new ArgumentNullException(nameof(declaredProfileReferenceId)); if (sourceDefault == null) throw new ArgumentNullException(nameof(sourceDefault));
            RewardSourceProfileV1 current=sourceDefault; var applied=new List<StableId>(); current=Apply(current,gameModeOverride,applied); current=Apply(current,missionOverride,applied); current=Apply(current,difficultyOverride,applied);
            var events=new List<RewardProfileOverrideV1>(); if(eventOverrides!=null) foreach(RewardProfileOverrideV1 value in eventOverrides){ if(value==null) throw new ArgumentException("Event overrides must not contain null entries.",nameof(eventOverrides)); events.Add(value); }
            events.Sort(); for(int index=0;index<events.Count;index++) current=Apply(current,events[index],applied); current=Apply(current,placementOverride,applied);
            return new RewardProfileResolutionV1(declaredProfileReferenceId,sourceDefault,current,applied);
        }
        private static RewardSourceProfileV1 Apply(RewardSourceProfileV1 inherited,RewardProfileOverrideV1 layer,ICollection<StableId> applied)
        {
            if(layer==null)return inherited; applied.Add(layer.OverrideStableId);
            switch(layer.Operation){case RewardProfileOverrideOperationV1.Replace:return layer.ReplacementProfile;case RewardProfileOverrideOperationV1.Disable:return RewardSourceProfileV1.CreateExplicitNoDrop(RewardGenerationFingerprintV1.DeriveStableId("resolvedrewardprofile",inherited.Fingerprint,layer.Fingerprint,"disabled"));case RewardProfileOverrideOperationV1.AddGroups:return AddGroups(inherited,layer);case RewardProfileOverrideOperationV1.Modify:return Modify(inherited,layer);default:throw new InvalidOperationException("Unsupported reward-profile override operation.");}
        }
        private static RewardSourceProfileV1 AddGroups(RewardSourceProfileV1 inherited,RewardProfileOverrideV1 layer)
        {
            var groups=new List<RewardRollGroupV1>(inherited.Groups); int ordinal=groups.Count;
            for(int index=0;index<layer.AddedGroups.Count;index++){RewardRollGroupV1 authored=layer.AddedGroups[index];groups.Add(authored.With(RewardGenerationFingerprintV1.DeriveStableId("resolvedrewardgroup",inherited.Fingerprint,layer.Fingerprint,authored.GroupStableId.ToString()),ordinal++,authored.ProbabilityMillionths,authored.BoxPacingMode,authored.Outcomes));}
            StableId tierProfile=inherited.DefaultStrongboxTierSelectionProfileId; for(int index=0;index<layer.AddedGroups.Count;index++) if(layer.AddedGroups[index].ContainsStrongbox&&tierProfile==null) throw new InvalidOperationException("Adding a strongbox group requires an inherited tier-selection profile.");
            return RewardSourceProfileV1.Create(RewardGenerationFingerprintV1.DeriveStableId("resolvedrewardprofile",inherited.Fingerprint,layer.Fingerprint,"add"),tierProfile,groups);
        }
        private static RewardSourceProfileV1 Modify(RewardSourceProfileV1 inherited,RewardProfileOverrideV1 layer)
        {
            if(inherited.ExplicitNoDrop)return inherited; var groups=new List<RewardRollGroupV1>(inherited.Groups.Count);
            for(int index=0;index<inherited.Groups.Count;index++)
            {
                RewardRollGroupV1 group=inherited.Groups[index]; var outcomes=new List<RewardOutcomeV1>(group.Outcomes.Count);
                for(int outcomeIndex=0;outcomeIndex<group.Outcomes.Count;outcomeIndex++) outcomes.Add(ModifyOutcome(inherited,layer,group,group.Outcomes[outcomeIndex]));
                if(group.BoxPacingMode==RewardBoxPacingModeV1.RandomBox&&group.Behavior==RewardRollGroupBehaviorV1.ExclusiveWeightedOutcome) outcomes=ScaleExclusiveBoxProbability(outcomes,layer.ProbabilityMultiplierPermille);
                int probability=group.ProbabilityMillionths; if(group.Behavior==RewardRollGroupBehaviorV1.IndependentProbabilityRoll) probability=ClampProbability(checked((long)probability*layer.ProbabilityMultiplierPermille/1000L));
                groups.Add(group.With(RewardGenerationFingerprintV1.DeriveStableId("resolvedrewardgroup",group.Fingerprint,layer.Fingerprint),group.Ordinal,probability,group.BoxPacingMode,outcomes));
            }
            StableId tierProfile=layer.StrongboxTierSelectionProfileOverrideId??inherited.DefaultStrongboxTierSelectionProfileId;
            return RewardSourceProfileV1.Create(RewardGenerationFingerprintV1.DeriveStableId("resolvedrewardprofile",inherited.Fingerprint,layer.Fingerprint,"modify"),tierProfile,groups);
        }
        private static RewardOutcomeV1 ModifyOutcome(RewardSourceProfileV1 inherited,RewardProfileOverrideV1 layer,RewardRollGroupV1 group,RewardOutcomeV1 outcome)
        {
            if(outcome.IsExplicitNoDrop)return outcome; RewardGrantSpecificationV1 grant=outcome.Grant; RewardQuantityRangeV1 quantity=ScaleQuantity(grant.Quantity,layer.QuantityMultiplierPermille); StableId content=grant.Kind==RewardGrantKindV1.Strongbox&&layer.StrongboxTierSelectionProfileOverrideId!=null?layer.StrongboxTierSelectionProfileOverrideId:grant.ContentStableId;
            RewardGrantSpecificationV1 modifiedGrant=RewardGrantSpecificationV1.Create(RewardGenerationFingerprintV1.DeriveStableId("resolvedgrant",grant.Fingerprint,layer.Fingerprint),grant.Kind,content,quantity,grant.ScalingInputs);
            return RewardOutcomeV1.CreateGrant(RewardGenerationFingerprintV1.DeriveStableId("resolvedoutcome",inherited.Fingerprint,layer.Fingerprint,group.GroupStableId.ToString(),outcome.OutcomeStableId.ToString()),modifiedGrant,outcome.Weight);
        }
        private static List<RewardOutcomeV1> ScaleExclusiveBoxProbability(IReadOnlyList<RewardOutcomeV1> outcomes,int multiplierPermille)
        {
            ulong boxWeight=0UL,otherWeight=0UL; for(int index=0;index<outcomes.Count;index++){bool box=outcomes[index].Grant!=null&&outcomes[index].Grant.Kind==RewardGrantKindV1.Strongbox;if(box)boxWeight=checked(boxWeight+outcomes[index].Weight);else otherWeight=checked(otherWeight+outcomes[index].Weight);}
            if(boxWeight==0UL||otherWeight==0UL)return new List<RewardOutcomeV1>(outcomes); long total=checked((long)(boxWeight+otherWeight)); long rawMillionths=checked((long)boxWeight*RewardRollGroupV1.ProbabilityScale/total); int target=ClampProbability(checked(rawMillionths*multiplierPermille/1000L)); ulong targetBoxWeight=target>=RewardRollGroupV1.ProbabilityScale?checked(otherWeight*1000000UL):checked(otherWeight*checked((ulong)Math.Max(1,target))/checked((ulong)Math.Max(1,RewardRollGroupV1.ProbabilityScale-target)));
            var result=new List<RewardOutcomeV1>(outcomes.Count); for(int index=0;index<outcomes.Count;index++){RewardOutcomeV1 outcome=outcomes[index];bool box=outcome.Grant!=null&&outcome.Grant.Kind==RewardGrantKindV1.Strongbox;ulong weight=box?Math.Max(1UL,checked(targetBoxWeight*outcome.Weight/boxWeight)):outcome.Weight;result.Add(outcome.IsExplicitNoDrop?RewardOutcomeV1.CreateExplicitNoDrop(outcome.OutcomeStableId,weight):RewardOutcomeV1.CreateGrant(outcome.OutcomeStableId,outcome.Grant,weight));} return result;
        }
        private static RewardQuantityRangeV1 ScaleQuantity(RewardQuantityRangeV1 source,int multiplierPermille){long minimum=Math.Max(1L,checked(source.Minimum*multiplierPermille+500L)/1000L);long maximum=Math.Max(minimum,checked(source.Maximum*multiplierPermille+500L)/1000L);return RewardQuantityRangeV1.Create(minimum,maximum);}
        private static int ClampProbability(long value){return(int)Math.Max(0L,Math.Min(RewardRollGroupV1.ProbabilityScale,value));}
    }
}
