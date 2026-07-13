# Shooter Mover — Live Decisions

Status: recovery and Product Discovery log. Final acceptance occurs through the requirements/bootstrap pull request.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-063
- Unsaved accepted decisions: 0

## Recovery note

Decisions D-001 through D-039 were reconstructed from the surviving chat transcript and preserved in `RECOVERED_INTAKE_DRAFT.md`. They require section-by-section re-verification before becoming final requirements.

D-040 through D-063 were verified directly by the user after recovery.

## Decision log

### D-001–D-039 — Recovered gameplay decisions

- Status: unverified recovery set
- Accepted requirement source: `assembly/intake/RECOVERED_INTAKE_DRAFT.md`
- Rule: do not present any reconstructed detail as final until the user verifies it.
- Source: surviving guided-intake transcript

### D-040 — Map system

- Status: accepted
- Choice: B — fog-of-war exploration map
- Accepted requirement: Rooms, corridors, and connections reveal as the player explores. Discovered checkpoints, shops, and relevant objectives may appear; hidden rooms and undiscovered secrets remain hidden.
- User-supplied constraints: Keep additional objective guidance light and avoid GPS-style navigation.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-041 — Primary target-player strategy

- Status: accepted
- Choice: B — core-action-first, broadly accessible
- Accepted requirement: Design primarily for players who enjoy skill-based top-down combat, while providing many meaningfully tailored difficulty settings so casual, intermediate, expert, and mastery-focused players can all have an appropriate experience.
- User-supplied constraints: Difficulty options should genuinely tailor the game for different player types rather than merely applying simple health or damage multipliers.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-042 — Core connectivity model

- Status: accepted
- Choice: B — offline core with optional online features
- Accepted requirement: The complete MVP campaign, saves, progression, and difficulty modes must work offline. Optional online systems may be added later without making the core game dependent on connectivity.
- User-supplied constraints: Multiplayer is a post-MVP goal and should be pursued only after the MVP proves successful.
- Design implication: Keep clean extension boundaries for later online services and multiplayer, but do not include their operational complexity in the MVP.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-043 — Primary post-MVP multiplayer experience

- Status: accepted
- Choice: A — real-time cooperative play first
- Accepted requirement: After a successful single-player MVP, the first major multiplayer direction should be real-time cooperative play through campaign-style combat, exploration, objectives, and bosses.
- User-supplied constraints: Leaderboards are also desired later, but they remain post-MVP and do not replace cooperative multiplayer as the primary multiplayer direction.
- Design implication: MVP systems should avoid unnecessarily hard-coding a single-player-only architecture, while no multiplayer networking, scaling, shared progression, or leaderboard service is included in the MVP scope.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-044 — Target platform order

- Status: accepted
- Choice: A — Windows PC first
- Accepted requirement: Build and release the polished MVP for Windows PC first with keyboard/mouse and gamepad support. Android is the next intended platform after the PC version is stable; other platforms may follow later.
- User-supplied constraints: Keep gameplay actions platform-neutral, use scalable UI, support suspend saves, and avoid needless PC-specific assumptions so later mobile expansion remains practical.
- Design implication: Touch controls, mobile performance validation, and app-store release work are post-MVP rather than simultaneous MVP requirements.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-045 — Level size and expected session length

- Status: accepted
- Choice: B — substantial, checkpoint-segmented levels
- Accepted requirement: Each MVP level should target roughly 45–75 minutes for an exploratory first clear and 20–35 minutes for a mastered run, divided into natural 10–20 minute checkpoint sections.
- User-supplied constraints: Every level must remain interesting throughout and should later be genuinely worth replaying and grinding rather than becoming repetitive filler.
- Design implication: Level production must prioritize distinct encounters, routes, secrets, objectives, progression opportunities, and mastery depth over raw floor area or inflated runtime.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-046 — Repeat-run variation model

- Status: accepted
- Choice: B — stable levels with controlled variations
- Accepted requirement: Core layouts, bosses, major encounters, and official competitive categories remain deterministic, while optional curated modifiers, difficulty-specific enemy variants, challenge contracts, rotating bonuses, and limited reward variation may refresh repeat runs.
- User-supplied constraints: Preserve learnable mastery and fair speedrunning while making repeated play meaningfully varied.
- Design implication: Repeat-run variation must be authored and bounded rather than turning the campaign into heavily randomized roguelike runs.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-047 — Repeat-run reward and weapon-acquisition economy

