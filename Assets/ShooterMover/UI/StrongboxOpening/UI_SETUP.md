# Strongbox Opening UI setup

The canonical Strongbox opening now uses a vertical cinematic reel. The existing
`StrongboxMenu` still owns the real opening command, reward application, persistence,
retry behavior and navigation. The reel is presentation only.

## Architecture

```text
StrongboxMenu
    -> StrongboxScreen
        -> StrongboxRoller
            -> WeaponCard instances
        -> secondary ItemCard instances
```

The actual weapon is resolved before the reel is prepared. `StrongboxRollPlanner`
creates one immutable presentation sequence, inserts the real weapon at the winning
index, and samples one deterministic tension offset. The roll never rerolls the reward.

## 1. Open the canonical scene

```text
Assets/ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity
```

Keep the existing `StrongboxOpening` object and its `StrongboxMenu` component.

## 2. Add the Canvas

Create a Canvas named `StrongboxScreen`.

```text
Render Mode: Screen Space - Overlay
UI Scale Mode: Scale With Screen Size
Reference Resolution: 1920 x 1080
Screen Match Mode: Match Width Or Height
Match: 0.5
```

Add `StrongboxScreen.cs` to the Canvas. Add one EventSystem using the Input System UI
module if the scene does not already contain one.

## 3. Build the screen hierarchy

```text
StrongboxScreen                              [Canvas, StrongboxScreen]
├── Background                               [Image]
├── Header
│   ├── Title                                [Text]
│   ├── Tier                                 [Text]
│   ├── Status                               [Text]
│   └── Preview                              [Text]
├── ReelViewport                             [Image, RectMask2D]
│   ├── Cards                                [RectTransform, StrongboxRoller]
│   └── SelectorLine                         [Image]
├── SecondaryRewards
│   └── Cards                                [VerticalLayoutGroup]
├── TestModes
│   ├── Cinematic                            [Button]
│   ├── Fast                                 [Button]
│   ├── RevealOnly                           [Button]
│   └── Replay                               [Button]
└── Action                                   [Button]
    └── Label                                [Text]
```

The selector is one thin horizontal line. Do not use a moving rectangle or selection
frame. The reel moves behind the fixed line.

## 4. Create `WeaponCardTheme.asset`

Create:

```text
Create > Shooter Mover > UI > Weapon Card Theme
```

Save it under:

```text
Assets/ShooterMover/Art/UI/Common/WeaponCardTheme.asset
```

Add five entries:

```text
Common      grey
Rare        blue
Epic        green
Legendary   yellow
Mythic      red
```

A plain white background sprite tinted by the theme is enough for this pass. A final
redraw can later replace each background with a rarity-specific frame or gradient.

## 5. Create `WeaponCard.prefab`

Save under:

```text
Assets/ShooterMover/Prefabs/UI/Common/WeaponCard.prefab
```

Hierarchy:

```text
WeaponCard                                 [RectTransform, WeaponCard, CanvasGroup]
├── Glow                                   [Image]
├── Background                             [Image]
├── WeaponArt                              [Image]
└── Details
    ├── Name                               [Text]
    ├── Rarity                             [Text]
    ├── Level                              [Text]
    └── Augments                           [AugmentList prefab]
```

Recommended card size:

```text
Width:  404
Height: 154
```

Wire all fields on `WeaponCard` and assign `WeaponCardTheme.asset`.

The transparent weapon PNG belongs only in `WeaponArt`. The reel moves and scales the
card root. Do not animate, recolor or distort the weapon sprite itself.

During `Roll` display, Details and Augments are hidden. During `Reveal`, the same card
shows its name, rarity, level and augment list.

Add an `AugmentSlotGrid` under `Details` (or beside the augment list) for the clean
capacity presentation. Assign a `GridLayoutGroup` root, a 64x64 `Image` slot prefab,
and these sprites:

```text
Assets/ShooterMover/Art/UI/Common/augment_slot_empty_ai.png
Assets/ShooterMover/Art/UI/Common/augment_slot_filled_ai.png
```

