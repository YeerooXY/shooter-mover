# Inventory and loadout screen V1

## Purpose

`INV-002` implements the Inventory route as a presentation/application layer over
merged holdings and equipment contracts. It does not own inventory, equipment
instances, rewards, wallets, or persistent equipped-slot state.

The screen supports four ordered weapon slots, four armor slots, duplicate
definitions as distinct immutable instances, selection by concrete instance ID,
authority-delegated equip/unequip, immutable confirmation, and Back with the exact
incoming HUB payload object.

## Launch boundary

The implementation branch was launched from dispatch-time `main` commit
`45b276a8c415508ac8c0ddd8283234a6bbb2948e`. This supersedes the stale preparation
SHA because HUB-001 had to merge before INV-002 could start.

## Read-only holdings projection

`InventoryLoadoutScreenServiceV1` calls
`IPlayerHoldingsAuthorityV1.ExportSnapshot()` whenever the screen is created or
refreshed. It projects only unique `EquipmentReference` holdings using the
holding's concrete instance identity, definition identity, immutable
`EquipmentInstance`, read-only catalog definition, and catalog validation.

The screen never calls `IPlayerHoldingsAuthorityV1.Apply`. Selection changes only
update a transient draft keyed by concrete instance identity. That draft is not
inventory truth and cannot create, remove, replace, or transfer holdings.

Two holdings may share one definition identity. They remain independently
selectable because the projection is addressed by instance identity, never by
definition identity.

## Slot vocabulary

Weapon slots exactly match HUB-001:

1. `weapon-slot.slot-1`
2. `weapon-slot.slot-2`
3. `weapon-slot.slot-3`
4. `weapon-slot.slot-4`

Armor slots are:

1. `armor-slot.head`
2. `armor-slot.body`
3. `armor-slot.legs`
4. `armor-slot.feet`

V1 accepts any catalog-valid armor instance in any armor slot because no merged
armor-location compatibility contract exists. A later accepted policy can narrow
that through the authority adapter without changing holdings truth.

One concrete instance may occupy at most one slot. Definitions may repeat across
slots when the selected instances differ.

## Loadout authority adapter

Current `main` contains INV-001 holdings and immutable equipment instances, but no
merged persistent loadout-slot authority implementation. INV-002 therefore defines
`IInventoryLoadoutAuthorityPortV1` as a narrow composition boundary and deliberately
provides no production in-memory implementation.

The composing authority exposes an immutable complete slot snapshot and atomically
applies one complete command. A non-null binding means equip the concrete instance;
a null binding means unequip. `ExpectedSequence` protects against stale loadout
state, while `ExpectedHoldingsSequence` allows stale ownership rejection.

The authority result must exactly match the accepted command. The screen also
verifies that holdings sequence and fingerprint did not change during loadout
application. This keeps mutations behind an authority port without introducing a
second source of truth.

## Refresh and stale selections

Refresh rebuilds the equipment projection from the newest holdings snapshot.
Valid draft selections are retained by instance identity. If an item is removed or
replaced through INV-001, the draft preserves the missing ID for diagnostics but
marks the slot stale and disables confirmation. It never synthesizes a phantom
item or falls back to another instance sharing the definition.

Unknown definitions, invalid instances, unsupported categories, wrong slot
categories, missing instances, and repeated instance use reject without calling
the loadout authority or mutating holdings.

## Confirm and Back

Confirm requires all four weapon slots to contain distinct, currently owned,
catalog-valid weapon instances. Armor slots may be empty; non-empty armor slots
must contain distinct, currently owned, catalog-valid armor instances.

After authority acceptance, the screen creates a new
`PlayerRouteProfilePayloadV1` with the same character and loadout-profile IDs and
the four ordered confirmed weapon instance IDs. Armor remains in the loadout
authority projection because HUB payload V1 contains only weapon bindings.

Back/Cancel never calls the loadout authority. It returns the exact incoming
`PlayerRouteProfilePayloadV1` reference, preserving its fingerprint and ignoring
transient draft changes. Terminal input is guarded so repeated Confirm or Back
cannot dispatch a second mutation or route return.

## Unity projection

`InventoryLoadoutScreenControllerV1` implements
`IHubRouteDestinationAdapterV1` for `HubRouteV1.Inventory`. It provides functional
code-owned panels and buttons for slots, inventory instances, refresh, unequip,
confirm, and back. Escape/Backspace and controller East map to Back; Enter and
controller South map to Confirm.

`Assets/ShooterMover/Scenes/Flow/InventoryLoadout/InventoryLoadout.unity` is a
standalone authoring/manual-proof scene. It intentionally shows an unconnected
composition message until holdings, catalog, and loadout-authority adapters are
provided. It contains no fallback inventory/loadout truth and does not modify build
settings or the HUB-owned scene/controller.

## Focused coverage

EditMode filter:

```text
ShooterMover.Tests.EditMode.Inventory.LoadoutScreen
```

Coverage includes duplicate definitions, every weapon/armor slot, full-slot
admission, invalid/unknown/wrong-type equipment, stale removal refresh, exact
payload ordering, holdings non-mutation, repeat input, exact Back semantics, and
revisit identity.

PlayMode filter:

```text
ShooterMover.Tests.PlayMode.Flow.InventoryLoadout
```

Coverage includes controller confirmation, exact payload return, repeated Back,
and revisit projection.

A Unity pass may be claimed only when both named result files exist and report zero
failures:

```text
artifacts/test-results/INV-002-EditMode.xml
artifacts/test-results/INV-002-PlayMode.xml
```

Source inspection or authored tests are not substitutes for those XML results.

## Known limitations

- No merged persistent loadout authority implementation exists yet; production
  composition must provide the adapter.
- Armor-location compatibility is not encoded by current equipment contracts, so
  V1 applies category validation only.
- The standalone scene is intentionally not added to EditorBuildSettings.
- Drag-and-drop and final inventory artwork are outside V1.

## Rollback

Delete the INV-002 application, UI, standalone scene, focused tests, inseparable
Unity metadata, and this document. No holdings, equipment, HUB, wallet, reward,
shop, crafting, skill, gameplay, project-setting, or package migration is needed.
