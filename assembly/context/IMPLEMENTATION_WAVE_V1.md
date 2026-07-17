# Implementation Wave V1 handoff

The current implementation wave is downstream of the planning batch in PR #172 and the completed weapon-system planning session.

The critical identity rule remains: one strongbox instance produces one reward at most; separate boxes may produce the same weapon definition as separate equipment instances. Results displays unopened instances and does not open or consume them.

The next interactive implementation sessions are SKILL-003, AUG-002, ACT-001, and WEAPON-IMPL-001. Each must plan first, wait for approval, then implement.

The current equipment foundation already includes immutable equipment/augment definitions, deterministic augment upgrades, and deterministic scrap-funded crafting runtime. Production content, UI, live stat projection, and final route composition remain incomplete.
