# Shooter Mover — Product Discovery Batch D-111 through D-120

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-111 — Responsive hybrid base locomotion

- Status: accepted
- Choice: C — quick acceleration with bounded readable inertia
- Accepted requirement: Ordinary mech movement responds quickly enough for precise projectile dodging and aiming, but retains a small amount of physical momentum rather than changing velocity with perfectly instantaneous stops.
- Braking rule: Counter-steering should brake rapidly and predictably so the mech never feels slippery or difficult to place.
- Interaction rule: Recoil, thruster exits, ricochets, and authored weapon effects may influence motion without making the player wrestle the controls.
- Accessibility rule: Easier settings may strengthen braking assistance without secretly changing core route geometry or top-speed identity.
- Tuning note: Exact acceleration, braking, inertia, and directional response values remain playtest variables.

## D-112 — Brief boost-exit momentum with controlled decay

- Status: accepted
- Choice: C — preserve a short burst of exit momentum, then smoothly restore ordinary movement
- Accepted requirement: Thruster and ricochet velocity carries into ordinary movement for a brief readable window rather than disappearing instantly.
- Control rule: Steering and counter-input quickly pull the mech back toward normal movement limits.
- Mastery rule: Skilled players may exploit short-lived momentum for route optimization, corner cutting, and stylish transitions without making momentum tech mandatory for ordinary play.
- Tuning note: Exit carry duration, decay curve, and steering authority remain prototype variables.

## D-113 — Light-enemy shove-through with heavy blockers

- Status: accepted
- Choice: B — brush through or shove light enemies; heavy enemies remain solid
- Accepted requirement: Small and lightweight enemies may be pushed aside or softly deflected during a clean boost path so they do not constantly interrupt the signature movement flow.
- Weight rule: Heavy units, elites, bosses, and explicitly immovable enemies stop or strongly redirect the mech.
- Damage rule: Baseline boost contact is not automatically a major damage attack. Offensive ramming remains available as a later upgrade or specialised build direction.
- Communication rule: Enemy weight and collision response must be visually and mechanically legible.

## D-114 — Discrete contact hits with enemy variety

- Status: accepted
- Choice: B — one clear contact hit followed by brief protection from that same enemy
- Accepted requirement: Enemy contact damage is applied as readable discrete hits rather than uncontrolled frame-by-frame damage while bodies overlap.
- Encounter intent: Carelessly boosting into a crowd should remain potentially lethal. The intended response is to shoot a route open, route around the formation, or boost precisely through a safe gap.
- Enemy-variety rule: Different enemy classes may apply distinct contact consequences such as knockback, grabs, slowing, shield disruption, heavy impact, or other authored effects.
- Readability rule: Contact hits require strong audiovisual feedback and clear damage attribution.

## D-115 — Per-enemy contact grace

- Status: accepted
- Choice: B — repeat-hit grace is tracked independently for each enemy
- Accepted requirement: The same enemy cannot repeatedly grind the player down during one overlap, but different enemies may each inflict their own discrete contact hit.
- Crowd rule: A dense formation may therefore deal severe or lethal burst damage when entered recklessly.
- Fairness rule: A tiny simultaneous-hit aggregation window may prevent arbitrary frame-order differences without creating global immunity against the crowd.
- Verification rule: Competitive and replay outcomes must remain deterministic under identical collision state.

## D-116 — Light-enemy impacts drain boost momentum

- Status: accepted
- Choice: B — each collision progressively reduces the burst
- Accepted requirement: One or two light enemies may be shoved aside cleanly, while repeated impacts progressively reduce boost speed, steering authority, and remaining travel distance.
- Crowd rule: Packed formations can absorb the burst and leave a reckless player surrounded.
- Heavy-unit rule: Heavy enemies still stop or strongly deflect the mech rather than merely applying a small momentum tax.
- Feedback rule: Sparks, impact audio, animation, and visible speed response must communicate momentum loss immediately.

## D-117 — Bounded impact damage and stagger for shoved enemies

- Status: accepted
- Choice: B — collisions with walls, hazards, or other units may add modest consequences
- Accepted requirement: The initial shove causes little or no direct damage, but a light enemy slammed into a wall, hazard, or another unit may take bounded damage or stagger based on remaining boost momentum and enemy weight.
- Balance rule: Ramming and physics chains must not replace the four-weapon array as the dependable damage solution.
- MVP scope rule: The first usable build may implement displacement and stagger before detailed impact damage.
- Exploit rule: Collision damage and multi-enemy chains require strict caps and deterministic resolution.

## D-118 — Full four-weapon firing during boosts

- Status: accepted
- Choice: A — firing continues without a universal accuracy penalty
- Accepted requirement: All four mounted weapons continue their normal independent firing behaviour while the mech boosts.
- Accuracy rule: Boosting does not apply a universal spread, aim-lag, or accuracy penalty. Aim remains independent from movement direction.
- Fantasy rule: The intended signature presentation includes rocketing sideways or backward while the entire weapon array keeps firing.
- Exception rule: A genuinely exceptional weapon may define its own explicit behaviour, but boosting must never function as a general weapons-off state.

## D-119 — Boost speed linked to build movement speed and selected physical modifiers

- Status: accepted
- Choice: C — locomotion identity determines boost performance
- Accepted requirement: Thruster speed scales closely with the mech's underlying build movement speed rather than using one universal fixed burst velocity.
- Build rule: Faster movement builds produce faster boosts; slower or heavier builds produce correspondingly slower boosts.
- Override rule: Ordinary weapon drag and minor temporary slows are largely overridden so the signature escape action remains dependable.
- Exception rule: Clearly communicated severe slows, immobilization, or explicit anchored states may still reduce boost performance.
- Clarity rule: The game must classify which effects influence boost speed consistently and expose important effects to the player.

## D-120 — Boost activation replaces current velocity

- Status: accepted
- Choice: B — immediate arcade directional replacement
- Accepted requirement: Triggering a boost immediately replaces the mech's current velocity with the selected boost direction and speed instead of adding acceleration on top of prior momentum.
- Feel rule: Players may blaze sharply back and forth, reverse direction instantly, or chain perpendicular boosts without first overcoming existing travel velocity.
- Interaction rule: D-112 still applies after the burst ends: boost-exit speed briefly carries and then decays under the responsive ordinary movement model.
- Momentum boundary: Existing recoil, drift, ricochet velocity, and ordinary inertia affect movement until a new charge is activated; that new boost is an intentional hard directional command.
- Arcade priority: Responsiveness and expressive direction changes take precedence over strict physical conservation of momentum.

## Batch persistence

- Persisted through: D-120
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-130
