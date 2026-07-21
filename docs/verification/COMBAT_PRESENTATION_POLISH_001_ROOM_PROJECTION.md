# COMBAT-PRESENTATION-POLISH-001 room-projection route proof

The Stage 1 room runtime creates `Stage1RoomEnemyAuthorityProjection2D` objects before the retained enemy packages are registered for combat presentation.

`RegisterEnemyCombatPresentation(...)` now replaces every `projectedRoomEnemies` entry that still references the original `IEnemyActor2DAuthority` with the transparent `CombatPresentationEnemyActorAuthority2D` decorator. The replacement is based on authority identity only; it contains no moving-droid, turret, placement-ID, definition-ID, or package-type branch.

Therefore all three current Stage 1 routes resolve the same decorated authority:

- collider-backed `EnemyBinding` damage;
- `Stage1RoomEnemyAuthorityProjection2D.Apply(...)`;
- `Stage1RoomEnemyAuthorityProjection2D.Reset()`.

Focused PlayMode regression:

`Stage1RoomEnemyPresentationRouteTests.RoomProjection_LethalDamage_UsesDecoratedAuthority_AndReplaysVfxOnce`

The test loads the current Stage 1 scene, locates the moving-droid room projection by its authored room-instance identity, applies lethal damage through the projection, verifies exactly one pooled death VFX, and replays the exact command to verify no second VFX.

Unity compilation and execution remain required before merge; no passing XML is claimed by this static artifact.
