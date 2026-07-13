# Shooter Mover — Live Decisions

Status: recovery and Product Discovery log. Final acceptance occurs through the requirements/bootstrap pull request.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-054
- Unsaved accepted decisions: 0

## Recovery note

Decisions D-001 through D-039 were reconstructed from the surviving chat transcript and preserved in `RECOVERED_INTAKE_DRAFT.md`. They require section-by-section re-verification before becoming final requirements.

D-040 through D-054 were verified directly by the user after recovery.

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

## Guided intake presentation preference

- Place the agent recommendation after all A/B/C options, at the end of each decision card.

## Next discovery state

Continue with the highest-weight unresolved Product Discovery question. Shop pricing philosophy now outranks remaining economy details because the shop must support experimentation and rare jackpot purchases without making currency farming or shop fishing the dominant progression path.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Commit every newly accepted answer before asking the next question.