# Shooter Mover — Product Discovery Batch D-195 through D-200

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-195 — Hybrid personal room resolution with one-clear shared progress

- Status: accepted
- Choice: C — one valid clear settles coarse shared progress while personal instances finish independently
- Accepted requirement: The first valid local-instance clear marks the room globally complete and unlocks shared doors, map access, and teleports.
- Reward rule: The room's one-time major authored reward is issued once to every eligible connected player, including life-exhausted spectators, without duplicate granting from later local-instance clears.
- Local-resolution rule: Players already fighting in another local instance may finish for remaining personal ordinary loot or withdraw to the newly unlocked route.
- Personal-state rule: Enemies, pickups, unsecured loot, deaths, lives, banking state, and immediate feedback continue to resolve locally.
- Durability rule: A later local death, withdrawal, or divergent enemy state cannot relock globally completed progress.
- Anti-farming rule: Parallel instances must not duplicate the room's major reward or create permanent disagreement about durable campaign state.
- Future boundary: Deep shared-objective rooms may opt into stricter synchronization rules later.

## D-196 — Permanently clear late entries while mirroring qualifying non-XP rewards

- Status: accepted
- Choice: custom A — new entries find the room cleared, with mirrored personal reward events
- Accepted requirement: After the first valid global clear, a player who was not already fighting cannot start a fresh combat instance in that room during the same run.
- Existing-instance rule: Players already inside an active local instance may still finish or withdraw under D-195.
- XP rule: A non-participating player may miss personal kill or encounter XP because the globally completed room is not replayed for them.
- Reward-protection rule: When one player triggers a qualifying non-XP reward event such as a strongbox drop, matching personal reward events are created for the other eligible players so they do not miss box-like rewards.
- Ownership rule: Mirrored rewards remain individually owned and continue to obey personal unsecured-loot, death-loss, banking, deterministic grant, and save-transaction rules.
- Persistence rule: Respawn, teleport travel, reconnects, and later entry must consistently preserve the globally cleared state.
- Duplicate-prevention rule: A qualifying event may create at most one corresponding personal reward for each eligible player.

## D-197 — Fully independent personal reward rolls

- Status: accepted
- Choice: B — independently roll each player's complete reward
- Accepted requirement: A qualifying shared reward event gives every eligible player an independent personal roll, including the box tier and its eventual contents.
- Progression rule: Each roll uses the common mission identity, selected difficulty, and explicit challenge settings, plus only the reward owner's own account-level eligibility.
- Isolation rule: Teammate account level, equipment, performance, progression, and resulting rarity never modify another player's odds.
- Determinism rule: Each player receives a unique deterministic seed derived from the shared reward-event identifier and that player's stable identity, with reload-proof single granting.
- Fairness rule: Players may legitimately receive different tiers and very different items from the same shared event.
- Ownership rule: Every result remains personal and follows that owner's physical pickup, unsecured-loot, banking, death-loss, and persistence rules.

## D-198 — Physical personal drops with owner pickup responsibility

- Status: accepted
- Choice: custom A — owned rewards remain physical at their original drop location
- Accepted requirement: Each player's independently rolled reward appears as that player's personal physical drop at the location where the qualifying event occurred.
- Pickup rule: The owning player is responsible for reaching and collecting the drop; eligibility alone does not immediately place it into permanent inventory or a protected ledger.
- Theft rule: Other players cannot steal, consume, or redirect another player's personal drop.
- Risk rule: Until collected and later banked under the normal rules, the reward remains exposed to the intended run and extraction risks.
- Regrouping extension: A future forced-rendezvous or auto-teleport system may relocate or carry all uncollected personal boxes belonging to a teleported player so separation and trolling cannot make them permanently inaccessible.
- Deferred detail: Exact handling for spectators, unusual geometry, transition timing, and the visual presentation of relocated drops is deferred to co-op implementation and testing.

## D-199 — Vote-based forced rendezvous at valid transitions

- Status: accepted
- Choice: B — party vote after a readiness window
- Accepted requirement: At authored major transitions, ready players may initiate a visible regroup vote or countdown rather than allowing one separated player to block progression indefinitely.
- Warning rule: Players who are not ready receive a clear warning and a short collection or preparation window before teleportation.
- Guardrail rule: Forced regrouping is available only at valid progression transitions or equivalent authored points, not as an unrestricted combat escape.
- Loot-protection rule: When a player is forcibly teleported, that player's remaining owned strongboxes or equivalent protected personal drops travel with them or reappear safely at the destination.
- Anti-trolling rule: One malicious or idle player must not be able to stall the party forever, while one impatient player must not be able to teleport everyone arbitrarily.
- Tuning rule: Vote threshold, countdown length, combat cancellation, leader privileges, disconnect handling, and transition eligibility remain prototype-and-playtest variables.

## D-200 — Temporary reconnect reservation for uncollected owned drops

- Status: accepted
- Choice: B — reserve drops during a reconnect grace period
- Accepted requirement: If a player disconnects after becoming eligible for a personal physical reward but before collecting it, the drop remains reserved to that player for a bounded reconnect grace period.
- Exclusivity rule: Other players cannot steal or claim the reserved drop.
- Reconnect rule: Reconnecting within the grace period restores normal physical pickup responsibility.
- Regroup interaction: If a valid forced rendezvous occurs while the owner is disconnected, the reserved drops move into the same protected destination-transfer flow rather than being stranded in the old room.
- Forfeit rule: Explicitly leaving the session, or failing to return before the grace period expires, may forfeit the uncollected reward under the final implementation policy.
- Duplicate-prevention rule: Stable event identifiers, owner identifiers, and atomic state transitions must prevent disconnect, reload, host migration, or repeated reconnects from duplicating rewards.
- Testing requirement: This flow requires serious dedicated testing across timeout boundaries, repeated disconnect and reconnect cycles, crashes, host migration, player death, spectator state, rendezvous during reconnect, save interruption, packet loss, and intentional disconnect exploits.
- Deferred coefficient: The exact grace-period duration is not fixed during Product Discovery and must be chosen through prototypes and realistic network testing.

## Batch persistence

- Persisted through: D-200
- Unsaved decisions after checkpoint: 0
- Batch boundary reached normally
- Next direction: leave detailed post-MVP co-op settlement and return to the highest-impact MVP proof and delivery questions
