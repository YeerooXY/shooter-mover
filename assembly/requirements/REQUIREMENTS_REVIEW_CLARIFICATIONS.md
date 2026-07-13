# Shooter Mover Requirements Review Clarifications

Status: authoritative clarification for the requirements/bootstrap review. This file resolves representation gaps without adding new Product Discovery decisions. `REQUIREMENTS.md`, `project_intake.json`, `PROJECT_DNA.md`, and the verified decision logs remain the complete requirements package together.

## Minimal internal shop refresh rule

Verified decisions D-172 and D-173 refine the internal-slice shop scope after D-149.

The internal MVP includes:

- a small randomized shop inventory rolled once when the mission run begins;
- stock and sold-out state that remain stable for that run;
- optional refreshes only when the player spends an allowed mission-bound refresh token or allowance;
- refresh tokens earned during the current mission through authored optional rooms, challenges, strongboxes, or selected encounters;
- no free refresh from revisiting a shop, dying, respawning, reloading, or using teleport travel;
- no banking, exporting, or retaining unused refresh tokens after the run ends;
- anti-farming state that prevents repeated token generation from reclaimed or already-cleared content.

This is the smallest real run-only refresh foundation needed to test shop decisions and route rewards. It does **not** introduce the postponed mature economy.

The following remain postponed:

- persistent reroll currencies;
- broad or repeatedly purchasable rerolls;
- long-term item locks or reservations;
- mature shop manipulation, dismantling, stars, augments, enchantments, and per-instance weapon modifiers.

## Interpretation rule

Where `REQUIREMENTS.md` says that “broad shop rerolls” are postponed, it refers to the mature persistent reroll system. It does not remove the accepted bounded mission-only refresh-token mechanism above.

No other requirement or MVP boundary is changed by this clarification.
