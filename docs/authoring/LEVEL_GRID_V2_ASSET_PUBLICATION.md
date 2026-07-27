# Level Grid V2 generated-asset publication

## Authority and transaction boundary

The exported Level Grid V2 folder remains the authoring authority. The pure compiler still produces one deterministic `RoomContentJsonPackageV1`, and the retained V1 importer remains the compatibility gate.

Generated Unity assets are now published through a separate transaction:

```text
compile and validate in memory
→ write transaction-owned version staging folder
→ synchronously import and verify every TextAsset
→ move the validated folder to its content-addressed immutable version path
→ create, save, import and validate a staged JsonRoomContentDefinition2D
→ atomically replace the authoritative Resource .asset file
→ synchronously re-import and validate the authoritative asset
→ remove the transaction stage and unreferenced old versions best-effort
```

The authoritative `Resources` asset and every generated file it currently references remain untouched until the complete replacement package has imported and validated.

## Generated layout

```text
GeneratedRoot/
├── Versions/
│   ├── v-<package-content-hash>/
│   │   ├── compiled.manifest.json
│   │   └── <ordered-key>.json
│   └── ...
└── __RuntimeStages/
    └── <transaction-id>/
        └── RoomContent.asset
```

The version identity is derived from the canonical manifest plus sorted document keys and contents. An existing version is reused only when every imported TextAsset exactly matches the compiled package and the assembled runtime asset passes the real importer. A colliding, malformed or partially imported version fails closed.

A short-lived publishing marker protects a version that has reached its final path but has not completed the authoritative asset switch. A marker left after a committed switch is recoverable only when an existing runtime asset actually references that exact version.

## Authoritative asset switch

The staged runtime asset is serialized outside `Resources`. Its `.asset` bytes are copied to a same-directory temporary file beside the destination. Publication then uses an atomic filesystem replacement:

- existing destination: `File.Replace(new, destination, backup)`;
- absent destination: same-volume `File.Move(new, destination)`.

The existing destination `.meta` is never replaced, so its Unity GUID remains stable. After replacement Unity synchronously imports the destination and the compiler calls `JsonRoomContentDefinition2D.Import()` through the built-in object catalogue.

If replacement, import, TextAsset resolution or runtime validation fails, the previous `.asset` bytes are restored from the backup and synchronously re-imported before the original failure is reported. A newly created destination is removed together with its generated `.meta` on rollback.

The compiler snapshots both the destination `.asset` and `.meta` hashes before staging. Any external change before the switch blocks publication rather than overwriting the new owner.

## Cleanup semantics

Once the authoritative asset imports and validates, the transaction is committed. Cleanup is deliberately best-effort and cannot turn that committed publication into a reported compile failure.

Cleanup enumerates every `JsonRoomContentDefinition2D` in the AssetDatabase and retains every generated file or version referenced by any of them. It deletes only:

- the current transaction's runtime stage;
- unreferenced immutable versions without an active publishing marker;
- legacy top-level generated JSON that no runtime asset references.

A compiler transaction never deletes another transaction's runtime stage or marked publishing version.

## Editor assemblies

`Assets/ShooterMover/Editor/LevelDesign/Foundation/` is compiled by the dedicated Editor-only assembly `ShooterMover.Editor.LevelDesign.Foundation`. The existing EditMode assembly references it for established editor tests. Failure-injection coverage lives in the separate Editor-only test assembly `ShooterMover.Tests.EditorTooling`.

Neither assembly is available in player builds, and no `UnityEditor` dependency enters runtime assemblies.
