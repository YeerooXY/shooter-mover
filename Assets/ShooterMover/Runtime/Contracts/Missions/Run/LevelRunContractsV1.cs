using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Run
{
    public enum LevelRunStartStatusV1
    {
        Started = 1,
        InvalidRoutePayload = 2,
        MissingMode = 3,
        MissingLevel = 4,
        WrongLevel = 5,
        MissingCharacter = 6,
        MissingLoadoutProfile = 7,
    }

    public enum LevelRunEnemyDestructionStatusV1
    {
        Applied = 1,
        DuplicateNoChange = 2,
        UnregisteredEnemy = 3,
        UnknownRoom = 4,
        Unattributed = 5,
        InvalidRequest = 6,
    }

    public enum LevelRunExtractionStatusV1
    {
        Completed = 1,
        ExactDuplicateNoChange = 2,
        WrongRoom = 3,
        RoomNotClear = 4,
        AuthorityRejected = 5,
        InvalidRequest = 6,
    }

    public sealed class ResolvedLevelRunWeaponSlotV1
    {
        public ResolvedLevelRunWeaponSlotV1(
            StableId slotStableId,
            StableId equipmentInstanceStableId,
            StableId equipmentDefinitionStableId,
            StableId runtimeWeaponStableId,
            string displayName)
        {
            SlotStableId = slotStableId ?? throw new ArgumentNullException(nameof(slotStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableId));
            EquipmentDefinitionStableId = equipmentDefinitionStableId
                ?? throw new ArgumentNullException(nameof(equipmentDefinitionStableId));
            RuntimeWeaponStableId = runtimeWeaponStableId
                ?? throw new ArgumentNullException(nameof(runtimeWeaponStableId));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? runtimeWeaponStableId.ToString()
                : displayName.Trim();
        }

        public StableId SlotStableId { get; }
        public StableId EquipmentInstanceStableId { get; }
        public StableId EquipmentDefinitionStableId { get; }
        public StableId RuntimeWeaponStableId { get; }
        public string DisplayName { get; }
    }

    public sealed class LevelRunLoadoutResolutionV1
    {
        private readonly ReadOnlyCollection<ResolvedLevelRunWeaponSlotV1> slots;

        private LevelRunLoadoutResolutionV1(
            bool accepted,
            IEnumerable<ResolvedLevelRunWeaponSlotV1> slots,
            int activeSlotIndex,
            string rejectionCode)
        {
            Accepted = accepted;
            this.slots = new ReadOnlyCollection<ResolvedLevelRunWeaponSlotV1>(
                new List<ResolvedLevelRunWeaponSlotV1>(
                    slots ?? Array.Empty<ResolvedLevelRunWeaponSlotV1>()));
            ActiveSlotIndex = activeSlotIndex;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public IReadOnlyList<ResolvedLevelRunWeaponSlotV1> Slots { get { return slots; } }
        public int ActiveSlotIndex { get; }
        public string RejectionCode { get; }

        public static LevelRunLoadoutResolutionV1 Accept(
            IEnumerable<ResolvedLevelRunWeaponSlotV1> slots,
            int activeSlotIndex)
        {
            var copy = new List<ResolvedLevelRunWeaponSlotV1>(
                slots ?? throw new ArgumentNullException(nameof(slots)));
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "A production level run needs at least one resolved weapon.",
                    nameof(slots));
            }

            if (activeSlotIndex < 0 || activeSlotIndex >= copy.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(activeSlotIndex));
            }

            return new LevelRunLoadoutResolutionV1(
                true,
                copy,
                activeSlotIndex,
                string.Empty);
        }

        public static LevelRunLoadoutResolutionV1 Reject(string rejectionCode)
        {
            return new LevelRunLoadoutResolutionV1(
                false,
                Array.Empty<ResolvedLevelRunWeaponSlotV1>(),
                -1,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "level-run-loadout-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class LevelRunPlayerContributionV1 :
        IComparable<LevelRunPlayerContributionV1>
    {
        public LevelRunPlayerContributionV1(
            StableId playerStableId,
            int killCount,
            long experienceEarned)
        {
            PlayerStableId = playerStableId
                ?? throw new ArgumentNullException(nameof(playerStableId));
            if (killCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killCount));
            }

            if (experienceEarned < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(experienceEarned));
            }

            KillCount = killCount;
            ExperienceEarned = experienceEarned;
        }

        public StableId PlayerStableId { get; }
        public int KillCount { get; }
        public long ExperienceEarned { get; }

        public int CompareTo(LevelRunPlayerContributionV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : PlayerStableId.CompareTo(other.PlayerStableId);
        }
    }

    public sealed class LevelRunSummaryV1
    {
        private readonly ReadOnlyCollection<LevelRunPlayerContributionV1> contributions;
        private readonly string canonicalText;

        private LevelRunSummaryV1(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            MissionRunCompletionStateV1 completionState,
            IEnumerable<LevelRunPlayerContributionV1> contributions)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The level-run route payload fingerprint is invalid.",
                    nameof(routePayload));
            }

            SelectedModeStableId = selectedModeStableId
                ?? throw new ArgumentNullException(nameof(selectedModeStableId));
            SelectedLevelStableId = selectedLevelStableId
                ?? throw new ArgumentNullException(nameof(selectedLevelStableId));
            if (!Enum.IsDefined(typeof(MissionRunCompletionStateV1), completionState))
            {
                throw new ArgumentOutOfRangeException(nameof(completionState));
            }

            var ordered = new List<LevelRunPlayerContributionV1>(
                contributions ?? throw new ArgumentNullException(nameof(contributions)));
            ordered.Sort();
            var seen = new HashSet<StableId>();
            for (int index = 0; index < ordered.Count; index++)
            {
                LevelRunPlayerContributionV1 contribution = ordered[index];
                if (contribution == null || !seen.Add(contribution.PlayerStableId))
                {
                    throw new ArgumentException(
                        "Player contributions must be non-null and uniquely keyed.",
                        nameof(contributions));
                }
            }

            CompletionState = completionState;
            this.contributions =
                new ReadOnlyCollection<LevelRunPlayerContributionV1>(ordered);
            canonicalText = BuildCanonicalText();
            Fingerprint = ComputeSha256(canonicalText);
        }

        public StableId RunStableId { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public StableId SelectedModeStableId { get; }
        public StableId SelectedLevelStableId { get; }
        public MissionRunCompletionStateV1 CompletionState { get; }
        public IReadOnlyList<LevelRunPlayerContributionV1> Contributions
        {
            get { return contributions; }
        }
        public string Fingerprint { get; }

        public static LevelRunSummaryV1 Create(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            MissionRunCompletionStateV1 completionState,
            IEnumerable<LevelRunPlayerContributionV1> contributions)
        {
            return new LevelRunSummaryV1(
                runStableId,
                routePayload,
                selectedModeStableId,
                selectedLevelStableId,
                completionState,
                contributions);
        }

        public LevelRunPlayerContributionV1 FindContribution(StableId playerStableId)
        {
            if (playerStableId == null)
            {
                return null;
            }

            for (int index = 0; index < contributions.Count; index++)
            {
                if (contributions[index].PlayerStableId == playerStableId)
                {
                    return contributions[index];
                }
            }

            return null;
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                ComputeSha256(canonicalText),
                StringComparison.Ordinal);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private string BuildCanonicalText()
        {
            var builder = new StringBuilder();
            Append(builder, "run_stable_id", RunStableId.ToString());
            Append(builder, "route_fingerprint", RoutePayload.Fingerprint);
            Append(builder, "selected_mode_stable_id", SelectedModeStableId.ToString());
            Append(builder, "selected_level_stable_id", SelectedLevelStableId.ToString());
            Append(
                builder,
                "completion_state",
                ((int)CompletionState).ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "contribution_count",
                contributions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < contributions.Count; index++)
            {
                LevelRunPlayerContributionV1 value = contributions[index];
                Append(
                    builder,
                    "contribution_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    value.PlayerStableId
                        + "|"
                        + value.KillCount.ToString(CultureInfo.InvariantCulture)
                        + "|"
                        + value.ExperienceEarned.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name)
                .Append('=')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append(';');
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] digest = sha.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(
                        digest[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }

    public sealed class LevelRunEnemyDestructionResultV1
    {
        public LevelRunEnemyDestructionResultV1(
            LevelRunEnemyDestructionStatusV1 status,
            string rejectionCode,
            StableId attributedPlayerStableId,
            long experienceEarned,
            bool roomBecameClear)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            AttributedPlayerStableId = attributedPlayerStableId;
            if (experienceEarned < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(experienceEarned));
            }

            ExperienceEarned = experienceEarned;
            RoomBecameClear = roomBecameClear;
        }

        public LevelRunEnemyDestructionStatusV1 Status { get; }
        public string RejectionCode { get; }
        public StableId AttributedPlayerStableId { get; }
        public long ExperienceEarned { get; }
        public bool RoomBecameClear { get; }
        public bool Changed
        {
            get
            {
                return Status == LevelRunEnemyDestructionStatusV1.Applied
                    || Status == LevelRunEnemyDestructionStatusV1.Unattributed;
            }
        }
    }

    public sealed class LevelRunExtractionResultV1
    {
        public LevelRunExtractionResultV1(
            LevelRunExtractionStatusV1 status,
            string rejectionCode,
            MissionResultPayloadV1 missionResult,
            LevelRunSummaryV1 summary)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            MissionResult = missionResult;
            Summary = summary;
        }

        public LevelRunExtractionStatusV1 Status { get; }
        public string RejectionCode { get; }
        public MissionResultPayloadV1 MissionResult { get; }
        public LevelRunSummaryV1 Summary { get; }
        public bool Completed
        {
            get
            {
                return Status == LevelRunExtractionStatusV1.Completed
                    || Status == LevelRunExtractionStatusV1.ExactDuplicateNoChange;
            }
        }
    }
}
