using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Modifiers.StatusEffects
{
    public enum StatusEffectCommandStatusV1
    {
        Accepted = 1,
        AcceptedNoChange = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
        StaleSimulationTick = 5,
        LifecycleMismatch = 6,
    }

    public enum StatusEffectCommandActionV1
    {
        Applied = 1,
        Stacked = 2,
        Refreshed = 3,
        Replaced = 4,
        Ignored = 5,
        Advanced = 6,
        Expired = 7,
        Dispelled = 8,
        Restarted = 9,
        NoChange = 10,
        Rejected = 11,
    }

}
