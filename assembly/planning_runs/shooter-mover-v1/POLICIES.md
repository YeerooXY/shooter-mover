# Planning Policies

## 1. Toolchain lock

Baseline source-art tools:

- Blender for offline 3D modeling, rigging, animation, lighting, and sprite rendering;
- Krita for painted 2D, cleanup, normal-map finishing, UI art, and texture work;
- Audacity or another explicitly approved editor for simple audio cleanup;
- project-owned deterministic export scripts where repetition justifies them.

Exact versions are pinned before the first production asset is accepted in `docs/toolchain/TOOLCHAIN_LOCK.md`. Tool upgrades are isolated reviewed changes with representative re-export, Unity reimport, visual comparison, memory/performance validation, and rollback.

No critical pipeline step may depend solely on a cloud service or unrecorded personal preset.

## 2. Asset provenance

Every release-bound asset has a stable record under `source-assets/manifests/` or `docs/provenance/` containing:

- asset ID/package;
- author or supplier;
- creation date and source files;
- original, commissioned, purchased, licensed, or generated status;
- license, restrictions, attribution, and receipt/contract reference;
- source URLs/vendor IDs where applicable;
- generation tool/model/service, material inputs, and process notes;
- derivative chain and transformations;
- export recipe/output fingerprint;
- human approval;
- allowed channel: prototype-only, internal-test, release-bound, or rejected.

Unknown, incompatible, or unverifiable provenance blocks release-bound use. Prototype-only material is visibly marked and cannot be promoted accidentally.

## 3. Source assets and disaster recovery

Use 3-2-1 recovery:

1. working source plus approved Git-LFS assets;
2. encrypted versioned off-device backup/object storage;
3. separate encrypted offline backup refreshed at milestone boundaries.

The repository stores Unity-ready assets, reviewable source, manifests, recipes, previews, deterministic scripts, and selected large production source through Git LFS. Full high-resolution archives, raw recordings, intermediate renders, and supplier packages may live in a separate versioned archive referenced by manifest IDs.

At formal milestones identify the protected commit, export dependency/source manifests, back up planning/source/generated/release metadata, verify checksums, and perform a sample restore.

Perform at least one clean recovery drill in Stage 1 and two in Stage 2. An un-restored backup is not accepted evidence.

## 4. Secrets and release materials

Credentials, signing certificates, private keys, storefront secrets, and private supplier contracts never enter the product repository.

The human lead keeps primary secrets in an approved manager/store, encrypted offline recovery material, renewal/revocation steps, and a non-secret repository record of what exists. The internal MVP requires checksums, not public signing infrastructure.

## 5. Prototype-shortcut register

Create `docs/architecture/PROTOTYPE_DEBT.md` during implementation. Every shortcut records debt ID, paths/owner, reason/evidence question, preserved invariants, risks, detection tests, expiry milestone, removal condition, status, and review decision.

A shortcut is allowed only when isolated, readable, testable, deterministic, cheap to remove, and harmless to authoritative state/evidence.

Never acceptable as prototype debt:

- scene-owned durable mission truth;
- mutable runtime state in shared ScriptableObjects;
- unstable/reused persistent IDs;
- non-atomic reward, banking, completion, or save transitions;
- data loss or duplicate grants;
- hard-coded device input in gameplay rules;
- hand-edited generated files;
- unlicensed release assets;
- secrets in source/builds;
- missing diagnostics required for evidence;
- deferring accessibility or performance while retaining breadth.

Before Stage 2 acceptance:

- no blocking/expired debt remains;
- no temporary implementation leaks beyond its declared package/lane;
- retained non-blocking debt has owner, risk, test, and human approval;
- authority, persistence, IDs, generation, input, diagnostics, performance, and build reproduction use the intended architecture;
- a fresh contributor succeeds without hidden rescue steps.

## 6. Dependencies

Use Unity LTS, URP 2D, Input System, Test Framework, and only small justified dependencies. Record source, version, purpose, license, owner, update policy, risks, and removal path. Disable automatic upgrades and isolate external APIs behind owned adapters where reasonable.

Do not add analytics, crash-reporting SaaS, storefront, account, networking, relay, mobile, advertising, or monetization SDKs during the internal MVP.

## 7. Provisional Windows hardware matrix

Primary 1080p/60 profile:

- 64-bit Windows 10/11;
- mainstream six-core x86-64 CPU, approximately 2019 class or newer;
- 16 GB RAM;
- DirectX 11 dedicated GPU with about 6 GB VRAM, GTX 1660 / RX 5600 XT class;
- SSD.

Minimum readable-completion floor:

- 64-bit Windows 10/11;
- four-core x86-64 CPU;
- 8 GB RAM;
- DirectX 11 dedicated GPU with about 4 GB VRAM, GTX 1050 Ti / RX 570 class;
- SSD preferred; HDD loading measured but not promised before evidence.

Also test a different GPU/driver family, a clean Windows installation, and one high-refresh/high-resolution system. Record actual hardware IDs before formal evidence. A later hardware change versions the benchmark.

## 8. Performance acceptance

For a ten-minute representative heavy-combat capture after warm-up on the primary profile:

- target frame pacing is 16.67 ms;
- median CPU/GPU frame times remain within the 60 FPS budget;
- 95th-percentile total frame time is at or below 20 ms;
- no recurring normal-workload spike exceeds 50 ms;
- steady-combat managed allocations are zero or explicitly bounded;
- enemy, projectile, light, particle, audio, memory, atlas, and loading counts remain inside recorded budgets.

Minimum hardware completes identical gameplay readably at reduced visuals. Quality settings never alter simulation, collision, behavior, rewards, or progression.

## 9. Economy and tuning

Planning freezes boundaries, not mature coefficients. Versioned tuning profiles own movement curves, weapon values, enemy values, difficulty, currency, prices, reward distribution, shop size, and refresh-token supply. Formal evidence references one immutable profile.

Internal economy:

- eight identical-copy base weapons;
- one stable-per-run shop;
- mission-only refresh tokens;
- deterministic sealed strongboxes;
- one gameplay currency;
- immediate secure shop purchase;
- dedicated banking rooms;
- smallest safe duplicate conversion: deterministic currency refund;
- capacity-safe reward review and pending inbox.

## 10. Highest risks

1. movement becomes slippery, trivial, or mandatory;
2. four weapons become unreadable;
3. scene state diverges from mission state;
4. serialized assets collide across agents;
5. save/reward complexity consumes Stage 2 before fun is proven;
6. the 24-room slice expands through polish/variants;
7. art tooling becomes a bespoke manual bottleneck;
8. the solo reviewer becomes the throughput limit;
9. diagnostics/tests overgrow the product question;
10. future co-op tempts premature networking architecture.

Mitigate through milestone gates, strict ownership, generated integration, breadth-first cuts, local ports instead of speculative services, and debt exit rules.
