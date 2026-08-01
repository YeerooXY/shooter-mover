# Strongbox Opening UI setup

This is the first screen in the gradual UI replacement. The existing
`StrongboxMenu` remains the source of opening state and actions. The new
`StrongboxScreen` disables only its old IMGUI drawing/input and drives the same session.

## 1. Open the canonical scene

Open:

```text
Assets/ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity
```

Keep the existing `StrongboxOpening` object and its `StrongboxMenu` component. Do not
replace its runtime binding, session or navigation fields.

## 2. Add the Canvas

Create a `Canvas` named `StrongboxScreen`.

Canvas settings:

```text
Render Mode: Screen Space - Overlay
Canvas Scaler / UI Scale Mode: Scale With Screen Size
Reference Resolution: 1920 x 1080
Screen Match Mode: Match Width Or Height
Match: 0.5
```

Add `StrongboxScreen.cs` to the Canvas object.

Create one `EventSystem` using the Input System UI input module if the scene does not
already contain one.

## 3. Build this hierarchy

```text
StrongboxScreen                        [Canvas, StrongboxScreen]
├── Background                         [Image]
├── Panel                              [Image]
│   ├── Header
│   │   ├── Title                       [Text]
│   │   ├── Tier                        [Text]
│   │   └── Preview                     [Text]
│   ├── Status                          [Text]
│   ├── BoxArea
│   │   ├── Box                         [Image]
│   │   └── Progress
│   │       ├── Track                   [Image]
│   │       └── Fill                    [Image, Filled/Horizontal]
│   ├── Rewards                         [ScrollRect]
│   │   └── Viewport                    [Image, Mask]
│   │       └── Cards                   [VerticalLayoutGroup, ContentSizeFitter]
│   └── Action                          [Button]
│       └── Label                       [Text]
```

Suggested first-pass sizes at 1920 x 1080:

```text
Panel: 1400 x 900
Header: 1280 x 90
BoxArea: 1280 x 260
Rewards viewport: 1280 x 390
Action: 360 x 72
```

Use flat colors first. Do not wait for final frames or decoration.

## 4. Create `AugmentRow.prefab`

Create under:

```text
Assets/ShooterMover/Prefabs/UI/Common/AugmentRow.prefab
```

Hierarchy:

```text
AugmentRow                         [Image, HorizontalLayoutGroup, AugmentRow]
├── Icon                           [Image]
├── Name                           [Text]
└── Level                          [Text]
```

Wire the three fields on `AugmentRow`.

Recommended size: `720 x 42`.

## 5. Create `AugmentList.prefab`

Create under:

```text
Assets/ShooterMover/Prefabs/UI/Common/AugmentList.prefab
```

Hierarchy:

```text
AugmentList                        [VerticalLayoutGroup, AugmentList]
└── Rows                           [RectTransform]
```

Assign `Rows` and `AugmentRow.prefab` to `AugmentList`.

The list creates only as many rows as the item has augments. It must not create empty
slot rows merely to fill space.

## 6. Create `ItemCard.prefab`

Create under:

```text
Assets/ShooterMover/Prefabs/UI/Common/ItemCard.prefab
```

Hierarchy:

```text
ItemCard                           [Image, HorizontalLayoutGroup, ItemCard]
├── Art                            [Image]
├── Text
│   ├── Title                      [Text]
│   ├── Subtitle                   [Text]
│   ├── Detail                     [Text]
│   └── Quantity                   [Text]
└── Augments                       [AugmentList prefab]
```

Wire `Art`, the four text fields and `Augments` on `ItemCard`.

Recommended size: `1200 x 180`. Set the Art image to preserve aspect ratio.

## 7. Wire `StrongboxScreen`

On the Canvas `StrongboxScreen` component, assign:

```text
Menu            -> existing StrongboxOpening / StrongboxMenu
Title Text      -> Header / Title
Tier Text       -> Header / Tier
Status Text     -> Status
Preview Text    -> Header / Preview
Box Image       -> BoxArea / Box
Closed Box      -> <tier>_strongbox_closed_ai.png
Open Box        -> <tier>_strongbox_open_ai.png
Progress Fill   -> BoxArea / Progress / Fill
Cards Root      -> Rewards / Viewport / Cards
Card Prefab     -> ItemCard.prefab
Action Button   -> Action
Action Text     -> Action / Label
```

The screen will:

- show OPEN, RETRY or CONTINUE at the correct stage;
- animate the existing opening progress;
- reveal the existing reward list one card at a time;
- reuse the current gun art resolver;
- call the existing `StrongboxMenu` methods rather than owning reward logic.

## 8. Save the screen prefab

Drag the completed Canvas to:

```text
Assets/ShooterMover/Prefabs/UI/StrongboxOpening/StrongboxScreen.prefab
```

Keep the prefab instance in the canonical scene and save the scene.

## 9. First test

Run the scene unbound in preview mode and verify:

1. only the new Canvas is visible;
2. OPEN starts the existing opening sequence;
3. the progress fill moves;
4. reward cards appear one by one;
5. CONTINUE works;
6. Enter/Space and controller South confirm;
7. Escape/Backspace and controller East return only at the final stage;
8. no currency, inventory or save state is created by the screen itself.

## Deliberate first-pass limit

`StrongboxRewardRevealItem` currently exposes augment capacity, count and shared level,
but not each augment's actual definition/name. The common `AugmentList` is ready, but the
screen intentionally does not invent placeholder augment rows. The next targeted data
change should project the real installed augment names into the card and then populate
exactly those rows.
