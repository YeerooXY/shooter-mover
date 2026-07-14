# Stage 1 No-Player-Motion Recoil Amendment

**Status:** selected by the human lead; authoritative when this amendment pull request merges
**Scope:** Stage 1 combat planning, evidence, and downstream task dependencies
**Architecture impact:** weapon fire never changes player movement authority

## Decision

Weapon fire, including the Rocket Launcher and any future heavy weapon, must
not push the player, alter player velocity or position, consume or redirect a
thruster burst, change wall/contact resolution, or otherwise enter player
movement authority.

Existing `WeaponRecoilState` and `RecoilInfluence` contract/profile fields
remain compatibility and presentation data. They have no player-motion effect.
No Stage 1 task may consume them through `MovementActor2D`, `Rigidbody2D`, a
second velocity writer, or another movement ingress. A later, separately owned
presentation task may use an approved value for visual-only weapon kick,
particles, animation, or optional camera feedback, subject to accessibility
settings; this amendment authorizes none of that work.

## Task-splitting effect

- Retire `CB-007`; its requested combat-to-movement bridge is intentionally
  out of scope and must not be replaced by an MT-owned influence port.
- Remove `CB-007` from the combat batch, canonical backlog, and deferred
  full-MVP index. Its ID is historical and must not be reused.
- Remove the `CB-007 -> CB-011` dependency. Revise `CB-011` evidence to prove
  that firing leaves player movement, thruster, and collision authority
  unchanged.
- Later weapon, enemy, accessibility, reliability, and gate tasks therefore
  remain reachable through `CB-011` without a physical-recoil prerequisite.
- Do not rewrite already merged runtime contracts or add a visual-recoil
  feature in this planning amendment.

## Budget effect

Removing the 0.40 focused-lead-day `CB-007` task reduces direct S1.2
combat-foundation spend from 4.50 to 4.10 days. Combined with the amended
five-weapon workload, S1.2 planned direct spend becomes 8.70 of 10 days,
leaving 1.30 days for human review and bounded follow-up.

## Verification

The task-batch validator must confirm a 102-task acyclic Stage 1 backlog with
no remaining dispatchable or generated `CB-007` references. Regenerated
backlog output must match the updated batches. The amendment is planning-only
and adds no Unity runtime, scene, prefab, content, or presentation
implementation.
