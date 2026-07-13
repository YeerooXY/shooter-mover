# Shooter Mover — Live Decisions

Status: active Product Discovery log. Detailed historical decisions D-040 through D-063 are preserved verbatim in `assembly/intake/archive/LIVE_DECISIONS_THROUGH_D063.md`.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-080
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

### D-067 — Gameplay cross-progression and divergent offline saves

- Status: accepted
- Choice: B — whole-profile cloud cross-progression with explicit conflict selection
- Accepted requirement: Linked players may upload and restore a complete gameplay-profile snapshot for cross-device and eventual PC-to-Android continuity while retaining unrestricted offline play and optional account use.
- Conflict rule: When local and cloud profiles diverge, never merge individual weapons, currencies, strongboxes, inventory transactions, or progression events. Present both complete save lineages and require the player to choose either the local or cloud branch.
- Comparison rule: Conflict selection must show useful summaries such as timestamp, player level, playtime, campaign progress, inventory overview, and verification state before the player commits.
- Recovery rule: Preserve the discarded branch temporarily as a recoverable backup rather than deleting it immediately.
- Integrity rule: Profile lineage identifiers, transaction journals, monotonic grant counters, and save versioning must prevent choosing both branches sequentially to duplicate weapons, currency, boxes, purchases, or other rewards.
- Timing rule: Prepare profile identifiers, deterministic save schemas, and versioning during the Windows PC build. The actual cross-platform cloud service may arrive post-MVP with Android and must not block the PC MVP.
- Explicit exclusion: Do not attempt automatic item-by-item merging of divergent gameplay profiles.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-068 — Death respawn and difficulty-scaled room reclamation

- Status: accepted
- Choice: custom A — cleared-room persistence by default with stronger difficulty reclamation
- Accepted requirement: On the baseline and more accessible difficulty rulesets, rooms already cleared before death remain cleared. The room or active encounter in which the player died resets so the player can retry without routinely replaying the entire checkpoint section.
- Difficulty rule: Stronger difficulty rulesets may reclaim a larger authored area after death, potentially resetting several previously cleared rooms or, at the highest settings, a substantial part of the current checkpoint section.
- Determinism rule: The reclaimed area is fixed and predictable for each difficulty, checkpoint, and death location rather than selected through uncontrolled random respawning. Each difficulty remains learnable, repeatable, and suitable for fair speedrunning categories.
- Boundary rule: Reclamation must not undo checkpoint-banked strongboxes or other progress already secured under D-059, and it must not reach behind the latest activated checkpoint unless a separately defined challenge mode explicitly establishes that rule.
- Repetition guardrail: Multi-room reclamation must create meaningful route and survival pressure rather than repetitive cleanup. Encounter composition, shortcut state, one-way transitions, and respawn placement must avoid impossible or unfair recovery states.
- Implementation note: Level state must support authored reset groups larger than one room even when ordinary difficulties use only the death-room reset, preventing a later difficulty-system rewrite.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-069 — Activated-checkpoint fast travel

- Status: accepted
- Choice: B — controlled travel between activated checkpoints within the current level
- Accepted requirement: Once the player physically discovers and activates multiple teleport checkpoints in a level, those stations may be used to travel between one another while the player is outside combat and no encounter-lock condition is active.
- Exploration rule: Fast travel never reveals undiscovered rooms, secrets, routes, shops, objectives, or checkpoints. A destination must already have been reached and activated through ordinary play.
- State-safety rule: Travelling must preserve enemy-clear state, authored difficulty reclamation state, objectives, doors, switches, hazards, shop inventory, reroll state, reserved items, pickups, and all other persistent level state exactly as if the player had walked there.
- Economy and reward guardrail: Fast travel cannot respawn loot or enemies, refresh shops, regenerate one-time pickups, duplicate checkpoint banking, re-secure unsecured strongboxes, reset reward seeds, or bypass progression and lock requirements.
- Routing rule: Checkpoint travel is a deterministic traversal option and may support speedrunning routes, but official categories may define whether teleport use is allowed rather than relying on hidden or random restrictions.
- Difficulty rule: Stronger rulesets may apply explicit authored restrictions, such as limiting travel to the latest checkpoint, requiring nearby rooms to be secure, or disabling selected links, provided each ruleset remains clear, deterministic, and learnable.
- Implementation note: Destination validation and state restoration must be designed for later co-op and Android session practicality, while the Windows PC MVP needs only safe same-level activated-station travel.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-070 — Puzzle, key, lock, and environmental-interaction complexity

