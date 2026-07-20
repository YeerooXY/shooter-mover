using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public sealed class PropRuntimeV1
    {
        private sealed class Replay<T>
        {
            public Replay(string fingerprint, T result)
            {
                Fingerprint = fingerprint;
                Result = result;
            }

            public string Fingerprint { get; }
            public T Result { get; }
        }

        private static readonly StableId ExplosionKind =
            StableId.Parse("fact-kind.prop-explosion");
        private static readonly StableId DropKind =
            StableId.Parse("fact-kind.prop-drop-request");
        private static readonly StableId ObjectiveKind =
            StableId.Parse("fact-kind.prop-objective");
        private static readonly StableId InteractionKind =
            StableId.Parse("fact-kind.prop-interaction");
        private static readonly StableId SwitchOnKind =
            StableId.Parse("fact-kind.prop-switch-on");
        private static readonly StableId SwitchOffKind =
            StableId.Parse("fact-kind.prop-switch-off");

        private readonly PropDefinitionV1 _definition;
        private readonly IPropDamageEligibilityPolicyV1 _policy;
        private readonly Dictionary<StableId, double> _resistances;
        private readonly Dictionary<StableId, Replay<PropDamageResultV1>> _damageHistory =
            new Dictionary<StableId, Replay<PropDamageResultV1>>();
        private readonly Dictionary<StableId, Replay<PropInteractionResultV1>> _interactionHistory =
            new Dictionary<StableId, Replay<PropInteractionResultV1>>();
        private readonly bool _solid;
        private readonly bool _combat;
        private readonly PropDestructibilityModeV1 _destructibility;
        private readonly double? _maximumHealth;
        private readonly PropDamageAlignmentV1 _alignment;
        private readonly StableId _policyId;
        private readonly StableId _explosionProfileId;
        private readonly StableId _dropProfileId;
        private readonly StableId _interactionFactId;
        private readonly StableId _switchId;
        private readonly StableId _objectiveFactId;
        private readonly bool _blocksRoomClear;
        private double? _currentHealth;
        private bool _terminal;
        private bool? _switchActive;

        internal PropRuntimeV1(
            PropPlacementV1 placement,
            PropDefinitionV1 definition,
            IPropDamageEligibilityPolicyV1 policy)
        {
            Placement = placement;
            _definition = definition;
            _policy = policy;
            ParticipantId = placement.Identity.Value;
            _solid = PropCatalogV1.ReadBoolean(
                definition,
                PropCapabilityIdsV1.Collision,
                "solid");
            _combat = PropCatalogV1.Has(
                definition,
                PropCapabilityIdsV1.Destructibility);
            _destructibility = (PropDestructibilityModeV1)PropCatalogV1.ReadInteger(
                definition,
                PropCapabilityIdsV1.Destructibility,
                "mode");
            if (_combat
                && _destructibility == PropDestructibilityModeV1.HealthBased)
            {
                _maximumHealth = ReadDouble(
                    definition,
                    PropCapabilityIdsV1.Destructibility,
                    "maximum-health");
                _currentHealth = _maximumHealth;
            }

            _alignment = (PropDamageAlignmentV1)PropCatalogV1.ReadInteger(
                definition,
                PropCapabilityIdsV1.DamageBehavior,
                "alignment");
            _policyId = ReadId(
                definition,
                PropCapabilityIdsV1.DamageBehavior,
                "policy-id");
            _explosionProfileId = ReadId(
                definition,
                PropCapabilityIdsV1.ExplodeOnDestroy,
                "profile-id");
            _dropProfileId = ReadId(
                definition,
                PropCapabilityIdsV1.DropOnDestroy,
                "profile-id");
            _interactionFactId = ReadId(
                definition,
                PropCapabilityIdsV1.Interactable,
                "fact-id");
            _switchId = ReadId(
                definition,
                PropCapabilityIdsV1.Switch,
                "switch-id");
            _objectiveFactId = ReadId(
                definition,
                PropCapabilityIdsV1.Objective,
                "fact-id");
            _blocksRoomClear = PropCatalogV1.ReadBoolean(
                definition,
                PropCapabilityIdsV1.RoomClear,
                "blocks");
            if (_switchId != null)
            {
                _switchActive = PropCatalogV1.ReadBoolean(
                    definition,
                    PropCapabilityIdsV1.Switch,
                    "initially-active");
            }

            _resistances = ReadResistances(definition);
        }

        public PropPlacementV1 Placement { get; }

        public StableId ParticipantId { get; }

        public PropRuntimeSnapshotV1 Snapshot
        {
            get { return BuildSnapshot(); }
        }

        public PropDamageResultV1 ApplyDamage(PropDamageCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Replay<PropDamageResultV1> replay;
            if (_damageHistory.TryGetValue(command.OperationId, out replay))
            {
                if (!string.Equals(
                    replay.Fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return DamageResult(
                        PropDamageStatusV1.RejectedConflictingReplay,
                        CurrentHealth(),
                        CurrentHealth(),
                        0d,
                        PropFactBatchV1.Empty,
                        "Damage operation ID was reused with conflicting input.");
                }

                return DamageResult(
                    PropDamageStatusV1.DuplicateNoChange,
                    replay.Result.PreviousHealth,
                    replay.Result.CurrentHealth,
                    0d,
                    PropFactBatchV1.Empty,
                    "Exact damage retry produced no mutation or repeated facts.");
            }

            PropDamageResultV1 result = ApplyFirstDamage(command);
            _damageHistory.Add(
                command.OperationId,
                new Replay<PropDamageResultV1>(command.Fingerprint, result));
            return result;
        }

        public PropInteractionResultV1 Interact(PropInteractionCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Replay<PropInteractionResultV1> replay;
            if (_interactionHistory.TryGetValue(command.OperationId, out replay))
            {
                if (!string.Equals(
                    replay.Fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return InteractionResult(
                        PropInteractionStatusV1.RejectedConflictingReplay,
                        null,
                        null,
                        null,
                        "Interaction operation ID was reused with conflicting input.");
                }

                return InteractionResult(
                    PropInteractionStatusV1.DuplicateNoChange,
                    null,
                    null,
                    null,
                    "Exact interaction retry produced no repeated facts.");
            }

            PropInteractionResultV1 result = ApplyFirstInteraction(command);
            _interactionHistory.Add(
                command.OperationId,
                new Replay<PropInteractionResultV1>(command.Fingerprint, result));
            return result;
        }

        private PropDamageResultV1 ApplyFirstDamage(PropDamageCommandV1 command)
        {
            double previous = CurrentHealth();
            if (!_combat)
            {
                return DamageResult(
                    PropDamageStatusV1.RejectedNoCombatAuthority,
                    previous,
                    previous,
                    0d,
                    PropFactBatchV1.Empty,
                    "This prop has no combat authority.");
            }

            if (_terminal)
            {
                return DamageResult(
                    PropDamageStatusV1.RejectedTerminal,
                    previous,
                    previous,
                    0d,
                    PropFactBatchV1.Empty,
                    "Terminal props cannot accept damage.");
            }

            PropDamageEligibilityContextV1 context =
                new PropDamageEligibilityContextV1(
                    command,
                    ParticipantId,
                    _alignment,
                    _policyId);
            if (_policy == null || !_policy.CanDamage(context))
            {
                return DamageResult(
                    PropDamageStatusV1.RejectedByPolicy,
                    previous,
                    previous,
                    0d,
                    PropFactBatchV1.Empty,
                    "Injected damage policy rejected the hit.");
            }

            if (_destructibility == PropDestructibilityModeV1.Indestructible)
            {
                return DamageResult(
                    PropDamageStatusV1.RejectedIndestructible,
                    previous,
                    previous,
                    0d,
                    PropFactBatchV1.Empty,
                    "The prop is explicitly indestructible.");
            }

            double multiplier;
            if (!_resistances.TryGetValue(command.DamageChannelId, out multiplier))
            {
                multiplier = 1d;
            }

            double applied = command.RequestedDamage * multiplier;
            _currentHealth = Math.Max(0d, _currentHealth.Value - applied);
            if (_currentHealth.Value > 0d)
            {
                return DamageResult(
                    PropDamageStatusV1.Applied,
                    previous,
                    _currentHealth.Value,
                    applied,
                    PropFactBatchV1.Empty,
                    "Prop damage accepted.");
            }

            _terminal = true;
            PropTerminalFactV1 terminal =
                new PropTerminalFactV1(command, ParticipantId, _definition.DefinitionId);
            PropTriggeredFactV1 explosion = Triggered(
                command.OperationId,
                ExplosionKind,
                _explosionProfileId,
                command.SourceParticipantId);
            PropTriggeredFactV1 drop = Triggered(
                command.OperationId,
                DropKind,
                _dropProfileId,
                command.SourceParticipantId);
            PropTriggeredFactV1 objective = Triggered(
                command.OperationId,
                ObjectiveKind,
                _objectiveFactId,
                command.SourceParticipantId);
            return DamageResult(
                PropDamageStatusV1.Destroyed,
                previous,
                0d,
                applied,
                new PropFactBatchV1(terminal, explosion, drop, objective),
                "Prop entered terminal destroyed state.");
        }

        private PropInteractionResultV1 ApplyFirstInteraction(
            PropInteractionCommandV1 command)
        {
            if (_terminal)
            {
                return InteractionResult(
                    PropInteractionStatusV1.RejectedTerminal,
                    null,
                    null,
                    null,
                    "Terminal props cannot be interacted with.");
            }

            if (_interactionFactId == null)
            {
                return InteractionResult(
                    PropInteractionStatusV1.RejectedNotInteractable,
                    null,
                    null,
                    null,
                    "The prop has no interactable capability.");
            }

            PropTriggeredFactV1 interaction = Triggered(
                command.OperationId,
                InteractionKind,
                _interactionFactId,
                command.SourceParticipantId);
            PropTriggeredFactV1 switchFact = null;
            if (_switchId != null)
            {
                _switchActive = !_switchActive.Value;
                switchFact = Triggered(
                    command.OperationId,
                    _switchActive.Value ? SwitchOnKind : SwitchOffKind,
                    _switchId,
                    command.SourceParticipantId);
            }

            PropTriggeredFactV1 objective = Triggered(
                command.OperationId,
                ObjectiveKind,
                _objectiveFactId,
                command.SourceParticipantId);
            return InteractionResult(
                PropInteractionStatusV1.Applied,
                interaction,
                switchFact,
                objective,
                "Prop interaction accepted.");
        }

        private PropTriggeredFactV1 Triggered(
            StableId factId,
            StableId kindId,
            StableId valueId,
            StableId sourceParticipantId)
        {
            return valueId == null
                ? null
                : new PropTriggeredFactV1(
                    factId,
                    kindId,
                    valueId,
                    ParticipantId,
                    sourceParticipantId);
        }

        private PropDamageResultV1 DamageResult(
            PropDamageStatusV1 status,
            double previous,
            double current,
            double applied,
            PropFactBatchV1 facts,
            string diagnostic)
        {
            return new PropDamageResultV1(
                status,
                previous,
                current,
                applied,
                facts,
                BuildSnapshot(),
                diagnostic);
        }

        private PropInteractionResultV1 InteractionResult(
            PropInteractionStatusV1 status,
            PropTriggeredFactV1 interaction,
            PropTriggeredFactV1 switchFact,
            PropTriggeredFactV1 objective,
            string diagnostic)
        {
            return new PropInteractionResultV1(
                status,
                interaction,
                switchFact,
                objective,
                BuildSnapshot(),
                diagnostic);
        }

        private PropRuntimeSnapshotV1 BuildSnapshot()
        {
            return new PropRuntimeSnapshotV1(
                ParticipantId,
                _definition.DefinitionId,
                _definition.PresentationId,
                _solid,
                _combat,
                _terminal,
                _maximumHealth,
                _currentHealth,
                _blocksRoomClear && !_terminal,
                _switchId,
                _switchActive);
        }

        private double CurrentHealth()
        {
            return _currentHealth.HasValue ? _currentHealth.Value : 0d;
        }

        private static StableId ReadId(
            PropDefinitionV1 definition,
            StableId capabilityId,
            string key)
        {
            string text = PropCatalogV1.Read(definition, capabilityId, key);
            StableId value;
            return StableId.TryParse(text, out value) ? value : null;
        }

        private static double ReadDouble(
            PropDefinitionV1 definition,
            StableId capabilityId,
            string key)
        {
            double value;
            return double.TryParse(
                PropCatalogV1.Read(definition, capabilityId, key),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
                    ? value
                    : 0d;
        }

        private static Dictionary<StableId, double> ReadResistances(
            PropDefinitionV1 definition)
        {
            Dictionary<StableId, double> result =
                new Dictionary<StableId, double>();
            PropCapabilityV1 capability;
            if (!definition.TryGet(
                PropCapabilityIdsV1.DamageResistance,
                out capability))
            {
                return result;
            }

            foreach (KeyValuePair<string, string> pair in capability.Parameters)
            {
                StableId channel = StableId.Parse(pair.Key);
                double multiplier = double.Parse(
                    pair.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                result.Add(channel, multiplier);
            }

            return result;
        }
    }
}
