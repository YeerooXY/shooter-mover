# Shooter Mover — Product Discovery Batch D-171 through D-180

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-171 — Teleports provide checkpoint respawning and fast travel

- Status: accepted
- Choice: B with a bounded custom extension — activated teleports provide checkpoint respawning and fast travel, while only a small selected subset also hosts shops
- Accepted requirement: Activated teleports connect to previously activated teleport destinations and remain usable only under safe, non-combat conditions.
- Scope rule: Most teleports remain focused transport and checkpoint locations. Only a few authored teleports per map may include a shop so the network does not become visually or mechanically bloated.
- Navigation rule: Teleport destinations, activation state, shop presence, and unavailable routes must be clear on the room map.
- Co-op rule: The teleport model should later support regrouping without requiring co-op implementation in the Windows internal MVP.

## D-172 — Stable per-run shop stock with optional run-bound refreshes

- Status: accepted
- Choice: A with a custom refresh extension
- Accepted requirement: Each teleport shop rolls a small inventory once when the mission run begins. Stock and sold-out state remain stable during that run unless the player deliberately spends an allowed refresh.
- Refresh rule: Optional refreshes use a mission-bound resource or allowance that cannot be banked, exported, or retained after the run ends.
- Economy rule: Revisiting a shop does not refresh it for free, and death or teleport travel must not regenerate stock or refresh resources.
- Scope rule: This is the smallest real refresh foundation; persistent reroll currencies and mature shop-manipulation systems remain deferred.

## D-173 — Earned run-only shop-refresh tokens

- Status: accepted
- Choice: B — refresh tokens earned within the current mission
- Accepted requirement: Shop-refresh tokens may be awarded through authored optional rooms, challenges, strongboxes, or selected encounters during a run.
- Persistence rule: Unused refresh tokens disappear when the mission ends and never enter permanent account inventory.
- Balance rule: Tokens should be scarce enough to preserve shop decisions and may use deterministic or authored placement where fair replay and route learning matter.
- Anti-farming rule: Cleared-room persistence, stable encounter rewards, and checkpoint rollback rules must prevent repeated token farming inside one run.

## D-174 — Loot banks only at specific authored banking locations

- Status: accepted
- Choice: B refined by the user — carried loot is secured at dedicated authored rooms rather than every teleport
- Accepted requirement: Weapons, strongboxes, currency, and other eligible mission rewards collected since the previous banking point remain provisional until deposited at a banking location.
- Death rule: Unbanked eligible loot is lost or rolled back according to the active checkpoint snapshot when the player dies.
- Shop rule: Purchases completed directly at a teleport shop are secured immediately so a paid item is not lost merely because the player leaves the shop segment.
- Communication rule: The interface must clearly distinguish secured rewards from loot currently at risk.

## D-175 — Dedicated secure-storage rooms

- Status: accepted
- Choice: B — distinct secure-storage or transfer rooms
- Accepted requirement: A small number of clearly marked vault, uplink, cargo-transfer, or secure-storage rooms permanently bank carried loot.
- Map rule: Banking rooms use a distinct icon and readable visual language. They may be located near route junctions or selected teleports but remain a separate room function.
- Pacing rule: Placement should create meaningful risk segments without forcing tedious backtracking merely to secure ordinary progress.
- Reuse rule: The dedicated room type should remain usable in later, larger, and less linear missions.

## D-176 — Persistent world progress with temporary-state rollback

- Status: accepted
- Choice: C — preserve permanent room and objective progress while rolling back temporary state
- Accepted requirement: Defeated enemies remain defeated, explored rooms remain mapped, completed objectives remain complete, and permanently opened routes remain open after death.
- Rollback rule: Death removes unsecured loot and resets temporary post-checkpoint resources, buffs, deployed objects, loose pickups, and other explicitly temporary state.
- Architecture rule: Runtime and save data must classify state explicitly as persistent mission progress, checkpoint-respawn state, temporary run state, or permanently banked progression.
- Exploit rule: Temporary-state rollback must prevent duplication and death-based farming without causing cleared enemies to reappear.

## D-177 — Solo suspend saves outside combat; no assumption of co-op suspension

- Status: accepted
- Choice: B for solo play with a co-op limitation
- Accepted requirement: In solo play, the game may create one automatic resumable suspend snapshot when the player quits from a cleared or otherwise safe room outside combat.
- Continuity rule: The suspend snapshot preserves the current room-map state, unsecured loot, temporary resources, and route progress, and is consumed or advanced as normal play continues rather than serving as a rewind slot.
- Safety rule: Writes must use the accepted versioned local-first save protections, including atomic replacement, validation, and recovery handling.
- Co-op rule: This decision does not grant suspension to co-op sessions; co-op suspension is explicitly resolved separately by D-178.

## D-178 — Non-suspendable co-op runs with authority continuity and personal lives

- Status: accepted
- Choice: custom — co-op sessions cannot be suspended, remaining players may continue after the host leaves, and each player has a personal life allowance
- Accepted requirement: A live co-op mission cannot be suspended and resumed later as a shared run.
- Host-continuity rule: Later co-op architecture must support host migration, transferable authority, or an equivalent design so the remaining players can continue when the original host disconnects or leaves.
- Life rule: Each player has a separate limited number of lives. A player who exhausts all personal lives becomes a spectator while surviving teammates continue.
- Failure rule: The co-op run ends only when no active player remains capable of continuing, allowing a final survivor to attempt a clutch completion.
- Scope rule: These are future co-op requirements and do not expand the offline Windows internal MVP into networking work now.

## D-179 — Immediate personal-life consumption for the first co-op implementation

- Status: accepted
- Choice: A for the initial co-op version; revivable downed states deferred for experimentation
- Accepted requirement: In the first co-op implementation, reaching zero health immediately consumes one of that player's remaining lives and respawns the player according to the active checkpoint rules.
- Deferred extension: A limited downed state with teammate revival is considered promising but remains a post-MVP prototype and playtest decision.
- Simplicity rule: Do not build revive timers, incapacitated-state protection, revive interactions, or enemy anti-camping behaviour merely for the offline internal MVP.
- Spectator rule: When a player has no lives remaining, the accepted spectator-and-clutch behaviour from D-178 applies.

## D-180 — Fixed personal lives for the mission, scaled by difficulty

- Status: accepted
- Choice: A — fixed lives that cannot be replenished during the run
- Accepted requirement: Every co-op player starts a mission with a fixed personal life allowance. Lost lives are not restored by teleports, objectives, optional rooms, shops, or pickups during that run.
- Difficulty rule: The exact starting allowance is determined by the selected difficulty and remains a balancing variable until co-op prototypes and playtests exist.
- Fairness rule: Player count may inform later balancing, but the game should preserve individual responsibility rather than replacing personal lives with a shared pool.
- Scope rule: Rare extra-life rewards and life-restoring checkpoints remain deferred unless later testing demonstrates unacceptable spectator downtime.

## Batch persistence

- Persisted through: D-180
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-190
