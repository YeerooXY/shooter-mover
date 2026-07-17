# SKILL-003 — Interactive session for real skill effects

## Objective

Plan and implement the first real player skill effects. Skill ranks must change gameplay through a reusable modifier/effect projection, not merely update the Skills screen.

## Interactive protocol

Phase 1: inspect XP, movement, combat, weapon, ability, and HUD authorities. Propose a small approved starter set, such as weapon damage, fire-rate, armor, maximum health, boost recharge, and pickup radius. Define stacking, rank curves, category gates, and whether effects apply before or during a run.

Stop and wait for explicit approval. Phase 2: implement only the approved effects and their runtime projection.

## Dependencies

SKILL-002 and the relevant combat/movement contracts.

## Acceptance

- Skill effects are data-driven and deterministic.
- Rank changes produce a versioned player-stat snapshot consumed by runtime systems.
- No skill directly edits a weapon, scene object, or UI singleton.
- Existing skill allocation remains exactly-once.
- Tests prove rank 0/1/max behavior, stacking, and rejected allocations.

## Validation

Focused EditMode tests, one PlayMode proof showing an approved skill changing a live demo stat, and a manual before/after capture. No additional skill effects may be invented after approval.
