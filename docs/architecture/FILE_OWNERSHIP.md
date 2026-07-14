# File ownership and repository creation rules

This map is the authority for parallel work under the accepted Shooter Mover
repository plan. Task `allowed_areas` remain the narrowest authority: this
document explains how ownership is resolved; it does not enlarge a task's
scope.

## Ownership principles

1. **One serialized owner.** A Unity scene, prefab, ScriptableObject, imported
   source asset, or its paired `.meta` file has one active task owner.
2. **One shared-file owner.** Central tables, shared modules, assembly
   definitions, package locks, project settings, and shared tools are edited
   only by their declared owner task or a separately approved stronger-review
   task.
3. **Package isolation.** A weapon, enemy, room, encounter, or environment task
   owns only its package subtree and may not edit sibling packages or shared
   foundations.
4. **Generated means regenerate.** Generated outputs are rebuilt from
   authoritative inputs. Merge conflicts are resolved in the inputs or
   generator, followed by regeneration; generated files are never manually
   merged.
5. **No ownership by discovery.** Finding an unowned or missing path does not
   grant permission to create or edit it. Escalate to the human lead or the
   shared-system owner.

## Accepted top-level roots

<!-- layout-root path="Packages/" creation="tracked-unity-baseline" -->
<!-- layout-root path="ProjectSettings/" creation="tracked-unity-baseline" -->
<!-- layout-root path=".github/" creation="create-by-verification-owner" -->
<!-- layout-root path=".github/workflows/" creation="create-by-verification-owner" -->
<!-- layout-root path="docs/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="docs/architecture/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="docs/verification/" creation="create-by-owning-task" -->
<!-- layout-root path="docs/toolchain/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="docs/art-pipeline/" creation="create-by-art-pipeline-owner" -->
<!-- layout-root path="source-assets/" creation="create-by-art-pipeline-owner" -->
<!-- layout-root path="source-assets/manifests/" creation="create-by-art-pipeline-owner" -->
<!-- layout-root path="source-assets/export-recipes/" creation="create-by-art-pipeline-owner" -->
<!-- layout-root path="source-assets/reference/" creation="create-by-art-pipeline-owner" -->
<!-- layout-root path="assembly/" creation="tracked-lifecycle-root" -->
<!-- layout-root path="assembly/generated/" creation="tracked-workflow-output" -->

## Required ownership rules

The validator requires these rule IDs. The prose following each marker is the
human escalation rule for sample paths.

<!-- ownership-rule id="scenes" mode="single-task-serialized" -->
**Scenes (`*.unity`).** The task naming the exact scene in its `allowed_areas`
owns that scene and its `.meta`. No parallel scene edits. Cross-room or shared
scene changes become a separate integration task.

<!-- ownership-rule id="prefabs" mode="single-task-serialized" -->
**Prefabs (`*.prefab`).** The creating task owns the exact prefab. Package
prefabs stay inside the package. A prefab shared by several packages requires a
separate shared-system owner before consumers begin.

<!-- ownership-rule id="scriptable-objects" mode="single-task-serialized" -->
**ScriptableObjects (`*.asset`).** The exact definition or settings task owns
the asset and `.meta`. Mutable runtime state never lives in a shared
ScriptableObject.

<!-- ownership-rule id="shared-modules" mode="strong-review-shared-owner" -->
**Shared modules.** Files below `Assets/ShooterMover/Content/SharedModules/`
belong to the task that introduces the reusable module. Content-package tasks
consume them read-only. Extensions require a separate shared-module task.

<!-- ownership-rule id="central-tables" mode="strong-review-single-owner" -->
**Central tables and shared registries.** The named contract, registry, or
generator task owns the source table. Package tasks contribute validated inputs
only and never hand-edit central outputs.

<!-- ownership-rule id="generated-registries" mode="regenerate-only" -->
**Generated registries and snapshots.** Only the designated generator/workflow
writes approved generated roots. Reviewers resolve conflicts in inputs, rerun
the generator, and review the resulting complete diff.