- Status: accepted
- Choice: custom — randomized floor strongboxes plus randomized shop inventory
- Accepted requirement: Weapons are acquired primarily from level-floor pickups, potentially represented as tiered strongboxes with random contents, and from shops that offer a randomized loadout or inventory for purchase.
- User-supplied constraints: Random weapon discovery should be a central source of excitement during play, while shops provide a second paid chance to obtain useful weapons from a random selection.
- Open balancing detail: Duplicate handling, bad-luck protection, strongbox tier odds, shop refresh rules, pricing, and deterministic fallback rewards are not yet decided.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-048 — RNG fairness, progression bounds, and duplicate handling

- Status: accepted
- Choice: B — RNG with soft protection
- Accepted requirement: Strongboxes and shops remain genuinely random, but their results are bounded by player progression and protected against extreme bad-luck streaks. Duplicate weapons remain possible and can be converted into useful value through selling or dismantling.
- Shop rule: A shop may expose a broad randomized inventory of up to roughly 40 weapon slots, allowing the player a realistic chance to purchase almost any weapon currently appropriate to their progression.
- Progression rule: Base weapon types enter the possible loot pool gradually as the player gains levels. For example, one weapon may be available at level 1, another at level 2, and a new type at level 4; a level-3 strongbox may roll from a bounded nearby progression window that includes those level-1 through level-4 possibilities rather than the full game-wide pool.
- Variation rule: Eligible weapons may roll with enchantments or affixes, creating meaningful variants within the same base weapon type.
- Fairness rule: Higher-tier boxes guarantee an appropriate minimum quality, repeated misses may softly improve relevant odds, and limited shop reroll or equivalent fallback mechanisms may prevent pathological unlucky streaks.
- User-supplied constraints: The progression window, enchantments, shop breadth, probabilities, and power curve must be carefully balanced rather than allowing early jackpots or unlucky droughts to trivialize or stall progression.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-049 — Weapon power curve

- Status: accepted
- Choice: B — level-scaled recurring weapon families with later complex archetypes
- Accepted requirement: Core weapon families may return at later progression milestones as stronger counterparts with improved statistics and modest mechanical changes, while some more complex, interesting, and inherently powerful weapon types only enter the loot pool at higher levels.
- Example: A basic machine gun may first appear at level 1, return as an improved counterpart around level 11, and receive another stronger family variant around level 39.
- User-supplied constraints: Deliberate power inflation is acceptable and should be planned, balanced, and paced rather than treated as an accidental side effect.
- Design implication: Early individual drops become obsolete, but their broader weapon family may remain relevant through authored successor variants. New late-game archetypes must justify their later unlock through added complexity, novelty, or power rather than being simple reskins.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-050 — Shop inventory persistence and rerolls

- Status: accepted
- Choice: B — persistent inventory with limited paid rerolls
- Accepted requirement: A shop's broad randomized inventory remains stable until deliberately refreshed. The player may buy a limited number of rerolls with escalating costs and may lock a small number of selected items so they survive the refresh.
- User-supplied constraints: The reroll system should create interesting spending decisions without enabling cheap or unlimited fishing for perfect weapons.
- Design implication: Shop inventory state, reroll count, lock count, and escalating reroll prices must be tracked explicitly and balanced against strongbox acquisition.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-051 — High-rarity source weighting

- Status: accepted
- Choice: B — floor-biased jackpots with ultra-rare shop miracles
- Accepted requirement: The strongest high-star, high-augment weapons should primarily come from high-tier floor strongboxes. Shops may still generate exceptional jackpot weapons, such as a two-star weapon with augment level 7 or above, but only at screenshot-worthy rarity.
- Strongbox structure: Plan around eight strongbox tiers and a two-star, ten-level augment scale. Tier 7–8 boxes should be heavily weighted toward or nearly guarantee a high-quality, high-augment item while keeping the exact weapon family and enchantments random.
- Progression rule: Higher strongbox tiers must not be available at low player levels. Box tiers unlock progressively so advancing through later levels reveals new reward ceilings and creates continued motivation to play.
- User-supplied constraints: Exact odds, guarantees, level gates, and availability require playtesting and must prevent both early progression-breaking jackpots and unrewarding endgame droughts.
- Design implication: Shops provide breadth and extremely rare miracle rolls; high-tier floor boxes provide the dependable route to elite overall quality without allowing deterministic perfect-item targeting.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-052 — Strongbox tier unlock and acquisition rules

