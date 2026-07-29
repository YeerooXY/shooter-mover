using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>Authoritative per-participant orchestration and exact retry boundary.</summary>
    public sealed class PersonalRewardGenerationActions
    {
        private readonly ParticipantDropPacing pacingAuthority;
        public PersonalRewardGenerationActions(ParticipantDropPacing pacingAuthority) { this.pacingAuthority = pacingAuthority ?? throw new ArgumentNullException(nameof(pacingAuthority)); }
        public PersonalRewardGenerationResult Generate(PersonalRewardRollContext context) { return pacingAuthority.Execute(context, delegate(ParticipantDropPacingState before) { return PersonalRewardGroupGeneration.Generate(context, before); }); }
        public IReadOnlyList<PersonalRewardGenerationResult> GenerateForParticipants(IEnumerable<PersonalRewardRollContext> participantContexts)
        {
            if (participantContexts == null) throw new ArgumentNullException(nameof(participantContexts));
            var contexts = new List<PersonalRewardRollContext>(participantContexts); contexts.Sort(delegate(PersonalRewardRollContext left, PersonalRewardRollContext right) { return left.ParticipantStableId.CompareTo(right.ParticipantStableId); });
            var results = new List<PersonalRewardGenerationResult>(contexts.Count); for (int index = 0; index < contexts.Count; index++) results.Add(Generate(contexts[index]));
            return new ReadOnlyCollection<PersonalRewardGenerationResult>(results);
        }
        public PersonalRewardGenerationResult GenerateRunMinimum(PersonalRewardRollContext completionContext) { return pacingAuthority.Execute(completionContext, delegate(ParticipantDropPacingState before) { return PersonalRewardGroupGeneration.GenerateRunMinimum(completionContext, before); }); }
    }
}
