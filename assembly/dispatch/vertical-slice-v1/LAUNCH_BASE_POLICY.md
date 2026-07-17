# Launch-base policy for deferred packets

The batch was prepared against verified `main` commit `645cf24f30ee6c8762214a84060e59e35df67a05`. That SHA is the exact launch base only for tasks whose dependencies were already merged at preparation:

- ROOM-001
- LEVELDES-001
- XP-002
- DROP-001
- WEAPON-DATA-001

MENU-002 has its own exact base and PR base in its packet.

Every other packet is deferred behind one or more unmerged dependencies. Before dispatching a deferred packet, the coordinator must:

1. verify every named dependency is merged and proof-complete;
2. fetch current `main`;
3. record the exact current-main SHA as a launch override in the agent instruction/PR;
4. create a fresh branch from that exact SHA;
5. verify zero commits behind at branch creation.

The dispatch-time launch override supersedes the preparation-baseline line inside a deferred packet. Never branch a deferred task from `645cf24` after its dependencies have merged, and never merge dependency branches into a stale task branch to simulate readiness.
