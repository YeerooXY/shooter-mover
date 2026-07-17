# SKILLUI-001 — Functional Skills screen

## Purpose

SKILLUI-001 presents the merged XP-001 and SKILL-001 authorities as a functional Hub destination. It does not own player level, skill points, skill ranks, route state, or gameplay effects.

## Authority composition

`SkillsScreenSessionV1` receives three existing objects:

1. the exact immutable `PlayerRouteProfilePayloadV1` from HUB-001;
2. the persistent `IPlayerExperienceAuthorityV1` from XP-001;
3. the persistent `SkillProgressionAuthorityV1` from SKILL-001.

Before each projection or allocation, the session synchronizes the SKILL-001 player-level input from `XP.CurrentState.Level`. Total points come from `XP.CurrentState.TotalSkillPointsAwarded`. Spent ranks, available points, operation replay protection, prerequisites, and caps remain SKILL-001 authority behavior.

The screen never writes a rank dictionary, point balance, or route payload. `Back()` returns the exact incoming payload object.

## Projection

Each of the 20 catalog definitions projects:

- stable skill ID;
- display name;
- description;
- prerequisite skill ID, required rank, current prerequisite rank, and satisfaction state;
- current and maximum rank;
- one display state: `Locked`, `Available`, `Purchased`, or `Capped`;
- `CanAllocate` and a stable blocking code.

State precedence is:

1. `Capped` when current rank equals maximum rank;
2. `Locked` when the prerequisite rank is missing;
3. `Purchased` when rank is positive and not capped;
4. `Available` otherwise.

A node may remain visually `Available` or `Purchased` while `CanAllocate` is false because XP-001 has no available point. This preserves the semantic difference between progression locks and an empty point balance.

## Allocation commands

`SkillsScreenSessionV1.Allocate(operationId, skillId)` delegates directly to `SkillProgressionAuthorityV1.Allocate` and returns the real `SkillMutationFactV1` plus a refreshed immutable projection.

The UI surfaces:

- `Applied`;
- `DuplicateNoChange`;
- `InsufficientPoints`;
- `PrerequisiteMissing`;
- `RankCapped`;
- invalid or unknown input.

UI-created commands use unique operation identities. Tests and external callers may supply an explicit operation identity to verify replay behavior. A repeated applied operation spends no additional point because replay protection remains in SKILL-001.

## Hub and revisit flow

`SkillsHubDestinationAdapterV1` implements the existing HUB destination adapter contract. When HUB presents `HubRouteV1.Skills`, it creates a fresh presentation session over the same persistent XP and SKILL authorities. Presenting any other route hides the screen.

The controller invokes `ISkillsScreenNavigationPortV1.ReturnToHub` with the exact incoming route payload. A production composition root should implement that callback with HUB-001 navigation. Revisit rebuilds from current authority snapshots, so allocation state survives without local caches.

## Backplate and controls

`Assets/ShooterMover/Art/UI/Skills/skills_demo_screen.png.bytes` is a base64 PNG TextAsset. It is drawn as a passive backplate only. All labels, state badges, allocation buttons, scroll behavior, keyboard/controller Back handling, and navigation dispatch are real IMGUI overlays.

The standalone `Assets/ShooterMover/Scenes/Flow/Skills/Skills.unity` scene enables an authoring preview. It composes actual XP-001 and SKILL-001 implementations with a level-20 preview state; this preview is not the production player save.

## Focused tests

EditMode filter:

```text
ShooterMover.Tests.EditMode.Skills.Presentation
```

Covers XP totals, all definition fields, all four node states, real allocation, duplicate operation replay, insufficient points, missing prerequisites, max rank, exact Back payload identity, and revisit projection.

PlayMode filter:

```text
ShooterMover.Tests.PlayMode.Flow.Skills
```

Covers controller allocation feedback, duplicate input, one-shot Back dispatch, exact payload return, Hub hide/show, and revisit over persistent authority state.

## Manual proof checklist

1. Open `Assets/ShooterMover/Scenes/Flow/Skills/Skills.unity`.
2. Confirm level, total points, spent points, and available points are visible.
3. Confirm all 20 IDs, names, descriptions, prerequisites, ranks, and states are shown by code overlays.
4. Allocate a root skill and verify one point is spent and the next prerequisite unlocks.
5. Reach rank five and verify the node is capped.
6. Exhaust points and verify otherwise-unlocked nodes show `NO POINTS`.
7. In production composition, navigate Hub → Skills → Back → Skills and verify the exact route payload and authority ranks persist.

## Non-goals

- no skill gameplay effects or combat modifiers;
- no XP grant or level mutation from the screen;
- no alternative rank/point persistence;
- no HUB route authority replacement;
- no changes to SKILL-001 or XP-001 production algorithms.
