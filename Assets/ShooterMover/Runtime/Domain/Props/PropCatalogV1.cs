using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public sealed class PropDefinitionV1
    {
        private readonly ReadOnlyCollection<PropCapabilityV1> _capabilities;
        private readonly Dictionary<StableId, PropCapabilityV1> _byId;

        public PropDefinitionV1(
            StableId definitionId,
            StableId presentationId,
            IEnumerable<PropCapabilityV1> capabilities)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            PresentationId = presentationId ?? throw new ArgumentNullException(nameof(presentationId));
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            List<PropCapabilityV1> ordered =
                new List<PropCapabilityV1>(capabilities);
            for (int index = 0; index < ordered.Count; index++)
            {
                if (ordered[index] == null)
                {
                    throw new ArgumentException(
                        "Prop definitions cannot contain null capabilities.",
                        nameof(capabilities));
                }
            }

            ordered.Sort((left, right) =>
                left.CapabilityId.CompareTo(right.CapabilityId));
            _byId = new Dictionary<StableId, PropCapabilityV1>();
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=1|definition=").Append(DefinitionId);
            builder.Append("|presentation=").Append(PresentationId);
            for (int index = 0; index < ordered.Count; index++)
            {
                PropCapabilityV1 capability = ordered[index];
                if (_byId.ContainsKey(capability.CapabilityId))
                {
                    throw new ArgumentException(
                        "Duplicate prop capability '" + capability.CapabilityId + "'.",
                        nameof(capabilities));
                }

                _byId.Add(capability.CapabilityId, capability);
                builder.Append("|capability=").Append(capability.CanonicalText);
            }

            _capabilities = new ReadOnlyCollection<PropCapabilityV1>(ordered);
            Fingerprint = PropFingerprintV1.Compute64Hex(builder.ToString());
        }

        public int SchemaVersion
        {
            get { return 1; }
        }

        public StableId DefinitionId { get; }

        public StableId PresentationId { get; }

        public IReadOnlyList<PropCapabilityV1> Capabilities
        {
            get { return _capabilities; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId capabilityId,
            out PropCapabilityV1 capability)
        {
            if (capabilityId == null)
            {
                capability = null;
                return false;
            }

            return _byId.TryGetValue(capabilityId, out capability);
        }
    }

    public sealed class PropCatalogValidationException : InvalidOperationException
    {
        public PropCatalogValidationException(string message)
            : base(message)
        {
        }
    }

    public sealed class PropCatalogV1
    {
        private readonly ReadOnlyCollection<PropDefinitionV1> _definitions;
        private readonly Dictionary<StableId, PropDefinitionV1> _byId;

        public PropCatalogV1(
            PropCapabilityRegistryV1 registry,
            IEnumerable<PropDefinitionV1> definitions)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<PropDefinitionV1> ordered =
                new List<PropDefinitionV1>(definitions);
            for (int index = 0; index < ordered.Count; index++)
            {
                if (ordered[index] == null)
                {
                    throw new PropCatalogValidationException(
                        "Prop catalog cannot contain null definitions.");
                }
            }

            ordered.Sort((left, right) =>
                left.DefinitionId.CompareTo(right.DefinitionId));
            _byId = new Dictionary<StableId, PropDefinitionV1>();
            StringBuilder builder = new StringBuilder("schema=1");
            for (int index = 0; index < ordered.Count; index++)
            {
                PropDefinitionV1 definition = ordered[index];
                if (_byId.ContainsKey(definition.DefinitionId))
                {
                    throw new PropCatalogValidationException(
                        "Duplicate prop definition ID '" + definition.DefinitionId + "'.");
                }

                ValidateDefinition(registry, definition);
                _byId.Add(definition.DefinitionId, definition);
                builder.Append("|definition=")
                    .Append(definition.DefinitionId)
                    .Append(':').Append(definition.Fingerprint);
            }

            _definitions = new ReadOnlyCollection<PropDefinitionV1>(ordered);
            Fingerprint = PropFingerprintV1.Compute64Hex(builder.ToString());
        }

        public int SchemaVersion
        {
            get { return 1; }
        }

        public IReadOnlyList<PropDefinitionV1> Definitions
        {
            get { return _definitions; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId definitionId,
            out PropDefinitionV1 definition)
        {
            if (definitionId == null)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(definitionId, out definition);
        }

        private static void ValidateDefinition(
            PropCapabilityRegistryV1 registry,
            PropDefinitionV1 definition)
        {
            List<string> diagnostics = new List<string>();
            for (int index = 0; index < definition.Capabilities.Count; index++)
            {
                PropCapabilityV1 capability = definition.Capabilities[index];
                PropCapabilityValidatorV1 validator;
                if (!registry.TryGet(capability.CapabilityId, out validator))
                {
                    diagnostics.Add(
                        "Unknown prop capability '" + capability.CapabilityId + "'.");
                }
                else
                {
                    validator(capability, diagnostics);
                }
            }

            bool decorative = Has(definition, PropCapabilityIdsV1.Decorative);
            bool destructible = Has(definition, PropCapabilityIdsV1.Destructibility);
            bool healthBased = destructible
                && ReadInteger(definition, PropCapabilityIdsV1.Destructibility, "mode")
                    == (int)PropDestructibilityModeV1.HealthBased;
            bool damageBehavior = Has(definition, PropCapabilityIdsV1.DamageBehavior);
            bool resistance = Has(definition, PropCapabilityIdsV1.DamageResistance);
            bool explosion = Has(definition, PropCapabilityIdsV1.ExplodeOnDestroy);
            bool drop = Has(definition, PropCapabilityIdsV1.DropOnDestroy);
            bool interactable = Has(definition, PropCapabilityIdsV1.Interactable);
            bool switchCapability = Has(definition, PropCapabilityIdsV1.Switch);
            bool objective = Has(definition, PropCapabilityIdsV1.Objective);
            bool roomClear = ReadBoolean(
                definition,
                PropCapabilityIdsV1.RoomClear,
                "blocks");

            if (decorative
                && (destructible
                    || damageBehavior
                    || resistance
                    || explosion
                    || drop
                    || interactable
                    || switchCapability
                    || objective
                    || roomClear))
            {
                diagnostics.Add(
                    "Decorative-only props cannot own combat, interaction, reward, "
                    + "objective, explosion, or room-clear capabilities.");
            }

            if (destructible && !damageBehavior)
            {
                diagnostics.Add(
                    "Combat-capable props require a damage-behavior capability.");
            }

            if (resistance && !healthBased)
            {
                diagnostics.Add(
                    "Damage resistance requires health-based destructibility.");
            }

            if ((explosion || drop || roomClear) && !healthBased)
            {
                diagnostics.Add(
                    "Explosion, drop, and room-clear blocking require "
                    + "health-based destructibility.");
            }

            if (switchCapability && !interactable)
            {
                diagnostics.Add("Switch capability requires interactable capability.");
            }

            if (diagnostics.Count > 0)
            {
                throw new PropCatalogValidationException(
                    "Prop definition '" + definition.DefinitionId
                    + "' is invalid: " + string.Join(" ", diagnostics));
            }
        }

        internal static bool Has(
            PropDefinitionV1 definition,
            StableId capabilityId)
        {
            PropCapabilityV1 unused;
            return definition.TryGet(capabilityId, out unused);
        }

        internal static string Read(
            PropDefinitionV1 definition,
            StableId capabilityId,
            string key)
        {
            PropCapabilityV1 capability;
            string value;
            return definition.TryGet(capabilityId, out capability)
                && capability.TryGet(key, out value)
                    ? value
                    : null;
        }

        internal static int ReadInteger(
            PropDefinitionV1 definition,
            StableId capabilityId,
            string key)
        {
            int value;
            return int.TryParse(
                Read(definition, capabilityId, key),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                    ? value
                    : 0;
        }

        internal static bool ReadBoolean(
            PropDefinitionV1 definition,
            StableId capabilityId,
            string key)
        {
            return string.Equals(
                Read(definition, capabilityId, key),
                "1",
                StringComparison.Ordinal);
        }
    }
}
