using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public sealed class PropDefinition
    {
        private readonly ReadOnlyCollection<PropCapability> _capabilities;
        private readonly Dictionary<StableId, PropCapability> _byId;

        public PropDefinition(
            StableId definitionId,
            StableId presentationId,
            IEnumerable<PropCapability> capabilities)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            PresentationId = presentationId ?? throw new ArgumentNullException(nameof(presentationId));
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            List<PropCapability> ordered =
                new List<PropCapability>(capabilities);
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
            _byId = new Dictionary<StableId, PropCapability>();
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=1|definition=").Append(DefinitionId);
            builder.Append("|presentation=").Append(PresentationId);
            for (int index = 0; index < ordered.Count; index++)
            {
                PropCapability capability = ordered[index];
                if (_byId.ContainsKey(capability.CapabilityId))
                {
                    throw new ArgumentException(
                        "Duplicate prop capability '" + capability.CapabilityId + "'.",
                        nameof(capabilities));
                }

                _byId.Add(capability.CapabilityId, capability);
                builder.Append("|capability=").Append(capability.CanonicalText);
            }

            _capabilities = new ReadOnlyCollection<PropCapability>(ordered);
            Fingerprint = PropFingerprint.Compute64Hex(builder.ToString());
        }

        public int SchemaVersion
        {
            get { return 1; }
        }

        public StableId DefinitionId { get; }

        public StableId PresentationId { get; }

        public IReadOnlyList<PropCapability> Capabilities
        {
            get { return _capabilities; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId capabilityId,
            out PropCapability capability)
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

    public sealed class PropCatalog
    {
        private readonly ReadOnlyCollection<PropDefinition> _definitions;
        private readonly Dictionary<StableId, PropDefinition> _byId;

        public PropCatalog(
            PropCapabilityRegistry registry,
            IEnumerable<PropDefinition> definitions)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<PropDefinition> ordered =
                new List<PropDefinition>(definitions);
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
            _byId = new Dictionary<StableId, PropDefinition>();
            StringBuilder builder = new StringBuilder("schema=1");
            for (int index = 0; index < ordered.Count; index++)
            {
                PropDefinition definition = ordered[index];
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

            _definitions = new ReadOnlyCollection<PropDefinition>(ordered);
            Fingerprint = PropFingerprint.Compute64Hex(builder.ToString());
        }

        public int SchemaVersion
        {
            get { return 1; }
        }

        public IReadOnlyList<PropDefinition> Definitions
        {
            get { return _definitions; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId definitionId,
            out PropDefinition definition)
        {
            if (definitionId == null)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(definitionId, out definition);
        }

        private static void ValidateDefinition(
            PropCapabilityRegistry registry,
            PropDefinition definition)
        {
            List<string> diagnostics = new List<string>();
            for (int index = 0; index < definition.Capabilities.Count; index++)
            {
                PropCapability capability = definition.Capabilities[index];
                PropCapabilityValidator validator;
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

            bool decorative = Has(definition, PropCapabilityIds.Decorative);
            bool destructible = Has(definition, PropCapabilityIds.Destructibility);
            bool healthBased = destructible
                && ReadInteger(definition, PropCapabilityIds.Destructibility, "mode")
                    == (int)PropDestructibilityMode.HealthBased;
            bool damageBehavior = Has(definition, PropCapabilityIds.DamageBehavior);
            bool resistance = Has(definition, PropCapabilityIds.DamageResistance);
            bool explosion = Has(definition, PropCapabilityIds.ExplodeOnDestroy);
            bool drop = Has(definition, PropCapabilityIds.DropOnDestroy);
            bool interactable = Has(definition, PropCapabilityIds.Interactable);
            bool switchCapability = Has(definition, PropCapabilityIds.Switch);
            bool objective = Has(definition, PropCapabilityIds.Objective);
            bool roomClear = ReadBoolean(
                definition,
                PropCapabilityIds.RoomClear,
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
            PropDefinition definition,
            StableId capabilityId)
        {
            PropCapability unused;
            return definition.TryGet(capabilityId, out unused);
        }

        internal static string Read(
            PropDefinition definition,
            StableId capabilityId,
            string key)
        {
            PropCapability capability;
            string value;
            return definition.TryGet(capabilityId, out capability)
                && capability.TryGet(key, out value)
                    ? value
                    : null;
        }

        internal static int ReadInteger(
            PropDefinition definition,
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
            PropDefinition definition,
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