- Status: accepted
- Choice: B — progression gates plus challenge-weighted odds across all unlocked levels
- Accepted requirement: Player level and campaign progress impose a hard maximum on eligible strongbox tiers. Difficulty, optional objectives, bosses, challenge performance, and other mastery signals improve the probability of finding the highest currently eligible tiers but cannot bypass progression gates.
- Cross-level rule: Once a strongbox tier is unlocked, every level may retain a non-zero chance to produce it so the full campaign remains replayable.
- Anti-farming rule: Earlier or easier levels must be substantially less likely to produce high unlocked tiers and should skew toward slightly weaker item-quality rolls, preventing them from becoming the optimal farming route.
- User-supplied constraints: Earlier levels should remain worth revisiting, but later and more difficult content must provide materially better expected rewards. Exact weights and quality penalties require playtesting.
- Design implication: Reward tables must combine progression eligibility, level position, difficulty, and challenge performance rather than scaling from player level alone.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-053 — Strongbox tier to augment-quality mapping

- Status: accepted
- Choice: B — overlapping weighted quality bands with minimum guarantees
- Accepted requirement: Each strongbox tier has a dependable minimum quality floor and a strongly weighted expected star-and-augment range, while neighboring tiers overlap and retain a limited upper-tail jackpot chance.
- Cross-level jackpot rule: A box may roll an older, lower-base-level weapon with unusually strong stars and augments. For example, around player level 10, a Tier 2 crate may produce a level-2-to-4 base weapon with a rare high-star, high-augment roll.
- Balance rule: Base weapon level, weapon-family power, stars, augment level, and enchantment synergy must be evaluated together. Exceptional older weapons may remain exciting and temporarily competitive, but the combined system must not let common low-tier farming reliably overpower current progression.
- Tier rule: Higher box tiers raise minimums and expected quality rather than making every top-tier result perfect; exact ranges, overlap, and jackpot tails require playtesting.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-054 — Modifier compensation for lower base weapon levels

- Status: accepted
- Choice: B — bounded bridge weapons with contextual older-weapon value
- Accepted requirement: Exceptional stars, augments, and enchantment synergy may let an older weapon compete with ordinary weapons from several progression levels later, creating a meaningful bridge period, but a strong authored successor should eventually overtake it.
- User-supplied constraints: Every newly introduced weapon or successor should feel meaningful, while older weapons should retain contextual value through distinct handling, range, damage profile, status effects, efficiency, encounter matchups, or build synergy rather than becoming immediate vendor trash.
- Balance rule: Modifier power must use explicit budgets and bounded level-gap compensation. Older jackpot weapons may remain situationally useful after losing raw-stat leadership, but should not become permanent universal best-in-slot outliers.
- Design implication: Weapon comparison must communicate both effective power and contextual strengths instead of reducing every decision to a single damage number.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-055 — Shop pricing philosophy

- Status: accepted
- Choice: B — meaningful investments with rarity-scaled prices
- Accepted requirement: Ordinary shop weapons should be reasonably affordable while still requiring a meaningful choice. Strong stars, augment levels, desirable enchantments, later weapon families, and exceptional combinations increase prices sharply; screenshot-worthy jackpot weapons may require savings from several runs.
- Economy rule: Selling or dismantling old and duplicate equipment returns only partial value. Weapon purchases, inventory rerolls, and other shop services compete for the same core currency so spending decisions remain consequential.
- User-supplied constraints: The shop should support experimentation without replacing floor strongboxes as the primary elite-loot path, and income, resale values, rarity multipliers, and reroll costs require playtesting.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-056 — Rare shop-item reservation token

