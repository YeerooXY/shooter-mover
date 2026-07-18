using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Progression.Skills.Presentation
{
    public enum RankedSkillCardStateV2 { Locked, Available, Purchased, Capped }

    public sealed class RankedSkillRequirementProjectionV2
    {
        public RankedSkillRequirementProjectionV2(string id, int current, int required) { Id=id??string.Empty; Current=current; Required=required; }
        public string Id { get; }
        public int Current { get; }
        public int Required { get; }
        public bool Satisfied => Current >= Required;
    }

    public sealed class RankedSkillCardProjectionV2
    {
        public RankedSkillCardProjectionV2(string skillId,string categoryId,string displayName,string description,int currentRank,int maximumRank,decimal currentValue,decimal nextValue,RankedSkillCardStateV2 state,IReadOnlyList<RankedSkillRequirementProjectionV2> prerequisites,IReadOnlyList<RankedSkillRequirementProjectionV2> categoryGates,IReadOnlyList<int> milestoneRanks)
        { SkillId=skillId; CategoryId=categoryId; DisplayName=displayName; Description=description; CurrentRank=currentRank; MaximumRank=maximumRank; CurrentValue=currentValue; NextValue=nextValue; State=state; Prerequisites=prerequisites; CategoryGates=categoryGates; MilestoneRanks=milestoneRanks; }
        public string SkillId { get; } public string CategoryId { get; } public string DisplayName { get; } public string Description { get; }
        public int CurrentRank { get; } public int MaximumRank { get; } public decimal CurrentValue { get; } public decimal NextValue { get; }
        public RankedSkillCardStateV2 State { get; }
        public IReadOnlyList<RankedSkillRequirementProjectionV2> Prerequisites { get; }
        public IReadOnlyList<RankedSkillRequirementProjectionV2> CategoryGates { get; }
        public IReadOnlyList<int> MilestoneRanks { get; }
    }

    public sealed class RankedSkillSynergyProjectionV2
    {
        public RankedSkillSynergyProjectionV2(string id,string name,string description,bool active,IReadOnlyList<RankedSkillRequirementProjectionV2> requirements)
        { SynergyId=id; DisplayName=name; Description=description; Active=active; Requirements=requirements; }
        public string SynergyId { get; } public string DisplayName { get; } public string Description { get; } public bool Active { get; }
        public IReadOnlyList<RankedSkillRequirementProjectionV2> Requirements { get; }
    }

    public sealed class RankedSkillsScreenSnapshotV2
    {
        public RankedSkillsScreenSnapshotV2(string profileId,string classId,int playerLevel,long cumulativeXp,int totalPoints,int spentPoints,long creditBalance,long allocationVersion,IReadOnlyList<RankedSkillCardProjectionV2> cards,IReadOnlyList<RankedSkillSynergyProjectionV2> synergies,string feedback,string catalogFingerprint)
        { ProfileId=profileId; ClassId=classId; PlayerLevel=playerLevel; CumulativeXp=cumulativeXp; TotalPoints=totalPoints; SpentPoints=spentPoints; CreditBalance=creditBalance; AllocationVersion=allocationVersion; Cards=cards; Synergies=synergies; Feedback=feedback??string.Empty; CatalogFingerprint=catalogFingerprint??string.Empty; }
        public string ProfileId { get; } public string ClassId { get; } public int PlayerLevel { get; } public long CumulativeXp { get; }
        public int TotalPoints { get; } public int SpentPoints { get; } public int AvailablePoints => Math.Max(0,TotalPoints-SpentPoints);
        public long CreditBalance { get; } public long AllocationVersion { get; } public IReadOnlyList<RankedSkillCardProjectionV2> Cards { get; }
        public IReadOnlyList<RankedSkillSynergyProjectionV2> Synergies { get; } public string Feedback { get; } public string CatalogFingerprint { get; }
    }

    public interface IRankedSkillsPlayerProgressSourceV2 { string ProfileId { get; } string ClassId { get; } int PlayerLevel { get; } long CumulativeXp { get; } }
    public interface IRankedSkillsCreditProjectionV2 { long Balance(string profileId); }
    public interface IRankedSkillTextCatalogV2 { string DisplayName(string id); string Description(string id); }
    public interface IRankedSkillsScreenFacadeV2
    { RankedSkillsScreenSnapshotV2 Refresh(string feedback=""); SkillAllocationResultV2 Allocate(string operationId,string skillId,long expectedVersion); SkillRespecQuoteV2 QuoteFullRespec(); SkillRespecReceiptV2 ConfirmFullRespec(string operationId,SkillRespecQuoteV2 quote); }

    public sealed class RankedSkillsScreenFacadeV2 : IRankedSkillsScreenFacadeV2
    {
        private readonly RankedSkillsImportedCatalogBundleV2 bundle; private readonly RankedSkillAllocationAuthorityV2 allocation; private readonly SkillRespecOrchestratorV2 respec; private readonly IRankedSkillsPlayerProgressSourceV2 progress; private readonly IRankedSkillsCreditProjectionV2 credits;
        public RankedSkillsScreenFacadeV2(RankedSkillsImportedCatalogBundleV2 bundle,RankedSkillAllocationAuthorityV2 allocation,SkillRespecOrchestratorV2 respec,IRankedSkillsPlayerProgressSourceV2 progress,IRankedSkillsCreditProjectionV2 credits)
        { this.bundle=bundle??throw new ArgumentNullException(nameof(bundle)); this.allocation=allocation??throw new ArgumentNullException(nameof(allocation)); this.respec=respec??throw new ArgumentNullException(nameof(respec)); this.progress=progress??throw new ArgumentNullException(nameof(progress)); this.credits=credits??throw new ArgumentNullException(nameof(credits)); }
        public RankedSkillsScreenSnapshotV2 Refresh(string feedback="")
        {
            var snapshot=allocation.Get(progress.ProfileId);
            var cards=bundle.Catalog.Skills.Where(x=>x.IsEligible(progress.ClassId)).Select(x=>ProjectCard(x,snapshot)).ToList();
            var synergies=bundle.Catalog.Synergies.Select(x=>ProjectSynergy(x,snapshot)).ToList();
            return new RankedSkillsScreenSnapshotV2(progress.ProfileId,progress.ClassId,progress.PlayerLevel,progress.CumulativeXp,progress.PlayerLevel,snapshot.AllocatedPoints,credits.Balance(progress.ProfileId),snapshot.Version,new ReadOnlyCollection<RankedSkillCardProjectionV2>(cards),new ReadOnlyCollection<RankedSkillSynergyProjectionV2>(synergies),feedback,bundle.Fingerprint);
        }
        public SkillAllocationResultV2 Allocate(string operationId,string skillId,long expectedVersion)=>allocation.Allocate(new AllocateSkillRankCommandV2(operationId,progress.ProfileId,skillId,expectedVersion,progress.PlayerLevel));
        public SkillRespecQuoteV2 QuoteFullRespec()=>respec.Quote(progress.ProfileId);
        public SkillRespecReceiptV2 ConfirmFullRespec(string operationId,SkillRespecQuoteV2 quote)=>respec.Execute(operationId,quote);
        private RankedSkillCardProjectionV2 ProjectCard(RankedSkillDefinitionV2 skill,RankedSkillAllocationSnapshotV2 snapshot)
        {
            int rank=snapshot.RankOf(skill.Id), max=skill.EffectiveMaximumRank(progress.ClassId);
            var prerequisites=skill.Prerequisites.Select(x=>new RankedSkillRequirementProjectionV2(x.SkillId,snapshot.RankOf(x.SkillId),x.RequiredRank)).ToList();
            var gates=skill.CategoryGates.Select(x=>new RankedSkillRequirementProjectionV2(x.CategoryId,snapshot.Ranks.Where(p=>InCategory(p.Key,x.CategoryId)).Sum(p=>p.Value),x.RequiredPoints)).ToList();
            bool locked=prerequisites.Any(x=>!x.Satisfied)||gates.Any(x=>!x.Satisfied);
            var state=rank>=max?RankedSkillCardStateV2.Capped:locked?RankedSkillCardStateV2.Locked:rank>0?RankedSkillCardStateV2.Purchased:RankedSkillCardStateV2.Available;
            return new RankedSkillCardProjectionV2(skill.Id,skill.CategoryId,bundle.Text.DisplayName(skill.Id),bundle.Text.Description(skill.Id),rank,max,rank>0?skill.RankValue(progress.ClassId,rank):0m,rank<max?skill.RankValue(progress.ClassId,rank+1):skill.RankValue(progress.ClassId,max),state,new ReadOnlyCollection<RankedSkillRequirementProjectionV2>(prerequisites),new ReadOnlyCollection<RankedSkillRequirementProjectionV2>(gates),new ReadOnlyCollection<int>(skill.Milestones.Select(x=>x.Rank).ToList()));
        }
        private RankedSkillSynergyProjectionV2 ProjectSynergy(SkillSynergyDefinitionV2 synergy,RankedSkillAllocationSnapshotV2 snapshot)
        {
            var requirements=synergy.Requirements.Select(x=>new RankedSkillRequirementProjectionV2(x.SkillId,snapshot.RankOf(x.SkillId),x.MinimumRank)).ToList();
            requirements.AddRange(synergy.CombinedRankRequirements.Select(x=>new RankedSkillRequirementProjectionV2("combined:"+string.Join("+",x.SkillIds),x.CurrentCombinedRank(snapshot),x.MinimumCombinedRank)));
            return new RankedSkillSynergyProjectionV2(synergy.Id,bundle.Text.DisplayName(synergy.Id),bundle.Text.Description(synergy.Id),synergy.IsSatisfied(snapshot),new ReadOnlyCollection<RankedSkillRequirementProjectionV2>(requirements));
        }
        private bool InCategory(string skillId,string categoryId){ RankedSkillDefinitionV2 value; return bundle.Catalog.TryGet(skillId,out value)&&value.CategoryId==categoryId; }
    }
}
