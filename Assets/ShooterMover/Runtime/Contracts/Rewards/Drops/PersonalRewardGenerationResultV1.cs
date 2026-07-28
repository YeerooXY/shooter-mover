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
    public enum PersonalRewardGenerationStatusV1 { Generated = 1, ExplicitNoDrop = 2, Ineligible = 3, ExactReplay = 4, ConflictingReplay = 5, Rejected = 6 }

    public sealed class PersonalRewardDecisionV1 : IComparable<PersonalRewardDecisionV1>
    {
        public PersonalRewardDecisionV1(int groupOrdinal, StableId groupStableId, int rawStrongboxProbabilityMillionths, int effectiveStrongboxProbabilityMillionths, bool pityApplied, bool roomSaturationApplied, bool runSaturationApplied, StableId selectedOutcomeStableId, int generatedRandomBoxCount, int generatedGuaranteedBoxCount)
        {
            if (groupOrdinal < 0 || rawStrongboxProbabilityMillionths < 0 || rawStrongboxProbabilityMillionths > RewardRollGroupV1.ProbabilityScale || effectiveStrongboxProbabilityMillionths < 0 || effectiveStrongboxProbabilityMillionths > RewardRollGroupV1.ProbabilityScale || generatedRandomBoxCount < 0 || generatedGuaranteedBoxCount < 0) throw new ArgumentOutOfRangeException(nameof(groupOrdinal));
            GroupOrdinal = groupOrdinal; GroupStableId = groupStableId ?? throw new ArgumentNullException(nameof(groupStableId)); RawStrongboxProbabilityMillionths = rawStrongboxProbabilityMillionths; EffectiveStrongboxProbabilityMillionths = effectiveStrongboxProbabilityMillionths; PityApplied = pityApplied; RoomSaturationApplied = roomSaturationApplied; RunSaturationApplied = runSaturationApplied; SelectedOutcomeStableId = selectedOutcomeStableId; GeneratedRandomBoxCount = generatedRandomBoxCount; GeneratedGuaranteedBoxCount = generatedGuaranteedBoxCount;
        }
        public int GroupOrdinal { get; } public StableId GroupStableId { get; } public int RawStrongboxProbabilityMillionths { get; } public int EffectiveStrongboxProbabilityMillionths { get; } public bool PityApplied { get; } public bool RoomSaturationApplied { get; } public bool RunSaturationApplied { get; } public StableId SelectedOutcomeStableId { get; } public int GeneratedRandomBoxCount { get; } public int GeneratedGuaranteedBoxCount { get; }
        public int CompareTo(PersonalRewardDecisionV1 other) { return ReferenceEquals(other, null) ? 1 : GroupOrdinal.CompareTo(other.GroupOrdinal); }
        public string ToCanonicalString() { return "group_ordinal=" + GroupOrdinal.ToString(CultureInfo.InvariantCulture) + "\ngroup_id=" + GroupStableId + "\nraw_box_probability=" + RawStrongboxProbabilityMillionths.ToString(CultureInfo.InvariantCulture) + "\neffective_box_probability=" + EffectiveStrongboxProbabilityMillionths.ToString(CultureInfo.InvariantCulture) + "\npity_applied=" + (PityApplied ? "1" : "0") + "\nroom_saturation_applied=" + (RoomSaturationApplied ? "1" : "0") + "\nrun_saturation_applied=" + (RunSaturationApplied ? "1" : "0") + "\nselected_outcome=" + (SelectedOutcomeStableId == null ? "none" : SelectedOutcomeStableId.ToString()) + "\ngenerated_random_boxes=" + GeneratedRandomBoxCount.ToString(CultureInfo.InvariantCulture) + "\ngenerated_guaranteed_boxes=" + GeneratedGuaranteedBoxCount.ToString(CultureInfo.InvariantCulture); }
    }

    public sealed class PersonalRewardGenerationResultV1
    {
        private readonly ReadOnlyCollection<RewardGrantV1> grants;
        private readonly ReadOnlyCollection<PersonalRewardDecisionV1> decisions;
        private readonly string canonicalText;
        public PersonalRewardGenerationResultV1(PersonalRewardGenerationStatusV1 status, PersonalRewardRollContextV1 context, ParticipantDropPacingStateV1 pacingBefore, ParticipantDropPacingStateV1 pacingAfter, IEnumerable<RewardGrantV1> grants, IEnumerable<PersonalRewardDecisionV1> decisions, bool runMinimumGrant, string diagnostic)
        {
            if (!Enum.IsDefined(typeof(PersonalRewardGenerationStatusV1), status)) throw new ArgumentOutOfRangeException(nameof(status));
            Status = status; Context = context ?? throw new ArgumentNullException(nameof(context)); PacingBefore = pacingBefore ?? throw new ArgumentNullException(nameof(pacingBefore)); PacingAfter = pacingAfter ?? throw new ArgumentNullException(nameof(pacingAfter)); this.grants = CopyGrants(grants); this.decisions = CopyDecisions(decisions); RunMinimumGrant = runMinimumGrant; Diagnostic = diagnostic ?? string.Empty;
            var builder = new StringBuilder("schema=personal-reward-generation-result-v1");
            builder.Append("\nstatus=").Append(((int)Status).ToString(CultureInfo.InvariantCulture)).Append("\ncontext=").Append(Context.Fingerprint).Append("\npacing_before=").Append(PacingBefore.Fingerprint).Append("\npacing_after=").Append(PacingAfter.Fingerprint).Append("\nrun_minimum_grant=").Append(RunMinimumGrant ? "1" : "0").Append("\ndiagnostic=").Append(Diagnostic.Replace("\r", string.Empty).Replace("\n", "\\n")).Append("\ngrant_count=").Append(this.grants.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.grants.Count; index++) builder.Append("\ngrant_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append(":\n").Append(this.grants[index].ToCanonicalString());
            builder.Append("\ndecision_count=").Append(this.decisions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.decisions.Count; index++) builder.Append("\ndecision_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append(":\n").Append(this.decisions[index].ToCanonicalString());
            canonicalText = builder.ToString(); Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }
        public PersonalRewardGenerationStatusV1 Status { get; } public PersonalRewardRollContextV1 Context { get; } public ParticipantDropPacingStateV1 PacingBefore { get; } public ParticipantDropPacingStateV1 PacingAfter { get; } public IReadOnlyList<RewardGrantV1> Grants { get { return grants; } } public IReadOnlyList<PersonalRewardDecisionV1> Decisions { get { return decisions; } } public bool RunMinimumGrant { get; } public string Diagnostic { get; } public string Fingerprint { get; }
        public bool IsSuccess { get { return Status == PersonalRewardGenerationStatusV1.Generated || Status == PersonalRewardGenerationStatusV1.ExplicitNoDrop || Status == PersonalRewardGenerationStatusV1.Ineligible || Status == PersonalRewardGenerationStatusV1.ExactReplay; } }
        public PersonalRewardGenerationResultV1 AsExactReplay() { return this; }
        public string ToCanonicalString() { return canonicalText; }
        private static ReadOnlyCollection<RewardGrantV1> CopyGrants(IEnumerable<RewardGrantV1> source) { var copy = new List<RewardGrantV1>(source ?? Array.Empty<RewardGrantV1>()); copy.Sort(); return new ReadOnlyCollection<RewardGrantV1>(copy); }
        private static ReadOnlyCollection<PersonalRewardDecisionV1> CopyDecisions(IEnumerable<PersonalRewardDecisionV1> source) { var copy = new List<PersonalRewardDecisionV1>(source ?? Array.Empty<PersonalRewardDecisionV1>()); copy.Sort(); return new ReadOnlyCollection<PersonalRewardDecisionV1>(copy); }
    }
}
