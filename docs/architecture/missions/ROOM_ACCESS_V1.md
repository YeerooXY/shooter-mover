# ROOM-ACCESS-001 — Room access conditions and consumptive locks

## Purpose

ROOM-ACCESS-001 adds an engine-neutral, data-driven access layer on top of the existing room graph and ROOM-LIVE facts. It does not replace room topology, occupancy, completion, traversal, inventory, objective, switch, drop, or presentation authority.

The access layer answers one question deterministically:

> Given immutable room/run facts, current run holdings, and validated authored references, which doors are open and which consumptive locks are permanently unlocked in this lifecycle?

## Immutable authoring boundaries

`RoomAccessDefinitionV1` owns immutable authored access data:

- stable condition identities;
- condition kind, exact subject identity, threshold, or child identities;
- the exact room door controlled by each root condition;
- the optional exact holding consumed when that door is unlocked;
- the immutable reference-registry fingerprint used to validate external leaves;
- canonical JSON and deterministic SHA-256 fingerprinting.

`RoomAccessReferenceCatalogV1` is a narrow authoring-validation catalog. It contains sorted immutable registrations for:

- run holding IDs;
- objective IDs;
- switch IDs;
- collected-drop references.

Each registration records both its kind and source. Collected-drop references must be explicitly registered as either:

- `AuthoredDropInstance`; or
- `ExternalDropReference`.

A syntactically valid `StableId` is never accepted as a collected-drop reference merely because it parses.

The catalog is **not** an inventory, objective, switch, reward, drop, or room authority. It provides membership checks, canonical JSON, and a deterministic fingerprint only. Registration order does not affect its canonical representation or fingerprint.

## Definition validation

`RoomAccessDefinitionV1` validates all references before an authority can be created.

Room-owned references are checked against the authored room graph:

- `room-entered` and `room-complete` subjects must be exact room IDs;
- `exact-terminal` subjects must be exact authored enemy/prop placement IDs;
- child condition references must exist;
- door IDs must exist in the authored room;
- root condition IDs must exist;
- condition graphs must be acyclic.

External leaves are checked against `IRoomAccessReferenceRegistryV1`:

- `holding-present`;
- `holding-consumed`;
- door `consume_holding`;
- `objective-complete`;
- `switch-active`;
- `collected-drop`.

The compatibility constructor that does not receive a registry uses the immutable empty catalog. Consequently, it remains safe for room-only conditions but fails closed when an external reference is present.

The definition canonical JSON includes `reference_registry_fingerprint`. Therefore the definition fingerprint changes when its validated external-reference provenance changes.

## Runtime authority boundary

`RoomAccessAuthorityV1` owns only:

- deterministic condition-tree evaluation;
- retained unlocked state for consumptive locks;
- retained consumed-holding facts created by accepted access operations;
- exact operation replay/conflict history;
- immutable access projections.

It does **not** own:

- room graph topology, room entry, room completion, occupant terminal state, or collected drops;
- inventory, loadout, rewards, XP, objectives, switches, or difficulty selection;
- door animation, colliders, scenes, UI, key art, or pickup presentation;
- Stage 1 composition or cutover.

## Narrow runtime ports

`IRoomAccessFactPortV1` exposes one immutable `RoomAccessFactSnapshotV1`. Facts are exact stable identities for:

- entered rooms;
- completed rooms;
- terminal enemy or prop instances;
- collected drop references;
- completed objectives;
- active switches;
- consumed holding types;
- current difficulty.

`IRoomRunHoldingPortV1` is intentionally narrower than inventory authority. It exposes quantities for stable run-holding IDs and one exactly-once consume command. The room layer cannot enumerate or mutate general equipment, loadout, strongboxes, currency, or persistent inventory.

The authoring-time reference catalog never queries either runtime port. Import and definition validation are deterministic and independent of mutable runtime state.

`RoomLiveAccessFactProjectionV1` is a pure bridge from the existing immutable ROOM-LIVE projection:

- `IsVisited` becomes `room-entered`;
- `IsCompleted` becomes `room-complete`;
- accepted defeated occupant identities become `exact-terminal`;
- retained collected drop identities become `collected-drop`.

The bridge does not infer completion from clear state and does not infer entity identity from type, package, or hierarchy names.

## Supported condition kinds

