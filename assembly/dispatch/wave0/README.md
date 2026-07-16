# Wave 0 dispatch index

Status: completed. PR #130 (`AUD-001`), PR #132 (`ADR-001`), and PR #131
(`DEMO-001`) are merged. This directory is retained as historical dispatch
evidence. Current prompts are under `assembly/dispatch/wave1/`.

Base commit for all three tasks:

`56a84838558fdfe67fb97254d832b2dd7cd5c018`

| Task | Agent type | May edit Stage 1? | Main output |
|---|---|---:|---|
| `ADR-001` | GitHub web agent | No | Architecture/lifecycle decision records |
| `AUD-001` | GitHub web agent | No | Evidence-backed existing-system audit |
| `DEMO-001` | Local/path-capable agent | Yes, exclusive | Complete playable baseline with robot |

Dispatch all three in parallel on separate branches.

Historical merge rules used for Wave 0:

- Each PR targets `main`.
- `ADR-001` and `AUD-001` must not modify implementation.
- `DEMO-001` must not add reward/economy architecture.
- The former Wave 1 blocker was satisfied when `ADR-001` merged through PR #132.
- The former `BASE_AFTER_DEPENDENCIES` placeholders have been replaced by the
  exact Wave 1 base in `assembly/dispatch/wave1/`.

Prompt files:

- [ADR-001 web prompt](ADR-001_WEB_AGENT.md)
- [AUD-001 web prompt](AUD-001_WEB_AGENT.md)
- [DEMO-001 local prompt](DEMO-001_LOCAL_AGENT.md)
