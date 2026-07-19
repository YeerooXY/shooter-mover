from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def write(path, content):
    (ROOT / path).write_text(content, encoding="utf-8", newline="\n")


def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


model_path = "Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardModel.cs"
model = read(model_path)
old_damage = '''    public sealed class VoidHazardDamageRequest
    {
        public VoidHazardDamageRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            double amount)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));

            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Amount = amount;
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public double Amount { get; }

        public CombatChannel Channel
        {
            get { return CombatChannel.Environmental; }
        }
    }
'''
new_damage = '''    public sealed class VoidHazardDamageRequest
    {
        public VoidHazardDamageRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            double amount)
            : this(eventId, hazardId, targetId, amount, 0L)
        {
        }

        public VoidHazardDamageRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            double amount,
            long attemptGeneration)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));

            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }
            if (attemptGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptGeneration));
            }

            Amount = amount;
            AttemptGeneration = attemptGeneration;
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public double Amount { get; }

        public long AttemptGeneration { get; }

        public CombatChannel Channel
        {
            get { return CombatChannel.Environmental; }
        }
    }
'''
model = replace_once(model, old_damage, new_damage, "void damage generation")
old_death = '''    public sealed class VoidHazardInstantDeathRequest
    {
        public VoidHazardInstantDeathRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel
        {
            get { return CombatChannel.Environmental; }
        }
    }
'''
new_death = '''    public sealed class VoidHazardInstantDeathRequest
    {
        public VoidHazardInstantDeathRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId)
            : this(eventId, hazardId, targetId, 0L)
        {
        }

        public VoidHazardInstantDeathRequest(
            StableId eventId,
            StableId hazardId,
            StableId targetId,
            long attemptGeneration)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            if (attemptGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptGeneration));
            }
            AttemptGeneration = attemptGeneration;
        }

        public StableId EventId { get; }

        public StableId HazardId { get; }

        public StableId TargetId { get; }

        public long AttemptGeneration { get; }

        public CombatChannel Channel
        {
            get { return CombatChannel.Environmental; }
        }
    }
'''
model = replace_once(model, old_death, new_death, "void instant death generation")
write(model_path, model)

routing_path = "Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardAuthoring2D.Routing.cs"
routing = read(routing_path)
routing = replace_once(
    routing,
    '''                        damagePort.RequestDamage(new VoidHazardDamageRequest(
                            eventId,
                            _restartParticipantId,
                            targetId,
                            _resolvedPolicy.PlayerDamageAmount)));''',
    '''                        damagePort.RequestDamage(new VoidHazardDamageRequest(
                            eventId,
                            _restartParticipantId,
                            targetId,
                            _resolvedPolicy.PlayerDamageAmount,
                            placedObject.BoundScope.AttemptGeneration)));''',
    "route void damage generation")
routing = replace_once(
    routing,
    '''                        deathPort.RequestInstantDeath(new VoidHazardInstantDeathRequest(
                            eventId,
                            _restartParticipantId,
                            targetId)));''',
    '''                        deathPort.RequestInstantDeath(new VoidHazardInstantDeathRequest(
                            eventId,
                            _restartParticipantId,
                            targetId,
                            placedObject.BoundScope.AttemptGeneration)));''',
    "route void death generation")
write(routing_path, routing)

adapter_path = "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs"
adapter = read(adapter_path)
adapter = replace_once(
    adapter,
    '''            long generation;
            if (!TryResolveLifecycleGeneration(request.EventId, out generation))
            {
                return VoidHazardPortResult.Rejected;
            }

            DamageReceiverResult result = runtime.ApplyDamage(''',
    '''            DamageReceiverResult result = runtime.ApplyDamage(''',
    "adapter damage removes hashed-id parsing")
adapter = replace_once(
    adapter,
    '''                    request.Amount,
                    request.Channel,
                    generation));''',
    '''                    request.Amount,
                    request.Channel,
                    request.AttemptGeneration));''',
    "adapter damage uses attempt generation")
adapter = replace_once(
    adapter,
    '''            long generation;
            if (!TryResolveLifecycleGeneration(request.EventId, out generation))
            {
                return VoidHazardPortResult.Rejected;
            }

            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;''',
    '''            PlayerActorSnapshot player = runtime.ExportSnapshot().Player;''',
    "adapter death removes hashed-id parsing")
adapter = replace_once(
    adapter,
    '''                    player.MaximumHealth,
                    request.Channel,
                    generation));''',
    '''                    player.MaximumHealth,
                    request.Channel,
                    request.AttemptGeneration));''',
    "adapter death uses attempt generation")
write(adapter_path, adapter)

play_path = "Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/Stage1PlayerLiveAuthorityPlayModeTests.cs"
play = read(play_path)
play = replace_once(
    play,
    '''                    after.Player.ActorInstanceId,
                    35d));''',
    '''                    after.Player.ActorInstanceId,
                    35d,
                    before.Player.LifecycleGeneration));''',
    "stale void test explicit generation")
write(play_path, play)

doc_path = "docs/architecture/gameplay/PLAYER_LIVE_AUTHORITY_V1.md"
doc = read(doc_path)
doc += '''

Void hazard event IDs remain deterministic hashes. The damage and instant-death
request contracts now carry the originating gameplay attempt generation explicitly;
the compatibility constructors remain generation zero for existing fixtures. Live
routing always supplies the bound scope generation, so delayed pre-restart requests
are rejected by `PlayerActorAuthority` without trying to reverse a hash.
'''
write(doc_path, doc)

print("void generation contract repaired")
