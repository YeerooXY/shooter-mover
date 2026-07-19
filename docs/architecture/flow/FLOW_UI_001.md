# FLOW-UI-001 — Canonical production screen flow

## Boundary

FLOW-UI-001 does not define another route or character authority.

- `HubNavigationServiceV1` remains the sole Main Menu / Character Selection /
  Hub route-history owner.
- `CharacterSelectionServiceV1` remains the sole character/class selection
  coordinator and preserves the exact incoming equipment-instance identities.
- `PlayerRouteProfilePayloadV1` remains the character/profile/loadout route
  contract.
- RUN-001 `MissionResultPayloadV1` and
  `MissionRunStrongboxResultV1` remain Results facts.
- `StrongboxOpeningServiceV1` remains the BOX opening authority.

`ProductionSceneTransitionCoordinatorV1` owns only one accepted in-flight scene
request. It checks the existing route service before loading, rejects further
input while pending, and reconciles an unexpected scene completion back to the
accepted destination.

## Canonical flow

```text
Bootstrap
  -> Main Menu
  -> Character Selection
       empty profile -> Character Creation
       existing profile -> Hub
  -> Hub
       -> Inventory
       -> Skills
       -> Shop
       -> Crafting
       -> Play Selection
            -> Level Selection
                 -> gameplay scene
                 -> Results
                      -> exact Strongbox Opening
                      -> refreshed authoritative Results
```

Character Selection and Character Creation intentionally share the same scene
and one controller. Creation adds only a required display name; character and
class identities are selected through the existing catalog and
`CharacterSelectionServiceV1`.

## Profile persistence

`PlayerPrefsProductionFlowProfileStoreV1` stores:

- the display name;
- the existing route-profile envelope;
- all four exact equipment-instance identities;
- the existing route fingerprint.

It does not introduce a second character, class, inventory, or loadout model.
Malformed persistence is rejected and cleared.

## Screen ownership and art

The canonical Main Menu and Character Selection scenes contain one active
screen controller each. The old embedded multi-screen Main Menu owner is no
longer present in the canonical scene, and HUB no longer installs an overlay
into Main Menu.

The supplied Main Menu, Character Selection, Character Creation, class,
Skills, Shop, Crafting, and Results assets remain active presentation inputs.
When an authority bundle is not yet supplied, the real destination controller
shows an artwork-backed disconnected state and creates no fallback authority.

## Results and Strongbox Opening

Results receives the exact immutable `MissionResultPayloadV1`. Selecting a box
requires reference identity with one object in
`Result.UnopenedStrongboxes`. The exact object is passed to the injected command
factory, and the resulting immutable command plus the real
`StrongboxOpeningServiceV1` are bound to `StrongboxOpeningController`.

After opening, Results is refreshed only through an injected authoritative
RUN/BOX composition function. The refresh is rejected unless the same run and
route remain present, the exact selected instance alone changes to opened after a
successful BOX result, and every other strongbox fact remains equal. UI code never
marks a box opened locally.

## Cameras

The Bootstrap scene explicitly owns the one persistent flow coordinator. Its
Awake path creates one UI camera before the first rendered frame; the old
Bootstrap camera was removed, so no duplicate camera exists during startup.
Scene cameras are disabled only while a canonical UI screen is active. The flow
camera disables itself for gameplay scenes, leaving gameplay camera ownership
untouched.

## Validation boundary

Focused source tests cover:

- existing and empty profiles;
- required name and explicit class selection;
- exact equipment identity retention;
- pending-load input rejection;
- unexpected-scene reconciliation;
- exact strongbox object binding into the real `StrongboxOpeningController`;
- authoritative refresh where only the exact selected box may change;
- persisted existing-profile reload using the existing route envelope;
- real Bootstrap and canonical scene loading with one active camera.

Unity XML is still required before the draft can be marked ready.
`Stage1VisibleSliceController.cs` is outside this change.
