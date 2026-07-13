# Shooter Mover — Product Discovery Batch D-191 through D-194

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`, persisted early because the user requested a context handoff.

## D-191 — Four-player co-op target with staged validation

- Status: accepted
- Choice: B — architect for up to four players while validating two players first
- Accepted requirement: Future co-op supports parties of up to four players, but implementation may first prove networking, lives, rewards, room synchronization, and host continuity with two players before graduating through three- and four-player testing.
- Architecture rule: Player collections, protocol messages, interface layouts, save ownership, session state, and encounter contracts must not assume a permanent two-player maximum.
- Validation rule: Two-player success is an intermediate milestone rather than proof that four-player projectile density, effects, bandwidth, room readability, and balance are complete.
- Delivery rule: Co-op remains post-MVP relative to the one-level offline Windows slice.

## D-192 — Relay-backed latency-tolerant player-hosted PvE networking

- Status: accepted
- Choice: B — player-hosted authority through relay with explicit latency-tolerance work
- Accepted requirement: Online co-op should use a relay-assisted player-hosted model rather than mandatory dedicated servers, while treating responsive cross-region play as a product requirement.
- Responsiveness rule: Each player's own movement, aiming, boosting, firing, and immediate presentation should respond locally without waiting for a round trip whenever the trusted cooperative model permits it.
- Reality rule: Cross-country play cannot be literally lag-free. The target is responsive and enjoyable EU-to-USA-style play with smoothing, anticipation, reconciliation, adaptive buffering, and packet-loss handling where necessary.
- Testing rule: Multiplayer validation must simulate latency, jitter, packet loss, and asymmetric connections early, including severe divergence cases where enemies or attacks appear on only one player's client.
- Tradeoff rule: Occasional reconciliation or imperfect remote-state fidelity is acceptable when it materially improves the player's immediate experience.
- Continuity rule: Preserve the later requirement for host migration, transferable authority, or an equivalent method that lets remaining players continue if the original host leaves.

## D-193 — Local authority for each player's immediate experience

- Status: accepted
- Choice: custom — local-first trusted cooperative simulation
- Accepted requirement: A player's own client resolves the feel-sensitive parts of that player's experience locally, including movement, firing, taking damage, pickups, loot acquisition, and immediate inventory or combat feedback.
- Feel rule: Do not delay ordinary movement, damage response, or loot feedback merely to obtain strict centralized moment-to-moment authority in friendly PvE.
- Trust rule: The initial co-op model may accept greater client trust and weaker anti-cheat guarantees than a competitive game in exchange for responsiveness.
- Objective boundary: Deep team objectives requiring exact shared moment-to-moment simulation are deferred beyond the first co-op MVP.
- Shared-state rule: The session still needs a minimal canonical layer for navigation, room access, coarse completion, teleports, major rewards, campaign state, and other facts that cannot safely diverge forever.
- Future rule: Stronger validation or authority may be added later where public matchmaking, abuse, or shared-objective complexity creates a demonstrated need.

## D-194 — Optional shared-room entry with local room instances and coarse confirmation

- Status: accepted
- Choice: C with a custom room-instance extension
- Accepted requirement: Moment-to-moment combat remains locally simulated, while a lightweight shared layer confirms coarse facts such as room entry, encounter identity, doors, room completion, map access, and major rewards.
- Entry rule: Players may choose to enter a room instance already entered by teammates, allowing them to fight together in one local encounter context.
- Split-instance rule: Players may also enter their own local version of an available room rather than being forced into the teammate's active instance.
- Scaling rule: Active-player-count encounter scaling is determined for each entered instance at its start and remains stable during that engagement.
- Divergence rule: Enemy positions, targeting, hits, pickups, and exact death timing may differ between local simulations; the coarse shared layer must prevent permanent disagreement about accessible routes and durable campaign progress.
- MVP boundary: Exact shared team-objective simulation, strict bullet-level agreement, and advanced reconciliation between parallel room instances are not required for the first co-op MVP.
- Open rule: The next decision must define how global room completion, doors, and rewards settle when players use joined or separate local instances.

## Batch persistence

- Persisted through: D-194
- Unsaved decisions after checkpoint: 0
- Reason for early persistence: explicit context-handoff request
- Remaining boundary: queue D-195 through D-200 and persist at D-200
