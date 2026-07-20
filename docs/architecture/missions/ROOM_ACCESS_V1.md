# ROOM-ACCESS-001 — Room access conditions and consumptive locks

## Purpose

ROOM-ACCESS-001 adds an engine-neutral, data-driven access layer on top of the existing room graph and ROOM-LIVE facts. It does not replace room topology, occupancy, completion, traversal, inventory, or presentation authority.

The access layer answers one question deterministically:

> Given immutable room/run facts and the current run holdings, which authored doors are open, and which consumptive locks have been permanently unlocked in this lifecycle?

## Authority boundary

`RoomAccessDefinitionV1` owns immutable authored access data:

- stable condition identities;
- condition kind, exact subject identity, threshold, or child identities;
- the exact room door controlled by each root condition;
- the optional exact holding consumed when that door is unlocked;
- canonical JSON and deterministic SHA-256 fingerprinting.

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

## Narrow ports

`IRoomAccessFactPortV1` exposes one immutable `RoomAccessFactSnapshotV1`. Facts are exact stable identities for:

- entered rooms;
- completed rooms;
- terminal enemy or prop instances;
- collected drop instances;
- completed objectives;
- active switches;
- consumed holding types;
- current difficulty.

`IRoomRunHoldingPortV1` is intentionally narrower than inventory authority. It exposes quantities for stable run-holding IDs and one exactly-once consume command. The room layer cannot enumerate or mutate general equipment, loadout, strongboxes, currency, or persistent inventory.

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
| `holding-present` | Current run quantity for one exact holding ID is greater than zero. |
| `holding-consumed` | One exact holding ID has been consumed and retained as a fact. |
| `collected-drop` | One exact drop instance has been collected. |
| `objective-complete` | One exact objective ID is complete. |
| `switch-active` | One exact switch ID is active. |
| `difficulty-at-least` | Current difficulty meets the authored threshold. |
| `all` | Every child condition is true. |
| `any` | At least one child condition is true. |
| `not` | The single child condition is false. |

Definitions reject unknown child references, duplicate identities, invalid subjects, unknown rooms/placements/doors, invalid composite shapes, and circular graphs before an authority can be created.

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

## JSON authoring

Access is a readable companion document to split room content. It can select a door by exact stable ID or by authored meaning within one exact room:

```json
{
  "version": 1,
  "layout": "layout.level1-authorable-two-room",
  "conditions": [
    {
      "id": "access.level1-entry-complete",
      "kind": "room-complete",
      "subject": "room.level1-entry"
    },
    {
      "id": "access.level1-blue-key-present",
      "kind": "holding-present",
      "subject": "holding.level1-blue-key"
    },
    {
      "id": "access.level1-forward-gate",
      "kind": "all",
      "children": [
        "access.level1-entry-complete",
        "access.level1-blue-key-present"
      ]
    }
  ],
  "doors": [
    {
      "room": "room.level1-entry",
      "exit_type": "progression",
      "condition": "access.level1-forward-gate",
      "consume_holding": "holding.level1-blue-key"
    }
  ]
}
```

Supported door selectors are:

- exact `door`;
- `exit_type`: `progression`, `return`, `optional`, or `secret`;
- `link_kind`: `room` or `final-exit`;
- both `exit_type` and `link_kind` together.

Meaning-based selectors must resolve to exactly one door in the authored room. Ambiguity fails closed and asks the author to use the exact door ID. This preserves return/progression/final-exit semantics without inferring them from room order.

`RoomAccessJsonImporterV1` returns one structured issue with a code, JSON path, and message and never returns a partial definition. Canonical JSON uses exact door IDs, sorted condition/door records, sorted child IDs, and explicit null/default fields. Re-importing it produces the same fingerprint.

`JsonRoomAccessDefinition2D` composes an existing `JsonRoomContentDefinition2D` with one access `TextAsset`, so adding another key or locked door normally changes JSON only. The checked-in `level1.access.json` is an authoring example and is not installed into the playable Stage 1 composition by this task.

## Lifecycle and persistence

Consumptive unlock state is scoped to one `runtimeInstanceStableId` and lifecycle generation. ROOM-ACCESS-001 deliberately does not define save-game persistence or cross-run key persistence. A new room runtime lifecycle should create a new access authority from the same immutable definition and current authoritative ports.

## Non-goals

- no inventory or key UI;
- no pickup or door art;
- no general inventory authority;
- no Stage 1 scene/controller modification;
- no JSON-to-live-level cutover;
- no mission reward, XP, drop-generation, or Results behavior.
