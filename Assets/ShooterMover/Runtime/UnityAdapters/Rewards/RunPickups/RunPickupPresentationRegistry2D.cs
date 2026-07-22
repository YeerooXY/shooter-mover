using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    [Serializable]
    public sealed class RunPickupPresentationEntryV1
    {
        [SerializeField] private RewardGrantKindV1 rewardKind = RewardGrantKindV1.Money;
        [SerializeField] private string contentStableId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField, Min(0.01f)] private float triggerRadius = 0.75f;
        [SerializeField] private string label;

        public RewardGrantKindV1 RewardKind { get { return rewardKind; } }
        public GameObject Prefab { get { return prefab; } }
        public Sprite Sprite { get { return sprite; } }
        public Vector3 LocalScale { get { return localScale; } }
        public float TriggerRadius { get { return triggerRadius; } }
        public string Label { get { return label ?? string.Empty; } }
        public bool IsKindFallback { get { return string.IsNullOrWhiteSpace(contentStableId); } }

        public bool TryGetContentStableId(out StableId value)
        {
            value = null;
            return !string.IsNullOrWhiteSpace(contentStableId)
                && StableId.TryParse(contentStableId.Trim(), out value);
        }

        public bool Matches(RunPickupSnapshotV1 pickup, bool exactContent)
        {
            if (pickup == null || pickup.Reward.Kind != rewardKind)
                return false;
            StableId content;
            bool hasContent = TryGetContentStableId(out content);
            return exactContent
                ? hasContent && content == pickup.Reward.ContentStableId
                : !hasContent;
        }

        public bool IsUsable(out string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RewardGrantKindV1), rewardKind))
            {
                diagnostic = "run-pickup-presentation-kind-invalid";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(contentStableId))
            {
                StableId ignored;
                if (!StableId.TryParse(contentStableId.Trim(), out ignored))
                {
                    diagnostic = "run-pickup-presentation-content-id-invalid";
                    return false;
                }
            }
            if (prefab == null && sprite == null)
            {
                diagnostic = "run-pickup-presentation-visual-missing";
                return false;
            }
            if (triggerRadius <= 0f
                || float.IsNaN(triggerRadius)
                || float.IsInfinity(triggerRadius))
            {
                diagnostic = "run-pickup-presentation-trigger-radius-invalid";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        public void Configure(
            RewardGrantKindV1 kind,
            StableId contentId,
            GameObject prefab,
            Sprite sprite,
            Vector3 scale,
            float radius,
            string displayLabel)
        {
            rewardKind = kind;
            contentStableId = contentId == null ? string.Empty : contentId.ToString();
            this.prefab = prefab;
            this.sprite = sprite;
            localScale = scale;
            triggerRadius = radius;
            label = displayLabel;

            string diagnostic;
            if (!IsUsable(out diagnostic))
                throw new ArgumentException(diagnostic);
        }

        public void ConfigureForTests(
            RewardGrantKindV1 kind,
            string contentId,
            GameObject prefab,
            Sprite sprite,
            Vector3 scale,
            float radius,
            string displayLabel)
        {
            StableId parsed = null;
            if (!string.IsNullOrWhiteSpace(contentId)
                && !StableId.TryParse(contentId.Trim(), out parsed))
            {
                throw new ArgumentException("Pickup presentation content StableId is invalid.");
            }
            Configure(kind, parsed, prefab, sprite, scale, radius, displayLabel);
        }
    }

    /// <summary>
    /// Typed presentation lookup. Exact content mappings win; a reward-kind fallback may
    /// serve ordinary content. No pickup type controller or GameObject-name lookup exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPickupPresentationRegistry2D : MonoBehaviour
    {
        [SerializeField] private RunPickupPresentationEntryV1[] entries =
            new RunPickupPresentationEntryV1[0];

        public bool TryResolve(
            RunPickupSnapshotV1 pickup,
            out RunPickupPresentationEntryV1 entry,
            out string diagnostic)
        {
            entry = null;
            diagnostic = string.Empty;
            if (pickup == null)
            {
                diagnostic = "run-pickup-presentation-pickup-null";
                return false;
            }

            RunPickupPresentationEntryV1 fallback = null;
            for (int index = 0; index < entries.Length; index++)
            {
                RunPickupPresentationEntryV1 candidate = entries[index];
                if (candidate == null) continue;
                if (candidate.Matches(pickup, true))
                {
                    if (!candidate.IsUsable(out diagnostic)) return false;
                    entry = candidate;
                    return true;
                }
                if (fallback == null && candidate.Matches(pickup, false))
                    fallback = candidate;
            }

            if (fallback == null)
            {
                diagnostic = "run-pickup-presentation-route-missing:"
                    + pickup.Reward.Kind
                    + ":"
                    + pickup.Reward.ContentStableId;
                return false;
            }
            if (!fallback.IsUsable(out diagnostic)) return false;
            entry = fallback;
            return true;
        }

        public void Configure(
            IEnumerable<RunPickupPresentationEntryV1> configuredEntries)
        {
            entries = configuredEntries == null
                ? new RunPickupPresentationEntryV1[0]
                : new List<RunPickupPresentationEntryV1>(configuredEntries).ToArray();
        }

        public void ConfigureForTests(
            IEnumerable<RunPickupPresentationEntryV1> configuredEntries)
        {
            Configure(configuredEntries);
        }
    }
}