- Status: accepted
- Choice: B — extremely rare one-slot layaway token
- Accepted requirement: Consuming an extremely rare token moves one shop item into a dedicated long-term reservation slot. The reserved item survives inventory rerolls, shop refreshes, future visits, and completed runs until it is purchased or deliberately discarded.
- Capacity rule: Only one item may be reserved at a time across the player profile. Another reservation cannot be made until the slot is cleared, and the token is consumed immediately when the item is reserved.
- Economy rule: Reservation preserves the exact rolled weapon but does not discount it, reduce its original price, or accelerate earning the required currency.
- Persistence rule: The reserved item does not expire. The opportunity cost of occupying the sole slot supplies the pressure that an artificial timer otherwise would.
- Distinction: Ordinary item locks only protect selected stock during the current shop's limited rerolls; the rare layaway token is the only long-term preservation mechanism.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-057 — Duplicate conversion and inventory cleanup

- Status: accepted
- Choice: B — choose between selling and dismantling
- Accepted requirement: Unwanted, obsolete, and duplicate weapons may be sold for ordinary shop currency or dismantled into bounded materials whose yields reflect factors such as base level, rarity, stars, augments, and enchantment categories.
- Material-use rule: Dismantling materials may support tightly controlled services such as minor weapon improvement, limited enchantment rerolling, utility crafting, or later progression systems, but must not allow players to freely manufacture perfect weapons.
- Economy rule: Selling offers immediate flexible purchasing power; dismantling offers slower, more specialized progression value. Both returns remain partial so opening undesirable loot is useful without becoming an optimal infinite-farming loop.
- Inventory rule: Provide favourites, item locking, bulk marking, automatic junk filters, sorting and comparison tools, and a safe “keep best copy” workflow so large quantities of randomized loot can be processed quickly.
- Anti-farming rule: Material yields, crafting costs, and low-tier returns must prevent easy duplicate farming from overtaking difficult content and high-tier strongbox progression.
- Explicit exclusion: Do not make direct same-weapon duplicate fusion the primary upgrade path.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-058 — Inventory capacity and in-run loot timing

- Status: accepted
- Choice: custom B — forgiving universal inventory with expandable limits and deferred strongbox opening
- Accepted requirement: Use one profile-wide universal weapon inventory or armory with generous but finite capacity. Inventory limits should exist, remain forgiving during ordinary play, and allow later expansion through progression upgrades.
- Shop rule: Weapons bought from an in-level shop enter the player's usable inventory immediately and may provide an intentional mid-run power increase.
- Strongbox rule: Strongboxes earned or collected during a round remain sealed and are awarded for opening after the round rather than immediately granting their weapon contents during combat.
- Pacing rule: The shop supplies controlled immediate upgrades, while the strongest randomized floor rewards are delayed so a lucky high-tier box does not instantly trivialize the remainder of the current run.
- Safety rule: Capacity and overflow handling must avoid silent item deletion and should remain practical for later Android interfaces.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-059 — Strongbox banking and failure retention

- Status: accepted
- Choice: B — checkpoint-banked sealed strongboxes
- Accepted requirement: Reaching a checkpoint permanently secures every sealed strongbox collected since the previous checkpoint. Secured boxes remain owned even if the player later dies, abandons the level, or exits the run.
- Risk rule: Strongboxes collected after the latest reached checkpoint remain at risk until the player reaches another checkpoint or completes the level. Death or abandonment loses only this unsecured current-section set.
- Opening rule: Both checkpoint-secured and completion-secured boxes remain sealed during the run and become available for opening only after the run ends.
- Pacing rule: Each 10–20-minute checkpoint segment carries meaningful extraction tension without allowing a late failure to erase the rewards from the entire 45–75-minute level.
- Abuse guardrail: Continuing after banking must remain worthwhile through completion rewards, later-section reward quality, objectives, boss rewards, and efficient full-clear incentives rather than punishing legitimate exits.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-060 — Post-run strongbox opening and reward-review flow

