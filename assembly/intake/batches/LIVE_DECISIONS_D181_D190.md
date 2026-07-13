# Shooter Mover — Product Discovery Batch D-181 through D-190

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-181 — Personal ordinary loot with simultaneous individual major-reward boxes

- Status: accepted
- Choice: C — personal routine rewards with duplicated major rewards
- Accepted requirement: Routine drops, currency, refresh tokens, shop purchases, unsecured loot, banking state, and permanent progression remain personal to each player.
- Major-reward rule: When the team earns a major authored reward, every eligible player receives a strongbox at the same moment rather than competing for one shared item.
- Rarity rule: Each player's granted strongbox rolls its rarity independently; one player's result does not remove, downgrade, or consume another player's reward.
- Fairness rule: Preserve individual save ownership and prevent loot stealing, griefing, or permanent-progression disputes.

## D-182 — Connected players and eliminated spectators receive major rewards

- Status: accepted
- Choice: B — reward every eligible player still connected when the reward is earned
- Accepted requirement: Active players and players who exhausted their personal lives but remain connected as spectators receive their own major-reward strongbox when the team succeeds.
- Disconnection rule: Players who have left the live session do not receive the reward under the initial rule set.
- Future rule: A brief reconnection grace period may be considered during actual online implementation, but it is not required for the first co-op version.
- Anti-abuse rule: Merely having started the run is insufficient if the player has permanently left the session.

## D-183 — Strongbox rarity uses mission settings and the owner's account level

- Status: accepted
- Choice: custom — mission configuration plus personal account progression
- Accepted requirement: Each player's major-reward strongbox rarity table is determined by the mission settings and that player's own account level.
- Mission-settings rule: Mission identity, selected difficulty, and explicit challenge modifiers may affect the base reward table.
- Personal-progression rule: Account-level influence applies only to the owner of that reward roll.
- Independence rule: Teammate account levels, equipment power, performance, or progression never modify another player's rarity odds.
- Mixed-level rule: A level-100 player may play with a level-17 player without directly upgrading or degrading the level-17 player's reward table.

## D-184 — Fixed mission difficulty with real player power preserved

- Status: accepted
- Choice: A — one selected mission difficulty for all participants
- Accepted requirement: Co-op enemies use the chosen mission and difficulty configuration for every participant. The game does not normalize players to one another or automatically scale difficulty from account levels.
- Progression rule: Players keep their real equipment, builds, and account power, so experienced players may carry newer friends.
- Transparency rule: The mission configuration, rather than hidden account-level scaling, determines the baseline challenge.
- Challenge rule: Later achievements or challenge modes may explicitly reward level-capped teams, equal-level parties, standardized loadouts, or other constrained runs.

## D-185 — Separate solo and co-op completion records

- Status: accepted
- Choice: B with custom completion separation
- Accepted requirement: Players retain valid co-op loot, currency, account experience, and rewards, while solo and co-op mission completions are recorded separately.
- Solo rule: A co-op clear does not automatically count as a solo campaign clear or solo record.
- Co-op rule: Co-op clears have their own completion, difficulty, challenge, and record state.
- Clarity rule: Results and campaign interfaces must make the two completion categories explicit rather than silently treating them as interchangeable.

## D-186 — Separate co-op campaign progression track

- Status: accepted
- Choice: A — independent co-op campaign advancement
- Accepted requirement: Completing an eligible mission in co-op unlocks subsequent missions in that player's co-op progression track.
- Separation rule: Solo campaign progression remains unchanged by co-op advancement.
- Continuity rule: A group may play through the campaign together without requiring every participant to repeat each mission solo between co-op sessions.
- Interface rule: Mission selection must visibly distinguish solo unlock state, co-op unlock state, and completion badges.

## D-187 — Party-leader mission access with visible guest-run status

- Status: accepted
- Choice: C — leader access plus per-player eligibility
- Accepted requirement: The party leader may launch any co-op mission available in the leader's own co-op campaign.
- Guest rule: Players who have not reached that mission in their own co-op progression join with explicit guest-run status.
- Reward rule: Guest players retain personal loot, account rewards, and valid guest-run records.
- Advancement rule: A guest clear advances that player's co-op campaign only when the player's own prerequisites are satisfied; otherwise the clear remains recorded without sequence-breaking progression.
- Pre-launch rule: The lobby must show which players are eligible for campaign advancement before mission launch.

## D-188 — Teleport-based late joining as the end goal, staged after lobby-only co-op

- Status: accepted
- Choice: B as the desired end state, with reduced first-version scope
- Accepted requirement: The long-term co-op design allows new players to join an active mission at an activated teleport rather than spawning directly into combat.
- Safety rule: Late joining occurs only at a controlled synchronized teleport state.
- Initial-scope rule: The first co-op MVP may support lobby-only party formation plus reconnection, without requiring entirely new players to join mid-run.
- Delivery rule: Teleport-based late joining is added only after session synchronization, reward eligibility, life allocation, and reconnect behavior are reliable.

## D-189 — Late-join lives scale down with mission progress

- Status: accepted
- Choice: B — authored mission-progress scaling
- Accepted requirement: When teleport-based late joining exists, a new player receives fewer personal lives when joining later in the mission.
- Calculation rule: Use a simple, understandable authored rule tied primarily to reached teleport segments or comparable mission milestones.
- Independence rule: The allowance does not derive from teammate account levels or a complicated average of current players' remaining lives.
- Validation rule: Test carefully to avoid both unfairly weak late entry and exploitation through rotating fresh players into a failing run.
- Deferred detail: Exact starting-life counts and reduction steps remain difficulty- and playtest-dependent.

## D-190 — Combat scales modestly with active player count

- Status: accepted
- Choice: B — player-count-based encounter scaling
- Accepted requirement: Co-op combat pressure changes with the number of active players. Three similarly progressed players and four similarly progressed players must not receive an identical effective encounter.
- Scaling rule: Adjust a restrained combination of encounter budget, reinforcement count, enemy count, durability, or other authored variables according to active party size.
- Level-independence rule: Scaling uses active player count and selected mission settings, not participant account levels.
- Room-lock rule: Determine or lock the encounter's party-size scaling when the room engagement begins so deaths, spectating, disconnects, or joins do not cause unstable mid-fight changes.
- Feel rule: Prefer additional pressure and composition changes over excessive health multiplication that makes ordinary enemies feel spongy.
- Deferred detail: Exact coefficients and per-room scaling curves require prototyping and playtesting.

## Batch persistence

- Persisted through: D-190
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-200