- Status: accepted
- Choice: B — straightforward mandatory routing with deeper optional puzzles
- Accepted requirement: Main-path keys, locks, switches, destructible generators, power-routing tasks, and comparable interactions remain quick, readable, and subordinate to combat pacing. Optional secrets, shortcuts, rare rewards, lore spaces, and side encounters may use moderate multi-step environmental puzzles.
- Replay rule: Mandatory interactions must remain fast on repeat runs, while optional puzzles may reward route knowledge and permit efficient solutions once mastered.
- Difficulty rule: Puzzle logic remains fixed and deterministic across difficulties. Easier modes may provide clearer visual hints or reminders; harder modes may reduce hints or add combat pressure but must not secretly change the correct logical solution.
- Accessibility rule: Required interactions need clear visual and audio language, remappable controls, and practical PC, gamepad, and later Android input paths. Avoid obscure pixel hunting or solutions that depend only on colour, sound, or precise timing without alternatives.
- Co-op rule: Optional puzzles may support coordination later, but ordinary campaign progress cannot depend on fragile simultaneous-input sequences or create co-op deadlocks.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-071 — Mission objectives and kill-based XP progression

- Status: accepted
- Choice: custom B — mixed authored mission objectives with robot-kill XP
- Accepted requirement: Each campaign level or major section has authored mission-wide goals that define success, such as destroying targets, activating systems, retrieving an object, defending a position, escaping, or defeating a boss. Combat remains the dominant moment-to-moment activity.
- XP rule: Destroying enemy robots grants XP toward the player’s persistent level, making ordinary combat a primary progression source even when clearing every enemy is not itself the mission objective.
- Objective rule: Objectives structure routes and pacing without replacing combat as the game’s core. Optional objectives may grant additional XP, currency, strongbox-quality influence, mastery credit, or other bounded rewards.
- Anti-farming rule: XP rewards, reclaimed enemies, repeat clears, and objective resets must be controlled so repeatedly cycling restored rooms cannot become the dominant progression strategy.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-072 — XP accrual and checkpoint-activated level-ups

- Status: accepted
- Choice: B — earn XP immediately and activate level-ups at checkpoints
- Accepted requirement: Kills and objectives add XP to the visible progression bar immediately. Crossing a threshold provides immediate feedback, but the actual player-level increase, baseline stat changes, unlocks, and new loot eligibility activate only upon reaching or using the next valid checkpoint.
- Encounter rule: Player power and loot eligibility do not change unexpectedly during an active fight.
- Loot rule: Strongboxes already collected retain the immutable progression snapshot committed at pickup under D-062. Pickups after checkpoint activation use the newly active player level.
- Failure rule: XP already earned is not erased merely because the player dies before checkpoint activation; reclaimed-enemy reward suppression is handled separately under D-075.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-073 — Fixed core growth with authored milestone choices

- Status: accepted
- Choice: C — deterministic baseline progression plus occasional permanent upgrade choices
- Accepted requirement: Ordinary player levels provide predictable authored baseline growth, unlocks, and loot-pool progression. Specific milestone levels offer a choice among a small set of balanced permanent upgrades, such as shield behaviour, movement efficiency, weapon handling, or recovery characteristics.
- Balance rule: Random weapon rolls already provide frequent variation, so foundational character growth remains sufficiently stable for encounter tuning, comparison, and deterministic mastery.
- Choice rule: Milestone options must represent understandable trade-offs rather than obvious traps or strictly superior picks.
- Competitive rule: Verified runs record the active milestone build, and categories may normalize or constrain builds where necessary.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-074 — Paid milestone respec at checkpoints

- Status: accepted
- Choice: B — checkpoint respec using gameplay resources
- Accepted requirement: Milestone upgrade choices may be reset and reassigned at activated checkpoints or equivalent safe progression interfaces by paying a bounded gameplay-currency or material cost.
- UX rule: The complete resulting build and cost are previewed before confirmation. The first respecs should remain affordable enough to support learning and experimentation.
- Economy rule: Respec costs must remain meaningful without becoming punitive, permanently trapping a profile, or pressuring real-money spending.
- Run rule: Milestone choices cannot be changed during active combat. Verified runs record or lock the selected build according to category rules.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-075 — Permanent earned XP with reclaimed-enemy suppression

- Status: accepted
- Choice: C — keep earned XP and reduce repeated reclaimed-enemy rewards
- Accepted requirement: Legitimately earned XP is retained immediately and is not removed by death or abandonment. Enemies restored through the same death-and-reclamation cycle grant sharply reduced or zero additional XP until genuine progression resets their reward eligibility.
- Reset rule: Reward eligibility may reset through reaching a new checkpoint, starting a fresh level attempt, or another clearly defined legitimate progression boundary, not by repeatedly dying or teleporting.
- Communication rule: Suppressed or reduced XP must be clearly signalled so the player understands that the enemy is a repeated reclaimed target rather than a bugged reward.
- Difficulty rule: Reward-reduction values may vary by ruleset, but already earned XP is not deleted to create difficulty.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-076 — Authored level bands with optional player-scaled modes

