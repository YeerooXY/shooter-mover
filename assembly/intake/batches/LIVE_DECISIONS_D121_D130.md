# Shooter Mover — Product Discovery Batch D-121 through D-130

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-121 — Hybrid enemy attack density

- Status: accepted
- Choice: C — readable aimed pressure with bounded authored barrages
- Accepted requirement: Most enemies use readable aimed attacks, bursts, melee advances, flanking, and area denial. Selected units, elites, room events, and bosses may create denser bullet-pattern barrages as authored peaks rather than the permanent baseline.
- Difficulty rule: Stronger modes should emphasize smarter combinations, coordination, projectile speed, and additional pattern layers rather than simply filling the screen with bullets.
- Readability rule: Enemy threats must remain legible alongside four simultaneous player weapons and later Android controls.

## D-122 — Starting formations with limited reinforcements

- Status: accepted
- Choice: C — authored initial formations plus deterministic reinforcements
- Accepted requirement: Most threats are physically present and readable when combat begins. Selected enemies may enter through clearly telegraphed doors, vents, elevators, dropships, or teleporters.
- Trigger rule: Reinforcements respond to deterministic conditions such as objectives, elapsed combat state, enemy deaths, or player position.
- Fairness rule: Enemies must not appear invisibly on top of or unfairly behind the player.
- Mastery rule: Skilled players may learn and manipulate reinforcement timing.

## D-123 — Selective authored combat lockdowns

- Status: accepted
- Choice: C — ordinary skirmishes remain connected; major encounters may seal exits
- Accepted requirement: Ordinary encounters usually permit retreat or repositioning. Major ambushes, elite fights, defensive objectives, bosses, and set pieces may temporarily lock clearly signalled exits.
- Warning rule: One-way combat thresholds and lockdowns must be unmistakable before commitment.
- Anti-cheese rule: Retreatable fights use enemy behaviour, pursuit, suppression, and positioning rather than invisible walls to resist doorway exploitation.

## D-124 — Role-based pursuit within encounter boundaries

- Status: accepted
- Choice: C — selected hunters pursue while other roles hold or reposition
- Accepted requirement: Fast melee units, hunters, and selected elites may chase through nearby connected rooms. Defensive, heavy, ranged, and objective-bound enemies normally hold or reposition within their authored encounter area.
- Safety rule: Enemies do not invade checkpoints, shops, or other protected spaces.
- Anti-cheese rule: Repeated doorway peeking may provoke flanking, suppression, grenades, or a coordinated push.

## D-125 — Enemy- and weapon-specific predictive aiming

- Status: accepted
- Choice: B — bounded leading for specialised ranged threats
- Accepted requirement: Basic enemies mostly fire toward the player's current position. Skilled gunners, snipers, launchers, elites, and selected attacks may lead the player's current velocity.
- Fairness rule: Prediction must remain bounded and cannot perfectly foresee sudden boost reversals.
- Difficulty rule: Stronger modes may improve prediction accuracy and coordination without becoming clairvoyant.

## D-126 — Tracking wind-up followed by visible aim lock

- Status: accepted
- Choice: B — track for most of the telegraph, then visibly commit
- Accepted requirement: Selected attacks may follow the player during their wind-up, but lock direction shortly before firing.
- Telegraph rule: A clear flash, sound, animation, or equivalent signal marks the commitment moment.
- Skill rule: Players may bait attacks and use last-second movement or boost reversals after the visible lock.

## D-127 — Cover-respecting projectiles with explicit exceptions

- Status: accepted
- Choice: B — ordinary attacks collide with cover; specialised attacks deliberately break the rule
- Accepted requirement: Bullets, rockets, beams, and other direct attacks collide consistently with appropriate walls and obstacles.
- Exception rule: Clearly marked attacks may arc over cover, ricochet, pierce thin structures, destroy cover, or create area hazards.
- Readability rule: Enemy role, projectile visuals, and telegraphs must make cover exceptions obvious and learnable.

## D-128 — Selectively destructible enemy projectiles

- Status: accepted
- Choice: B — only specific physical threats may be shot down
- Accepted requirement: Ordinary bullets and beams generally cannot be erased. Rockets, mines, drones, slow plasma spheres, and comparable clearly marked physical threats may be destroyed by player fire.
- Balance rule: Four continuously firing weapons must not become an automatic permanent projectile shield.
- Scope note: Exact interception health, hit detection, and audiovisual tuning are deferred to later detailed combat design.
- Discovery preference: From this point, prioritize higher-level product, campaign, world, scope, platform, and MVP decisions before returning to micro-level combat rules.

## D-129 — Light but meaningful campaign narrative

- Status: accepted
- Choice: B — coherent story that supports rather than interrupts replayable action
- Accepted requirement: The campaign uses recurring characters, factions, discoveries, and escalating stakes.
- Delivery rule: Story is conveyed through concise briefings, environmental storytelling, short in-level dialogue, and restrained scenes.
- Replay rule: Narrative delivery must be skippable or accelerated on repeat runs and speed-oriented play.
- Scope rule: Story provides identity and emotional momentum without turning the project into a cinematic-first game.

## D-130 — Central hub with controlled branching missions

- Status: accepted
- Choice: B — persistent hub connecting authored main and optional missions
- Accepted requirement: A central base or mission interface connects authored levels, shops, progression, characters, challenges, and optional content.
- Structure rule: Main missions advance the story. Optional missions and selected branches may be tackled in different orders before converging at major story points.
- Scope rule: The campaign provides meaningful route choice without becoming an open-world or broadly nonlinear production burden.
- Balance rule: Branching must remain compatible with authored progression bands, weapon unlocks, narrative pacing, and deterministic replay.

## Batch persistence

- Persisted through: D-130
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-140
