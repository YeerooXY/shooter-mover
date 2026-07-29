using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>Precedence: source, game mode, mission, difficulty, sorted events, placement.</summary>
    public sealed class RewardProfileResolver
    {
        public RewardProfileResolution Resolve(StableId declaredProfileReferenceId, RewardSourceProfile sourceDefault, RewardProfileOverride gameModeOverride, RewardProfileOverride missionOverride, RewardProfileOverride difficultyOverride, IEnumerable<RewardProfileOverride> eventOverrides, RewardProfileOverride placementOverride)
        {
            if (declaredProfileReferenceId == null) throw new ArgumentNullException(nameof(declaredProfileReferenceId)); if (sourceDefault == null) throw new ArgumentNullException(nameof(sourceDefault));
            RewardSourceProfile current=sourceDefault; var applied=new List<StableId>(); current=Apply(current,gameModeOverride,applied); current=Apply(current,missionOverride,applied); current=Apply(current,difficultyOverride,applied);
            var events=new List<RewardProfileOverride>(); if(eventOverrides!=null) foreach(RewardProfileOverride value in eventOverrides){ if(value==null) throw new ArgumentException("Event overrides must not contain null entries.",nameof(eventOverrides)); events.Add(value); }
            events.Sort(); for(int index=0;index<events.Count;index++) current=Apply(current,events[index],applied); current=Apply(current,placementOverride,applied);
            return new RewardProfileResolution(declaredProfileReferenceId,sourceDefault,current,applied);
        }
        private static RewardSourceProfile Apply(RewardSourceProfile inherited,RewardProfileOverride layer,ICollection<StableId> applied)
        {
            if(layer==null)return inherited; applied.Add(layer.OverrideStableId);
            switch(layer.Operation){case RewardProfileOverrideOperation.Replace:return layer.ReplacementProfile;case RewardProfileOverrideOperation.Disable:return RewardSourceProfile.CreateExplicitNoDrop(RewardGenerationFingerprint.DeriveStableId("resolvedrewardprofile",inherited.Fingerprint,layer.Fingerprint,"disabled"));case RewardProfileOverrideOperation.AddGroups:return AddGroups(inherited,layer);case RewardProfileOverrideOperation.Modify:return Modify(inherited,layer);default:throw new InvalidOperationException("Unsupported reward-profile override operation.");}
        }
        private static RewardSourceProfile AddGroups(RewardSourceProfile inherited,RewardProfileOverride layer)
        {
            var groups=new List<RewardRollGroup>(inherited.Groups); int ordinal=groups.Count;
            for(int index=0;index<layer.AddedGroups.Count;index++){RewardRollGroup authored=layer.AddedGroups[index];groups.Add(authored.With(RewardGenerationFingerprint.DeriveStableId("resolvedrewardgroup",inherited.Fingerprint,layer.Fingerprint,authored.GroupStableId.ToString()),ordinal++,authored.ProbabilityMillionths,authored.BoxPacingMode,authored.Outcomes));}
            StableId tierProfile=inherited.DefaultStrongboxTierSelectionProfileId; for(int index=0;index<layer.AddedGroups.Count;index++) if(layer.AddedGroups[index].ContainsStrongbox&&tierProfile==null) throw new InvalidOperationException("Adding a strongbox group requires an inherited tier-selection profile.");
            return RewardSourceProfile.Create(RewardGenerationFingerprint.DeriveStableId("resolvedrewardprofile",inherited.Fingerprint,layer.Fingerprint,"add"),tierProfile,groups);
        }
        private static RewardSourceProfile Modify(RewardSourceProfile inherited,RewardProfileOverride layer)
        {
            if(inherited.ExplicitNoDrop)return inherited; var groups=new List<RewardRollGroup>(inherited.Groups.Count);
            for(int index=0;index<inherited.Groups.Count;index++)
            {
                RewardRollGroup group=inherited.Groups[index]; var outcomes=new List<RewardOutcome>(group.Outcomes.Count);
                for(int outcomeIndex=0;outcomeIndex<group.Outcomes.Count;outcomeIndex++) outcomes.Add(ModifyOutcome(inherited,layer,group,group.Outcomes[outcomeIndex]));
                if(group.BoxPacingMode==RewardBoxPacingMode.RandomBox&&group.Behavior==RewardRollGroupBehavior.ExclusiveWeightedOutcome) outcomes=ScaleExclusiveBoxProbability(outcomes,layer.ProbabilityMultiplierPermille);
                int probability=group.ProbabilityMillionths; if(group.Behavior==RewardRollGroupBehavior.IndependentProbabilityRoll) probability=ClampProbability(checked((long)probability*layer.ProbabilityMultiplierPermille/1000L));
                groups.Add(group.With(RewardGenerationFingerprint.DeriveStableId("resolvedrewardgroup",group.Fingerprint,layer.Fingerprint),group.Ordinal,probability,group.BoxPacingMode,outcomes));
            }
            StableId tierProfile=layer.StrongboxTierSelectionProfileOverrideId??inherited.DefaultStrongboxTierSelectionProfileId;
            return RewardSourceProfile.Create(RewardGenerationFingerprint.DeriveStableId("resolvedrewardprofile",inherited.Fingerprint,layer.Fingerprint,"modify"),tierProfile,groups);
        }
        private static RewardOutcome ModifyOutcome(RewardSourceProfile inherited,RewardProfileOverride layer,RewardRollGroup group,RewardOutcome outcome)
        {
            if(outcome.IsExplicitNoDrop)return outcome; RewardGrantSpecification grant=outcome.Grant; RewardQuantityRange quantity=ScaleQuantity(grant.Quantity,layer.QuantityMultiplierPermille); StableId content=grant.Kind==RewardGrantKind.Strongbox&&layer.StrongboxTierSelectionProfileOverrideId!=null?layer.StrongboxTierSelectionProfileOverrideId:grant.ContentStableId;
            RewardGrantSpecification modifiedGrant=RewardGrantSpecification.Create(RewardGenerationFingerprint.DeriveStableId("resolvedgrant",grant.Fingerprint,layer.Fingerprint),grant.Kind,content,quantity,grant.ScalingInputs);
            return RewardOutcome.CreateGrant(RewardGenerationFingerprint.DeriveStableId("resolvedoutcome",inherited.Fingerprint,layer.Fingerprint,group.GroupStableId.ToString(),outcome.OutcomeStableId.ToString()),modifiedGrant,outcome.Weight);
        }
        private static List<RewardOutcome> ScaleExclusiveBoxProbability(IReadOnlyList<RewardOutcome> outcomes,int multiplierPermille)
        {
            ulong boxWeight=0UL,otherWeight=0UL; for(int index=0;index<outcomes.Count;index++){bool box=outcomes[index].Grant!=null&&outcomes[index].Grant.Kind==RewardGrantKind.Strongbox;if(box)boxWeight=checked(boxWeight+outcomes[index].Weight);else otherWeight=checked(otherWeight+outcomes[index].Weight);}
            if(boxWeight==0UL||otherWeight==0UL)return new List<RewardOutcome>(outcomes); long total=checked((long)(boxWeight+otherWeight)); long rawMillionths=checked((long)boxWeight*RewardRollGroup.ProbabilityScale/total); int target=ClampProbability(checked(rawMillionths*multiplierPermille/1000L)); ulong targetBoxWeight=target>=RewardRollGroup.ProbabilityScale?checked(otherWeight*1000000UL):checked(otherWeight*checked((ulong)Math.Max(1,target))/checked((ulong)Math.Max(1,RewardRollGroup.ProbabilityScale-target)));
            var result=new List<RewardOutcome>(outcomes.Count); for(int index=0;index<outcomes.Count;index++){RewardOutcome outcome=outcomes[index];bool box=outcome.Grant!=null&&outcome.Grant.Kind==RewardGrantKind.Strongbox;ulong weight=box?Math.Max(1UL,checked(targetBoxWeight*outcome.Weight/boxWeight)):outcome.Weight;result.Add(outcome.IsExplicitNoDrop?RewardOutcome.CreateExplicitNoDrop(outcome.OutcomeStableId,weight):RewardOutcome.CreateGrant(outcome.OutcomeStableId,outcome.Grant,weight));} return result;
        }
        private static RewardQuantityRange ScaleQuantity(RewardQuantityRange source,int multiplierPermille){long minimum=Math.Max(1L,checked(source.Minimum*multiplierPermille+500L)/1000L);long maximum=Math.Max(minimum,checked(source.Maximum*multiplierPermille+500L)/1000L);return RewardQuantityRange.Create(minimum,maximum);}
        private static int ClampProbability(long value){return(int)Math.Max(0L,Math.Min(RewardRollGroup.ProbabilityScale,value));}
    }
}
