# HUB route/profile contract V1

## Purpose

`HUB-001` is the sole owner of the session route/profile payload shared by the
Main Menu, Character Select, Inventory/Loadout Hub, Inventory, Skills, Shop,
Crafting, and Play routes.

The UI is a projection/router. It does not own or mutate holdings, equipment,
wallets, XP, rewards, shops, crafting, or skills.

## Immutable payload

`PlayerRouteProfilePayloadV1` contains:

- schema version `1`;
- contract identity `route-profile.player-v1`;
- one selected character `StableId`;
- one selected loadout-profile `StableId`;
- four ordered weapon-slot bindings;
- one concrete equipment-instance `StableId` per slot;
- one canonical SHA-256 fingerprint.

The ordered V1 slot identities are:

1. `weapon-slot.slot-1`
2. `weapon-slot.slot-2`
3. `weapon-slot.slot-3`
4. `weapon-slot.slot-4`

Equipment definition identities are intentionally absent from the route payload.
Two owned equipment instances may share a definition, but the four selected
instance identities must be distinct so route changes never collapse duplicate
items into one selection.

Construction and import defensively copy input collections. `Copy()` creates a
new equivalent payload and new slot records. No API exposes a mutable slot list.

## Validation and fingerprinting

External/session data enters through `PlayerRouteProfileEnvelopeV1` and
`PlayerRouteProfilePayloadV1.TryImport`.

Validation is fail-closed and rejects, without changing any live route state:

- null envelopes;
- unsupported schema versions;
- missing, malformed, or mismatched contract identity;
- missing or malformed character/loadout identities;
- missing or non-four-slot collections;
- null slot records;
- missing, malformed, duplicate, unexpected, or out-of-order slot identities;
- missing, malformed, or duplicate equipment-instance identities;
- missing or inconsistent fingerprints.

The canonical fingerprint covers schema, contract, character, loadout profile,
slot count, ordered slot identities, and ordered equipment-instance identities.
Fields are length-prefixed before SHA-256 hashing to avoid delimiter ambiguity.

## Route state and history

`HubNavigationServiceV1` retains the exact same payload object for its complete
lifetime. It owns only:

- current route;
- deterministic back stack;
- monotonic route sequence;
- immutable route-history projections.

Each route-history record repeats the payload fingerprint. Invalid transitions
return a rejection status and do not mutate route, history, or payload.

Forward transitions are:

```text
Main Menu -> Character Select
Character Select -> Inventory/Loadout Hub
Inventory/Loadout Hub -> Inventory | Skills | Shop | Crafting | Play
Destination -> Inventory/Loadout Hub
Any non-main route -> Main Menu
```

Back navigation pops the deterministic route stack. Back at Main Menu is a
no-change result.

## Unity projection

`HubFlowControllerV1` renders real buttons for:

- Character Select continuation;
- Inventory;
- Skills;
- Shop;
- Crafting;
- Play;
- Back;
- Main Menu.

Destination content is currently presented through
`IHubRouteDestinationAdapterV1`. Separate screen owners can replace the
placeholder adapter while consuming the same immutable payload read-only.

A runtime bootstrap installs the HUB projection when the accepted
`Assets/ShooterMover/Scenes/Menu/MainMenu.unity` scene loads. It does not edit
that scene or any MENU-owned file. `Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity`
is also available as a standalone authoring/manual-proof scene. Build-settings
ownership remains outside this task.

The default standalone scene uses explicit placeholder StableIds only to keep the
shell demonstrable before CHAR-001 and INV-002 connect real selections. Production
composition must create the payload from the selected character/profile and the
read-only holdings/loadout projection; it must not invent or mutate holdings.

## Authority boundary

HUB code never calls grant, spend, purchase, craft, skill-allocation, XP, reward,
strongbox-opening, or holdings-mutation APIs. Route adapters receive only a route
enum and the immutable payload.

## Proof boundary

Focused filters:

- `ShooterMover.Tests.EditMode.Flow.Hub`
- `ShooterMover.Tests.PlayMode.Flow.Hub`

Passing proof requires the named XML result files to exist and report zero
failures. Source inspection, authored tests, logs, or GitHub mergeability are not
substitutes.

## Known limitations

- Destination implementations are placeholders until their separate owners land.
- Persistence is session/navigation persistence only; save-profile persistence is
  outside HUB-001.
- The standalone fallback IDs are not a holdings authority and must be replaced by
  composition input when the downstream character/inventory screens integrate.
- `ProjectSettings/EditorBuildSettings.asset` is intentionally unchanged.

## Rollback

Delete the HUB-owned contract, application, UI, scene, tests, and this document.
No wallet, inventory, equipment, XP, reward, shop, crafting, skill, gameplay,
MENU-owned scene, package, project setting, or generated artifact requires a
migration or restoration.
