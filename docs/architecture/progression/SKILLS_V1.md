# Skills V1

SKILL-001 adds an engine-independent, data-driven skill progression authority and a standalone presentation scene.

## Contract

- A catalog contains exactly 20 uniquely identified skills.
- Each skill has an authorable display name, description, maximum rank and optional prerequisite.
- Total earned skill points equal the XP authority's player level. Level 1 therefore starts with one point.
- Allocation is exactly-once by operation identity. Replaying an applied operation is a no-op.
- Rank caps, prerequisites and available-point checks are enforced before mutation.
- Snapshots expose player level, sequence, immutable rank data, applied operation identities, spent points and available points.

## XP integration

The runtime accepts player level as an input boundary. Production composition should update `SetPlayerLevel` from `IPlayerExperienceAuthorityV1.CurrentState.Level`; the skill authority does not grant XP or mutate the XP authority.

## Scene

`Assets/ShooterMover/Scenes/Skills/Skills.unity` is presentation-only. It creates the default 20-skill catalog and renders four branches of five cards. The preview level is serialized for deterministic authoring and demonstrations.

## Authority boundaries

The scene does not mutate Stage 1, rewards, wallets, inventory, equipment, shops or crafting. `Stage1VisibleSlice.unity` remains untouched.
