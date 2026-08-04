using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Readable access to the character currently selected by the player.
    ///
    /// This is the preferred game-facing API for UI and scene code. It keeps the
    /// migration-era CharacterLiveGraph, ExperienceAuthority, MoneyWallet,
    /// ScrapWallet, and LoadoutRuntime names behind one temporary boundary while
    /// their owning feature is migrated safely.
    /// </summary>
    public sealed class CurrentCharacter
    {
        private readonly CharacterLiveGraph source;

        private CurrentCharacter(CharacterLiveGraph source)
        {
            this.source = source
                ?? throw new ArgumentNullException(nameof(source));
        }

        public bool IsDisposed
        {
            get { return source.IsDisposed; }
        }

        public int SlotIndex
        {
            get { return source.Character.SlotIndex; }
        }

        public StableId CharacterId
        {
            get { return source.Character.CharacterInstanceStableId; }
        }

        public StableId ClassId
        {
            get { return source.Character.ClassDefinitionStableId; }
        }

        public int Level
        {
            get
            {
                return source.ExperienceAuthority == null
                    || source.ExperienceAuthority.CurrentState == null
                    ? 1
                    : source.ExperienceAuthority.CurrentState.Level;
            }
        }

        public long Money
        {
            get
            {
                return source.MoneyWallet == null
                    ? 0L
                    : source.MoneyWallet.Balance;
            }
        }

        public long Scrap
        {
            get
            {
                return source.ScrapWallet == null
                    ? 0L
                    : source.ScrapWallet.Balance;
            }
        }

        public PlayerRouteProfilePayload Loadout
        {
            get
            {
                return source.LoadoutRuntime == null
                    ? null
                    : source.LoadoutRuntime.CurrentRoutePayload;
            }
        }

        public object Holdings
        {
            get
            {
                return source.LoadoutRuntime == null
                    ? null
                    : source.LoadoutRuntime.Holdings;
            }
        }

        public GunItem FindGun(StableId equipmentInstanceId)
        {
            if (equipmentInstanceId == null
                || source.LoadoutRuntime == null
                || source.LoadoutRuntime.GunInventory == null)
            {
                return null;
            }

            return source.LoadoutRuntime.GunInventory.Find(
                equipmentInstanceId);
        }

        public static bool TryResolve(
            out CurrentCharacter character,
            out FlowProfileRecord profile)
        {
            CharacterLiveGraph graph;
            if (!CharacterSave.TryResolveCurrent(out graph, out profile)
                || graph == null
                || graph.IsDisposed
                || graph.Character == null
                || graph.LoadoutRuntime == null)
            {
                character = null;
                return false;
            }

            character = new CurrentCharacter(graph);
            return true;
        }
    }
}