- Status: accepted
- Choice: custom C — fixed approachable modes plus bounded scaled replay modes
- Accepted requirement: Each campaign level has an authored intended power band. Easier and ordinary modes preserve fixed approachable enemy tuning so newer players can progress without every level automatically matching a highly levelled profile.
- Scaled-mode rule: Separate replay, challenge, or mastery modes may scale earlier levels toward the player’s current level within authored bounds, keeping old content challenging and economically relevant without blindly equalizing every stat.
- Power-fantasy rule: Returning to ordinary earlier content while overlevelled should still communicate real progression and increased power.
- Reward rule: Scaled modes may provide appropriately improved challenge rewards, while fixed easy modes must not become the optimal high-level farming route.
- Competitive rule: Fixed authored and scaled variants use explicit separate rulesets and leaderboard or challenge categories.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-077 — Per-attempt difficulty commitment

- Status: accepted
- Choice: B — choose difficulty at level start and commit for the full attempt
- Accepted requirement: The player selects one difficulty or ruleset when starting or replaying a level. That selection remains fixed until the attempt ends or is deliberately restarted.
- Progress rule: Different campaign levels may be completed on different difficulties. Completion records, rewards, achievements, mastery results, and verification data store the selected ruleset for each run.
- Anti-exploit rule: Difficulty cannot be lowered for one troublesome room or increased only before a reward. Changing it requires ending or restarting the attempt.
- Speedrun rule: Verified categories retain one deterministic ruleset for the entire run.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-078 — Immediate core difficulties with unlockable extreme mastery modes

- Status: accepted
- Choice: C — normal spectrum available immediately; transformative extremes unlock through achievement
- Accepted requirement: New players may immediately choose from the ordinary difficulty spectrum, including accessible, standard, hard, and an appropriately demanding expert option, without being forced to clear easier settings first.
- Unlock rule: The most transformative mastery rulesets—such as severe room reclamation, near-zero recovery, expanded encounter compositions, or specialised challenge modifiers—unlock after the player demonstrates familiarity through campaign progress, level completion, or relevant achievements.
- UX rule: Extreme modes must be clearly labelled as specialised mastery experiences rather than ordinary recommended first-play options.
- Speedrun rule: Main categories remain available without mandatory low-difficulty grinding; specialised unlocked categories use predictable requirements.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-079 — Difficulty-scaled between-room health recovery

- Status: accepted
- Choice: custom B — health recovery varies from generous to nearly absent by difficulty
- Accepted requirement: Clearing a legitimate room or encounter may restore a difficulty-dependent amount of health while shield recovery follows its own rules. Easier modes provide substantial recovery and may make attrition almost trivial for inexperienced players; standard modes provide modest recovery; hard modes sharply reduce it; extreme modes may provide almost none or zero.
- Abuse rule: Reclaimed rooms and repeatedly restored encounters cannot repeatedly grant full health recovery. Recovery is tied to legitimate first-clear or progression eligibility.
- Balance rule: Pickups, shops, upgrades, perks, and checkpoints may provide additional authored healing, but the complete system must avoid both infinite recovery loops and unwinnable no-resource states on modes intended for ordinary completion.
- Communication rule: The selected difficulty clearly explains its health-recovery rules before the level begins.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-080 — Capped passive shield recovery with difficulty and perk scaling

- Status: accepted
- Choice: custom — minimal capped baseline recovery, extensible through skills and rulesets
- Accepted requirement: Passive shield recovery between or after encounters is deliberately limited by default rather than automatically restoring a full shield after every room. Recovery may stop at a maximum percentage cap and should remain small enough that ordinary mistakes still matter.
- Perk rule: Authored skills, perks, or milestone upgrades may raise the passive recovery cap, improve regeneration speed, enable additional recovery conditions, or add aggression-driven recharge as an optional build mechanic rather than a universal baseline requirement.
- Difficulty rule: Accessible modes may override the baseline with generous or near-complete shield recovery and substantial health recovery, making the game intentionally easy for newer players. Standard modes provide restrained recovery. Hard and extreme modes reduce passive shield and between-room health recovery toward almost zero or zero.
- Anti-stalling rule: Recovery timing and caps must avoid making doorway waiting the optimal rhythm. The game may complete eligible recovery quickly once a room is secure rather than rewarding prolonged inactivity.
- Abuse rule: Reclaimed or repeatedly cleared rooms cannot repeatedly restore shield or health beyond the active ruleset’s legitimate recovery allowance.
- Supersedes: none
- Source: guided Product Discovery recovery

## Guided intake presentation preference

- Place the agent recommendation after all A/B/C options, at the end of each decision card.

## Next discovery state

Continue the core-experience recovery by deciding when the player may change the equipped four-weapon loadout during a level.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Product Discovery decisions may be batch-persisted every ten questions when explicitly requested by the user; the next planned batch boundary is D-090.
