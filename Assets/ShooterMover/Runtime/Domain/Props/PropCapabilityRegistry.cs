using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public delegate void PropCapabilityValidator(
        PropCapability capability,
        IList<string> diagnostics);

    public sealed class PropCapabilityRegistry
    {
        private readonly Dictionary<StableId, PropCapabilityValidator> _validators =
            new Dictionary<StableId, PropCapabilityValidator>();

        public void Register(
            StableId capabilityId,
            PropCapabilityValidator validator)
        {
            if (capabilityId == null)
            {
                throw new ArgumentNullException(nameof(capabilityId));
            }

            if (validator == null)
            {
                throw new ArgumentNullException(nameof(validator));
            }

            if (_validators.ContainsKey(capabilityId))
            {
                throw new InvalidOperationException(
                    "Prop capability '" + capabilityId + "' is already registered.");
            }

            _validators.Add(capabilityId, validator);
        }

        public bool TryGet(
            StableId capabilityId,
            out PropCapabilityValidator validator)
        {
            if (capabilityId == null)
            {
                validator = null;
                return false;
            }

            return _validators.TryGetValue(capabilityId, out validator);
        }

        public static PropCapabilityRegistry CreateBuiltIns()
        {
            PropCapabilityRegistry registry = new PropCapabilityRegistry();
            registry.Register(PropCapabilityIds.Collision, ValidateCollision);
            registry.Register(PropCapabilityIds.Destructibility, ValidateDestructibility);
            registry.Register(PropCapabilityIds.DamageResistance, ValidateResistances);
            registry.Register(PropCapabilityIds.ExplodeOnDestroy, ValidateProfile);
            registry.Register(PropCapabilityIds.DropOnDestroy, ValidateProfile);
            registry.Register(PropCapabilityIds.Interactable, ValidateFact);
            registry.Register(PropCapabilityIds.Switch, ValidateSwitch);
            registry.Register(PropCapabilityIds.Objective, ValidateFact);
            registry.Register(PropCapabilityIds.DamageBehavior, ValidateDamageBehavior);
            registry.Register(PropCapabilityIds.RoomClear, ValidateRoomClear);
            registry.Register(PropCapabilityIds.Decorative, ValidateDecorative);
            return registry;
        }

        private static void ValidateCollision(
            PropCapability capability,
            IList<string> diagnostics)
        {
            RequireBoolean(capability, "solid", diagnostics);
            RequireOnly(capability, diagnostics, "solid");
        }

        private static void ValidateDestructibility(
            PropCapability capability,
            IList<string> diagnostics)
        {
            int mode;
            if (!TryInteger(capability, "mode", out mode)
                || !Enum.IsDefined(typeof(PropDestructibilityMode), mode))
            {
                diagnostics.Add("Destructibility mode is invalid.");
                return;
            }

            string maximumHealth;
            bool hasHealth = capability.TryGet("maximum-health", out maximumHealth);
            if ((PropDestructibilityMode)mode == PropDestructibilityMode.HealthBased)
            {
                double health;
                if (!hasHealth
                    || !double.TryParse(
                        maximumHealth,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out health)
                    || double.IsNaN(health)
                    || double.IsInfinity(health)
                    || health <= 0d)
                {
                    diagnostics.Add(
                        "Health-based destructibility requires positive finite maximum health.");
                }

                RequireOnly(capability, diagnostics, "mode", "maximum-health");
            }
            else
            {
                if (hasHealth)
                {
                    diagnostics.Add(
                        "Inbreakables cannot declare maximum health.");
                }

                RequireOnly(capability, diagnostics, "mode");
            }
        }

        private static void ValidateResistances(
            PropCapability capability,
            IList<string> diagnostics)
        {
            if (capability.Parameters.Count == 0)
            {
                diagnostics.Add(
                    "Damage resistance requires at least one damage channel.");
            }

            foreach (KeyValuePair<string, string> pair in capability.Parameters)
            {
                StableId channel;
                double multiplier;
                if (!StableId.TryParse(pair.Key, out channel))
                {
                    diagnostics.Add(
                        "Damage-resistance channel '" + pair.Key + "' is invalid.");
                }

                if (!double.TryParse(
                    pair.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out multiplier)
                    || double.IsNaN(multiplier)
                    || double.IsInfinity(multiplier)
                    || multiplier < 0d)
                {
                    diagnostics.Add(
                        "Damage-resistance multiplier for '" + pair.Key
                        + "' must be finite and non-negative.");
                }
            }
        }

        private static void ValidateProfile(
            PropCapability capability,
            IList<string> diagnostics)
        {
            RequireStableId(capability, "profile-id", diagnostics);
            RequireOnly(capability, diagnostics, "profile-id");
        }

        private static void ValidateFact(
            PropCapability capability,
            IList<string> diagnostics)
        {
            RequireStableId(capability, "fact-id", diagnostics);
            RequireOnly(capability, diagnostics, "fact-id");
        }

        private static void ValidateSwitch(
            PropCapability capability,
            IList<string> diagnostics)
        {
            RequireStableId(capability, "switch-id", diagnostics);
            RequireBoolean(capability, "initially-active", diagnostics);
            RequireOnly(capability, diagnostics, "switch-id", "initially-active");
        }

        private static void ValidateDamageBehavior(
            PropCapability capability,
            IList<string> diagnostics)
        {
            int alignment;
            if (!TryInteger(capability, "alignment", out alignment)
                || !Enum.IsDefined(typeof(PropDamageAlignment), alignment))
            {
                diagnostics.Add("Prop damage alignment is invalid.");
            }

            RequireStableId(capability, "policy-id", diagnostics);
            RequireOnly(capability, diagnostics, "alignment", "policy-id");
        }

        private static void ValidateRoomClear(
            PropCapability capability,
            IList<string> diagnostics)
        {
            RequireBoolean(capability, "blocks", diagnostics);
            RequireOnly(capability, diagnostics, "blocks");
        }

        private static void ValidateDecorative(
            PropCapability capability,
            IList<string> diagnostics)
        {
            string value;
            if (!capability.TryGet("only", out value) || value != "1")
            {
                diagnostics.Add("Decorative capability must declare only=1.");
            }

            RequireOnly(capability, diagnostics, "only");
        }

        private static bool TryInteger(
            PropCapability capability,
            string key,
            out int value)
        {
            string text;
            if (!capability.TryGet(key, out text))
            {
                value = 0;
                return false;
            }

            return int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static void RequireBoolean(
            PropCapability capability,
            string key,
            IList<string> diagnostics)
        {
            string value;
            if (!capability.TryGet(key, out value)
                || (value != "0" && value != "1"))
            {
                diagnostics.Add(
                    "Capability '" + capability.CapabilityId
                    + "' requires boolean parameter '" + key + "'.");
            }
        }

        private static void RequireStableId(
            PropCapability capability,
            string key,
            IList<string> diagnostics)
        {
            string text;
            StableId ignored;
            if (!capability.TryGet(key, out text)
                || !StableId.TryParse(text, out ignored))
            {
                diagnostics.Add(
                    "Capability '" + capability.CapabilityId
                    + "' requires StableId parameter '" + key + "'.");
            }
        }

        private static void RequireOnly(
            PropCapability capability,
            IList<string> diagnostics,
            params string[] allowed)
        {
            HashSet<string> allowedKeys =
                new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (string key in capability.Parameters.Keys)
            {
                if (!allowedKeys.Contains(key))
                {
                    diagnostics.Add(
                        "Unknown parameter '" + key + "' for capability '"
                        + capability.CapabilityId + "'.");
                }
            }
        }
    }
}