The grid is driven by the equipment's real augment capacity and installed count. It
uses four columns by default, matching the weapon maximum; gear can use three or
fewer cells. Fresh strongbox equipment therefore shows clean empty cells; no augment
is invented by the presentation. Installed augment definitions can replace filled
cells in a later projection update.

## 6. Create the augment prefabs

### `AugmentRow.prefab`

```text
AugmentRow                                 [Image, HorizontalLayoutGroup, AugmentRow]
├── Icon                                   [Image]
├── Name                                   [Text]
└── Level                                  [Text]
```

Save under:

```text
Assets/ShooterMover/Prefabs/UI/Common/AugmentRow.prefab
```

### `AugmentList.prefab`

```text
AugmentList                                [VerticalLayoutGroup, AugmentList]
└── Rows                                   [RectTransform]
```

Save under:

```text
Assets/ShooterMover/Prefabs/UI/Common/AugmentList.prefab
```

Only actual augments create rows. Empty augment slots are never displayed.

## 7. Configure `StrongboxRoller`

Put `StrongboxRoller` on `ReelViewport/Cards` and assign:

```text
Cards Root  -> ReelViewport/Cards
Card Prefab -> WeaponCard.prefab
```

The frozen cinematic baseline is already serialized in `StrongboxRollSettings`:

```text
calm          0.50
acceleration  1.50
fullRoll      2.05
slowdown      6.60
lock          0.50
winnerScale   1.15
rarityHold    0.20
reveal        0.40
finish        0.70
```

Other baseline values:

```text
entry count             96
winner index            88
card height             154
card gap                 14
start index             2
acceleration end index  22
full-roll end index     61
edge padding             6 px
winner final scale       1.26
winner overshoot         1.32
```

Fast mode halves every duration. Reveal Only starts with the real winner centered and
plays the normal-speed winner scale and reveal.

The six-second slowdown ends at a continuous random position inside the already-chosen
winner. The lock phase then moves that exact card to the exact center of the selector.

## 8. Wire `StrongboxScreen`

Assign:

```text
Menu                  -> existing StrongboxOpening / StrongboxMenu
Roller                -> ReelViewport/Cards / StrongboxRoller
Title Text            -> Header/Title
Tier Text             -> Header/Tier
Status Text           -> Header/Status
Preview Text          -> Header/Preview
Secondary Root        -> SecondaryRewards
Secondary Cards Root  -> SecondaryRewards/Cards
Secondary Card Prefab -> ItemCard.prefab
Action Button         -> Action
Action Text           -> Action/Label
Cinematic Button      -> TestModes/Cinematic
Fast Button           -> TestModes/Fast
Reveal Only Button    -> TestModes/RevealOnly
Replay Button         -> TestModes/Replay
```

The testing buttons are intentionally on this first screen. Production Settings can
later provide the saved default mode and the visible test controls can be removed.

## 9. Save the screen prefab

```text
Assets/ShooterMover/Prefabs/UI/StrongboxOpening/StrongboxScreen.prefab
```

Keep its instance in `StrongboxOpening.unity` and save the scene.

## 10. First test

Verify:

1. OPEN resolves the reward once before the roll starts.
2. The visible sequence does not change when OPEN is pressed.
3. Cards remain visible through acceleration and full roll.
4. The fixed horizontal line selects the item crossing it.
5. The slowdown ends at varying positions inside the winning card.
6. Lock moves the same card to the exact center.
7. Only the card root enlarges; the transparent weapon sprite remains unchanged.
8. The final rarity always matches the card that landed.
9. Replay uses the same sequence, winner and tension offset without granting again.
10. Cinematic, Fast and Reveal Only all reveal the same reward.
11. Scrap and other secondary rewards appear after the weapon reveal.
12. CONTINUE uses the existing Strongbox navigation path.

## Current limitation

The reward projection still exposes augment count and shared level but not each installed
augment's display name. `WeaponCard` and `AugmentList` are ready for real rows; the next
small projection change should pass the installed augment definitions through without
inventing placeholder names.
