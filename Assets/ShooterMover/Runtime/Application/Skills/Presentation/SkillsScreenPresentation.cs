using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Skills.Presentation
{
    public enum SkillsScreenSkillState
    {
        Locked = 1,
        Available = 2,
        Purchased = 3,
        Capped = 4,
    }

    /// <summary>
    /// Immutable presentation projection for one SKILL-001 definition. The state is
    /// derived exclusively from the current SKILL-001 snapshot and XP-001 point total.
    /// </summary>
    public sealed class SkillsScreenSkillView
    {
        public SkillsScreenSkillView(
            string skillId,
            string displayName,
            string description,
            string prerequisiteSkillId,
            int prerequisiteRequiredRank,
            int prerequisiteCurrentRank,
            bool prerequisiteSatisfied,
            int currentRank,
            int maximumRank,
            SkillsScreenSkillState state,
            bool canAllocate,
            string allocationBlockCode)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                throw new ArgumentException("A skill identity is required.", nameof(skillId));
            }

            if (currentRank < 0 || maximumRank < 1 || currentRank > maximumRank)
            {
                throw new ArgumentOutOfRangeException(nameof(currentRank));
            }

            if (prerequisiteRequiredRank < 0 || prerequisiteCurrentRank < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prerequisiteRequiredRank));
            }

            if (!Enum.IsDefined(typeof(SkillsScreenSkillState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            SkillId = skillId;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            PrerequisiteSkillId = prerequisiteSkillId ?? string.Empty;
            PrerequisiteRequiredRank = prerequisiteRequiredRank;
            PrerequisiteCurrentRank = prerequisiteCurrentRank;
            PrerequisiteSatisfied = prerequisiteSatisfied;
            CurrentRank = currentRank;
            MaximumRank = maximumRank;
            State = state;
            CanAllocate = canAllocate;
            AllocationBlockCode = allocationBlockCode ?? string.Empty;
        }

        public string SkillId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string PrerequisiteSkillId { get; }
        public int PrerequisiteRequiredRank { get; }
        public int PrerequisiteCurrentRank { get; }
        public bool PrerequisiteSatisfied { get; }
        public int CurrentRank { get; }
        public int MaximumRank { get; }
        public SkillsScreenSkillState State { get; }
        public bool CanAllocate { get; }
        public string AllocationBlockCode { get; }

        public string PrerequisiteLabel
        {
            get
            {
                if (PrerequisiteSkillId.Length == 0)
                {
                    return "None";
                }

                return PrerequisiteSkillId
                    + " rank "
                    + PrerequisiteRequiredRank
                    + " (current "
                    + PrerequisiteCurrentRank
                    + ")";
            }
        }
    }

    /// <summary>
    /// Immutable, read-only screen projection. The exact incoming HUB payload instance is
    /// retained so Back can return it without reconstructing or mutating route state.
    /// </summary>
    public sealed class SkillsScreenView
    {
        private readonly ReadOnlyCollection<SkillsScreenSkillView> skills;

        internal SkillsScreenView(
            PlayerRouteProfilePayload routePayload,
            int playerLevel,
            int totalSkillPoints,
            int spentSkillPoints,
            int availableSkillPoints,
            long skillAuthoritySequence,
            IEnumerable<SkillsScreenSkillView> skills)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (playerLevel < 1 || playerLevel > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }

            if (totalSkillPoints < 0
                || spentSkillPoints < 0
                || availableSkillPoints < 0
                || skillAuthoritySequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSkillPoints));
            }

            PlayerLevel = playerLevel;
            TotalSkillPoints = totalSkillPoints;
            SpentSkillPoints = spentSkillPoints;
            AvailableSkillPoints = availableSkillPoints;
            SkillAuthoritySequence = skillAuthoritySequence;
            this.skills = new ReadOnlyCollection<SkillsScreenSkillView>(
                new List<SkillsScreenSkillView>(
                    skills ?? throw new ArgumentNullException(nameof(skills))));
        }

        public PlayerRouteProfilePayload RoutePayload { get; }
        public int PlayerLevel { get; }
        public int TotalSkillPoints { get; }
        public int SpentSkillPoints { get; }
        public int AvailableSkillPoints { get; }
        public long SkillAuthoritySequence { get; }
        public IReadOnlyList<SkillsScreenSkillView> Skills
        {
            get { return skills; }
        }

        public bool TryGetSkill(
            string skillId,
            out SkillsScreenSkillView projection)
        {
            projection = null;
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            for (int index = 0; index < skills.Count; index++)
            {
                if (string.Equals(
                    skills[index].SkillId,
                    skillId,
                    StringComparison.Ordinal))
                {
                    projection = skills[index];
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class SkillsScreenAllocationResult
    {
        internal SkillsScreenAllocationResult(
            string operationId,
            SkillMutationFact mutationFact,
            SkillsScreenView projection)
        {
            OperationId = operationId ?? string.Empty;
            MutationFact = mutationFact
                ?? throw new ArgumentNullException(nameof(mutationFact));
            Projection = projection
                ?? throw new ArgumentNullException(nameof(projection));
        }

        public string OperationId { get; }
        public SkillMutationFact MutationFact { get; }
        public SkillsScreenView Projection { get; }
        public bool Changed
        {
            get { return MutationFact.Status == SkillMutationStatus.Applied; }
        }
    }

    public sealed class SkillsScreenBackResult
    {
        internal SkillsScreenBackResult(
            PlayerRouteProfilePayload routePayload,
            SkillsScreenView projection)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            Projection = projection
                ?? throw new ArgumentNullException(nameof(projection));
        }

        public PlayerRouteProfilePayload RoutePayload { get; }
        public SkillsScreenView Projection { get; }
    }

    /// <summary>
    /// Engine-independent SKILLUI-001 application boundary. It owns no ranks, XP, points,
    /// or route state: every projection is rebuilt from XP-001 and SKILL-001, and every
    /// allocation is delegated to SkillProgressionState.Allocate.
    /// </summary>
    public sealed class SkillsScreenSession
    {
        private readonly PlayerRouteProfilePayload routePayload;
        private readonly IPlayerExperience experienceAuthority;
        private readonly SkillProgressionState skillAuthority;

        public SkillsScreenSession(
            PlayerRouteProfilePayload routePayload,
            IPlayerExperience experienceAuthority,
            SkillProgressionState skillAuthority)
        {
            this.routePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The incoming HUB route payload fingerprint is invalid.",
                    nameof(routePayload));
            }

            this.experienceAuthority = experienceAuthority
                ?? throw new ArgumentNullException(nameof(experienceAuthority));
            this.skillAuthority = skillAuthority
                ?? throw new ArgumentNullException(nameof(skillAuthority));
            SynchronizeLevel();
        }

        public PlayerRouteProfilePayload RoutePayload
        {
            get { return routePayload; }
        }

        public SkillsScreenView CurrentProjection
        {
            get { return BuildProjection(); }
        }

        public SkillsScreenAllocationResult Allocate(
            string operationId,
            string skillId)
        {
            PlayerExperienceState experienceState = SynchronizeLevel();
            SkillMutationFact fact = skillAuthority.Allocate(operationId, skillId);
            return new SkillsScreenAllocationResult(
                operationId,
                fact,
                BuildProjection(experienceState, fact.Snapshot));
        }

        public SkillsScreenBackResult Back()
        {
            return new SkillsScreenBackResult(
                routePayload,
                BuildProjection());
        }

        private SkillsScreenView BuildProjection()
        {
            PlayerExperienceState experienceState = SynchronizeLevel();
            return BuildProjection(
                experienceState,
                skillAuthority.CurrentSnapshot);
        }

        private PlayerExperienceState SynchronizeLevel()
        {
            PlayerExperienceState state = experienceAuthority.CurrentState;
            if (state == null)
            {
                throw new InvalidOperationException(
                    "XP-001 returned no current player experience state.");
            }

            skillAuthority.SetPlayerLevel(state.Level);
            return state;
        }

        private SkillsScreenView BuildProjection(
            PlayerExperienceState experienceState,
            SkillProgressionSnapshot skillSnapshot)
        {
            if (experienceState == null)
            {
                throw new ArgumentNullException(nameof(experienceState));
            }

            if (skillSnapshot == null)
            {
                throw new ArgumentNullException(nameof(skillSnapshot));
            }

            int totalPoints = experienceState.TotalSkillPointsAwarded;
            int availablePoints = Math.Max(0, totalPoints - skillSnapshot.SpentPoints);
            var projectedSkills = new List<SkillsScreenSkillView>(
                skillAuthority.Catalog.Definitions.Count);

            for (int index = 0;
                index < skillAuthority.Catalog.Definitions.Count;
                index++)
            {
                SkillDefinition definition =
                    skillAuthority.Catalog.Definitions[index];
                int currentRank = skillSnapshot.Ranks[definition.Id];
                int prerequisiteCurrentRank = 0;
                bool prerequisiteSatisfied = true;
                if (definition.PrerequisiteId.Length > 0)
                {
                    prerequisiteCurrentRank =
                        skillSnapshot.Ranks[definition.PrerequisiteId];
                    prerequisiteSatisfied = prerequisiteCurrentRank
                        >= definition.PrerequisiteRank;
                }

                SkillsScreenSkillState state;
                if (currentRank >= definition.MaxRank)
                {
                    state = SkillsScreenSkillState.Capped;
                }
                else if (!prerequisiteSatisfied)
                {
                    state = SkillsScreenSkillState.Locked;
                }
                else if (currentRank > 0)
                {
                    state = SkillsScreenSkillState.Purchased;
                }
                else
                {
                    state = SkillsScreenSkillState.Available;
                }

                string blockCode = string.Empty;
                if (state == SkillsScreenSkillState.Capped)
                {
                    blockCode = "skill-rank-capped";
                }
                else if (!prerequisiteSatisfied)
                {
                    blockCode = "skill-prerequisite-missing";
                }
                else if (availablePoints < 1)
                {
                    blockCode = "skill-points-insufficient";
                }

                projectedSkills.Add(new SkillsScreenSkillView(
                    definition.Id,
                    definition.DisplayName,
                    definition.Description,
                    definition.PrerequisiteId,
                    definition.PrerequisiteRank,
                    prerequisiteCurrentRank,
                    prerequisiteSatisfied,
                    currentRank,
                    definition.MaxRank,
                    state,
                    blockCode.Length == 0,
                    blockCode));
            }

            return new SkillsScreenView(
                routePayload,
                experienceState.Level,
                totalPoints,
                skillSnapshot.SpentPoints,
                availablePoints,
                skillSnapshot.Sequence,
                projectedSkills);
        }
    }
}
