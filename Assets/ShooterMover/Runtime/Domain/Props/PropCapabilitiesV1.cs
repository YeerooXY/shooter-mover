using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public enum PropDestructibilityModeV1
    {
        Indestructible = 0,
        HealthBased = 1
    }

    public enum PropDamageAlignmentV1
    {
        Neutral = 0,
        Hostile = 1
    }

    public static class PropCapabilityIdsV1
    {
        public static readonly StableId Collision = StableId.Parse("capability.prop-collision");
        public static readonly StableId Destructibility = StableId.Parse("capability.prop-destructibility");
        public static readonly StableId DamageResistance = StableId.Parse("capability.prop-damage-resistance");
        public static readonly StableId ExplodeOnDestroy = StableId.Parse("capability.prop-explode-on-destroy");
        public static readonly StableId DropOnDestroy = StableId.Parse("capability.prop-drop-on-destroy");
        public static readonly StableId Interactable = StableId.Parse("capability.prop-interactable");
        public static readonly StableId Switch = StableId.Parse("capability.prop-switch");
        public static readonly StableId Objective = StableId.Parse("capability.prop-objective");
        public static readonly StableId DamageBehavior = StableId.Parse("capability.prop-damage-behavior");
        public static readonly StableId RoomClear = StableId.Parse("capability.prop-room-clear");
        public static readonly StableId Decorative = StableId.Parse("capability.prop-decorative");
    }

    public sealed class PropCapabilityV1
    {
        private readonly ReadOnlyDictionary<string, string> _parameters;

        public PropCapabilityV1(
            StableId capabilityId,
            IEnumerable<KeyValuePair<string, string>> parameters)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            SortedDictionary<string, string> ordered =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (parameters != null)
            {
                foreach (KeyValuePair<string, string> pair in parameters)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    {
                        throw new ArgumentException(
                            "Prop capability parameters require non-empty keys and non-null values.",
                            nameof(parameters));
                    }

                    if (ordered.ContainsKey(pair.Key))
                    {
                        throw new ArgumentException(
                            "Duplicate prop capability parameter '" + pair.Key + "'.",
                            nameof(parameters));
                    }

                    ordered.Add(pair.Key, pair.Value);
                }
            }

            _parameters = new ReadOnlyDictionary<string, string>(ordered);
            StringBuilder builder = new StringBuilder();
            builder.Append(CapabilityId).Append('{');
            bool first = true;
            foreach (KeyValuePair<string, string> pair in ordered)
            {
                if (!first)
                {
                    builder.Append(';');
                }

                builder.Append(pair.Key.Length)
                    .Append(':').Append(pair.Key)
                    .Append('=').Append(pair.Value.Length)
                    .Append(':').Append(pair.Value);
                first = false;
            }

            builder.Append('}');
            CanonicalText = builder.ToString();
            Fingerprint = PropFingerprintV1.Compute64Hex(CanonicalText);
        }

        public StableId CapabilityId { get; }

        public IReadOnlyDictionary<string, string> Parameters
        {
            get { return _parameters; }
        }

        public string CanonicalText { get; }

        public string Fingerprint { get; }

        public bool TryGet(string key, out string value)
        {
            return _parameters.TryGetValue(key, out value);
        }
    }

    public static class PropCapabilitiesV1
    {
        public static PropCapabilityV1 Collision(bool solid)
        {
            return One(PropCapabilityIdsV1.Collision, "solid", Bool(solid));
        }

        public static PropCapabilityV1 Indestructible()
        {
            return One(
                PropCapabilityIdsV1.Destructibility,
                "mode",
                ((int)PropDestructibilityModeV1.Indestructible).ToString(
                    CultureInfo.InvariantCulture));
        }

        public static PropCapabilityV1 HealthBased(double maximumHealth)
        {
            return Many(
                PropCapabilityIdsV1.Destructibility,
                Pair(
                    "mode",
                    ((int)PropDestructibilityModeV1.HealthBased).ToString(
                        CultureInfo.InvariantCulture)),
                Pair("maximum-health", Number(maximumHealth)));
        }

        public static PropCapabilityV1 DamageResistance(
            IEnumerable<KeyValuePair<StableId, double>> channelMultipliers)
        {
            if (channelMultipliers == null)
            {
                throw new ArgumentNullException(nameof(channelMultipliers));
            }

            List<KeyValuePair<string, string>> values =
                new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<StableId, double> pair in channelMultipliers)
            {
                if (pair.Key == null)
                {
                    throw new ArgumentException(
                        "Damage-resistance channel IDs cannot be null.",
                        nameof(channelMultipliers));
                }

                values.Add(Pair(pair.Key.ToString(), Number(pair.Value)));
            }

            return new PropCapabilityV1(
                PropCapabilityIdsV1.DamageResistance,
                values);
        }

        public static PropCapabilityV1 ExplodeOnDestroy(StableId profileId)
        {
            return One(
                PropCapabilityIdsV1.ExplodeOnDestroy,
                "profile-id",
                RequiredId(profileId, nameof(profileId)));
        }

        public static PropCapabilityV1 DropOnDestroy(StableId profileId)
        {
            return One(
                PropCapabilityIdsV1.DropOnDestroy,
                "profile-id",
                RequiredId(profileId, nameof(profileId)));
        }

        public static PropCapabilityV1 Interactable(StableId factId)
        {
            return One(
                PropCapabilityIdsV1.Interactable,
                "fact-id",
                RequiredId(factId, nameof(factId)));
        }

        public static PropCapabilityV1 Switch(StableId switchId, bool initiallyActive)
        {
            return Many(
                PropCapabilityIdsV1.Switch,
                Pair("switch-id", RequiredId(switchId, nameof(switchId))),
                Pair("initially-active", Bool(initiallyActive)));
        }

        public static PropCapabilityV1 Objective(StableId factId)
        {
            return One(
                PropCapabilityIdsV1.Objective,
                "fact-id",
                RequiredId(factId, nameof(factId)));
        }

        public static PropCapabilityV1 DamageBehavior(
            PropDamageAlignmentV1 alignment,
            StableId policyId)
        {
            return Many(
                PropCapabilityIdsV1.DamageBehavior,
                Pair(
                    "alignment",
                    ((int)alignment).ToString(CultureInfo.InvariantCulture)),
                Pair("policy-id", RequiredId(policyId, nameof(policyId))));
        }

        public static PropCapabilityV1 RoomClear(bool blocksRoomClear)
        {
            return One(
                PropCapabilityIdsV1.RoomClear,
                "blocks",
                Bool(blocksRoomClear));
        }

        public static PropCapabilityV1 Decorative()
        {
            return One(PropCapabilityIdsV1.Decorative, "only", "1");
        }

        private static PropCapabilityV1 One(
            StableId capabilityId,
            string key,
            string value)
        {
            return Many(capabilityId, Pair(key, value));
        }

        private static PropCapabilityV1 Many(
            StableId capabilityId,
            params KeyValuePair<string, string>[] parameters)
        {
            return new PropCapabilityV1(capabilityId, parameters);
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

        private static string RequiredId(StableId value, string name)
        {
            return (value ?? throw new ArgumentNullException(name)).ToString();
        }

        private static string Bool(bool value)
        {
            return value ? "1" : "0";
        }

        private static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