- Status: accepted
- Choice: B — collection-ordered reward vault with individual and batch opening
- Accepted requirement: After a run ends, all secured sealed strongboxes enter a reward-vault flow in the exact order they were collected during the run. The default reveal sequence preserves that collection order.
- Opening rule: Players may open important boxes individually with a full reveal presentation or batch-open ordinary boxes quickly, but batch processing must still resolve and display results in original collection order.
- Review rule: Opened rewards enter a temporary capacity-safe review tray where players may inspect, compare, favourite, keep, sell, or dismantle them before final transfer.
- Capacity rule: The review tray does not consume normal inventory slots while rewards are being processed, and no reward may be silently deleted because the permanent inventory is full.
- Pacing rule: High-tier boxes retain ceremony and screenshot-worthy reveals, while routine lower-tier hauls can be processed efficiently.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-061 — Reward-vault exit and unresolved-reward handling

- Status: accepted
- Choice: B for MVP, with opt-in C-style automation after MVP
- Accepted requirement: The player may leave the reward vault with unresolved opened weapons. Those items remain exactly as rolled in a persistent, capacity-exempt pending-reward inbox that survives menu navigation, game closure, crashes, and save reloads.
- Progression gate: The pending inbox must be resolved before the player may open additional strongboxes or begin another reward-generating level run, preventing it from becoming unlimited long-term storage.
- Safety rule: Pending rewards may be kept, sold, dismantled, compared, or favourited later, and no unresolved reward may be silently deleted or automatically converted in the MVP.
- Post-MVP direction: Add optional configurable auto-processing rules for experienced grinders after the MVP. Automation must be explicitly enabled, clearly previewed, reversible where practical, and protect favourites, locked items, exceptional rarity, unusual contextual rolls, and other user-defined exclusions.
- UX rule: Provide quick bulk actions and clear blocking messages so resolving the inbox is fast rather than punitive.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-062 — Strongbox outcome commitment and reload-proof opening

- Status: accepted
- Choice: B — deterministic pickup seed and versioned loot snapshot
- Accepted requirement: When a strongbox is collected, record an immutable random seed together with its tier, source level, player progression, difficulty, applicable reward modifiers, collection order, and loot-table version. Opening later must always generate the same exact weapon from that committed snapshot.
- Manipulation rule: Levelling up, changing difficulty, updating the game, delaying the opening, closing the game, or restoring an ordinary save must not alter the box's eventual family, base level, stars, augments, enchantments, or rolled statistics.
- Transaction rule: Opening a box must use a crash-safe atomic transaction that consumes the sealed box and persists its exact generated reward before or together with the reveal presentation. A crash or reload during the animation resumes or restores the same already-committed reward instead of rerolling or duplicating it.
- Save-scumming rule: Reopening and repeatedly reloading the game can never produce alternative outcomes from the same box. The same box identifier and seed may grant its reward only once.
- Testing requirement: Automated deterministic replay tests, interrupted-transaction tests, duplicate-grant tests, and save-reload tests must verify this invariant before release.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-063 — Offline save integrity and future competitive verification

- Status: accepted
- Choice: B — hardened offline saves with separate verified competitive modes
- Accepted requirement: Keep the complete campaign, progression, inventory, economy, and strongbox systems playable offline while hardening normal local saves with checksums or signatures, transaction journals, rotating backups, monotonic counters for important grants and economy actions, and detection of suspicious rollback or mismatched save history.
- Recovery rule: Save-integrity checks must prioritize safe recovery from genuine corruption, crashes, interrupted writes, and device problems. They must not silently delete progression or punish a player merely because a backup was needed.
- Verification rule: Suspicious or manually altered local state may mark a profile as unverified while preserving unrestricted offline campaign play. An unverified flag affects only features that explicitly require trusted competitive results.
- Competitive boundary: Future leaderboards, official speedrun events, and competitive challenges should use a separate verified-run system, potentially including server-issued run identifiers, version and ruleset attestation, replay or result proofs, and server-side validation. They must not make the ordinary campaign server-authoritative.
- Explicit limitation: A determined user can ultimately alter a fully local game. The MVP should prevent casual rollback and duplication abuse rather than spending excessive effort pretending local files can be perfectly secured.
- Supersedes: none
- Source: guided Product Discovery recovery

## Guided intake presentation preference

- Place the agent recommendation after all A/B/C options, at the end of each decision card.

## Next discovery state

Continue with the highest-weight unresolved Product Discovery question. Monetization and release positioning now outrank remaining economy details because randomized strongboxes must be clearly separated from real-money loot boxes and the project needs a trustworthy revenue model.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Commit every newly accepted answer before asking the next question.