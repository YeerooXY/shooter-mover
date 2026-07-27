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
    public sealed class RankedSkillsPersistenceResultV2
    {
        public RankedSkillsPersistenceResultV2(bool succeeded, string rejectionCode, bool shouldRollbackAcceptedMutation = true)
        { Succeeded = succeeded; RejectionCode = rejectionCode ?? string.Empty; ShouldRollbackAcceptedMutation = !succeeded && shouldRollbackAcceptedMutation; }
        public bool Succeeded { get; } public string RejectionCode { get; } public bool ShouldRollbackAcceptedMutation { get; }
    }
    public interface IRankedSkillsPersistencePortV2
    { RankedSkillsPersistenceResultV2 Persist(string mutationScope, string immutableMutationFingerprint); }
    /// <summary>
    /// Trusted Skills V2 boundary. XP owns awarded points, ranked-skills V2 owns ranks,
    /// and durable selected-character persistence is the mutation commit point.
    /// </summary>
    public sealed class RankedSkillsScreenSessionV2
    {
        private const string MutationScope = "ranked-skills-v2-allocation";
        private readonly PlayerRouteProfilePayloadV1 route;
        private readonly IPlayerExperienceAuthorityV1 experience;
        private readonly RankedSkillAllocationAuthorityV2 authority;
        private readonly IRankedSkillsPersistencePortV2 persistence;
        private readonly string profileId;
        private SkillsScreenProjectionV1 projection;
        private bool mutationBlocked;
        private string mutationBlockedReason = string.Empty;
        private RankedSkillsScreenSessionV2(
            PlayerRouteProfilePayloadV1 route, IPlayerExperienceAuthorityV1 experience,
            RankedSkillAllocationAuthorityV2 authority, string profileId,
            IRankedSkillsPersistencePortV2 persistence, SkillsScreenProjectionV1 projection)
        {
            this.route = route; this.experience = experience; this.authority = authority;
            this.profileId = profileId; this.persistence = persistence; this.projection = projection;
        }
        public SkillsScreenProjectionV1 CurrentProjection => projection;
        public bool MutationBlocked => mutationBlocked;
        public static bool TryCreate(
            PlayerRouteProfilePayloadV1 route, IPlayerExperienceAuthorityV1 experience,
            RankedSkillAllocationAuthorityV2 authority, string profileId,
            IRankedSkillsPersistencePortV2 persistence, out RankedSkillsScreenSessionV2 session,
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
            PlayerExperienceStateV1 xp = experience.CurrentState;
            RankedSkillAllocationSnapshotV2 allocation;
            if (xp == null) return Reject("skills-v2-experience-state-missing", out rejectionCode);
            if (!authority.TryGet(normalizedProfileId, out allocation)) return Reject("skills-v2-profile-unknown", out rejectionCode);
            SkillsScreenProjectionV1 initial;
            if (!TryProject(route, xp, authority.Catalog, allocation, out initial, out rejectionCode)) return false;
            session = new RankedSkillsScreenSessionV2(route, experience, authority, normalizedProfileId, persistence, initial);
            return true;
        }
        public SkillsScreenAllocationResultV1 Allocate(string skillId)
        {
            if (mutationBlocked) return Invalid(skillId, mutationBlockedReason);
            PlayerExperienceStateV1 xp = experience.CurrentState;
            RankedSkillAllocationSnapshotV2 before;
            string stateError;
            if (xp == null || !authority.TryGet(profileId, out before)) return Invalid(skillId, "skills-v2-authoritative-state-unavailable");
            if (!Validate(authority.Catalog, before, xp.TotalSkillPointsAwarded, out stateError)) return Invalid(skillId, stateError);
            string operationId = OperationId(skillId, before, xp.TotalSkillPointsAwarded);
            AllocateSkillRankCommandV2 command;
            try
            {
                command = new AllocateSkillRankCommandV2(
                    operationId, profileId, skillId, before.Version, xp.TotalSkillPointsAwarded);
            }
            catch (Exception exception)
            {
                return Invalid(skillId, "skills-v2-command-invalid:" + exception.GetType().Name);
            }
            SkillAllocationResultV2 result = authority.Allocate(command);
            if (!result.Accepted)
            {
                projection = Project(xp, result.Snapshot);
                return Present(operationId, skillId, Status(result.Rejection), Code(result.Rejection), before, result.Snapshot, xp);
            }
            RankedSkillsPersistenceResultV2 persisted = Persist(result.Snapshot);
            if (!persisted.Succeeded)
            {
                if (!persisted.ShouldRollbackAcceptedMutation)
                {
                    return BlockUnverified(operationId, skillId, persisted.RejectionCode, before, result.Snapshot, xp);
                }
                if (!authority.RollBackAccepted(operationId, result.Snapshot, before))
                {
                    RankedSkillAllocationSnapshotV2 uncertain;
                    if (!authority.TryGet(profileId, out uncertain)) uncertain = result.Snapshot;
                    return BlockUnverified(
                        operationId, skillId,
                        persisted.RejectionCode + ";skills-v2-allocation-rollback-failed",
                        before, uncertain, xp);
                }
                RankedSkillAllocationSnapshotV2 restored;
                if (!authority.TryGet(profileId, out restored)) restored = before;
                projection = Project(xp, restored);
                return Present(operationId, skillId, SkillMutationStatusV1.InvalidRequest, persisted.RejectionCode, before, restored, xp);
            }
            projection = Project(xp, result.Snapshot);
            return Present(operationId, skillId, SkillMutationStatusV1.Applied, string.Empty, before, result.Snapshot, xp);
        }
        public SkillsScreenBackResultV1 Back() => new SkillsScreenBackResultV1(route, projection);
        private SkillsScreenAllocationResultV1 BlockUnverified(
            string operationId, string skillId, string rejectionCode,
            RankedSkillAllocationSnapshotV2 before, RankedSkillAllocationSnapshotV2 current,
            PlayerExperienceStateV1 xp)
        {
            mutationBlocked = true;
            mutationBlockedReason = rejectionCode + ";skills-v2-persistence-commit-unverified";
            if (!authority.MarkCommitUnverified(profileId, current.Fingerprint))
                mutationBlockedReason += ";skills-v2-persistence-quarantine-failed";
            projection = Project(xp, current);
            return Present(operationId, skillId, SkillMutationStatusV1.InvalidRequest, mutationBlockedReason, before, current, xp);
        }
        private RankedSkillsPersistenceResultV2 Persist(RankedSkillAllocationSnapshotV2 accepted)
        {
            try
            {
                RankedSkillsPersistenceResultV2 result = persistence.Persist(MutationScope, accepted.Fingerprint);
                if (result == null) return new RankedSkillsPersistenceResultV2(false, "skills-v2-persistence-result-null", true);
                return result.Succeeded
                    ? result
                    : new RankedSkillsPersistenceResultV2(
                        false,
                        "skills-v2-persistence-rejected:" + result.RejectionCode,
                        result.ShouldRollbackAcceptedMutation);
            }
            catch (Exception exception)
            {
                return new RankedSkillsPersistenceResultV2(false, "skills-v2-persistence-threw:" + exception.GetType().Name, true);
            }
        }
        private SkillsScreenAllocationResultV1 Invalid(string skillId, string rejectionCode)
        {
            var snapshot = new SkillProgressionSnapshotV1(
                projection.PlayerLevel, projection.SkillAuthoritySequence,
                new Dictionary<string, int>(StringComparer.Ordinal), Array.Empty<string>());
            var fact = new SkillMutationFactV1(
                SkillMutationStatusV1.InvalidRequest, skillId, 0, 0, snapshot,
                new SkillRejectionReasonV1(rejectionCode));
            return new SkillsScreenAllocationResultV1(string.Empty, fact, projection);
        }
        private SkillsScreenAllocationResultV1 Present(
            string operationId, string skillId, SkillMutationStatusV1 status,
            string rejectionCode, RankedSkillAllocationSnapshotV2 before,
            RankedSkillAllocationSnapshotV2 after, PlayerExperienceStateV1 xp)
        {
            var fact = new SkillMutationFactV1(
                status, skillId, before.RankOf(skillId), after.RankOf(skillId),
                new SkillProgressionSnapshotV1(xp.Level, after.Version, after.Ranks, Array.Empty<string>()),
                new SkillRejectionReasonV1(rejectionCode));
            return new SkillsScreenAllocationResultV1(operationId, fact, projection);
        }
        private SkillsScreenProjectionV1 Project(PlayerExperienceStateV1 xp, RankedSkillAllocationSnapshotV2 allocation)
        {
            SkillsScreenProjectionV1 result;
            string rejectionCode;
            if (!TryProject(route, xp, authority.Catalog, allocation, out result, out rejectionCode))
                throw new InvalidOperationException(rejectionCode);
            return result;
        }
        private static bool TryProject(
            PlayerRouteProfilePayloadV1 route, PlayerExperienceStateV1 xp,
            RankedSkillCatalogV2 catalog, RankedSkillAllocationSnapshotV2 allocation,
            out SkillsScreenProjectionV1 projection, out string rejectionCode)
        {
            projection = null;
            if (!Validate(catalog, allocation, xp.TotalSkillPointsAwarded, out rejectionCode)) return false;
            int available = xp.TotalSkillPointsAwarded - allocation.AllocatedPoints;
            var skills = new List<SkillsScreenSkillProjectionV1>(catalog.Skills.Count);
            foreach (RankedSkillDefinitionV2 definition in catalog.Skills)
            {
                int rank = allocation.RankOf(definition.Id);
                int cap = definition.EffectiveMaximumRank(allocation.ClassId);
                SkillPrerequisiteV1 prerequisite = definition.Prerequisites.FirstOrDefault();
                bool prerequisitesMet = definition.Prerequisites.All(item => allocation.RankOf(item.SkillId) >= item.RequiredRank);
                bool gatesMet = definition.CategoryGates.All(gate => Invested(catalog, allocation, gate.CategoryId) >= gate.RequiredPoints);
                bool eligible = definition.IsEligible(allocation.ClassId);
                string block = Block(eligible, rank, cap, prerequisitesMet, gatesMet, available);
                SkillsScreenSkillStateV1 state = rank >= cap
                    ? SkillsScreenSkillStateV1.Capped
                    : !eligible || !prerequisitesMet || !gatesMet
                        ? SkillsScreenSkillStateV1.Locked
                        : rank > 0 ? SkillsScreenSkillStateV1.Purchased : SkillsScreenSkillStateV1.Available;
                skills.Add(new SkillsScreenSkillProjectionV1(
                    definition.Id, definition.Id, "Category: " + definition.CategoryId,
                    prerequisite == null ? string.Empty : prerequisite.SkillId,
                    prerequisite == null ? 0 : prerequisite.RequiredRank,
                    prerequisite == null ? 0 : allocation.RankOf(prerequisite.SkillId),
                    prerequisitesMet, rank, cap, state, block.Length == 0, block));
            }
            projection = new SkillsScreenProjectionV1(
                route, xp.Level, xp.TotalSkillPointsAwarded, allocation.AllocatedPoints,
                available, allocation.Version, skills);
            return true;
        }
        private static bool Validate(
            RankedSkillCatalogV2 catalog, RankedSkillAllocationSnapshotV2 allocation,
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
                RankedSkillDefinitionV2 skill;
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
        private static int Invested(RankedSkillCatalogV2 catalog, RankedSkillAllocationSnapshotV2 allocation, string categoryId)
        {
            return allocation.Ranks.Sum(pair =>
            {
                RankedSkillDefinitionV2 skill;
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
        private static string OperationId(string skillId, RankedSkillAllocationSnapshotV2 allocation, int totalPoints)
        {
            return "operation.skills-v2-allocate-" + SkillFingerprintV2.Hash(
                allocation.ProfileId + "|" + (skillId ?? string.Empty) + "|" + allocation.Version + "|" + totalPoints);
        }
        private static SkillMutationStatusV1 Status(SkillAllocationRejectionV2 rejection)
        {
            switch (rejection)
            {
                case SkillAllocationRejectionV2.UnknownSkill: return SkillMutationStatusV1.UnknownSkill;
                case SkillAllocationRejectionV2.MaximumRank: return SkillMutationStatusV1.RankCapped;
                case SkillAllocationRejectionV2.InsufficientPoints: return SkillMutationStatusV1.InsufficientPoints;
                case SkillAllocationRejectionV2.MissingPrerequisite: return SkillMutationStatusV1.PrerequisiteMissing;
                case SkillAllocationRejectionV2.CategoryGate: return SkillMutationStatusV1.CategoryInvestmentMissing;
                default: return SkillMutationStatusV1.InvalidRequest;
            }
        }
        private static string Code(SkillAllocationRejectionV2 rejection)
        {
            switch (rejection)
            {
                case SkillAllocationRejectionV2.UnknownSkill: return "skill-unknown";
                case SkillAllocationRejectionV2.WrongClass: return "skill-class-ineligible";
                case SkillAllocationRejectionV2.MaximumRank: return "skill-rank-capped";
                case SkillAllocationRejectionV2.InsufficientPoints: return "skill-points-insufficient";
                case SkillAllocationRejectionV2.MissingPrerequisite: return "skill-prerequisite-missing";
                case SkillAllocationRejectionV2.CategoryGate: return "skill-category-investment-missing";
                case SkillAllocationRejectionV2.StaleVersion: return "skill-allocation-version-stale";
                case SkillAllocationRejectionV2.DuplicateConflict: return "skill-operation-conflict";
                case SkillAllocationRejectionV2.CommitUnverified: return "skills-v2-persistence-commit-unverified";
                default: return "skill-allocation-rejected";
            }
        }
        private static bool Reject(string code, out string rejectionCode)
        { rejectionCode = code; return false; }
    }
}
