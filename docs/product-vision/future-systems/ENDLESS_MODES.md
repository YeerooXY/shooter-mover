# Endless, survival and repeatable challenge modes

## Status

Product-planning document only. These modes should be built from validated static content and generic run systems after the core campaign loop works.

## Confirmed direction

- Endless modes are intended to keep max-level and well-equipped players engaged.
- They should reward mastery and build experimentation without becoming the only efficient way to progress.
- Early versions should reuse authored rooms and enemies rather than requiring full procedural geometry.
- Run-local progress and permanent account rewards must remain separate.
- Reward milestones must be exactly-once and cannot be duplicated by restart or reconnect.

## Mode family 1 — Endless Survival

### Player fantasy

Survive escalating waves in an authored arena or connected set of rooms.

### Working structure

```text
enter with persistent character/loadout
-> survive wave
-> receive run-local recovery/choice
-> continue or cash out at milestone
-> encounter stronger compositions and elites
-> defeat periodic boss wave
-> end by extraction, defeat or voluntary cash-out
```

### Escalation dimensions

- enemy role combinations;
- enemy count within readable performance limits;
- elite frequency;
- attack cadence and confidence;
- arena hazards;
- reduced recovery windows;
- boss intervals;
- optional challenge modifiers.

Avoid infinite health multiplication as the only scaling method.

## Mode family 2 — Endless Descent

### Player fantasy

Travel through an ongoing chain of rooms, choosing the next route and accumulating risk.

### Working structure

- Each stage selects from validated authored room templates.
- Door choices may preview broad risk/reward categories.
- Some rooms offer combat, recovery, shop/service, elite or boss encounters.
- Difficulty and reward build as the run continues.
- A run seed makes the room sequence reproducible for validation or leaderboards.

This is the preferred long-term procedural direction: seeded manifests assembled from static validated rooms, not unbounded runtime geometry generation.

## Mode family 3 — Holdout / Base Defence

### Player fantasy

Defend a reactor, terminal, convoy stop or other objective against increasingly complex attacks.

### Working mechanics

- Enemies approach from authored lanes or spawn regions.
- The objective has a visible health/condition state.
- Players choose between protecting the objective and eliminating priority enemies.
- Temporary run upgrades may improve barriers, turrets, healing or objective repair.
- Mine layers, artillery and suppression enemies create meaningful threat roles.

This mode should not require permanent tower-defence progression to be enjoyable.

## Mode family 4 — Boss Rush

### Player fantasy

Fight a sequence of bosses and elite encounters with limited recovery.

### Working structure

- Fixed or seeded boss order.
- Recovery choice after each victory.
- Optional escalating modifiers.
- Clear milestone rewards.
- Separate solo and co-op leaderboards if leaderboards are added.

Boss Rush is valuable because it concentrates mechanical mastery and avoids long filler sections.

## Mode family 5 — Overrun Extraction

### Player fantasy

Stay as long as desired while the world becomes more dangerous, then reach an extraction point before being overwhelmed.

### Working structure

- Rewards accumulate in a run-local manifest.
- Extraction opportunities appear at known milestones.
- Choosing to continue increases both reward quality and risk.
- Failing before extraction loses some or all uncommitted run rewards according to clearly displayed rules.
- Previously owned equipment is never destroyed by failure.

This provides tension without needing player-versus-player competition.

## Mode family 6 — Rotating Challenge Seed

### Player fantasy

Everyone receives the same room sequence, enemy modifiers and reward rules for a fixed period.

### Working structure

- daily or weekly deterministic seed;
- fixed difficulty and modifier package;
- comparable scoring rules;
- completion rewards independent from leaderboard placement;
- leaderboard rewards primarily cosmetic, prestige or bounded bonus items.

This mode supports replayability and community discussion without making random seeds impossible to compare.

## Run-local upgrade ideas

Endless modes may provide temporary choices that disappear after the run:

- temporary damage-channel bonus;
- movement or recovery enhancement;
- one-run augment effect;
- extra revive charge;
- improved pickup radius;
- temporary barrier or drone;
- choice between immediate recovery and future reward multiplier;
- weapon-family-specific temporary enhancement.

Run upgrades must not mutate the permanent exact equipment instance unless an explicit permanent reward is granted at run completion.

## Reward cadence proposal

Rewards should arrive at visible milestones rather than after every trivial wave.

Example working cadence:

| Milestone | Reward direction |
|---|---|
| early checkpoint | money/scrap and recovery choice |
| first elite checkpoint | guaranteed low-tier strongbox or targeted material |
| first boss | improved box and mode milestone |
| later interval | increasing box tier weighting and augment chance |
| major depth/wave | overclock core chance or deterministic capstone progress |
| personal best | one-time milestone reward |

Exact wave numbers are open.

## Cash-out and failure

### Working proposal

- The player sees what is permanently secured and what remains at risk.
- Voluntary cash-out commits current run rewards exactly once.
- Defeat may retain earlier checkpoint rewards while losing only the uncommitted segment.
- Reconnect resumes the same run where technically supported; otherwise the system applies a documented safe result.
- Restarting cannot regenerate milestone rewards or change committed box outcomes.

## Scoring ideas

Possible score components:

- rooms/waves completed;
- completion time;
- difficulty/modifier multiplier;
- enemies defeated;
- damage avoided;
- objective health retained;
- revives used;
- optional challenge completion.

Raw damage dealt should not dominate every leaderboard because it disadvantages support and defensive play.

## Solo and co-op

- Endless Survival, Descent and Boss Rush should support solo and co-op eventually.
- Party-size scaling should adjust compositions and objectives.
- Co-op scoring should credit team success and class contribution without encouraging kill stealing.
- Personal loot remains separate even when the run score is shared.
- A player disconnecting cannot cause the remaining party to lose already secured milestones.

## Content extensibility

A new endless room should require:

- authored room JSON/template;
- encounter tags or eligibility metadata;
- validation;
- presentation assets;
- no mode-specific gameplay controller for that room identity.

A new modifier should declare eligibility and effects generically rather than switch on enemy names.

## Open decisions

- Which endless mode launches first. Endless Survival is the smallest visible candidate; Endless Descent offers the strongest long-term variety.
- Whether cash-out is always available or appears only at milestones.
- How much uncommitted reward is lost on failure.
- Whether temporary run upgrades are random offers, drafted choices or purchased with run currency.
- Whether leaderboards are global, friends-only, seasonal or all three.
- Whether rotating seeds use fixed player equipment, normalised stats or persistent builds.
- How run length is capped for performance and practical session time.
- Whether Overrun Extraction becomes a separate mission type or an endless-mode rule set.
