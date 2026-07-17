# Crafting Screen V1

## Purpose

`CRAFTUI-001` adds the functional Hub Crafting destination. The screen is a presentation/controller over the accepted crafting and reward authorities; it does not own recipes, scrap, equipment, inventory, deterministic generation, or reward application.

The supplied `crafting_screen.png` is copied into the UI subtree as `crafting_screen.png.bytes` and loaded as a passive backplate. Every selectable recipe and every action is a code-owned overlay. Nothing painted into the image is authoritative or clickable by itself.

## Authority boundary

The screen consumes `ICraftingPresentationAuthorityPortV1`:

- `ExportSnapshot()` projects the current SCR balance/sequence, INV holdings sequence, CRA recipe catalog, and equipment catalog.
- `Preview(command)` is read-only and returns the exact immutable `EquipmentInstance` expected for that deterministic command.
- `Craft(command)` delegates the operation to CRA-001. The provided `CraftingServicePresentationAuthorityPortV1` maps the accepted `CraftingServiceV1` result without spending scrap or granting equipment itself.

Production composition must implement preview by running the existing CRA/GEN path against cloned SCR/INV/RAP snapshots, or by another read-only adapter that uses the same accepted generator inputs. Do not reimplement the generation policy in UI code.

## Projected data

Each recipe card and the selected-recipe panel show:

- stable recipe identity;
- target equipment definition and display name;
- player level;
- natural discovery level;
- deterministic actual crafting unlock level for the active operation seed;
- scrap cost and current balance;
- locked, available, insufficient-scrap, invalid-target, or preview-unavailable state;
- exact preview definition, concrete instance identity, item level, quality, augments, and fingerprint;
- exact authority result after crafting, including the concrete equipment-instance identity and fingerprint.

Duplicate equipment definitions are not collapsed. Two successful operations may return the same definition ID while retaining different immutable instance IDs.

## Operation lifecycle

A recipe attempt derives one stable `CraftEquipmentCommandV1` from:

- the screen-session identity;
- recipe identity;
- explicit attempt ordinal;
- run identity;
- claimant identity;
- immutable progression context;
- deterministic root seed.

Preview and craft use the same command fingerprint. A `RewardApplicationRetryRequired` result retains that exact command; **Retry Same Operation** resubmits it unchanged. A successful or exact-duplicate result closes the attempt locally, so repeated button/input callbacks cannot spend or grant again.

A second item requires the explicit **Craft Another** action. That increments the attempt ordinal and derives a new operation identity, seed, preview, and equipment-instance identity. This separates intentional repeated crafting from duplicate input.

The authoritative CRA/RAP/SCR/INV composition remains responsible for exactly-once spending and exactly one grant. Presentation additionally rejects an apparent success when:

- the returned command fingerprint differs from the submitted command;
- the returned result has no equipment instance; or
- the returned equipment fingerprint differs from the deterministic preview.

## Navigation

`CraftingScreenControllerV1` implements `IHubRouteDestinationAdapterV1` and accepts only `HubRouteV1.Crafting`.

Back returns the exact incoming `PlayerRouteProfilePayloadV1` object. It does not rebuild, replace, or mutate the payload. A controller dispatch guard prevents repeated keyboard, gamepad, GUI, or callback input from returning twice.

Revisit creates a fresh presentation service and reads current authority snapshots. No recipe, scrap, or inventory truth is retained locally between visits.

## Scene and composition

`Assets/ShooterMover/Scenes/Flow/Crafting/Crafting.unity` is a standalone authoring/manual-proof scene containing only the controller and the passive backplate reference. Without production composition it intentionally displays an `AWAITING HUB COMPOSITION` state instead of inventing fallback recipes, currency, inventory, or rewards.

A Hub composition root should call:

1. `Configure(...)` with the crafting presentation port, immutable progression context, deterministic identities/seed, and return callback.
2. `Present(HubRouteV1.Crafting, routePayload)` with the exact current Hub payload.

## Focused verification

EditMode filter:

```text
ShooterMover.Tests.EditMode.Crafting.Presentation
```

Coverage includes locked/available projection, natural and actual unlock levels, cost/balance, deterministic preview stability, successful exact result, insufficient scrap, duplicate/conflict behavior, retry identity, duplicate definitions as separate instances, Back identity, revisit, and no presentation-side mutation.

PlayMode filter:

```text
ShooterMover.Tests.PlayMode.Flow.Crafting
```

Coverage includes controller craft, retry, exact result projection, single Back dispatch, unchanged route payload, revisit projection, and route validation.

Passing Unity claims require both named XML result files with zero failures:

- `artifacts/test-results/CRAFTUI-001-EditMode.xml`
- `artifacts/test-results/CRAFTUI-001-PlayMode.xml`

## Manual proof checklist

1. Open Crafting from Hub and compare the route payload fingerprint before/after Back.
2. Verify locked and available recipes show natural and actual unlock levels.
3. Verify the displayed preview instance exactly matches the successful result instance/fingerprint.
4. Trigger repeated craft input and confirm one scrap spend and one equipment grant.
5. Force a pending reward application; retry and confirm the command fingerprint does not change.
6. Use Craft Another twice on one target definition and confirm separate instance IDs.
7. Leave and revisit; verify the current SCR balance and INV holdings sequence are re-read.

## Non-goals

- crafting gameplay effects;
- recipe or production-balance authoring;
- direct SCR debit or INV grant;
- a second reward/generation algorithm;
- dismantling or salvage;
- Hub-owned scene registration, build settings, or changes to other screens.

## Rollback

Delete the CRAFTUI-001 application-presentation, UI, standalone scene, focused tests, their Unity metadata, and this document. No CRA, SCR, INV, GEN, RAP, Hub payload, recipe, equipment, ProjectSettings, or Packages migration is required.
