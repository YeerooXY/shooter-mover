using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Production.Stage1;
using UnityEngine;

namespace ShooterMover.Production.Stage1
{
    /// <summary>
    /// Adapts an existing enemy authority to the exact placed-instance identity expected
    /// by ROOM-LIVE without creating a second enemy-health authority.
    /// </summary>
    [DisallowMultipleComponent]
    internal class Stage1RoomEnemyAuthorityAdapter2D :
        MonoBehaviour,
        IEnemyActor2DAuthority
    {
        public long Generation
        {
            get
            {
                RoomPlacedInstance2D marker =
                    GetComponent<RoomPlacedInstance2D>();
                return marker == null ? 0L : marker.RuntimeLifecycleGeneration;
            }
        }

        public bool TryReadState(out EnemyActorState state)
        {
            state = null;
            RoomPlacedInstance2D marker = GetComponent<RoomPlacedInstance2D>();
            IEnemyActor2DAuthority source;
            EnemyActorState sourceState;
            if (marker == null
                || !marker.IsConfigured
                || !Stage1PlayableLoopCompositionV1.TryResolveProjectedEnemy(
                    marker.InstanceStableId,
                    out source)
                || !source.TryReadState(out sourceState)
                || sourceState == null)
            {
                return false;
            }

            EnemyActorState projected = EnemyActorState.Create(
                marker.InstanceStableId,
                sourceState.RoleId,
                sourceState.MaximumHealth,
                sourceState.WeightClassValue,
                sourceState.ContactPolicy);
            double missing = sourceState.MaximumHealth - sourceState.Health;
            if (missing > 0d)
            {
                projected = EnemyActorStepper.Step(
                    projected,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            0L,
                            StableId.Create(
                                "combat-event",
                                "room-projection-" + HashProjectionToken(
                                    marker.InstanceStableId.ToString())),
                            sourceState.ActorId,
                            (int)CombatChannel.Kinetic,
                            missing),
                    }).State;
            }

            state = projected;
            return true;
        }

        private static string HashProjectionToken(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(24);
                for (int index = 0; index < 12; index++)
                {
                    builder.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            RoomPlacedInstance2D marker = GetComponent<RoomPlacedInstance2D>();
            IEnemyActor2DAuthority source;
            if (marker == null
                || !Stage1PlayableLoopCompositionV1.TryResolveProjectedEnemy(
                    marker.InstanceStableId,
                    out source))
            {
                return null;
            }
            return source.Apply(command);
        }

        public bool Reset()
        {
            RoomPlacedInstance2D marker = GetComponent<RoomPlacedInstance2D>();
            IEnemyActor2DAuthority source;
            return marker != null
                && Stage1PlayableLoopCompositionV1.TryResolveProjectedEnemy(
                    marker.InstanceStableId,
                    out source)
                && source.Reset();
        }
    }
}
