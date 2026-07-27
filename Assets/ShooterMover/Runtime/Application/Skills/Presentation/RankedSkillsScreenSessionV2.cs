using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public RankedSkillsPersistenceResultV2(bool succeeded, string rejectionCode)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
    }

    public interface IRankedSkillsPersistencePortV2
    {
        RankedSkillsPersistenceResultV2 Persist(
            string mutationScope,
            string immutableMutationFingerprint);
    }

    /// <summary>
    /// Trusted production boundary: XP owns the point budget, ranked-skills V2 owns ranks,
    /// and durable account persistence is the commit point.
    /// </summary>
    public sealed class RankedSkillsScreenSessionV2
    {
        private const string MutationScope = "ranked-skills-v2-allocation";
        private readonly PlayerRouteProfilePayloadV1 routePayload;
        private readonly IPlayerExperienceAuthorityV1 experienceAuthority;
        private readonly RankedSkillAllocationAuthorityV2 skillAuthority;
        private readonly RankedSkillCatalogV2 catalog;
        private readonly string profileId;
        private readonly IRankedSkillsPersistencePortV2 persistence;
        private SkillsScreenProjectionV1 projection;

        private RankedSkillsScreenSessionV2(
            PlayerRouteProfilePayloadV1 routePayload,
            IPlayerExperienceAuthorityV1 experienceAuthority,
            RankedSkillAllocationAuthorityV2 skillAuthority,
            string profileId,
            IRankedSkillsPersistencePortV2 persistence,
            SkillsScreenProjectionV1 projection)
        {
            this.routePayload = routePayload;
            this.experienceAuthority = experienceAuthority;
            this.skillAuthority = skillAuthority;
            catalog = skillAuthority.Catalog;
            this.profileId = profileId;
            this.persistence = persistence;
            this.projection = projection;
        }

        public PlayerRouteProfilePayloadV1 RoutePayload => routePayload;
        public SkillsScreenProjectionV1 CurrentProjection => projection;

        public static bool TryCreate(
            PlayerRouteProfilePayloadV1 routePayload,
            IPlayerExperienceAuthorityV1 experienceAuthority,
            RankedSkillAllocationAuthorityV2 skillAuthority,
            string profileId,
            IRankedSkillsPersistencePortV2 persistence,
            out RankedSkillsScreenSessionV2 session,
            out string rejectionCode)
        {
            session = null;
            rejectionCode = string.Empty;
            if (routePayload == null || !routePayload.HasValidFingerprint())
                return RejectCreate("skills-v2-route-invalid", out rejectionCode);
            if (experienceAuthority == null)
                return RejectCreate("skills-v2-experience-authority-missing", out rejectionCode);
            if (skillAuthority == null)
                return RejectCreate("skills-v2-allocation-authority-missing", out rejectionCode);
            if (string.IsNullOrWhiteSpace(profileId))
                return RejectCreate("skills-v2-profile-id-missing", out rejectionCode);
            if (persistence == null)
                return RejectCreate("skills-v2-persistence-port-missing", out rejectionCode);

            PlayerExperienceStateV1 xp = experienceAuthority.CurrentState;
            RankedSkillAllocationSnapshotV2 skills;
            if (xp == null)
                return RejectCreate("skills-v2-experience-state-missing", out rejectionCode);
            if (!skillAuthority.TryGet(profileId.Trim(), out skills))
                return RejectCreate("skills-v2-profile-unknown", out rejectionCode);

            SkillsScreenProjectionV1 initial;
            if (!TryBuildProjection(
                    routePayload,
                    xp,
                    skillAuthority.Catalog,
                    skills,
                    out initial,
                    out rejectionCode))
                return false;

            session = new RankedSkillsScreenSessionV2(
                routePayload,
                experienceAuthority,
                skillAuthority,
                profileId.Trim(),
                persistence,
                initial);
            return true;
        }

        public SkillsScreenAllocationResultV1 Allocate(string skillId)
        {
            PlayerExperienceStateV1 xp = experienceAuthority.CurrentState;
            RankedSkillAllocationSnapshotV2 before;
            string stateError;
            if (xp == null || !skillAuthority.TryGet(profileId, out before))
                return RejectWithoutMutation(skillId, "skills-v2-authoritative-state-unavailable", xp, null);
            if (!TryValidateSnapshot(catalog, before, xp.TotalSkillPointsAwarded, out stateError))
                return RejectWithoutMutation(skillId, stateError, xp, before);

            string operationId = CreateOperationId(skillId, before, xp.TotalSkillPointsAwarded);
            AllocateSkillRankCommandV2 command;
            try
            {
                command = new AllocateSkillRankCommandV2(
                    operationId,
                    profileId,
                    skillId,
                    before.Version,
                    xp.TotalSkillPointsAwarded);
            }
            catch (Exception exception)
            {
                return RejectWithoutMutation(
                    skillId,
                    "skills-v2-command-invalid:" + exception.GetType().Name,
                    xp,
                    before);
            }

            SkillAllocationResultV2 allocation = skillAuthority.Allocate(command);
            if (!allocation.Accepted)
            {
                projection = BuildProjection(allocation.Snapshot, xp);
                return Result(
                    operationId,
                    skillId,
                    MapStatus(allocation.Rejection),
                    MapCode(allocation.Rejection),
                    before,
                    allocation.Snapshot,
                    xp,
                    projection);
            }

            RankedSkillsPersistenceResultV2 persisted;
            try
            {
                persisted = persistence.Persist(
                    MutationScope,
                    allocation.Snapshot.Fingerprint);
            }
            catch (Exception exception)
            {
                return RollBack(
                    operationId,
                    skillId,
                    before,
                    allocation.Snapshot,
                    xp,
                    "skills-v2-persistence-threw:" + exception.GetType().Name);
            }
            if (persisted == null || !persisted.Succeeded)
            {
                return RollBack(
                    operationId,
                    skillId,
                    before,
                    allocation.Snapshot,
                    xp,
                    persisted == null
                        ? "skills-v2-persistence-result-null"
                        : "skills-v2-persistence-rejected:" + persisted.RejectionCode);
            }

            projection = BuildProjection(allocation.Snapshot, xp);
            return Result(
                operationId,
                skillId,
                SkillMutationStatusV1.Applied,
                string.Empty,
                before,
                allocation.Snapshot,
                xp,
                projection);
        }

        public SkillsScreenBackResultV1 Back()
        {
            return new SkillsScreenBackResultV1(routePayload, projection);
        }

        private SkillsScreenAllocationResultV1 RollBack(
            string operationId,
            string skillId,
            RankedSkillAllocationSnapshotV2 before,
            RankedSkillAllocationSnapshotV2 accepted,
            PlayerExperienceStateV1 xp,
            string rejectionCode)
        {
            if (!skillAuthority.RollBackAccepted(operationId, accepted, before))
                rejectionCode += ";skills-v2-allocation-rollback-failed";
            RankedSkillAllocationSnapshotV2 current;
            if (!skillAuthority.TryGet(profileId, out current)) current = before;
            projection = BuildProjection(current, xp);
            return Result(
                operationId,
                skillId,
                SkillMutationStatusV1.InvalidRequest,
                rejectionCode,
                before,
                current,
                xp,
                projection);
        }

        private SkillsScreenAllocationResultV1 RejectWithoutMutation(
            string skillId,
            string rejectionCode,
            PlayerExperienceStateV1 xp,
            RankedSkillAllocationSnapshotV2 snapshot)
        {
            int rank = snapshot == null ? 0 : snapshot.RankOf(skillId);
            SkillProgressionSnapshotV1 compatibility = CompatibilitySnapshot(snapshot, xp);
            var fact = new SkillMutationFactV1(
                SkillMutationStatusV1.InvalidRequest,
                skillId,
                rank,
                rank,
                compatibility,
                new SkillRejectionReasonV1(rejectionCode));
            return new SkillsScreenAllocationResultV1(string.Empty, fact, projection);
        }

        private static SkillsScreenAllocationResultV1 Result(
            string operationId,
            string skillId,
            SkillMutationStatusV1 status,
            string rejectionCode,
            RankedSkillAllocationSnapshotV2 before,
            RankedSkillAllocationSnapshotV2 after,
            PlayerExperienceStateV1 xp,
            SkillsScreenProjectionV1 projection)
        {
            var fact = new SkillMutationFactV1(
                status,
                skillId,
                before.RankOf(skillId),
                after.RankOf(skillId),
                CompatibilitySnapshot(after, xp),
                new SkillRejectionReasonV1(rejectionCode));
            return new SkillsScreenAllocationResultV1(operationId, fact, projection);
        }

        private SkillsScreenProjectionV1 BuildProjection(
            RankedSkillAllocationSnapshotV2 snapshot,
            PlayerExperienceStateV1 xp)
        {
            SkillsScreenProjectionV1 refreshed;
            string rejectionCode;
            if (!TryBuildProjection(
                    routePayload,
                    xp,
                    catalog,
                    snapshot,
                    out refreshed,
                    out rejectionCode))
                throw new InvalidOperationException(rejectionCode);
            return refreshed;
        }

        private static bool TryBuildProjection(
            PlayerRouteProfilePayloadV1 route,
            PlayerExperienceStateV1 xp,
            RankedSkillCatalogV2 catalog,
            RankedSkillAllocationSnapshotV2 snapshot,
            out SkillsScreenProjectionV1 result,
            out string rejectionCode)
        {
            result = null;
            if (xp == null || catalog == null || snapshot == null)
                return RejectCreate("skills-v2-projection-input-missing", out rejectionCode);
            if (!TryValidateSnapshot(catalog, snapshot, xp.TotalSkillPointsAwarded, out rejectionCode))
                return false;

            int available = xp.TotalSkillPointsAwarded - snapshot.AllocatedPoints;
            var items = new List<SkillsScreenSkillProjectionV1>(catalog.Skills.Count);
            foreach (RankedSkillDefinitionV2 skill in catalog.Skills)
            {
                int rank = snapshot.RankOf(skill.Id);
                int cap = skill.EffectiveMaximumRank(snapshot.ClassId);
                SkillPrerequisiteV1 first = skill.Prerequisites.FirstOrDefault();
                bool prerequisites = skill.Prerequisites.All(
                    item => snapshot.RankOf(item.SkillId) >= item.RequiredRank);
                bool gates = skill.CategoryGates.All(
                    gate => Invested(catalog, snapshot, gate.CategoryId) >= gate.RequiredPoints);
                bool eligible = skill.IsEligible(snapshot.ClassId);
                string block = BlockCode(eligible, rank, cap, prerequisites, gates, available);
                SkillsScreenSkillStateV1 state = rank >= cap
                    ? SkillsScreenSkillStateV1.Capped
                    : !eligible || !prerequisites || !gates
                        ? SkillsScreenSkillStateV1.Locked
                        : rank > 0
                            ? SkillsScreenSkillStateV1.Purchased
                            : SkillsScreenSkillStateV1.Available;
                items.Add(new SkillsScreenSkillProjectionV1(
                    skill.Id,
                    Humanize(skill.Id),
                    "Ranked " + Humanize(skill.CategoryId).ToLowerInvariant() + " skill.",
                    first == null ? string.Empty : first.SkillId,
                    first == null ? 0 : first.RequiredRank,
                    first == null ? 0 : snapshot.RankOf(first.SkillId),
                    prerequisites,
                    rank,
                    cap,
                    state,
                    block.Length == 0,
                    block));
            }
            result = new SkillsScreenProjectionV1(
                route,
                xp.Level,
                xp.TotalSkillPointsAwarded,
                snapshot.AllocatedPoints,
                available,
                snapshot.Version,
                items);
            return true;
        }

        private static bool TryValidateSnapshot(
            RankedSkillCatalogV2 catalog,
            RankedSkillAllocationSnapshotV2 snapshot,
            int totalPoints,
            out string rejectionCode)
        {
            if (!string.Equals(snapshot.SchemaVersion, catalog.SchemaVersion, StringComparison.Ordinal)
                || !string.Equals(snapshot.ContentVersion, catalog.ContentVersion, StringComparison.Ordinal))
                return RejectCreate("skills-v2-definition-version-stale", out rejectionCode);
            foreach (KeyValuePair<string, int> pair in snapshot.Ranks)
            {
                RankedSkillDefinitionV2 skill;
                if (!catalog.TryGet(pair.Key, out skill))
                    return RejectCreate("skills-v2-rank-skill-unknown:" + pair.Key, out rejectionCode);
                if (!skill.IsEligible(snapshot.ClassId))
                    return RejectCreate("skills-v2-rank-class-ineligible:" + pair.Key, out rejectionCode);
                if (pair.Value > skill.EffectiveMaximumRank(snapshot.ClassId))
                    return RejectCreate("skills-v2-rank-over-cap:" + pair.Key, out rejectionCode);
                if (pair.Value > 0 && skill.Prerequisites.Any(
                    prerequisite => snapshot.RankOf(prerequisite.SkillId)
                        < prerequisite.RequiredRank))
                    return RejectCreate("skills-v2-rank-prerequisite-invalid:" + pair.Key, out rejectionCode);
                if (pair.Value > 0 && skill.CategoryGates.Any(
                    gate => Invested(catalog, snapshot, gate.CategoryId)
                        < gate.RequiredPoints))
                    return RejectCreate("skills-v2-rank-category-gate-invalid:" + pair.Key, out rejectionCode);
            }
            if (snapshot.AllocatedPoints > totalPoints)
                return RejectCreate("skills-v2-allocation-exceeds-awarded-points", out rejectionCode);
            rejectionCode = string.Empty;
            return true;
        }

        private static SkillProgressionSnapshotV1 CompatibilitySnapshot(
            RankedSkillAllocationSnapshotV2 snapshot,
            PlayerExperienceStateV1 xp)
        {
            var ranks = snapshot == null
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                : snapshot.Ranks.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            return new SkillProgressionSnapshotV1(
                xp == null ? 1 : xp.Level,
                snapshot == null ? 0L : snapshot.Version,
                new ReadOnlyDictionary<string, int>(ranks),
                new ReadOnlyCollection<string>(new List<string>()));
        }

        private static int Invested(
            RankedSkillCatalogV2 catalog,
            RankedSkillAllocationSnapshotV2 snapshot,
            string categoryId)
        {
            return snapshot.Ranks.Sum(pair =>
            {
                RankedSkillDefinitionV2 item;
                return catalog.TryGet(pair.Key, out item)
                    && string.Equals(item.CategoryId, categoryId, StringComparison.Ordinal)
                        ? pair.Value
                        : 0;
            });
        }

        private static string BlockCode(
            bool eligible,
            int rank,
            int cap,
            bool prerequisites,
            bool gates,
            int available)
        {
            if (!eligible) return "skill-class-ineligible";
            if (rank >= cap) return "skill-rank-capped";
            if (!prerequisites) return "skill-prerequisite-missing";
            if (!gates) return "skill-category-investment-missing";
            return available < 1 ? "skill-points-insufficient" : string.Empty;
        }

        private static string CreateOperationId(
            string skillId,
            RankedSkillAllocationSnapshotV2 snapshot,
            int totalPoints)
        {
            return "operation.skills-v2-allocate-" + SkillFingerprintV2.Hash(
                snapshot.ProfileId + "|" + (skillId ?? string.Empty) + "|"
                + snapshot.Version + "|" + totalPoints);
        }

        private static SkillMutationStatusV1 MapStatus(SkillAllocationRejectionV2 value)
        {
            switch (value)
            {
                case SkillAllocationRejectionV2.UnknownSkill: return SkillMutationStatusV1.UnknownSkill;
                case SkillAllocationRejectionV2.MaximumRank: return SkillMutationStatusV1.RankCapped;
                case SkillAllocationRejectionV2.InsufficientPoints: return SkillMutationStatusV1.InsufficientPoints;
                case SkillAllocationRejectionV2.MissingPrerequisite: return SkillMutationStatusV1.PrerequisiteMissing;
                case SkillAllocationRejectionV2.CategoryGate: return SkillMutationStatusV1.CategoryInvestmentMissing;
                default: return SkillMutationStatusV1.InvalidRequest;
            }
        }

        private static string MapCode(SkillAllocationRejectionV2 value)
        {
            switch (value)
            {
                case SkillAllocationRejectionV2.UnknownSkill: return "skill-unknown";
                case SkillAllocationRejectionV2.WrongClass: return "skill-class-ineligible";
                case SkillAllocationRejectionV2.MaximumRank: return "skill-rank-capped";
                case SkillAllocationRejectionV2.InsufficientPoints: return "skill-points-insufficient";
                case SkillAllocationRejectionV2.MissingPrerequisite: return "skill-prerequisite-missing";
                case SkillAllocationRejectionV2.CategoryGate: return "skill-category-investment-missing";
                case SkillAllocationRejectionV2.StaleVersion: return "skill-allocation-version-stale";
                case SkillAllocationRejectionV2.DuplicateConflict: return "skill-operation-conflict";
                default: return "skill-allocation-rejected";
            }
        }

        private static string Humanize(string value)
        {
            string leaf = (value ?? string.Empty).Trim();
            int separator = leaf.LastIndexOf('.');
            if (separator >= 0) leaf = leaf.Substring(separator + 1);
            string[] words = leaf.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
                words[index] = char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
            return string.Join(" ", words);
        }

        private static bool RejectCreate(string code, out string rejectionCode)
        {
            rejectionCode = code;
            return false;
        }
    }
}