| JSON kind | Meaning |
|---|---|
| `always` | Always true. |
| `room-entered` | One exact room has been visited. |
| `room-complete` | One exact room is completed by ROOM-LIVE. |
| `exact-terminal` | One exact authored enemy or prop instance is terminal. |
| `holding-present` | Current run quantity for one registered holding ID is greater than zero. |
| `holding-consumed` | One registered holding ID has been consumed and retained as a fact. |
| `collected-drop` | One explicitly registered drop reference has been collected. |
| `objective-complete` | One registered objective ID is complete. |
| `switch-active` | One registered switch ID is active. |
| `difficulty-at-least` | Current difficulty meets the authored threshold. |
| `all` | Every child condition is true. |
| `any` | At least one child condition is true. |
| `not` | The single child condition is false. |

## Door behavior

A non-consuming door is open whenever its root condition evaluates true. This allows live switch or difficulty conditions to close again if their facts change.

A door with `consume_holding` requires an explicit `TryUnlock` command. The authority:

1. validates runtime, lifecycle, door, and operation identity;
2. evaluates the root condition against one immutable fact/holding snapshot;
3. derives a deterministic child operation identity for the holding port;
4. consumes exactly one unit through `IRoomRunHoldingPortV1`;
5. retains the door as unlocked only after an accepted consume result.

An exact unlock-command replay never calls the holding port again. A crash-safe retry at the holding port may return `DuplicateAccepted`, which still allows the authority to finish retaining the unlocked door. Reusing an operation ID with another payload rejects without mutation.

Rejected attempts are journaled. A later retry after facts or holdings change must use a new operation ID.

## JSON authoring and provenance

Access is a readable companion document to split room content. Version 1 documents remain accepted for existing authored content. Canonical version 2 output includes the immutable reference-registry fingerprint:

```json
{
  "version": 2,
  "layout": "layout.level1-authorable-two-room",
  "reference_registry_fingerprint": "<sha256>",
  "conditions": [
    {
      "id": "access.level1-blue-key-present",
      "kind": "holding-present",
      "subject": "holding.level1-blue-key"
    }
  ],
  "doors": [
    {
      "room": "room.level1-entry",
      "door": "door-instance.level1-forward",
      "condition": "access.level1-blue-key-present",
      "consume_holding": "holding.level1-blue-key"
    }
  ]
}
```

When a version 2 document supplies a registry fingerprint, import rejects a different registry with `room-access-reference-registry-fingerprint-mismatch`. Version 2 documents without the fingerprint reject. Version 1 input receives the active registry fingerprint when compiled into the canonical definition.

Supported door selectors are:

- exact `door`;
- `exit_type`: `progression`, `return`, `optional`, or `secret`;
- `link_kind`: `room` or `final-exit`;
- both `exit_type` and `link_kind` together.

Meaning-based selectors must resolve to exactly one door in the authored room. Ambiguity fails closed and asks the author to use the exact door ID. This preserves return/progression/final-exit semantics without inferring them from room order.

`RoomAccessJsonImporterV1` returns one structured issue with a stable code, precise JSON path, and message, and never returns a partial definition. External reference diagnostics include:

- `room-access-holding-reference-unknown`;
- `room-access-consume-holding-reference-unknown`;
- `room-access-objective-reference-unknown`;
- `room-access-switch-reference-unknown`;
- `room-access-drop-reference-unknown`.

Canonical JSON uses exact door IDs, sorted condition/door records, sorted child IDs, explicit null/default fields, and the reference-registry fingerprint. Re-importing it with the same registry produces the same fingerprint.

`JsonRoomAccessDefinition2D` serializes authoring-only reference registrations and builds one immutable `RoomAccessReferenceCatalogV1` before import. Its test/import overload also accepts an already-built immutable registry. It never resolves references through mutable runtime authorities.

The checked-in `level1.access.json` remains an authoring example and is not installed into the playable Stage 1 composition by this task. Its `holding.level1-blue-key` reference must be supplied by a future composition's immutable registry before that example can import; the current task intentionally does not wire that composition.

## Lifecycle and persistence

Consumptive unlock state is scoped to one `runtimeInstanceStableId` and lifecycle generation. ROOM-ACCESS-001 deliberately does not define save-game persistence or cross-run key persistence. A new room runtime lifecycle should create a new access authority from the same immutable definition and current authoritative ports.

## Non-goals

- no inventory or key UI;
- no pickup or door art;
- no general inventory authority;
- no Stage 1 scene/controller modification;
- no JSON-to-live-level cutover;
- no mission reward, XP, drop-generation, or Results behavior.
