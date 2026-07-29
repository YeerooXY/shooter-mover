using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public sealed class PropLive
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

        private readonly PropDefinition _definition;
        private readonly IPropDamageEligibilityPolicy _policy;
        private readonly Dictionary<StableId, double> _resistances;
        private readonly Dictionary<StableId, Replay<PropDamageResult>> _damageHistory =
            new Dictionary<StableId, Replay<PropDamageResult>>();
        private readonly Dictionary<StableId, Replay<PropInteractionResult>> _interactionHistory =
            new Dictionary<StableId, Replay<PropInteractionResult>>();
        private readonly bool _solid;
        private readonly bool _combat;
        private readonly PropDestructibilityMode _destructibility;
        private readonly double? _maximumHealth;
        private readonly PropDamageAlignment _alignment;
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

        internal PropLive(
            PropPlacement placement,
            PropDefinition definition,
            IPropDamageEligibilityPolicy policy)
        {
            Placement = placement;
            _definition = definition;
            _policy = policy;
            ParticipantId = placement.Identity.Value;
            _solid = PropCatalog.ReadBoolean(
                definition,
                PropCapabilityIds.Collision,
                "solid");
            _combat = PropCatalog.Has(
                definition,
                PropCapabilityIds.Destructibility);
            _destructibility = (PropDestructibilityMode)PropCatalog.ReadInteger(
                definition,
                PropCapabilityIds.Destructibility,
                "mode");
            if (_combat
                && _destructibility == PropDestructibilityMode.HealthBased)
            {
                _maximumHealth = ReadDouble(
                    definition,
                    PropCapabilityIds.Destructibility,
                    "maximum-health");
                _currentHealth = _maximumHealth;
            }

            _alignment = (PropDamageAlignment)PropCatalog.ReadInteger(
                definition,
                PropCapabilityIds.DamageBehavior,
                "alignment");
            _policyId = ReadId(
                definition,
                PropCapabilityIds.DamageBehavior,
                "policy-id");
            _explosionProfileId = ReadId(
                definition,
                PropCapabilityIds.ExplodeOnDestroy,
                "profile-id");
            _dropProfileId = ReadId(
                definition,
                PropCapabilityIds.DropOnDestroy,
                "profile-id");
            _interactionFactId = ReadId(
                definition,
                PropCapabilityIds.Interactable,
                "fact-id");
            _switchId = ReadId(
                definition,
                PropCapabilityIds.Switch,
                "switch-id");
            _objectiveFactId = ReadId(
                definition,
                PropCapabilityIds.Objective,
                "fact-id");
            _blocksRoomClear = PropCatalog.ReadBoolean(
                definition,
                PropCapabilityIds.RoomClear,
                "blocks");
            if (_switchId != null)
            {
                _switchActive = PropCatalog.ReadBoolean(
                    definition,
                    PropCapabilityIds.Switch,
                    "initially-active");
            }

            _resistances = ReadResistances(definition);
        }

        public PropPlacement Placement { get; }

        public StableId ParticipantId { get; }

        public PropLiveSnapshot Snapshot
        {
            get { return BuildSnapshot(); }
        }

        public PropDamageResult ApplyDamage(PropDamageCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Replay<PropDamageResult> replay;
            if (_damageHistory.TryGetValue(command.OperationId, out replay))
            {
                if (!string.Equals(
                    replay.Fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return DamageResult(
                        PropDamageStatus.RejectedConflictingReplay,
                        CurrentHealth(),
                        CurrentHealth(),
                        0d,
                        PropFactBatch.Empty,
                        "Damage operation ID was reused with conflicting input.");
                }

                return replay.Result;
            }

            PropDamageResult result = ApplyFirstDamage(command);
            _damageHistory.Add(
                command.OperationId,
                new Replay<PropDamageResult>(command.Fingerprint, result));
            return result;
        }

        public PropInteractionResult Interact(PropInteractionCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            Replay<PropInteractionResult> replay;
            if (_interactionHistory.TryGetValue(command.OperationId, out replay))
            {
                if (!string.Equals(
                    replay.Fingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return InteractionResult(
                        PropInteractionStatus.RejectedConflictingReplay,
                        null,
                        null,
                        null,
                        "Interaction operation ID was reused with conflicting input.");
                }

                return replay.Result;
            }

            PropInteractionResult result = ApplyFirstInteraction(command);
            _interactionHistory.Add(
                command.OperationId,
                new Replay<PropInteractionResult>(command.Fingerprint, result));
            return result;
        }

        private PropDamageResult ApplyFirstDamage(PropDamageCommand command)
        {
            double previous = CurrentHealth();
            if (!_combat)
            {
                return DamageResult(
                    PropDamageStatus.RejectedNoCombatAuthority,
                    previous,
                    previous,
                    0d,
                    PropFactBatch.Empty,
                    "This prop has no combat authority.");
            }

            if (_terminal)
            {
                return DamageResult(
                    PropDamageStatus.RejectedTerminal,
                    previous,
                    previous,
                    0d,
                    PropFactBatch.Empty,
                    "Terminal props cannot accept damage.");
            }

            PropDamageEligibilityContext context =
                new PropDamageEligibilityContext(
                    command,
                    ParticipantId,
                    _alignment,
                    _policyId);
            if (_policy == null || !_policy.CanDamage(context))
            {
                return DamageResult(
                    PropDamageStatus.RejectedByPolicy,
                    previous,
                    previous,
                    0d,
                    PropFactBatch.Empty,
                    "Injected damage policy rejected the hit.");
            }

            if (_destructibility == PropDestructibilityMode.Indestructible)
            {
                return DamageResult(
                    PropDamageStatus.RejectedIndestructible,
                    previous,
                    previous,
                    0d,
                    PropFactBatch.Empty,
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
                    PropDamageStatus.Applied,
                    previous,
                    _currentHealth.Value,
                    applied,
                    PropFactBatch.Empty,
                    "Prop damage accepted.");
            }

            _terminal = true;
            PropTerminalFact terminal = new PropTerminalFact(
                PropFactIdentity.Derive(
                    command.OperationId,
                    ParticipantId,
                    PropFactKindIds.Terminal,
                    _definition.DefinitionId),
                PropFactKindIds.Terminal,
                command,
                ParticipantId,
                _definition.DefinitionId);
            PropTriggeredFact explosion = Triggered(
                command.OperationId,
                PropFactKindIds.ExplosionRequest,
                _explosionProfileId,
                command.SourceParticipantId);
            PropTriggeredFact drop = Triggered(
                command.OperationId,
                PropFactKindIds.DropRequest,
                _dropProfileId,
                command.SourceParticipantId);
            PropTriggeredFact objective = Triggered(
                command.OperationId,
                PropFactKindIds.ObjectiveOnDestroy,
                _objectiveFactId,
                command.SourceParticipantId);
            return DamageResult(
                PropDamageStatus.Destroyed,
                previous,
                0d,
                applied,
                new PropFactBatch(terminal, explosion, drop, objective),
                "Prop entered terminal destroyed state.");
        }

        private PropInteractionResult ApplyFirstInteraction(
            PropInteractionCommand command)
        {
            if (_terminal)
            {
                return InteractionResult(
                    PropInteractionStatus.RejectedTerminal,
                    null,
                    null,
                    null,
                    "Terminal props cannot be interacted with.");
            }

            if (_interactionFactId == null)
            {
                return InteractionResult(
                    PropInteractionStatus.RejectedNotInteractable,
                    null,
                    null,
                    null,
                    "The prop has no interactable capability.");
            }

            PropTriggeredFact interaction = Triggered(
                command.OperationId,
                PropFactKindIds.Interaction,
                _interactionFactId,
                command.SourceParticipantId);
            PropTriggeredFact switchFact = null;
            if (_switchId != null)
            {
                _switchActive = !_switchActive.Value;
                switchFact = Triggered(
                    command.OperationId,
                    _switchActive.Value
                        ? PropFactKindIds.SwitchOn
                        : PropFactKindIds.SwitchOff,
                    _switchId,
                    command.SourceParticipantId);
            }

            PropTriggeredFact objective = Triggered(
                command.OperationId,
                PropFactKindIds.ObjectiveOnInteraction,
                _objectiveFactId,
                command.SourceParticipantId);
            return InteractionResult(
                PropInteractionStatus.Applied,
                interaction,
                switchFact,
                objective,
                "Prop interaction accepted.");
        }

        private PropTriggeredFact Triggered(
            StableId rootOperationId,
            StableId kindId,
            StableId valueId,
            StableId sourceParticipantId)
        {
            return valueId == null
                ? null
                : new PropTriggeredFact(
                    PropFactIdentity.Derive(
                        rootOperationId,
                        ParticipantId,
                        kindId,
                        valueId),
                    kindId,
                    valueId,
                    ParticipantId,
                    sourceParticipantId);
        }

        private PropDamageResult DamageResult(
            PropDamageStatus status,
            double previous,
            double current,
            double applied,
            PropFactBatch facts,
            string diagnostic)
        {
            return new PropDamageResult(
                status,
                previous,
                current,
                applied,
                facts,
                BuildSnapshot(),
                diagnostic);
        }

        private PropInteractionResult InteractionResult(
            PropInteractionStatus status,
            PropTriggeredFact interaction,
            PropTriggeredFact switchFact,
            PropTriggeredFact objective,
            string diagnostic)
        {
            return new PropInteractionResult(
                status,
                interaction,
                switchFact,
                objective,
                BuildSnapshot(),
                diagnostic);
        }

        private PropLiveSnapshot BuildSnapshot()
        {
            return new PropLiveSnapshot(
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
            PropDefinition definition,
            StableId capabilityId,
            string key)
        {
            string text = PropCatalog.Read(definition, capabilityId, key);
            StableId value;
            return StableId.TryParse(text, out value) ? value : null;
        }

        private static double ReadDouble(
            PropDefinition definition,
            StableId capabilityId,
            string key)
        {
            double value;
            return double.TryParse(
                PropCatalog.Read(definition, capabilityId, key),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
                    ? value
                    : 0d;
        }

        private static Dictionary<StableId, double> ReadResistances(
            PropDefinition definition)
        {
            Dictionary<StableId, double> result =
                new Dictionary<StableId, double>();
            PropCapability capability;
            if (!definition.TryGet(
                PropCapabilityIds.DamageResistance,
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
