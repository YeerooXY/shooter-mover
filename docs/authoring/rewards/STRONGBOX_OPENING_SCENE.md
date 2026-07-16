# BOXSCENE-001 — Strongbox opening scene

## Purpose

`Assets/ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity` is a standalone presentation scene for opening one existing BOX-001 strongbox instance. It owns reveal timing, input, layout, and navigation only.

Durable truth remains with the existing authorities:

- **BOX-001** freezes generation, opening identity, retry state, and terminal facts;
- **RAP-001** commits, claims, and applies the generated reward;
- **INV-001** owns equipment, armor, strongboxes, and miscellaneous holdings and consumes the opened box only after RAP is applied;
- **SCR-001** owns scrap balance;
- the existing money authority owns money balance.

The scene does not synthesize grants, mutate wallets, add inventory, consume strongboxes, or reproduce reward-generation logic.

## Reveal flow

The controller exposes four deterministic stages:

1. `BoxClosed`
2. `OpeningAnimation`
3. `RewardReveal`
4. `ContinueOrBack`

Opening animation duration, per-item reveal interval, completed-reveal hold, tier label/identity, and deterministic test seed are serialized on `StrongboxOpeningController`.

## Runtime binding

A composition root that owns the selected box should bind the already-created BOX service and immutable opening command:

```csharp
controller.BindRuntime(
    strongboxOpeningService,
    StrongboxOpenCommandV1.Create(
        openingStableId,
        runStableId,
        strongboxInstanceStableId,
        claimantStableId,
        moneyAuthorityStableId,
        scrapAuthorityStableId,
        holdingsAuthorityStableId),
    equipmentCatalog);
```

The controller delegates through `StrongboxOpeningRuntimePortV1`.

- The first user Open action submits one immutable opening command.
- If BOX reports `ClaimedPendingApplication` or `ConsumePending`, the Retry action resubmits the **same** command and identities.
- Once a terminal result is observed, the presentation port caches it. Repeated UI callbacks do not call BOX again.
- BOX remains the final exact-once defense: replaying the same opening returns `ExactDuplicateNoChange`, while another opening identity for the same physical box conflicts.

## Reward presentation

`StrongboxRewardRevealProjectorV1` reads the frozen `GeneratedOutcome.Payloads` returned by BOX.

- money and scrap are displayed as value rewards;
- miscellaneous and premium-ammunition grants use the miscellaneous card path;
- equipment payloads are expanded one card per immutable `EquipmentInstance`;
- definitions classified as `equipment-category.armor` are shown as armor;
- all other equipment definitions use the equipment card path;
- two equipment instances with the same definition remain two cards because instance identity is never grouped by definition.

Pass the accepted `EquipmentCatalog` to obtain authored display names and armor classification. With no catalog, equipment still reveals safely by definition and instance identity.

## Deterministic preview

The committed scene starts in an explicitly labeled, non-authoritative preview mode so artists and UI authors can inspect all card families without creating account value. The serialized seed deterministically changes preview instance suffixes. The preview includes two separate weapon instances sharing one definition, one armor item, money, scrap, and miscellaneous value.

Preview mode is never represented as an awarded runtime opening. Calling `BindRuntime(...)` replaces the preview session with the actual BOX-backed session.

## Controls

- Open / confirm: Enter, Space, controller South, or the on-screen button.
- Safe retry of a pending BOX/RAP/INV transaction: the same confirm inputs or the Retry button.
- Continue/back: Enter, controller South, Escape, Backspace, controller East, or the on-screen button.
- When `backScenePath` is empty, the controller emits `ContinueOrBackRequested` and leaves navigation to the composition root.

## Manual proof

1. Open `StrongboxOpening.unity` and enter Play Mode.
2. Confirm the tier identity, deterministic seed, four reveal stages, and preview-only warning are visible.
3. Open the preview and verify two Blaster Rifle cards have the same definition but different instance identities.
4. Bind a real owned strongbox, BOX service, immutable command, and equipment catalog.
5. Verify the real opening reveals only after BOX returns `Opened` or `ExactDuplicateNoChange`.
6. Trigger repeated Open callbacks and confirm only the retained result is shown.
7. Force a RAP/consume pending result and verify Retry uses the same command until terminal.
8. Confirm money, scrap, equipment, armor, and miscellaneous cards match the frozen payload.

## Validation command

Run the focused PlayMode assembly with Unity `6000.3.19f1`:

```text
-runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Rewards.Strongboxes
```

## Scope and rollback

The scene and runtime presentation are isolated to BOXSCENE-001-owned paths. `Stage1VisibleSlice.unity` and `Stage1VisibleSliceController.cs` are not referenced or modified.

Rollback removes the StrongboxOpening scene/UI/test folders and this document. No authority, save schema, balance data, Stage 1 scene, or existing strongbox record requires migration.
