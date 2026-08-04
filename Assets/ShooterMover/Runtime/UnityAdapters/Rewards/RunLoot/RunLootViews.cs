using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunLoot;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunLoots
{
    public enum RunLootTriggerShape
    {
        Circle = 0,
        Rectangle = 1,
    }

    [Serializable]
    public sealed class RunLootPresentationEntry
    {
        [SerializeField] private RewardGrantKind rewardKind = RewardGrantKind.Money;
        [SerializeField] private string contentStableId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private RunLootTriggerShape triggerShape = RunLootTriggerShape.Circle;
        [SerializeField, Min(0.01f)] private float triggerRadius = 0.75f;
        [SerializeField] private Vector2 triggerSize = Vector2.one;
        [SerializeField] private string label;

        public RewardGrantKind RewardKind { get { return rewardKind; } }
        public GameObject Prefab { get { return prefab; } }
        public Sprite Sprite { get { return sprite; } }
        public Vector3 LocalScale { get { return localScale; } }
        public RunLootTriggerShape TriggerShape { get { return triggerShape; } }
        public float TriggerRadius { get { return triggerRadius; } }
        public Vector2 TriggerSize { get { return triggerSize; } }
        public string Label { get { return label ?? string.Empty; } }
        public bool IsKindFallback { get { return string.IsNullOrWhiteSpace(contentStableId); } }

        public bool TryGetContentStableId(out StableId value)
        {
            value = null;
            return !string.IsNullOrWhiteSpace(contentStableId)
                && StableId.TryParse(contentStableId.Trim(), out value);
        }

        public bool Matches(RunLootSnapshot pickup, bool exactContent)
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
            if (!Enum.IsDefined(typeof(RewardGrantKind), rewardKind))
            {
                diagnostic = "run-pickup-presentation-kind-invalid";
                return false;
            }
            if (!Enum.IsDefined(typeof(RunLootTriggerShape), triggerShape))
            {
                diagnostic = "run-pickup-presentation-trigger-shape-invalid";
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
            if (triggerShape == RunLootTriggerShape.Circle
                && (triggerRadius <= 0f
                    || float.IsNaN(triggerRadius)
                    || float.IsInfinity(triggerRadius)))
            {
                diagnostic = "run-pickup-presentation-trigger-radius-invalid";
                return false;
            }
            if (triggerShape == RunLootTriggerShape.Rectangle
                && (!IsPositiveFinite(triggerSize.x)
                    || !IsPositiveFinite(triggerSize.y)))
            {
                diagnostic = "run-pickup-presentation-trigger-size-invalid";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        public void Configure(
            RewardGrantKind kind,
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
            triggerShape = RunLootTriggerShape.Circle;
            triggerRadius = radius;
            triggerSize = Vector2.one;
            label = displayLabel;
            ValidateConfiguration();
        }

        public void ConfigureRectangle(
            RewardGrantKind kind,
            StableId contentId,
            GameObject prefab,
            Sprite sprite,
            Vector3 scale,
            Vector2 size,
            string displayLabel)
        {
            rewardKind = kind;
            contentStableId = contentId == null ? string.Empty : contentId.ToString();
            this.prefab = prefab;
            this.sprite = sprite;
            localScale = scale;
            triggerShape = RunLootTriggerShape.Rectangle;
            triggerRadius = 0.75f;
            triggerSize = size;
            label = displayLabel;
            ValidateConfiguration();
        }

        public void ConfigureForTests(
            RewardGrantKind kind,
            string contentId,
            GameObject prefab,
            Sprite sprite,
            Vector3 scale,
            float radius,
            string displayLabel)
        {
            Configure(
                kind,
                ParseOptionalContentId(contentId),
                prefab,
                sprite,
                scale,
                radius,
                displayLabel);
        }

        public void ConfigureRectangleForTests(
            RewardGrantKind kind,
            string contentId,
            GameObject prefab,
            Sprite sprite,
            Vector3 scale,
            Vector2 size,
            string displayLabel)
        {
            ConfigureRectangle(
                kind,
                ParseOptionalContentId(contentId),
                prefab,
                sprite,
                scale,
                size,
                displayLabel);
        }

        private void ValidateConfiguration()
        {
            string diagnostic;
            if (!IsUsable(out diagnostic))
                throw new ArgumentException(diagnostic);
        }

        private static StableId ParseOptionalContentId(string contentId)
        {
            StableId parsed = null;
            if (!string.IsNullOrWhiteSpace(contentId)
                && !StableId.TryParse(contentId.Trim(), out parsed))
            {
                throw new ArgumentException("Pickup presentation content StableId is invalid.");
            }
            return parsed;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Typed presentation lookup. Exact content mappings win; a reward-kind fallback may
    /// serve ordinary content. Strongbox fallbacks retain their visual but derive a readable
    /// tier label and rectangular collection footprint from the exact tier identity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunLootViews : MonoBehaviour
    {
        private static readonly Vector2 StrongboxTriggerSize = new Vector2(1.2f, 1.1f);

        [SerializeField] private RunLootPresentationEntry[] entries =
            new RunLootPresentationEntry[0];

        public bool TryResolve(
            RunLootSnapshot pickup,
            out RunLootPresentationEntry entry,
            out string diagnostic)
        {
            entry = null;
            diagnostic = string.Empty;
            if (pickup == null)
            {
                diagnostic = "run-pickup-presentation-pickup-null";
                return false;
            }

            RunLootPresentationEntry fallback = null;
            for (int index = 0; index < entries.Length; index++)
            {
                RunLootPresentationEntry candidate = entries[index];
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

            if (pickup.Reward.Kind == RewardGrantKind.Strongbox)
            {
                entry = ResolveStrongboxPresentation(pickup, fallback);
                return true;
            }

            entry = fallback;
            return true;
        }

        public void Configure(
            IEnumerable<RunLootPresentationEntry> configuredEntries)
        {
            entries = configuredEntries == null
                ? new RunLootPresentationEntry[0]
                : new List<RunLootPresentationEntry>(configuredEntries).ToArray();
        }

        public void ConfigureForTests(
            IEnumerable<RunLootPresentationEntry> configuredEntries)
        {
            Configure(configuredEntries);
        }

        private static RunLootPresentationEntry ResolveStrongboxPresentation(
            RunLootSnapshot pickup,
            RunLootPresentationEntry fallback)
        {
            var resolved = new RunLootPresentationEntry();
            resolved.ConfigureRectangle(
                RewardGrantKind.Strongbox,
                pickup.Reward.ContentStableId,
                fallback.Prefab,
                fallback.Sprite,
                new Vector3(0.85f, 0.62f, 1f),
                StrongboxTriggerSize,
                StrongboxLabel(pickup.Reward.ContentStableId));
            return resolved;
        }

        private static string StrongboxLabel(StableId tierStableId)
        {
            string value = tierStableId == null ? string.Empty : tierStableId.ToString();
            int separator = value.LastIndexOf('.');
            string slug = separator >= 0 && separator < value.Length - 1
                ? value.Substring(separator + 1)
                : value;
            slug = slug.Replace('-', ' ').Replace('_', ' ').Trim();
            if (slug.Length == 0) return "Strongbox";
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                slug.ToLowerInvariant()) + " Strongbox";
        }
    }
}
