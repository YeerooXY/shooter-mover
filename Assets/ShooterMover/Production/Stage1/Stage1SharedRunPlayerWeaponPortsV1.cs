using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Production.Level1;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1ProductionRunStatInputResolverV1 :
        IProductionRunStatInputResolverV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;

        public Stage1ProductionRunStatInputResolverV1(
            Level1PlayerRuntimeSceneAdapterV1 player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public ProductionRunStatInputResolutionV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            ProductionCharacterRuntimeGraphV1 characterGraph,
            CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 currentRoutePayload,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
        {
            PlayerRuntimeSnapshot runtime = player.ExportSnapshot();
            if (runtime == null || runtime.Player == null)
            {
                throw new InvalidOperationException(
                    "stage1-run-player-snapshot-unavailable");
            }
            var baseValues = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                {
                    DerivedStatTargetIdsV1.MaximumHealth,
                    checked((decimal)runtime.Player.MaximumHealth)
                },
                { DerivedStatTargetIdsV1.MovementSpeed, 6m },
            };
            return new ProductionRunStatInputResolutionV1(
                new DerivedCharacterStatInputV1(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.stage1-production",
                        character.ClassDefinitionStableId.ToString(),
                        1,
                        character.Fingerprint,
                        baseValues),
                    null,
                    DerivedStatPolicyV1.CreateDefault()),
                null,
                null);
        }
    }

    internal sealed class Stage1PlayerRunPortV1 : IRunPlayerRuntimePortV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;

        public Stage1PlayerRunPortV1(Level1PlayerRuntimeSceneAdapterV1 player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            ReadGeneration();
        }

        public string PortId { get { return "stage1-player-runtime"; } }
        public long LifecycleGeneration { get { return ReadGeneration(); } }
        public string SnapshotFingerprint { get { return ExportSnapshot().Fingerprint; } }

        public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
        {
            PlayerRuntimeSnapshot snapshot = player.ExportSnapshot();
            return new RunPlayerRuntimeSnapshotV1(
                snapshot.Player.ActorInstanceId,
                snapshot.Player.RunParticipantId,
                snapshot.Player.LifecycleGeneration,
                snapshot.Player.CurrentHealth,
                snapshot.Player.MaximumHealth,
                snapshot.Movement.PositionX,
                snapshot.Movement.PositionY,
                snapshot.Player.AcceptedSequence);
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            long current = LifecycleGeneration;
            if (retiringLifecycleGeneration != current)
            {
                return retiringLifecycleGeneration < current
                    ? "stage1-player-runtime-stale-generation"
                    : "stage1-player-runtime-future-generation";
            }
            return replacementLifecycleGeneration == current + 1L
                ? string.Empty
                : "stage1-player-runtime-replacement-generation-invalid";
        }

        public RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return Result(false, rejection);
            }
            PlayerRuntimeSnapshot before = player.ExportSnapshot();
            PlayerRuntimeRestartResult applied = player.ApplyRestartCommand(
                new PlayerRuntimeRestartCommand(
                    operationStableId,
                    before.Player.ActorInstanceId,
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration));
            bool accepted = applied != null
                && (applied.Status == PlayerRuntimeRestartStatus.Applied
                    || applied.Status == PlayerRuntimeRestartStatus.Duplicate);
            return Result(
                accepted,
                accepted
                    ? string.Empty
                    : applied == null
                        ? "stage1-player-restart-null"
                        : "stage1-player-restart-" + applied.RejectionCode);
        }

        private long ReadGeneration()
        {
            if (!player.IsInitialized)
            {
                throw new InvalidOperationException(
                    "The Stage 1 player runtime is unavailable.");
            }
            return player.ExportSnapshot().Player.LifecycleGeneration;
        }

        private RunRuntimePortRestartResultV1 Result(bool accepted, string rejection)
        {
            return new RunRuntimePortRestartResultV1(
                accepted,
                rejection,
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class Stage1WeaponRunPortV1 : IRunWeaponRuntimePortV1
    {
        private readonly IReadOnlyList<StableId> equipmentIds;
        private readonly Action clearTransientEffects;
        private long lifecycleGeneration;

        public Stage1WeaponRunPortV1(
            long lifecycleGeneration,
            IEnumerable<StableId> frozenEquipmentInstanceStableIds,
            Action clearTransientEffects)
        {
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            this.clearTransientEffects = clearTransientEffects
                ?? throw new ArgumentNullException(nameof(clearTransientEffects));
            List<StableId> ids = (frozenEquipmentInstanceStableIds
                    ?? throw new ArgumentNullException(
                        nameof(frozenEquipmentInstanceStableIds)))
                .Where(item => item != null)
                .Distinct()
                .OrderBy(item => item)
                .ToList();
            if (ids.Count == 0)
            {
                throw new ArgumentException(
                    "At least one frozen weapon identity is required.",
                    nameof(frozenEquipmentInstanceStableIds));
            }
            this.lifecycleGeneration = lifecycleGeneration;
            equipmentIds = ids.AsReadOnly();
        }

        public string PortId { get { return "stage1-inventory-weapon-runtime"; } }
        public long LifecycleGeneration { get { return lifecycleGeneration; } }
        public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
        {
            get { return equipmentIds; }
        }
        public string SnapshotFingerprint
        {
            get
            {
                var builder = new StringBuilder(PortId)
                    .Append('|')
                    .Append(lifecycleGeneration.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < equipmentIds.Count; index++)
                {
                    builder.Append('|').Append(equipmentIds[index]);
                }
                return Stage1ProductionFingerprintV1.Hash(builder.ToString());
            }
        }

        public string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            if (retiringLifecycleGeneration != lifecycleGeneration)
            {
                return retiringLifecycleGeneration < lifecycleGeneration
                    ? "stage1-weapon-runtime-stale-generation"
                    : "stage1-weapon-runtime-future-generation";
            }
            return replacementLifecycleGeneration == lifecycleGeneration + 1L
                ? string.Empty
                : "stage1-weapon-runtime-replacement-generation-invalid";
        }

        public RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick)
        {
            string rejection = ValidateRestart(
                retiringLifecycleGeneration,
                replacementLifecycleGeneration,
                authoritativeTick);
            if (!string.IsNullOrEmpty(rejection))
            {
                return Result(false, rejection);
            }
            clearTransientEffects();
            lifecycleGeneration = replacementLifecycleGeneration;
            return Result(true, string.Empty);
        }

        private RunRuntimePortRestartResultV1 Result(bool accepted, string rejection)
        {
            return new RunRuntimePortRestartResultV1(
                accepted,
                rejection,
                lifecycleGeneration,
                SnapshotFingerprint);
        }
    }
}
