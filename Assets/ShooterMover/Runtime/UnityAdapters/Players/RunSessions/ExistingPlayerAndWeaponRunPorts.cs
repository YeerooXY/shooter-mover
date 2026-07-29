using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.UnityAdapters.Players.RunSessions
{
    /// <summary>
    /// Narrow run-session adapter over the already-constructed PlayerLiveSetup.
    /// It does not create or own another PlayerActorState.
    /// </summary>
    public sealed class ExistingPlayerLiveRunPort :
        IRunPlayerLivePort
    {
        private readonly PlayerLiveSetup runtime;

        public ExistingPlayerLiveRunPort(
            PlayerLiveSetup runtime)
        {
            this.runtime = runtime
                ?? throw new ArgumentNullException(nameof(runtime));
        }

        public string PortId
        {
            get { return "player-runtime-composition"; }
        }

        public long LifecycleGeneration
        {
            get { return runtime.ExportSnapshot().Player.LifecycleGeneration; }
        }

        public string SnapshotFingerprint
        {
            get { return ExportSnapshot().Fingerprint; }
        }

        public RunPlayerLiveSnapshot ExportSnapshot()
        {
            PlayerLiveSnapshot snapshot = runtime.ExportSnapshot();
            return new RunPlayerLiveSnapshot(
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
            if (runtime.IsDisposed)
            {
                return "player-runtime-disposed";
            }
            PlayerLiveSnapshot snapshot = runtime.ExportSnapshot();
            if (snapshot.Player.LifecycleGeneration
                != snapshot.Movement.Generation)
            {
                return "player-runtime-generation-split";
            }
            if (retiringLifecycleGeneration
                != snapshot.Player.LifecycleGeneration)
            {
                return retiringLifecycleGeneration
                    < snapshot.Player.LifecycleGeneration
                    ? "player-runtime-stale-generation"
                    : "player-runtime-future-generation";
            }
            return replacementLifecycleGeneration
                == retiringLifecycleGeneration + 1L
                ? string.Empty
                : "player-runtime-generation-invalid";
        }

        public RunLivePortRestartResult Restart(
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
                return new RunLivePortRestartResult(
                    false,
                    rejection,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
            PlayerLiveSnapshot before = runtime.ExportSnapshot();
            PlayerLiveRestartResult result = runtime.Restart(
                new PlayerLiveRestartCommand(
                    operationStableId,
                    before.Player.ActorInstanceId,
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration));
            bool succeeded = result != null
                && (result.Status == PlayerLiveRestartStatus.Applied
                    || result.Status == PlayerLiveRestartStatus.Duplicate);
            return new RunLivePortRestartResult(
                succeeded,
                result == null
                    ? "player-runtime-null-result"
                    : (succeeded
                        ? string.Empty
                        : result.RejectionCode.ToString()),
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    /// <summary>
    /// Run lifecycle projection over existing inventory-backed weapon execution. Cooldown
    /// and fire replay remain in WeaponExecutionCore and are generation-keyed. This port
    /// only freezes exact equipment identities and clears authored transient effects.
    /// </summary>
    public sealed class ExistingWeaponExecutionRunPort :
        IRunWeaponLivePort
    {
        private readonly ReadOnlyCollection<StableId> equipmentIds;
        private readonly Func<string> executionSnapshotFingerprintExporter;
        private readonly Action clearProjectiles;
        private readonly Action clearAttackIntents;
        private readonly Action clearContactOperations;
        private long lifecycleGeneration;

        public ExistingWeaponExecutionRunPort(
            long lifecycleGeneration,
            IEnumerable<StableId> frozenEquipmentInstanceStableIds,
            Func<string> executionSnapshotFingerprintExporter,
            Action clearProjectiles,
            Action clearAttackIntents,
            Action clearContactOperations)
        {
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            List<StableId> ids =
                (frozenEquipmentInstanceStableIds
                    ?? throw new ArgumentNullException(
                        nameof(frozenEquipmentInstanceStableIds)))
                .ToList();
            if (ids.Count < 1
                || ids.Any(id => id == null)
                || ids.Distinct().Count() != ids.Count)
            {
                throw new ArgumentException(
                    "Frozen weapon equipment identities must be non-null and unique.",
                    nameof(frozenEquipmentInstanceStableIds));
            }
            ids.Sort();
            this.lifecycleGeneration = lifecycleGeneration;
            equipmentIds = new ReadOnlyCollection<StableId>(ids);
            this.executionSnapshotFingerprintExporter =
                executionSnapshotFingerprintExporter
                ?? throw new ArgumentNullException(
                    nameof(executionSnapshotFingerprintExporter));
            this.clearProjectiles = clearProjectiles
                ?? throw new ArgumentNullException(nameof(clearProjectiles));
            this.clearAttackIntents = clearAttackIntents
                ?? throw new ArgumentNullException(nameof(clearAttackIntents));
            this.clearContactOperations = clearContactOperations
                ?? throw new ArgumentNullException(nameof(clearContactOperations));
        }

        public string PortId
        {
            get { return "inventory-weapon-execution"; }
        }

        public long LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }

        public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
        {
            get { return equipmentIds; }
        }

        public string SnapshotFingerprint
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append(PortId)
                    .Append('|')
                    .Append(lifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(executionSnapshotFingerprintExporter()
                        ?? string.Empty);
                for (int index = 0; index < equipmentIds.Count; index++)
                {
                    builder.Append('|').Append(equipmentIds[index]);
                }
                using (SHA256 sha = SHA256.Create())
                {
                    return BitConverter.ToString(
                            sha.ComputeHash(
                                Encoding.UTF8.GetBytes(builder.ToString())))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
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
                    ? "weapon-runtime-stale-generation"
                    : "weapon-runtime-future-generation";
            }
            return replacementLifecycleGeneration
                == retiringLifecycleGeneration + 1L
                ? string.Empty
                : "weapon-runtime-generation-invalid";
        }

        public RunLivePortRestartResult Restart(
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
                return new RunLivePortRestartResult(
                    false,
                    rejection,
                    lifecycleGeneration,
                    SnapshotFingerprint);
            }
            clearProjectiles();
            clearAttackIntents();
            clearContactOperations();
            lifecycleGeneration = replacementLifecycleGeneration;
            return new RunLivePortRestartResult(
                true,
                string.Empty,
                lifecycleGeneration,
                SnapshotFingerprint);
        }
    }
}
