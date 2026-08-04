using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.Game
{
    public enum GunMountAvailability
    {
        Active = 1,
        LockedBySkill = 2,
    }

    /// <summary>
    /// One physical class-owned gun mount. A physical mount may be active and empty, or visible
    /// but skill-locked. Nonexistent mounts are absent from the layout entirely.
    /// </summary>
    public sealed class GunSlot
    {
        public GunSlot(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
            double lateralOffset)
            : this(
                mountStableId,
                loadoutSlotStableId,
                displayName,
                GunMountAvailability.Active,
                lateralOffset)
        {
        }

        public GunSlot(
            StableId mountStableId,
            StableId loadoutSlotStableId,
            string displayName,
            GunMountAvailability availability,
            double lateralOffset)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            LoadoutSlotStableId = loadoutSlotStableId
                ?? throw new ArgumentNullException(nameof(loadoutSlotStableId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A mount display name is required.",
                    nameof(displayName));
            }
            if (!Enum.IsDefined(
                    typeof(GunMountAvailability),
                    availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }
            if (double.IsNaN(lateralOffset)
                || double.IsInfinity(lateralOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(lateralOffset));
            }

            DisplayName = displayName.Trim();
            Availability = availability;
            LateralOffset = lateralOffset;
            LockReason = availability
                    == GunMountAvailability.LockedBySkill
                ? "A skill is required to activate this mount."
                : string.Empty;
        }

        public StableId MountStableId { get; }
        public StableId LoadoutSlotStableId { get; }
        public string DisplayName { get; }
        public GunMountAvailability Availability { get; }
        public double LateralOffset { get; }
        public string LockReason { get; }

        public bool IsActive
        {
            get
            {
                return Availability
                    == GunMountAvailability.Active;
            }
        }

        public bool IsLockedBySkill
        {
            get
            {
                return Availability
                    == GunMountAvailability.LockedBySkill;
            }
        }
    }

    public sealed class GunMountViewBinding
    {
        public GunMountViewBinding(
            StableId mountStableId,
            StableId equipmentInstanceStableId)
        {
            MountStableId = mountStableId
                ?? throw new ArgumentNullException(nameof(mountStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public StableId MountStableId { get; }
        public StableId EquipmentInstanceStableId { get; }
        public bool IsBound { get { return EquipmentInstanceStableId != null; } }
    }

    public sealed class GunSlots
    {
        private readonly ReadOnlyCollection<GunSlot>
            physicalPositions;
        private readonly ReadOnlyCollection<GunSlot>
            activePositions;
        private readonly ReadOnlyCollection<GunSlot>
            lockedBySkillPositions;

        internal GunSlots(
            StableId loadoutProfileStableId,
            IEnumerable<GunSlot> positions)
        {
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            var physical = new List<GunSlot>(
                positions ?? throw new ArgumentNullException(nameof(positions)));
            if (physical.Count < 2 || physical.Count > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(positions));
            }

            var mountIds = new HashSet<StableId>();
            var slotIds = new HashSet<StableId>();
            var active = new List<GunSlot>();
            var locked = new List<GunSlot>();
            for (int index = 0; index < physical.Count; index++)
            {
                GunSlot position = physical[index];
                if (position == null
                    || !mountIds.Add(position.MountStableId)
                    || !slotIds.Add(position.LoadoutSlotStableId))
                {
                    throw new ArgumentException(
                        "Physical mount and loadout bridge identities must be unique.",
                        nameof(positions));
                }
                if (position.IsActive)
                {
                    active.Add(position);
                }
                else
                {
                    locked.Add(position);
                }
            }
            if (active.Count == 0)
            {
                throw new ArgumentException(
                    "A production class requires at least one active gun mount.",
                    nameof(positions));
            }

            physicalPositions =
                new ReadOnlyCollection<GunSlot>(physical);
            activePositions =
                new ReadOnlyCollection<GunSlot>(active);
            lockedBySkillPositions =
                new ReadOnlyCollection<GunSlot>(locked);
        }

        public StableId LoadoutProfileStableId { get; }
        public IReadOnlyList<GunSlot> PhysicalPositions
        {
            get { return physicalPositions; }
        }
        public IReadOnlyList<GunSlot> Positions
        {
            get { return physicalPositions; }
        }
        public IReadOnlyList<GunSlot> ConfigurablePositions
        {
            get { return activePositions; }
        }
        public IReadOnlyList<GunSlot> LockedBySkillPositions
        {
            get { return lockedBySkillPositions; }
        }
        public int PhysicalMountCount { get { return physicalPositions.Count; } }
        public int ActiveMountCount { get { return activePositions.Count; } }
        public int LockedBySkillMountCount
        {
            get { return lockedBySkillPositions.Count; }
        }
        public int BaselineEnabledMountCount { get { return ActiveMountCount; } }

        public bool ContainsLoadoutSlot(StableId slotStableId)
        {
            return Contains(activePositions, slotStableId);
        }

        public bool ContainsPhysicalLoadoutSlot(StableId slotStableId)
        {
            return Contains(physicalPositions, slotStableId);
        }

        private static bool Contains(
            IReadOnlyList<GunSlot> positions,
            StableId slotStableId)
        {
            if (slotStableId == null)
            {
                return false;
            }
            for (int index = 0; index < positions.Count; index++)
            {
                if (positions[index].LoadoutSlotStableId == slotStableId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// One projection per physical mount. Active mounts may be empty. EnabledBindings contains
    /// only active mounts that currently hold an exact instance ID.
    /// </summary>
    public sealed class GunMountSet
    {
        private readonly ReadOnlyCollection<GunMountViewBinding>
            physicalBindings;
        private readonly ReadOnlyCollection<GunMountViewBinding>
            configuredBindings;
        private readonly ReadOnlyCollection<GunMountViewBinding>
            enabledBindings;

        internal GunMountSet(
            GunSlots layout,
            IEnumerable<GunMountViewBinding> bindings)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            var physical = new List<GunMountViewBinding>(
                bindings ?? throw new ArgumentNullException(nameof(bindings)));
            if (physical.Count != layout.PhysicalPositions.Count)
            {
                throw new ArgumentException(
                    "Every physical mount requires one projection binding.",
                    nameof(bindings));
            }

            var active = new List<GunMountViewBinding>();
            var enabled = new List<GunMountViewBinding>();
            for (int index = 0; index < physical.Count; index++)
            {
                GunSlot position =
                    layout.PhysicalPositions[index];
                GunMountViewBinding binding = physical[index];
                if (binding == null
                    || binding.MountStableId != position.MountStableId)
                {
                    throw new ArgumentException(
                        "Mount bindings must follow the physical layout order.",
                        nameof(bindings));
                }
                if (position.IsLockedBySkill && binding.IsBound)
                {
                    throw new ArgumentException(
                        "A skill-locked gun mount must remain unbound.",
                        nameof(bindings));
                }
                if (position.IsActive)
                {
                    active.Add(binding);
                    if (binding.IsBound)
                    {
                        enabled.Add(binding);
                    }
                }
            }

            physicalBindings =
                new ReadOnlyCollection<GunMountViewBinding>(physical);
            configuredBindings =
                new ReadOnlyCollection<GunMountViewBinding>(active);
            enabledBindings =
                new ReadOnlyCollection<GunMountViewBinding>(enabled);
        }

        public GunSlots Layout { get; }
        public IReadOnlyList<GunMountViewBinding> PhysicalBindings
        {
            get { return physicalBindings; }
        }
        public IReadOnlyList<GunMountViewBinding> ConfiguredBindings
        {
            get { return configuredBindings; }
        }
        public IReadOnlyList<GunMountViewBinding> EnabledBindings
        {
            get { return enabledBindings; }
        }
    }

    public static class GunLoadoutSlotIds
    {
        public static readonly StableId GunOne =
            StableId.Parse("gun-slot.slot-1");
        public static readonly StableId GunTwo =
            StableId.Parse("gun-slot.slot-2");
        public static readonly StableId GunThree =
            StableId.Parse("gun-slot.slot-3");
        public static readonly StableId GunFour =
            StableId.Parse("gun-slot.slot-4");

        public static int IndexOf(StableId slotStableId)
        {
            if (slotStableId == GunOne) return 0;
            if (slotStableId == GunTwo) return 1;
            if (slotStableId == GunThree) return 2;
            if (slotStableId == GunFour) return 3;
            return -1;
        }
    }

    public static class GunMountPolicy
    {
        public const string AggressiveLoadoutProfileId =
            "loadout-profile.striker";
        public const string HealerLoadoutProfileId =
            "loadout-profile.combat-medic";
        public const string DefensiveLoadoutProfileId =
            "loadout-profile.juggernaut";

        private const string AggressiveProfileSuffix = "-aggressive";
        private const string HealerProfileSuffix = "-healer";
        private const string DefensiveProfileSuffix = "-defensive";
        private const string LoadoutProfilePrefix = "loadout-profile.";

        public static readonly StableId OuterLeftMountStableId =
            StableId.Parse("gun-mount.outer-left");
        public static readonly StableId InnerLeftMountStableId =
            StableId.Parse("gun-mount.inner-left");
        public static readonly StableId CenterMountStableId =
            StableId.Parse("gun-mount.center");
        public static readonly StableId InnerRightMountStableId =
            StableId.Parse("gun-mount.inner-right");
        public static readonly StableId OuterRightMountStableId =
            StableId.Parse("gun-mount.outer-right");

        private static readonly GunSlot OuterLeft =
            new GunSlot(
                OuterLeftMountStableId,
                GunLoadoutSlotIds.GunOne,
                "Outer Left",
                -0.28d);
        private static readonly GunSlot InnerLeft =
            new GunSlot(
                InnerLeftMountStableId,
                GunLoadoutSlotIds.GunTwo,
                "Inner Left",
                -0.09d);
        private static readonly GunSlot Center =
            new GunSlot(
                CenterMountStableId,
                GunLoadoutSlotIds.GunTwo,
                "Center",
                0d);
        private static readonly GunSlot
            AggressiveLockedCenter =
                new GunSlot(
                    CenterMountStableId,
                    GunLoadoutSlotIds.GunTwo,
                    "Center",
                    GunMountAvailability.LockedBySkill,
                    0d);
        private static readonly GunSlot InnerRight =
            new GunSlot(
                InnerRightMountStableId,
                GunLoadoutSlotIds.GunThree,
                "Inner Right",
                0.09d);
        private static readonly GunSlot OuterRight =
            new GunSlot(
                OuterRightMountStableId,
                GunLoadoutSlotIds.GunFour,
                "Outer Right",
                0.28d);

        private static readonly GunSlots Aggressive =
            new GunSlots(
                StableId.Parse(AggressiveLoadoutProfileId),
                new[] { OuterLeft, AggressiveLockedCenter, OuterRight });
        private static readonly GunSlots Healer =
            new GunSlots(
                StableId.Parse(HealerLoadoutProfileId),
                new[] { OuterLeft, Center, OuterRight });
        private static readonly GunSlots Defensive =
            new GunSlots(
                StableId.Parse(DefensiveLoadoutProfileId),
                new[] { OuterLeft, InnerLeft, InnerRight, OuterRight });

        public static GunSlots ResolveLayout(
            StableId loadoutProfileStableId)
        {
            string value = loadoutProfileStableId == null
                ? string.Empty
                : loadoutProfileStableId.ToString();
            if (string.Equals(
                    value,
                    AggressiveLoadoutProfileId,
                    StringComparison.Ordinal)
                || IsCharacterClassProfile(value, AggressiveProfileSuffix))
            {
                return Aggressive;
            }
            if (string.Equals(
                    value,
                    HealerLoadoutProfileId,
                    StringComparison.Ordinal)
                || IsCharacterClassProfile(value, HealerProfileSuffix))
            {
                return Healer;
            }
            if (string.Equals(
                    value,
                    DefensiveLoadoutProfileId,
                    StringComparison.Ordinal)
                || IsCharacterClassProfile(value, DefensiveProfileSuffix))
            {
                return Defensive;
            }
            throw new ArgumentException(
                "Unsupported production gun mount profile: "
                    + (string.IsNullOrEmpty(value) ? "<null>" : value),
                nameof(loadoutProfileStableId));
        }

        public static bool IsConfigurableLoadoutSlot(
            StableId loadoutProfileStableId,
            StableId slotStableId)
        {
            return ResolveLayout(loadoutProfileStableId)
                .ContainsLoadoutSlot(slotStableId);
        }

        public static PlayerRouteProfilePayload NormalizeRoutePayload(
            PlayerRouteProfilePayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            GunSlots layout = ResolveLayout(
                payload.LoadoutProfileStableId);
            var instances = new List<StableId>(
                PlayerRouteProfilePayload.GunSlotCount);
            for (int index = 0;
                 index < PlayerRouteProfilePayload.GunSlotCount;
                 index++)
            {
                PlayerRouteGunSlot slot = payload.GunSlots[index];
                instances.Add(
                    layout.ContainsLoadoutSlot(slot.GunSlotStableId)
                        ? slot.EquipmentInstanceStableId
                        : null);
            }
            return PlayerRouteProfilePayload.Create(
                payload.SelectedCharacterStableId,
                payload.LoadoutProfileStableId,
                instances);
        }

        public static GunMountSet BuildMountSet(
            PlayerRouteProfilePayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            GunSlots layout = ResolveLayout(
                payload.LoadoutProfileStableId);
            var bindings = new List<GunMountViewBinding>(
                layout.PhysicalPositions.Count);
            for (int positionIndex = 0;
                 positionIndex < layout.PhysicalPositions.Count;
                 positionIndex++)
            {
                GunSlot position =
                    layout.PhysicalPositions[positionIndex];
                StableId instanceId = null;
                for (int slotIndex = 0;
                     slotIndex < payload.GunSlots.Count;
                     slotIndex++)
                {
                    PlayerRouteGunSlot slot = payload.GunSlots[slotIndex];
                    if (slot.GunSlotStableId
                        == position.LoadoutSlotStableId)
                    {
                        instanceId = slot.EquipmentInstanceStableId;
                        break;
                    }
                }
                if (position.IsLockedBySkill && instanceId != null)
                {
                    throw new InvalidOperationException(
                        "A skill-locked gun mount is bound: "
                        + position.MountStableId);
                }
                bindings.Add(new GunMountViewBinding(
                    position.MountStableId,
                    instanceId));
            }
            return new GunMountSet(layout, bindings);
        }

        public static GunSlot FindPosition(
            GunSlots layout,
            StableId mountStableId)
        {
            if (layout == null || mountStableId == null)
            {
                return null;
            }
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                GunSlot position =
                    layout.PhysicalPositions[index];
                if (position.MountStableId == mountStableId)
                {
                    return position;
                }
            }
            return null;
        }

        private static bool IsCharacterClassProfile(
            string value,
            string classSuffix)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(
                    LoadoutProfilePrefix,
                    StringComparison.Ordinal)
                && value.EndsWith(classSuffix, StringComparison.Ordinal);
        }
    }
}