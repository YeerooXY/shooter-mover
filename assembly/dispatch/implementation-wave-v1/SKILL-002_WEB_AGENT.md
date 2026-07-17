# SKILL-002 — Variable skill trees and category gates

## Objective

Replace the fixed four-branch/five-skill assumption with authorable skill trees of different sizes. A default tree may contain 15 skills; a specialized or mixed tree may contain 5. Add requirements such as “8 invested points in Offense.”

## Dependencies

SKILL-001 must be accepted. Preserve player-level-derived points and exactly-once allocation.

## Owned paths

- `Assets/ShooterMover/Runtime/Domain/Progression/Skills/`
- `Assets/ShooterMover/Runtime/Application/Progression/Skills/`
- `Assets/ShooterMover/Tests/EditMode/Progression/Skills/`
- `docs/architecture/progression/`

Do not own production scenes or final UI composition.

## Acceptance

- Catalogs no longer require exactly 20 skills.
- A skill tree/category has an explicit identity and can contain any positive number of skills.
- Skills can have multiple prerequisite rules and category-point thresholds.
- Rejections are deterministic and explain which requirement failed.
- Existing default 20-skill behavior remains representable as a compatibility fixture.
- Tests cover 7 versus 8 category points, mixed requirements, rank caps, and duplicate operations.

## Validation

Focused Unity EditMode XML with zero failures, static compile, changed-file audit, and a manual catalog example containing 15-, 5-, and mixed-size trees.
