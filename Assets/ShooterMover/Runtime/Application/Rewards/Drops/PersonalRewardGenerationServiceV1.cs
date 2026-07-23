using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>Authoritative per-participant orchestration and exact retry boundary.</summary>
    public sealed class PersonalRewardGenerationServiceV1
    {
        private readonly ParticipantDropPacingAuthorityV1 pacingAuthority;
        public PersonalRewardGenerationServiceV1(ParticipantDropPacingAuthorityV1 pacingAuthority) { this.pacingAuthority = pacingAuthority ?? throw new ArgumentNullException(nameof(pacingAuthority)); }
        public PersonalRewardGenerationResultV1 Generate(PersonalRewardRollContextV1 context) { return pacingAuthority.Execute(context, delegate(ParticipantDropPacingStateV1 before) { return PersonalRewardGroupGenerationV1.Generate(context, before); }); }
        public IReadOnlyList<PersonalRewardGenerationResultV1> GenerateForParticipants(IEnumerable<PersonalRewardRollContextV1> participantContexts)
        {
            if (participantContexts == null) throw new ArgumentNullException(nameof(participantContexts));
            var contexts = new List<PersonalRewardRollContextV1>(participantContexts); contexts.Sort(delegate(PersonalRewardRollContextV1 left, PersonalRewardRollContextV1 right) { return left.ParticipantStableId.CompareTo(right.ParticipantStableId); });
            var results = new List<PersonalRewardGenerationResultV1>(contexts.Count); for (int index = 0; index < contexts.Count; index++) results.Add(Generate(contexts[index]));
            return new ReadOnlyCollection<PersonalRewardGenerationResultV1>(results);
        }
        public PersonalRewardGenerationResultV1 GenerateRunMinimum(PersonalRewardRollContextV1 completionContext) { return pacingAuthority.Execute(completionContext, delegate(ParticipantDropPacingStateV1 before) { return PersonalRewardGroupGenerationV1.GenerateRunMinimum(completionContext, before); }); }
    }
}
