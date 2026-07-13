# Shooter Mover — Product Discovery Decisions D-101 through D-110

Status: verified and persisted extension of `assembly/intake/LIVE_DECISIONS.md`.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Decision range: D-101 through D-110
- Verification status: verified directly by the user
- Unsaved accepted decisions after this batch: 0
- Next persistence boundary: D-120

### D-101 — Chainable independently regenerating thruster charges

- Status: accepted
- Choice: B — two or three independently regenerating charges
- Accepted requirement: The signature directional thruster uses a small bank of independently regenerating charges that may be chained for rapid direction changes, aggressive pursuit, emergency escapes, traversal, and speedrun routing.
- Prototype rule: The exact baseline count, recharge duration, burst distance, acceleration curve, and chain cadence remain tunable playtest variables rather than prematurely fixed constants.
- Accessibility rule: Easier modes may regenerate charges faster or provide clearer charge feedback without changing the recognizable movement mechanic.
- Anti-spam rule: Spending the complete charge bank creates a real temporary vulnerability window; tuning must prevent near-continuous panic boosting while preserving frequent deliberate use.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-102 — Tiny startup forgiveness window rather than full invulnerability

- Status: accepted
- Choice: B — brief bounded forgiveness at burst startup
- Accepted requirement: Thruster survival primarily comes from physical displacement, but the burst may provide a very short startup forgiveness window or small temporary hitbox reduction so well-timed borderline dodges feel responsive.
- Boundary rule: The mechanic is not full-duration universal invulnerability. Walls, contact hazards, sustained beams, persistent area effects, and explosions may use explicit authored interactions.
- Difficulty rule: Easier modes may widen the forgiveness window slightly; extreme modes may reduce or remove it while preserving the same displacement physics.
- Feedback rule: The protected instant and the attacks that bypass it must be communicated clearly enough to support mastery rather than hidden immunity guessing.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-103 — Thruster direction follows movement input

- Status: accepted
- Choice: A — movement input controls boost independently from aim
- Accepted requirement: Thruster direction is selected through current movement input rather than weapon aim, preserving true twin-stick combat in which the player may shoot forward while boosting sideways, backward, or diagonally.
- Platform rule: The model maps directly to keyboard movement, gamepad movement stick, and later Android movement controls plus a boost action.
- Neutral-input rule: A no-input boost must use a deterministic fallback. The current working direction is the last valid movement direction, subject to prototype feel testing rather than aim direction.
- Mastery rule: Independent movement and aim support circle-strafing, retreats, corner cuts, aggressive fly-bys, and precise routing.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-104 — Limited steering during the burst

- Status: accepted
- Choice: B — strong launch commitment with bounded correction
- Accepted requirement: The initial movement direction defines most of the burst trajectory, while continued movement input may gently curve or correct the path before the burst ends.
- Commitment rule: Mid-burst steering cannot permit instant reversals, sharp U-turns, or unrestricted free-flight behaviour.
- Accessibility rule: Bounded steering provides useful correction for controller and later touchscreen imprecision without removing the importance of initial direction.
- Prototype rule: Steering strength, response curve, and duration remain feel-test variables.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-105 — Controlled wall ricochet

- Status: accepted
- Choice: C — predictable rebound rather than a hard stop
- Accepted requirement: A thruster burst striking a wall may rebound the mech rather than always ending immediately. Shallow impacts may skim while steeper impacts create stronger deflection.
- Feel rule: Ricochets should have high replay and mastery potential but must remain predictable enough that successful players understand and intentionally reproduce them.
- Safety rule: Speed caps, corner handling, collision iteration limits, spawn safety, cramped-room tests, and anti-stuck safeguards are mandatory.
- Scope rule: The concept requires careful testing and is not permission to turn every enclosed room into uncontrolled pinball movement.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-106 — Geometry-led ricochet with limited steering influence

- Status: accepted
- Choice: B — physical reflection shaped slightly by player input
- Accepted requirement: Surface angle and incoming velocity establish the main rebound direction. Movement input may bend the exit angle within a limited range.
- Mastery rule: Geometry must remain important enough for players to learn wall angles and routes, while steering influence supports intentional correction and expressive wall-tech.
- Exploit rule: Steering cannot make the wall normal irrelevant or convert every collision into a free arbitrary direction reset.
- Prototype rule: Input influence strength remains a tuning variable.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-107 — Bounded ricochet extension as a later prototype experiment

- Status: accepted with scope guard
- Choice: B — a successful ricochet may grant a small bounded movement extension
- Accepted direction: A valid wall impact may eventually restore a limited amount of burst time or momentum, with strict caps or diminishing returns for chained impacts.
- Scope guard: This is a promising prototype direction, not a mandatory first-playable requirement. Core movement, firing, collisions, ordinary boosting, enemies, and the basic game loop must reach a usable state before deep commitment or extensive tuning.
- Exploit rule: Infinite corridor bouncing, permanent propulsion, encounter trivialization, unreachable map skips, and unstable physics chains are unacceptable.
- Evaluation rule: Retain the mechanic only if playtests show that it materially improves feel and replayability without dominating level design.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-108 — Basic predictable ricochet early; advanced wall-tech later

- Status: accepted
- Choice: B — test the foundation without front-loading the complete system
- First-playable requirement: Include a simple deterministic wall reflection early enough that collision architecture, room geometry, and base movement feel can be evaluated with ricochets present.
- Deferred scope: Ricochet extensions, diminishing chains, momentum optimization, special bonuses, elaborate effects, and deep exploit tuning follow only after the ordinary playable loop is functioning.
- Delivery rule: The initial ricochet may be visually and mechanically plain. It exists to validate the foundation, not to represent final polish.
- Relationship: D-107 remains an optional later experiment built on this simpler foundation.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-109 — Immediate independent charge regeneration

- Status: accepted
- Choice: A — every spent charge starts its own timer immediately
- Accepted requirement: Spending a thruster charge immediately begins that charge’s independent regeneration timer. Using another charge does not reset or pause earlier recharge progress.
- Flow rule: Frequent deliberate single boosts should remain available, while fully emptying the bank creates temporary exposure until timers complete.
- Readability rule: The HUD must communicate independent charge progress clearly without requiring players to inspect hidden queues.
- Prototype rule: Recharge duration is a primary anti-spam and feel-tuning lever.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-110 — Stable baseline capacity with rare access to one additional charge

- Status: accepted
- Choice: B — complete baseline mechanic plus bounded capacity growth
- Accepted requirement: Every normal build receives a stable baseline thruster charge count sufficient to express the signature movement system. Rare authored progression, a milestone choice, a specialized mech, or another tightly controlled build effect may add at most one additional charge.
- Balance rule: The additional charge must compete with genuinely valuable alternatives rather than becoming an automatic best-in-slot choice.
- Level-design rule: Required routes and ordinary encounters must be completable with the baseline capacity; optional advanced routes may reward the extra charge without requiring it for campaign progression.
- Competitive rule: Verified categories record, constrain, or normalize charge count when fair comparison requires it.
- Prototype rule: The first playable may use only the baseline count. The extra-charge source and exact baseline capacity remain later decisions informed by testing.
- Supersedes: none
- Source: guided Product Discovery recovery

## Next discovery state

Continue core-experience recovery with D-111 by deciding the base locomotion model between thruster bursts.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not treat D-107’s advanced extension as a first-playable commitment.
- Queue D-111 through D-120 and persist at D-120 unless the user changes the batching rule or a context handoff requires earlier persistence.
