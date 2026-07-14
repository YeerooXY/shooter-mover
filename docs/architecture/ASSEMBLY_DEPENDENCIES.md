# Unity Assembly Dependencies

UF-004 establishes the initial compilation boundaries for Shooter Mover. These
assemblies encode the architecture's inward-only dependency direction before
gameplay code is introduced.

## Dependency graph

```text
ShooterMover.Domain
  ↑
ShooterMover.Contracts
  ↑
ShooterMover.Application
  ↑                  ↑
ShooterMover.UnityAdapters   ShooterMover.Content.Definitions
  ↑                  │
ShooterMover.Presentation   │
  └──────────┬───────────────┘
             ↑
ShooterMover.Bootstrap
             ↑
ShooterMover.Tests.EditMode / ShooterMover.Tests.PlayMode
```

The diagram is a layer summary. The exact direct references are:

| Assembly | Direct references | UnityEngine available |
|---|---|---|
| `ShooterMover.Domain` | none | no |
| `ShooterMover.Contracts` | Domain | no |
| `ShooterMover.Application` | Domain, Contracts | no |
| `ShooterMover.UnityAdapters` | Domain, Contracts, Application | yes |
| `ShooterMover.Content.Definitions` | Domain, Contracts, Application | yes |
| `ShooterMover.Presentation` | Domain, Contracts, Application, UnityAdapters | yes |
| `ShooterMover.Bootstrap` | all six lower runtime/content assemblies | yes |
| `ShooterMover.Tests.EditMode` | all seven product assemblies | yes, Editor only |
| `ShooterMover.Tests.PlayMode` | all seven product assemblies | yes |

## Boundary rules

1. `ShooterMover.Domain` is the innermost assembly and sets
   `noEngineReferences: true`. It may not use `UnityEngine`, scene objects,
   ScriptableObjects, or Unity lifecycle methods.
2. Contracts and Application also set `noEngineReferences: true`. They may use
   Domain types, but they may not reference UnityAdapters, Presentation,
   Bootstrap, or test assemblies.
3. UnityAdapters translates device, engine, scene, rendering, audio, and 2D
   physics concerns into Application/Contracts/Domain boundaries. It does not
   own durable game truth.
4. Content.Definitions contains authored Unity-facing definitions and may
   reference only the engine-independent layers.
5. Presentation may consume immutable read models and UnityAdapters, but it may
   not become a state authority.
6. Bootstrap is the composition root. It is the only baseline runtime assembly
   that references every outward product layer.
7. Tests point inward to product assemblies. Product assemblies never reference
   tests.
8. Baseline references use stable assembly names, not `GUID:` references.
9. New assembly definitions must remain acyclic and may not introduce a
   reference from a more-inward layer to a more-outward layer.

## Test assembly policy

`ShooterMover.Tests.EditMode` is restricted to the Editor platform.
`ShooterMover.Tests.PlayMode` is not Editor-only because its tests may be run
through Unity's player-capable test flow. Both assemblies:

- declare `optionalUnityReferences: ["TestAssemblies"]`;
- set `autoReferenced: false`;
- reference product assemblies without becoming product dependencies.

Editor-only helpers must remain in the EditMode tree. Runtime/player-facing test
fixtures belong in PlayMode or TestSupport under a separately approved task.

## Validation

From the repository root:

```powershell
python tools/validation/validate_unity_assembly_graph.py
```

The validator checks:

- all nine required baseline `.asmdef` files;
- exact names and direct references;
- `noEngineReferences` on Domain, Contracts, and Application;
- test-assembly flags and EditMode platform restriction;
- missing internal `ShooterMover.*` references;
- forbidden outward references;
- cycles across all `Assets/ShooterMover/**/*.asmdef` files.

A successful run prints an assembly count and exits with code `0`. For the
required negative check, temporarily add an outward reference such as
`ShooterMover.Domain -> ShooterMover.UnityAdapters`; the validator must fail.
Remove the temporary edit before committing.

## Unity review

Open the project with the pinned Unity `6000.3.19f1` editor and allow script
compilation to finish. In the Assembly Definition inspector, confirm the direct
references match the table above. Empty assembly folders are intentional at
this stage; later owned source files cause Unity to emit the corresponding
assemblies.

Do not add marker gameplay types merely to make an empty assembly visible. This
task defines compilation boundaries only.
