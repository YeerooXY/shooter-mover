namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// STAGE1-FREEZE-001 code-level retirement marker for the retained Stage 1
    /// migration surface.
    ///
    /// These existing types are migration-only. They must not gain new gameplay,
    /// reward, persistence, save, room, weapon, enemy, player-health, enemy-health,
    /// discovery, reflection, or self-installation ownership:
    ///
    /// - ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController
    /// - Stage1PlayableLoopCompositionV1 and every partial
    /// - Stage1RunPickupBootstrap2D and every partial
    /// - Stage1RunPickupPropBootstrap2D
    /// - Stage1WeaponPresentationRepairV1
    /// - the retained Stage 1 terminal fact/resolver/delivery support types
    /// - Stage1DestructiblePropIntegration
    ///
    /// Stage1VisibleSliceController and Stage1PlayableLoopCompositionV1 are both
    /// retirement targets. Moving behavior from the former into the latter is not
    /// an acceptable migration.
    ///
    /// The intended replacement boundaries are named below for documentation and
    /// auditing only. This file intentionally creates no placeholder runtime types.
    /// </summary>
    internal static class Stage1MigrationOnlySurfaceDocumentationV1
    {
        internal const string SceneInstallation =
            "Stage1SceneInstaller2D";

        internal const string RunLoop =
            "Stage1RunLoopDriver2D";

        internal const string WeaponEffectDamageRouting =
            "InventoryWeaponEffectDamageRouter2D";

        internal const string RoomFlow =
            "Stage1RoomFlowController2D";

        internal const string EnemyTerminalPickups =
            "Stage1EnemyTerminalPickupConsumerV1";

        internal const string PropTerminalPickups =
            "Stage1PropTerminalPickupConsumerV1";

        internal const string PickupLifecycleProjection =
            "RunPickupLifecycleProjection2D";

        internal const string LegacyPresentation =
            "Stage1LegacyScenePresentation2D";
    }
}
