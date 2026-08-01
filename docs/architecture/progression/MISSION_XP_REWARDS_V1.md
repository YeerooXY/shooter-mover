# Mission XP Rewards V1

Production missions record accepted enemy deaths in a run-local ledger. The
ledger stores the exact death and actor identities, XP profile, tier, room,
killer participant, calculated XP, and a deterministic fingerprint. Replaying
the same fact is a no-op; a conflicting replay is rejected. It never mutates a
character.

Enemy XP is:

```text
round(profile base XP * tier multiplier * mode multiplier)
```

Profile bases are Light 7, Standard 10, and Turret 12. Tier multipliers are
1.0, 1.5, 2.25, and 3.5. Normal mode is 1.0. Player level is not an input.

Successful mission completion adds `25 + 15 * completed rooms`. The terminal
composition combines that value with the ledger total and submits one
deterministic XP operation for the run. The grant occurs inside the existing
durable terminal character-save boundary. Failed and abandoned runs instead
award `round(enemy XP * 0.25)` with midpoint values rounded away from zero.
They receive no room-completion XP. Their shared incomplete-run operation is
duplicate-safe and crosses the same durable character-save boundary before
the Hub transition is accepted. Escape or Backspace explicitly abandons an
active mission.

Only published level catalog entries with `awards_persistent_xp: true` can
grant permanent XP. Missing flags default closed, so future unverified or
player-created content cannot opt in accidentally.

The production level curve is:

```text
round(100 + 47.4335 * (current level - 1))
```

It totals exactly 240,000 XP from level 1 to 100. Restoring a save from the
retired flat 100-XP curve preserves cumulative XP and grant receipts, then
re-evaluates level and skill-point projection against the production curve.

Results carry enemies killed, enemy XP, completed rooms, completion XP, total
XP, previous/new level, and skill points earned. The Player XP Simulator under
`Shooter Mover/Balance` reports mission/hour rates and level-by-level timing.
