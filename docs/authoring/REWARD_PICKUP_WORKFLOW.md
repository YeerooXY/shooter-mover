# Reward Pickup Authoring and Runtime Workflow

## Purpose

`PICK-001` provides reusable 2D physical projections for reward commitments. A pickup can present money, scrap, strongboxes, equipment references, premium-ammo/miscellaneous stacks, or a mixed commitment without becoming a reward authority itself.

The ownership boundary is strict:

- **SRC-001** owns authored source identity and the resolved reward profile.
- **GEN-001** resolves profile-based reward quantities and selections.
- **RAP-001** owns commitment, projection, claim, atomic child preflight/application, retry, and exact replay truth.
- **MON-001 / SCR-001 / INV-001** remain the only value and holdings authorities behind RAP child adapters.
- **PICK-001** owns transient collider, visual, category, and quick-restart projection state only.

No pickup component mutates a money or scrap wallet, and no pickup inserts holdings directly.

## Components

### `RewardPickupDropFactory2D`

Attach the factory to a runtime drop anchor and inject:

1. `RewardGenerationServiceV1`;
2. the immutable `ProgressionContext` for the source;
3. deterministic root seed and algorithm version;
4. a `RewardPickupApplicationAuthority2D` configured with the live RAP service;
5. the owning `GameplaySceneScope2D` for quick restart;
6. optionally, a pickup prefab and an equipment payload resolver.

The factory implements `IRewardSourceOperationSink`, so a `RewardSourceAuthoring2D` can submit its resolved SRC preview directly. Repeated SRC callbacks regenerate the same deterministic reward, reuse the same commitment and pickup identities, and return exact-duplicate/no-change instead of creating another projection.

Profile-based strongbox grants receive deterministic instance identities derived from source operation, grant identity, and instance ordinal. Profile-based equipment-reference grants require an `IRewardPickupEquipmentPayloadResolverV1`, because RAP/INV must retain the exact immutable `EquipmentInstance` objects. A missing resolver fails closed.

### Forced drops

Call `RewardPickupDropFactory2D.SpawnForced` with a fully prepared `RewardCommitCommandV1`. Forced and profile-based drops converge at the same RAP commit boundary. This path supports pre-resolved equipment instances, exact strongbox identities, value grants, stacks, and mixed commitments without introducing a second collection algorithm.

### `RewardPickup2D`

A pickup automatically ensures it has:

- a trigger `CircleCollider2D`;
- a `SpriteRenderer`;
- the configured collection radius;
- category-selected sprite, tint, and local scale.

Use `RewardPickupPresentationStyleV1` entries to configure money, scrap, strongbox, equipment, and miscellaneous presentation independently. A mixed commitment uses the miscellaneous category.

The trigger accepts only an explicit `RewardPickupClaimant2D`. Claimant identity is a canonical `StableId`; tags, object names, hierarchy positions, and Unity instance IDs never participate in reward identity.

## Deterministic identities

One physical pickup represents one complete RAP commitment. Its identities are derived from immutable reward identities:

- pickup: source operation + commitment;
- projection: pickup identity;
- claim: pickup identity + claimant identity;
- restart participant: pickup identity;
- generated strongbox instance: source operation + grant identity + instance ordinal.

Renaming, reparenting, collision callback count, frame timing, and quick-restart attempt number cannot change these identities.

## Collection and duplicate protection

Collection performs these steps through the injected RAP service:

1. project the commitment using the deterministic pickup projection identity;
2. claim using the deterministic pickup/claimant identity;
3. let RAP preflight every MON/SCR/INV child before the first apply;
4. retry a retained pending claim with the same claim identity;
5. hide the physical projection only after RAP reports applied or already-applied/no-change.

`RewardPickup2D` also has a local in-progress and collected guard. Repeated `OnTriggerEnter2D` callbacks therefore do not issue parallel claims. The durable guarantee remains RAP: even a recreated physical projection receives `AlreadyAppliedNoChange` and grants no additional value.

## Quick restart behavior

Pickups register with the owning `GameplaySceneScope2D` as typed restart participants.

- An **unclaimed** pickup re-enables its collider and renderer for the replacement attempt.
- An **applied** pickup remains retired because RAP claim truth survives quick restart.
- A **pending** claim remains visible and can retry with the same claim identity.

Quick restart never clears or rewrites RAP, wallet, scrap, or holdings truth.

## Validation checklist

Before placing a pickup factory or prefab, verify:

- the SRC source resolves a canonical operation and profile;
- the factory has generator, progression context, seed/version, RAP adapter, and scope;
- the RAP adapter uses the actual MON, SCR, and INV child authority IDs;
- equipment-producing profiles have an immutable equipment payload resolver;
- collection radius is positive;
- every intended category has a presentation style or accepts the prefab default;
- the collector carries `RewardPickupClaimant2D` with a stable claimant ID;
- no scene script performs an additional wallet or holdings mutation after pickup collection.

## Rollback

Revert the PICK-001 files. SRC generation, RAP commitments and claims, and MON/SCR/INV authority state remain independently owned and are not modified by this package.