<!-- ownership-rule id="tools" mode="explicit-file-owner" -->
**Tools.** A task owns only the exact tool files in its `allowed_areas`.
Validation/generation/build framework changes are separate stronger-review
tasks; product tasks do not opportunistically modify shared tooling.

## Explicit exclusive patterns

These markers describe repository-wide exclusive scopes. More specific task
cards may subdivide a delegated content area, but may not overlap these scopes
with another owner.

<!-- exclusive-owner pattern="ProjectSettings/**" owner="unity-foundation" -->
<!-- exclusive-owner pattern="Packages/manifest.json" owner="unity-foundation" -->
<!-- exclusive-owner pattern="Packages/packages-lock.json" owner="unity-foundation" -->
<!-- exclusive-owner pattern="Assets/ShooterMover/Settings/Rendering/**" owner="unity-foundation-rendering" -->
<!-- exclusive-owner pattern="Assets/ShooterMover/Generated/**" owner="generated-registry-owner" -->
<!-- exclusive-owner pattern="assembly/generated/**" owner="assembly-workflow-owner" -->
<!-- exclusive-owner pattern="Assets/ShooterMover/README.md" owner="UF-005" -->
<!-- exclusive-owner pattern="tools/README.md" owner="UF-005" -->
<!-- exclusive-owner pattern="tools/validation/validate_repository_layout.py" owner="UF-005" -->
<!-- exclusive-owner pattern="docs/architecture/FILE_OWNERSHIP.md" owner="UF-005" -->

## Approved generated outputs

<!-- generated-output path="Assets/ShooterMover/Generated/" mode="regenerate-only" owner="generated-registry-owner" -->
<!-- generated-output path="assembly/generated/" mode="regenerate-only" owner="assembly-workflow-owner" -->

Both roots are **regenerate-only**. They are not manual merge targets. A new
generated-output location requires a reviewed update to this document and the
validator before any generator writes there.

## Delegated package and presentation ownership

| Path family | Owner resolution |
|---|---|
| `Assets/ShooterMover/Runtime/Domain/**` | Exact domain task; engine-independent and Unity-free |
| `Assets/ShooterMover/Runtime/Contracts/**` | Contract Steward task; consumers propose versioned changes |
| `Assets/ShooterMover/Runtime/Application/**` | Exact application-service task |
| `Assets/ShooterMover/Runtime/UnityAdapters/**` | Exact adapter task; no domain authority |
| `Assets/ShooterMover/Runtime/Bootstrap/**` | Unity Foundation composition/bootstrap task |
| `Assets/ShooterMover/Runtime/Presentation/**` | Exact presentation task |
| `Assets/ShooterMover/Content/Definitions/**` | Exact definition task; serialized assets are single-owner |
| `Assets/ShooterMover/Content/SharedModules/**` | Separate shared-module task |
| `Assets/ShooterMover/ContentPackages/<Kind>/<Package>/**` | Exact package task; sibling packages are read-only |
| `Assets/ShooterMover/Scenes/**` | Exact scene task; never concurrent |
| `Assets/ShooterMover/UI/**` | Exact UI task |
| `Assets/ShooterMover/Localization/**` | Exact localization/presentation task |
| `Assets/ShooterMover/Tests/**` | Test task matching the feature or verification lane |
| `Assets/ShooterMover/TestSupport/**` | Shared test-support task |
| `.github/workflows/**` | Verification/release task |
| `docs/**` | Task that owns the documented decision or subsystem |
| `source-assets/**` | Content/art pipeline task with provenance record |

## Conflict and escalation procedure

- Stop before editing when two active tasks name the same serialized asset,
  central table, shared module, generated root, or tool.
- The human lead chooses one owner or sequences the tasks.
- Never resolve generated-output conflicts by editing the output.
- Never use `git add -A` in a mixed Unity worktree. Stage the exact owned paths.
- Ignore and do not commit `Library/`, `Temp/`, `Logs/`, `Obj/`,
  `UserSettings/`, IDE caches, or unrelated Unity-generated `.meta` files.
