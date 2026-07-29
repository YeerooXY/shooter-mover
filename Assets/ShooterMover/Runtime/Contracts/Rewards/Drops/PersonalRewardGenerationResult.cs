using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public enum PersonalRewardGenerationStatus { Generated = 1, ExplicitNoDrop = 2, Ineligible = 3, ExactReplay = 4, ConflictingReplay = 5, Rejected = 6 }

    public sealed class PersonalRewardDecision : IComparable<PersonalRewardDecision>
    {
        public PersonalRewardDecision(int groupOrdinal, StableId groupStableId, int rawStrongboxProbabilityMillionths, int effectiveStrongboxProbabilityMillionths, bool pityApplied, bool roomSaturationApplied, bool runSaturationApplied, StableId selectedOutcomeStableId, int generatedRandomBoxCount, int generatedGuaranteedBoxCount)
        {
            if (groupOrdinal < 0 || rawStrongboxProbabilityMillionths < 0 || rawStrongboxProbabilityMillionths > RewardRollGroup.ProbabilityScale || effectiveStrongboxProbabilityMillionths < 0 || effectiveStrongboxProbabilityMillionths > RewardRollGroup.ProbabilityScale || generatedRandomBoxCount < 0 || generatedGuaranteedBoxCount < 0) throw new ArgumentOutOfRangeException(nameof(groupOrdinal));
            GroupOrdinal = groupOrdinal; GroupStableId = groupStableId ?? throw new ArgumentNullException(nameof(groupStableId)); RawStrongboxProbabilityMillionths = rawStrongboxProbabilityMillionths; EffectiveStrongboxProbabilityMillionths = effectiveStrongboxProbabilityMillionths; PityApplied = pityApplied; RoomSaturationApplied = roomSaturationApplied; RunSaturationApplied = runSaturationApplied; SelectedOutcomeStableId = selectedOutcomeStableId; GeneratedRandomBoxCount = generatedRandomBoxCount; GeneratedGuaranteedBoxCount = generatedGuaranteedBoxCount;
        }
        public int GroupOrdinal { get; } public StableId GroupStableId { get; } public int RawStrongboxProbabilityMillionths { get; } public int EffectiveStrongboxProbabilityMillionths { get; } public bool PityApplied { get; } public bool RoomSaturationApplied { get; } public bool RunSaturationApplied { get; } public StableId SelectedOutcomeStableId { get; } public int GeneratedRandomBoxCount { get; } public int GeneratedGuaranteedBoxCount { get; }
        public int CompareTo(PersonalRewardDecision other) { return ReferenceEquals(other, null) ? 1 : GroupOrdinal.CompareTo(other.GroupOrdinal); }
        public string ToCanonicalString() { return "group_ordinal=" + GroupOrdinal.ToString(CultureInfo.InvariantCulture) + "\ngroup_id=" + GroupStableId + "\nraw_box_probability=" + RawStrongboxProbabilityMillionths.ToString(CultureInfo.InvariantCulture) + "\neffective_box_probability=" + EffectiveStrongboxProbabilityMillionths.ToString(CultureInfo.InvariantCulture) + "\npity_applied=" + (PityApplied ? "1" : "0") + "\nroom_saturation_applied=" + (RoomSaturationApplied ? "1" : "0") + "\nrun_saturation_applied=" + (RunSaturationApplied ? "1" : "0") + "\nselected_outcome=" + (SelectedOutcomeStableId == null ? "none" : SelectedOutcomeStableId.ToString()) + "\ngenerated_random_boxes=" + GeneratedRandomBoxCount.ToString(CultureInfo.InvariantCulture) + "\ngenerated_guaranteed_boxes=" + GeneratedGuaranteedBoxCount.ToString(CultureInfo.InvariantCulture); }
    }

    public sealed class PersonalRewardGenerationResult
    {
        private readonly ReadOnlyCollection<RewardGrant> grants;
        private readonly ReadOnlyCollection<PersonalRewardDecision> decisions;
        private readonly string canonicalText;
        public PersonalRewardGenerationResult(PersonalRewardGenerationStatus status, PersonalRewardRollContext context, ParticipantDropPacingState pacingBefore, ParticipantDropPacingState pacingAfter, IEnumerable<RewardGrant> grants, IEnumerable<PersonalRewardDecision> decisions, bool runMinimumGrant, string diagnostic)
        {
            if (!Enum.IsDefined(typeof(PersonalRewardGenerationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            Status = status; Context = context ?? throw new ArgumentNullException(nameof(context)); PacingBefore = pacingBefore ?? throw new ArgumentNullException(nameof(pacingBefore)); PacingAfter = pacingAfter ?? throw new ArgumentNullException(nameof(pacingAfter)); this.grants = CopyGrants(grants); this.decisions = CopyDecisions(decisions); RunMinimumGrant = runMinimumGrant; Diagnostic = diagnostic ?? string.Empty;
            var builder = new StringBuilder("schema=personal-reward-generation-result-v1");
            builder.Append("\nstatus=").Append(((int)Status).ToString(CultureInfo.InvariantCulture)).Append("\ncontext=").Append(Context.Fingerprint).Append("\npacing_before=").Append(PacingBefore.Fingerprint).Append("\npacing_after=").Append(PacingAfter.Fingerprint).Append("\nrun_minimum_grant=").Append(RunMinimumGrant ? "1" : "0").Append("\ndiagnostic=").Append(Diagnostic.Replace("\r", string.Empty).Replace("\n", "\\n")).Append("\ngrant_count=").Append(this.grants.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.grants.Count; index++) builder.Append("\ngrant_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append(":\n").Append(this.grants[index].ToCanonicalString());
            builder.Append("\ndecision_count=").Append(this.decisions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.decisions.Count; index++) builder.Append("\ndecision_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append(":\n").Append(this.decisions[index].ToCanonicalString());
            canonicalText = builder.ToString(); Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }
        public PersonalRewardGenerationStatus Status { get; } public PersonalRewardRollContext Context { get; } public ParticipantDropPacingState PacingBefore { get; } public ParticipantDropPacingState PacingAfter { get; } public IReadOnlyList<RewardGrant> Grants { get { return grants; } } public IReadOnlyList<PersonalRewardDecision> Decisions { get { return decisions; } } public bool RunMinimumGrant { get; } public string Diagnostic { get; } public string Fingerprint { get; }
        public bool IsSuccess { get { return Status == PersonalRewardGenerationStatus.Generated || Status == PersonalRewardGenerationStatus.ExplicitNoDrop || Status == PersonalRewardGenerationStatus.Ineligible || Status == PersonalRewardGenerationStatus.ExactReplay; } }
        public PersonalRewardGenerationResult AsExactReplay() { return this; }
        public string ToCanonicalString() { return canonicalText; }
        private static ReadOnlyCollection<RewardGrant> CopyGrants(IEnumerable<RewardGrant> source) { var copy = new List<RewardGrant>(source ?? Array.Empty<RewardGrant>()); copy.Sort(); return new ReadOnlyCollection<RewardGrant>(copy); }
        private static ReadOnlyCollection<PersonalRewardDecision> CopyDecisions(IEnumerable<PersonalRewardDecision> source) { var copy = new List<PersonalRewardDecision>(source ?? Array.Empty<PersonalRewardDecision>()); copy.Sort(); return new ReadOnlyCollection<PersonalRewardDecision>(copy); }
    }
}
