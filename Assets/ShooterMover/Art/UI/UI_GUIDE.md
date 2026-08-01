# Shooter Mover UI guide

This is the naming and folder contract for the content-first UI pass.

The current goal is not final artwork. Screens should become usable, readable and
consistent while temporary generated art is still being used. Final redraws can then
replace the images without changing scene routing, game state or screen code.

## Architecture rule

The flow is:

```text
Game/session state
    -> existing screen controller or menu
    -> scene screen component
    -> reusable UI components
    -> sprites
```

For the first screen:

```text
StrongboxMenu
    -> StrongboxScreen
    -> ItemCard
    -> AugmentList
    -> AugmentRow
```

`StrongboxMenu` remains responsible for the existing opening session, authority calls,
retries and navigation. `StrongboxScreen` owns only uGUI presentation and input.
Reusable components never grant rewards, spend currency, mutate inventory or save data.

## Folder contract

```text
Assets/ShooterMover/
├── Art/
│   ├── Content/
│   │   ├── Guns/
│   │   └── Strongboxes/
│   └── UI/
│       ├── Common/
│       └── Screens/
│           └── StrongboxOpening/
├── Prefabs/
│   └── UI/
│       ├── Common/
│       └── StrongboxOpening/
├── Resources/
│   └── GunArt/
├── Scenes/
│   └── StrongboxOpening/
└── UI/
    ├── Common/
    └── StrongboxOpening/
```

Use the folders as follows:

- `Art/Content`: pictures of game things, such as guns and strongboxes.
- `Art/UI/Common`: shared frames, buttons, currency icons and bars.
- `Art/UI/Screens/<Screen>`: artwork used by one screen only.
- `Prefabs/UI/Common`: reusable UI prefabs.
- `Prefabs/UI/<Screen>`: complete screen prefab or screen-only pieces.
- `Resources/GunArt`: keep current runtime weapon side art here until the existing gun
  art resolver is deliberately migrated.
- `UI/Common`: reusable view scripts.
- `UI/<Screen>`: the screen component and existing screen-specific code.

Do not place gameplay definitions, prices, reward values or other authored game data in
an art or prefab folder.

## Image names

Use lowercase snake case.

Temporary generated images:

```text
<subject>_<view>_ai.png
```

Final redrawn images:

```text
<subject>_<view>.png
```

Examples:

```text
rattler_side_ai.png
steel_strongbox_closed_ai.png
steel_strongbox_open_ai.png
strongbox_opening_bg_ai.png
icon_money_ai.png
icon_scrap_ai.png
reward_card_frame_ai.png
button_primary_ai.png
```

When an image is redrawn, replace `_ai` with the clean final name. Do not create names
such as `final`, `final2`, `new`, `latest` or dates.

Useful view words:

- `side`: side-profile content art.
- `icon`: compact square icon.
- `closed` / `open`: strongbox state.
- `bg`: full-screen or panel background.
- `frame`: border or card frame.
- `fill`: bar fill.

## Scene, prefab and code names

Use PascalCase and keep the same game-facing noun across files:

```text
StrongboxOpening.unity
StrongboxScreen.prefab
StrongboxScreen.cs
ItemCard.prefab
ItemCard.cs
AugmentRow.prefab
AugmentRow.cs
```

Future canonical scene names should follow the same pattern:

```text
MainMenu.unity
Map.unity
Shop.unity
Crafting.unity
InventoryLoadout.unity
Skills.unity
Results.unity
```

Avoid names containing `Manager`, `System`, `Framework`, `Bridge`, `PresenterV2` or
implementation details unless the object genuinely has that responsibility.

## Sprite import settings

For screen and card images:

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single`
- Alpha Is Transparency: enabled
- Mesh Type: `Full Rect`
- Filter Mode: `Bilinear`
- Wrap Mode: `Clamp`
- Compression: `None` for small UI pieces; normal compression is acceptable for large
  temporary backgrounds

The first uGUI canvases use a `1920 x 1080` reference resolution with
`Match Width Or Height = 0.5`.

## First Strongbox image set

Only create these now:

```text
Art/Content/Strongboxes/<tier>_strongbox_closed_ai.png
Art/Content/Strongboxes/<tier>_strongbox_open_ai.png
Art/UI/Screens/StrongboxOpening/strongbox_opening_bg_ai.png
Art/UI/Common/icon_money_ai.png
Art/UI/Common/icon_scrap_ai.png
```

The background and shared icons are optional for the first functional pass. Flat-color
uGUI panels are acceptable. Weapon side images continue to use:

```text
Resources/GunArt/<weapon>_side_ai.png
```

Do not pause screen implementation to create decorative corners, separators, particles,
rarity frames or a complete icon library. Add those only when a screen genuinely needs
them.
