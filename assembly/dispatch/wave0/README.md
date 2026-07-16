# Wave 0 dispatch index

Base commit for all three tasks:

`56a84838558fdfe67fb97254d832b2dd7cd5c018`

| Task | Agent type | May edit Stage 1? | Main output |
|---|---|---:|---|
| `ADR-001` | GitHub web agent | No | Architecture/lifecycle decision records |
| `AUD-001` | GitHub web agent | No | Evidence-backed existing-system audit |
| `DEMO-001` | Local/path-capable agent | Yes, exclusive | Complete playable baseline with robot |

Dispatch all three in parallel on separate branches.

Merge rules:

- Each PR targets `main`.
- `ADR-001` and `AUD-001` must not modify implementation.
- `DEMO-001` must not add reward/economy architecture.
- Wave 1 remains blocked until `ADR-001` merges.
- Replace future `BASE_AFTER_DEPENDENCIES` placeholders with exact merged
  `main` SHAs at dispatch time.

Prompt files:

- [ADR-001 web prompt](ADR-001_WEB_AGENT.md)
- [AUD-001 web prompt](AUD-001_WEB_AGENT.md)
- [DEMO-001 local prompt](DEMO-001_LOCAL_AGENT.md)
