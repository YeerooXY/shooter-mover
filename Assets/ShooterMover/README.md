# Shooter Mover repository layout

This directory is the root of all product-owned Unity assets. The repository is
intentionally split into engine-independent rules, Unity-facing adapters,
presentation, independently owned content packages, generated outputs, and
verification assets.

A path listed below may already exist, or it may be created later by the task
that explicitly owns that path. Empty directory trees are not committed merely
to reserve names. Creation never grants permission to edit a sibling package,
scene, prefab, ScriptableObject, shared module, central table, or generated
registry.

## Accepted Unity roots

The following machine-readable markers are consumed by
`tools/validation/validate_repository_layout.py`.

<!-- layout-root path="Assets/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/" creation="tracked-marker" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/Domain/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/Contracts/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/Application/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/UnityAdapters/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/Bootstrap/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Runtime/Presentation/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Content/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Content/Definitions/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Content/SharedModules/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/ContentPackages/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/ContentPackages/Weapons/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/ContentPackages/Enemies/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/ContentPackages/Rooms/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/ContentPackages/Encounters/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/ContentPackages/Environment/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Generated/" creation="create-by-generator-owner" -->
<!-- layout-root path="Assets/ShooterMover/Scenes/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Scenes/Bootstrap/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Scenes/MenuHub/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Scenes/Prototypes/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Scenes/Factory/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Scenes/Tests/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/UI/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Localization/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Tests/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Tests/EditMode/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Tests/PlayMode/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Tests/Performance/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/TestSupport/" creation="create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Settings/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="Assets/ShooterMover/Settings/Rendering/" creation="tracked-or-create-by-owning-task" -->

## Creation and editing rules

- A task creates a missing root only when that root appears in its
  `allowed_areas` or is a necessary parent of an explicitly owned file.
- One task owns each Unity scene, prefab, ScriptableObject, central table,
  generated registry, or shared module at a time.
- A content package owns only its package directory. It may consume shared
  contracts, adapters, and modules, but it may not edit them.
- `.meta` files follow the ownership of their paired Unity asset. Never stage
  unrelated Unity-generated metadata.
- `Library/`, `Temp/`, `Logs/`, `Obj/`, and `UserSettings/` are local outputs,
  not repository roots and never task deliverables.
- `Assets/ShooterMover/Generated/` is generator-owned. Its contents are
  regenerated from accepted inputs and are never hand-edited or manually
  conflict-resolved.
