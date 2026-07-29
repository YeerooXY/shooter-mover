using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
namespace ShooterMover.Application.Skills.Presentation
{
    public sealed class RankedSkillsPersistenceResult
    {
        public RankedSkillsPersistenceResult(bool succeeded, string rejectionCode, bool shouldRollbackAcceptedMutation = true)
        { Succeeded = succeeded; RejectionCode = rejectionCode ?? string.Empty; ShouldRollbackAcceptedMutation = !succeeded && shouldRollbackAcceptedMutation; }
        public bool Succeeded { get; } public string RejectionCode { get; } public bool ShouldRollbackAcceptedMutation { get; }
    }
    public interface IRankedSkillsPersistencePort
    { RankedSkillsPersistenceResult Persist(string mutationScope, string immutableMutationFingerprint); }
    /// <summary>
    /// Trusted Skills V2 boundary. XP owns awarded points, ranked-skills V2 owns ranks,
    /// and durable selected-character persistence is the mutation commit point.
    /// </summary>
    public sealed class RankedSkillsScreenSession
    {
        private const string MutationScope = "ranked-skills-v2-allocation";
        private readonly PlayerRouteProfilePayload route;
        private readonly IPlayerExperienceState experience;
        private readonly RankedSkillAllocationState authority;
        private readonly IRankedSkillsPersistencePort persistence;
        private readonly string profileId;
        private SkillsScreenView projection;
        private bool mutationBlocked;
        private string mutationBlockedReason = string.Empty;
        private RankedSkillsScreenSession(
            PlayerRouteProfilePayload route, IPlayerExperienceState experience,
            RankedSkillAllocationState authority, string profileId,
            IRankedSkillsPersistencePort persistence, SkillsScreenView projection)
        {
            this.route = route; this.experience = experience; this.authority = authority;
            this.profileId = profileId; this.persistence = persistence; this.projection = projection;
        }
        public SkillsScreenView CurrentProjection => projection;
        public bool MutationBlocked => mutationBlocked;
        public static bool TryCreate(
            PlayerRouteProfilePayload route, IPlayerExperienceState experience,
            RankedSkillAllocationState authority, string profileId,
            IRankedSkillsPersistencePort persistence, out RankedSkillsScreenSession session,
            out string rejectionCode)
        {
            session = null;
            if (route == null || !route.HasValidFingerprint()) return Reject("skills-v2-route-invalid", out rejectionCode);
            if (experience == null) return Reject("skills-v2-experience-authority-missing", out rejectionCode);
            if (authority == null) return Reject("skills-v2-allocation-authority-missing", out rejectionCode);
            if (string.IsNullOrWhiteSpace(profileId)) return Reject("skills-v2-profile-id-missing", out rejectionCode);
            if (persistence == null) return Reject("skills-v2-persistence-port-missing", out rejectionCode);
            string normalizedProfileId = profileId.Trim();
            if (authority.IsCommitUnverified(normalizedProfileId))
                return Reject("skills-v2-persistence-commit-unverified", out rejectionCode);
            PlayerExperienceState xp = experience.CurrentState;
            RankedSkillAllocationSnapshot allocation;
            if (xp == null) return Reject("skills-v2-experience-state-missing", out rejectionCode);
            if (!authority.TryGet(normalizedProfileId, out allocation)) return Reject("skills-v2-profile-unknown", out rejectionCode);
            SkillsScreenView initial;
            if (!TryProject(route, xp, authority.Catalog, allocation, out initial, out rejectionCode)) return false;
            session = new RankedSkillsScreenSession(route, experience, authority, normalizedProfileId, persistence, initial);
            return true;
        }
        public SkillsScreenAllocationResult Allocate(string skillId)
        {
            if (mutationBlocked) return Invalid(skillId, mutationBlockedReason);
            PlayerExperienceState xp = experience.CurrentState;
            RankedSkillAllocationSnapshot before;
            string stateError;
            if (xp == null || !authority.TryGet(profileId, out before)) return Invalid(skillId, "skills-v2-authoritative-state-unavailable");
            if (!Validate(authority.Catalog, before, xp.TotalSkillPointsAwarded, out stateError)) return Invalid(skillId, stateError);
            string operationId = OperationId(skillId, before, xp.TotalSkillPointsAwarded);
            AllocateSkillRankCommand command;
            try
            {
                command = new AllocateSkillRankCommand(
                    operationId, profileId, skillId, before.Version, xp.TotalSkillPointsAwarded);
            }
            catch (Exception exception)
            {
                return Invalid(skillId, "skills-v2-command-invalid:" + exception.GetType().Name);
            }
            SkillAllocationResult result = authority.Allocate(command);
            if (!result.Accepted)
            {
                projection = Project(xp, result.Snapshot);
                return Present(operationId, skillId, Status(result.Rejection), Code(result.Rejection), before, result.Snapshot, xp);
            }
            RankedSkillsPersistenceResult persisted = Persist(result.Snapshot);
            if (!persisted.Succeeded)
            {
                if (!persisted.ShouldRollbackAcceptedMutation)
                {
                    return BlockUnverified(operationId, skillId, persisted.RejectionCode, before, result.Snapshot, xp);
                }
                if (!authority.RollBackAccepted(operationId, result.Snapshot, before))
                {
                    RankedSkillAllocationSnapshot uncertain;
                    if (!authority.TryGet(profileId, out uncertain)) uncertain = result.Snapshot;
                    return BlockUnverified(
                        operationId, skillId,
                        persisted.RejectionCode + ";skills-v2-allocation-rollback-failed",
                        before, uncertain, xp);
                }
                RankedSkillAllocationSnapshot restored;
                if (!authority.TryGet(profileId, out restored)) restored = before;
                projection = Project(xp, restored);
                return Present(operationId, skillId, SkillMutationStatus.InvalidRequest, persisted.RejectionCode, before, restored, xp);
            }
            projection = Project(xp, result.Snapshot);
            return Present(operationId, skillId, SkillMutationStatus.Applied, string.Empty, before, result.Snapshot, xp);
        }
        public SkillsScreenBackResult Back() => new SkillsScreenBackResult(route, projection);
        private SkillsScreenAllocationResult BlockUnverified(
            string operationId, string skillId, string rejectionCode,
            RankedSkillAllocationSnapshot before, RankedSkillAllocationSnapshot current,
            PlayerExperienceState xp)
        {
            mutationBlocked = true;
            mutationBlockedReason = rejectionCode + ";skills-v2-persistence-commit-unverified";
            if (!authority.MarkCommitUnverified(profileId, current.Fingerprint))
                mutationBlockedReason += ";skills-v2-persistence-quarantine-failed";
            projection = Project(xp, current);
            return Present(operationId, skillId, SkillMutationStatus.InvalidRequest, mutationBlockedReason, before, current, xp);
        }
        private RankedSkillsPersistenceResult Persist(RankedSkillAllocationSnapshot accepted)
        {
            try
            {
                RankedSkillsPersistenceResult result = persistence.Persist(MutationScope, accepted.Fingerprint);
                if (result == null) return new RankedSkillsPersistenceResult(false, "skills-v2-persistence-result-null", true);
                return result.Succeeded
                    ? result
                    : new RankedSkillsPersistenceResult(
                        false,
                        "skills-v2-persistence-rejected:" + result.RejectionCode,
                        result.ShouldRollbackAcceptedMutation);
            }
            catch (Exception exception)
            {
                return new RankedSkillsPersistenceResult(false, "skills-v2-persistence-threw:" + exception.GetType().Name, true);
            }
        }
        private SkillsScreenAllocationResult Invalid(string skillId, string rejectionCode)
        {
            var snapshot = new SkillProgressionSnapshot(
                projection.PlayerLevel, projection.SkillAuthoritySequence,
                new Dictionary<string, int>(StringComparer.Ordinal), Array.Empty<string>());
            var fact = new SkillMutationFact(
                SkillMutationStatus.InvalidRequest, skillId, 0, 0, snapshot,
                new SkillRejectionReason(rejectionCode));
            return new SkillsScreenAllocationResult(string.Empty, fact, projection);
        }
        private SkillsScreenAllocationResult Present(
            string operationId, string skillId, SkillMutationStatus status,
            string rejectionCode, RankedSkillAllocationSnapshot before,
            RankedSkillAllocationSnapshot after, PlayerExperienceState xp)
        {
            var fact = new SkillMutationFact(
                status, skillId, before.RankOf(skillId), after.RankOf(skillId),
                new SkillProgressionSnapshot(xp.Level, after.Version, after.Ranks, Array.Empty<string>()),
                new SkillRejectionReason(rejectionCode));
            return new SkillsScreenAllocationResult(operationId, fact, projection);
        }
        private SkillsScreenView Project(PlayerExperienceState xp, RankedSkillAllocationSnapshot allocation)
        {
            SkillsScreenView result;
            string rejectionCode;
            if (!TryProject(route, xp, authority.Catalog, allocation, out result, out rejectionCode))
                throw new InvalidOperationException(rejectionCode);
            return result;
        }
        private static bool TryProject(
            PlayerRouteProfilePayload route, PlayerExperienceState xp,
            RankedSkillCatalog catalog, RankedSkillAllocationSnapshot allocation,
            out SkillsScreenView projection, out string rejectionCode)
        {
            projection = null;
            if (!Validate(catalog, allocation, xp.TotalSkillPointsAwarded, out rejectionCode)) return false;
            int available = xp.TotalSkillPointsAwarded - allocation.AllocatedPoints;
            var skills = new List<SkillsScreenSkillView>(catalog.Skills.Count);
            foreach (RankedSkillDefinition definition in catalog.Skills)
            {
                int rank = allocation.RankOf(definition.Id);
                int cap = definition.EffectiveMaximumRank(allocation.ClassId);
                SkillPrerequisite prerequisite = definition.Prerequisites.FirstOrDefault();
                bool prerequisitesMet = definition.Prerequisites.All(item => allocation.RankOf(item.SkillId) >= item.RequiredRank);
                bool gatesMet = definition.CategoryGates.All(gate => Invested(catalog, allocation, gate.CategoryId) >= gate.RequiredPoints);
                bool eligible = definition.IsEligible(allocation.ClassId);
                string block = Block(eligible, rank, cap, prerequisitesMet, gatesMet, available);
                SkillsScreenSkillState state = rank >= cap
                    ? SkillsScreenSkillState.Capped
                    : !eligible || !prerequisitesMet || !gatesMet
                        ? SkillsScreenSkillState.Locked
                        : rank > 0 ? SkillsScreenSkillState.Purchased : SkillsScreenSkillState.Available;
                skills.Add(new SkillsScreenSkillView(
                    definition.Id, definition.Id, "Category: " + definition.CategoryId,
                    prerequisite == null ? string.Empty : prerequisite.SkillId,
                    prerequisite == null ? 0 : prerequisite.RequiredRank,
                    prerequisite == null ? 0 : allocation.RankOf(prerequisite.SkillId),
                    prerequisitesMet, rank, cap, state, block.Length == 0, block));
            }
            projection = new SkillsScreenView(
                route, xp.Level, xp.TotalSkillPointsAwarded, allocation.AllocatedPoints,
                available, allocation.Version, skills);
            return true;
        }
        private static bool Validate(
            RankedSkillCatalog catalog, RankedSkillAllocationSnapshot allocation,
            int totalPoints, out string rejectionCode)
        {
            if (catalog == null || allocation == null) return Reject("skills-v2-projection-input-missing", out rejectionCode);
            if (!string.Equals(allocation.SchemaVersion, catalog.SchemaVersion, StringComparison.Ordinal)
                || !string.Equals(allocation.ContentVersion, catalog.ContentVersion, StringComparison.Ordinal))
                return Reject("skills-v2-definition-version-stale", out rejectionCode);
            if (allocation.AllocatedPoints > totalPoints)
                return Reject("skills-v2-allocation-exceeds-awarded-points", out rejectionCode);
            foreach (KeyValuePair<string, int> pair in allocation.Ranks)
            {
                RankedSkillDefinition skill;
                if (!catalog.TryGet(pair.Key, out skill)) return Reject("skills-v2-rank-skill-unknown:" + pair.Key, out rejectionCode);
                if (!skill.IsEligible(allocation.ClassId)) return Reject("skills-v2-rank-class-ineligible:" + pair.Key, out rejectionCode);
                if (pair.Value > skill.EffectiveMaximumRank(allocation.ClassId)) return Reject("skills-v2-rank-over-cap:" + pair.Key, out rejectionCode);
                if (skill.Prerequisites.Any(item => allocation.RankOf(item.SkillId) < item.RequiredRank))
                    return Reject("skills-v2-rank-prerequisite-invalid:" + pair.Key, out rejectionCode);
                if (skill.CategoryGates.Any(gate => Invested(catalog, allocation, gate.CategoryId) < gate.RequiredPoints))
                    return Reject("skills-v2-rank-category-gate-invalid:" + pair.Key, out rejectionCode);
            }
            rejectionCode = string.Empty;
            return true;
        }
        private static int Invested(RankedSkillCatalog catalog, RankedSkillAllocationSnapshot allocation, string categoryId)
        {
            return allocation.Ranks.Sum(pair =>
            {
                RankedSkillDefinition skill;
                return catalog.TryGet(pair.Key, out skill)
                    && string.Equals(skill.CategoryId, categoryId, StringComparison.Ordinal) ? pair.Value : 0;
            });
        }
        private static string Block(bool eligible, int rank, int cap, bool prerequisitesMet, bool gatesMet, int available)
        {
            if (!eligible) return "skill-class-ineligible";
            if (rank >= cap) return "skill-rank-capped";
            if (!prerequisitesMet) return "skill-prerequisite-missing";
            if (!gatesMet) return "skill-category-investment-missing";
            return available < 1 ? "skill-points-insufficient" : string.Empty;
        }
        private static string OperationId(string skillId, RankedSkillAllocationSnapshot allocation, int totalPoints)
        {
            return "operation.skills-v2-allocate-" + SkillFingerprint.Hash(
                allocation.ProfileId + "|" + (skillId ?? string.Empty) + "|" + allocation.Version + "|" + totalPoints);
        }
        private static SkillMutationStatus Status(SkillAllocationRejection rejection)
        {
            switch (rejection)
            {
                case SkillAllocationRejection.UnknownSkill: return SkillMutationStatus.UnknownSkill;
                case SkillAllocationRejection.MaximumRank: return SkillMutationStatus.RankCapped;
                case SkillAllocationRejection.InsufficientPoints: return SkillMutationStatus.InsufficientPoints;
                case SkillAllocationRejection.MissingPrerequisite: return SkillMutationStatus.PrerequisiteMissing;
                case SkillAllocationRejection.CategoryGate: return SkillMutationStatus.CategoryInvestmentMissing;
                default: return SkillMutationStatus.InvalidRequest;
            }
        }
        private static string Code(SkillAllocationRejection rejection)
        {
            switch (rejection)
            {
                case SkillAllocationRejection.UnknownSkill: return "skill-unknown";
                case SkillAllocationRejection.WrongClass: return "skill-class-ineligible";
                case SkillAllocationRejection.MaximumRank: return "skill-rank-capped";
                case SkillAllocationRejection.InsufficientPoints: return "skill-points-insufficient";
                case SkillAllocationRejection.MissingPrerequisite: return "skill-prerequisite-missing";
                case SkillAllocationRejection.CategoryGate: return "skill-category-investment-missing";
                case SkillAllocationRejection.StaleVersion: return "skill-allocation-version-stale";
                case SkillAllocationRejection.DuplicateConflict: return "skill-operation-conflict";
                case SkillAllocationRejection.CommitUnverified: return "skills-v2-persistence-commit-unverified";
                default: return "skill-allocation-rejected";
            }
        }
        private static bool Reject(string code, out string rejectionCode)
        { rejectionCode = code; return false; }
    }
}
