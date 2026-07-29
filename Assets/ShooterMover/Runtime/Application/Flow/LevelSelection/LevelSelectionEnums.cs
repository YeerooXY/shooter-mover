namespace ShooterMover.Application.Flow.LevelSelection
{
    public enum LevelAvailability
    {
        Locked = 1,
        Unlocked = 2,
    }

    public enum LevelReleaseState
    {
        Live = 1,
        Prototype = 2,
    }

    public enum LevelRouteKind
    {
        Gameplay = 1,
        Prototype = 2,
    }

    public enum LevelSelectionRoute
    {
        None = 0,
        PlaySelection = 1,
        GameplayScene = 2,
        PrototypeScene = 3,
    }

    public enum LevelSelectionStatus
    {
        RouteEmitted = 1,
        LevelLocked = 2,
        UnknownLevel = 3,
        InvalidContext = 4,
        InputLocked = 5,
    }
}
