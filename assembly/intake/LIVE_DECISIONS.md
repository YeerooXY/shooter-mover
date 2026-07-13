# Shooter Mover — Live Decisions

Status: active Product Discovery log. Detailed historical decisions D-040 through D-063 are preserved verbatim in `assembly/intake/archive/LIVE_DECISIONS_THROUGH_D063.md`.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-066
- Unsaved accepted decisions: 0

## Recovery and archive note

- Decisions D-001 through D-039 remain an unverified recovery set in `assembly/intake/RECOVERED_INTAKE_DRAFT.md`.
- Decisions D-040 through D-063 were verified directly by the user and are preserved verbatim in `assembly/intake/archive/LIVE_DECISIONS_THROUGH_D063.md`.
- This file continues the live verified log from D-064 onward. The archival split is organizational only and does not supersede or alter any accepted decision.

## Verified decision index through D-063

- D-040: Fog-of-war exploration map with light guidance and hidden undiscovered secrets.
- D-041: Core-action-first target with meaningful difficulty tailoring from casual through mastery.
- D-042: Complete offline MVP core with optional online features later.
- D-043: Real-time cooperative campaign play is the first post-MVP multiplayer direction.
- D-044: Windows PC first; Android follows after PC stability.
- D-045: Substantial checkpoint-segmented levels with replayable 10–20-minute sections.
- D-046: Deterministic core levels with bounded authored repeat-run variation.
- D-047: Randomized floor strongboxes plus broad randomized shop inventory.
- D-048: Progression-bounded RNG with soft bad-luck protection and useful duplicates.
- D-049: Recurring weapon families, authored successors, later complex archetypes, and planned power inflation.
- D-050: Stable shop inventory with limited escalating paid rerolls and short-term locks.
- D-051: Elite loot mainly from high-tier floor boxes; ultra-rare shop miracle rolls remain possible.
- D-052: Strongbox tier gates from progression, with challenge-weighted odds across all unlocked levels.
- D-053: Overlapping tier quality bands with minimum guarantees and limited jackpot tails.
- D-054: Exceptional older weapons may bridge several levels while retaining contextual value after successors appear.
- D-055: Meaningful rarity-scaled shop pricing with shared currency pressure.
- D-056: One extremely rare consumed token may reserve one shop item indefinitely at its original price.
- D-057: Unwanted weapons may be sold or dismantled; no primary duplicate-fusion upgrade path.
- D-058: Generous but finite universal inventory; shop purchases are immediate, strongboxes open after the run.
- D-059: Checkpoints permanently bank sealed boxes; only the current section’s boxes remain at risk.
- D-060: Collection-ordered reward vault with individual and batch opening plus a capacity-safe review tray.
- D-061: Persistent pending-reward inbox for MVP; opt-in protected auto-processing follows after MVP.
- D-062: Deterministic pickup seed and versioned loot snapshot with atomic reload-proof single granting.
- D-063: Hardened offline saves with separate verified competitive modes and no server-authoritative campaign requirement.

## Decision log

### D-064 — Monetization and release model

- Status: accepted
- Choice: custom — free-to-play freemium game with cosmetic-only monetization
- Accepted requirement: The game is free to download and enter. Real-money spending must not alter gameplay progression, combat power, loot quality, drop probabilities, difficulty, or competitive performance.
- Cosmetic rule: Monetization is based on optional skins and other visual variants for existing mechs, weapons, effects, and comparable already-available gameplay content. A paid cosmetic must not secretly introduce a stronger gameplay version of the underlying item.
- Strongbox boundary: Strongboxes, weapons, stars, augments, enchantments, currencies, materials, rerolls, reservation tokens, inventory capacity, player levels, and progression advantages cannot be purchased with real money. Randomized in-game strongboxes remain earned through gameplay and are not real-money loot boxes.
- Fairness rule: Free and paying players use the same gameplay balance, reward tables, progression rules, difficulty systems, and competitive rules. Spending may change appearance only.
- Readability rule: Cosmetic effects must preserve enemy, projectile, weapon-state, hitbox, and telegraph readability. Competitive or verified modes may normalize or restrict visually disruptive cosmetics when necessary.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-065 — Free-content boundary

- Status: accepted
- Choice: A — complete gameplay remains free
- Accepted requirement: Every campaign level, boss, weapon family, difficulty option, progression system, challenge, and gameplay update is available without payment. No authored gameplay content is placed behind a paid expansion, starter-campaign paywall, or one-time full-game unlock.
- Revenue rule: Revenue comes from optional direct-purchase cosmetics and cosmetic supporter bundles only. Paid offerings may alter appearance, presentation, or supporter recognition but cannot contain exclusive gameplay functionality or progression value.
- Community rule: Free and paying players always retain access to the same playable levels, modes, co-op content, challenges, weapon families, and balance rules, avoiding content-fragmented matchmaking or progression.
- Sustainability note: Cosmetic production, pricing, storefront presentation, and content cadence must be scoped honestly around team capacity; monetization pressure must never be solved by weakening free progression or introducing paid advantages.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-066 — Cosmetic ownership and optional accounts

- Status: accepted
- Choice: B — optional account linkage with permanent guest and offline play
- Accepted requirement: Players may begin, continue, and complete ordinary offline gameplay as guests without creating or linking an account. Login must never gate quick play, campaign access, progression, inventory, or difficulty settings.
- Account rule: An optional game account may link purchased cosmetic entitlements, cosmetic loadouts, favourites, cloud backup, device migration, and eventual PC-to-Android ownership portability where platform rules permit.
- Restoration rule: Purchases made through a platform storefront must remain restorable from valid storefront receipts on that original platform without requiring the optional game account.
- Portability rule: Linking an account may unify supported cosmetic ownership across devices and platforms, but must never turn gameplay progression or offline access into an always-online service.
- Privacy rule: Account creation is voluntary, requests only necessary data, supports recovery and unlinking, and clearly explains entitlement-merging and conflict behavior before committing changes.
- Implementation note: Entitlement merging, account recovery, cross-platform purchase recognition, and platform-policy differences require explicit testing and backend boundaries; Android portability is post-PC rather than an MVP release blocker.
- Supersedes: none
- Source: guided Product Discovery recovery

## Guided intake presentation preference

- Place the agent recommendation after all A/B/C options, at the end of each decision card.

## Next discovery state

Continue with the highest-weight unresolved Product Discovery question. Define whether optional account linkage also provides gameplay cross-progression, and how divergent offline saves are resolved without duplicating weapons, currency, or strongboxes.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Commit every newly accepted answer before asking the next question.