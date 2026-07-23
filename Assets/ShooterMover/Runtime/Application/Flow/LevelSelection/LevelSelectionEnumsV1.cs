namespace ShooterMover.Application.Flow.LevelSelection
{
    public enum LevelAvailabilityV1
    {
        Locked = 1,
        Unlocked = 2,
    }

    public enum LevelReleaseStateV1
    {
        Live = 1,
        Prototype = 2,
    }

    public enum LevelRouteKindV1
    {
        Gameplay = 1,
        Prototype = 2,
    }

    public enum LevelSelectionRouteV1
    {
        None = 0,
        PlaySelection = 1,
        GameplayScene = 2,
        PrototypeScene = 3,
    }

    public enum LevelSelectionStatusV1
    {
        RouteEmitted = 1,
        LevelLocked = 2,
        UnknownLevel = 3,
        InvalidContext = 4,
        InputLocked = 5,
    }
}
