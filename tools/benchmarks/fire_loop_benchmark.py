#!/usr/bin/env python3
"""Synthetic comparison of the removed replay loop and the new shot timer.

This is deliberately not presented as a Unity Profiler replacement. It measures the
shape of the two algorithms for three seconds of held Rattler fire at 50 game ticks/s:

- old: retain, copy, sort, hash, validate, and prune all prior tick records
- new: compare the current time with one NextShot value
"""

from __future__ import annotations

import hashlib
import statistics
import time
from collections.abc import Callable

TICKS = 150
TICKS_PER_SECOND = 50
SHOTS_PER_SECOND = 4.0


def old_fire() -> int:
    records: list[tuple[int, str, str]] = []
    receipts: dict[str, int] = {}
    next_shot = 0.0
    shots = 0

    for tick in range(TICKS):
        now = tick / TICKS_PER_SECOND
        fired = now + 1e-12 >= next_shot
        if fired:
            shots += 1
            next_shot = now + (1.0 / SHOTS_PER_SECOND)

        text = (
            f"tick={tick}|held=1|fired={int(fired)}|next={next_shot:.9f}|"
            f"actor=player|gun=rattler|operation=fire-{tick}|shot={shots}"
        )
        fingerprint = hashlib.sha256(text.encode()).hexdigest()
        records.append((tick, fingerprint, text))

        copied = sorted(records, key=lambda value: (value[0], value[1]))
        state_text = "\n".join(value[1] for value in copied).encode()
        hashlib.sha256(state_text).digest()

        for _, expected, record_text in copied:
            actual = hashlib.sha256(record_text.encode()).hexdigest()
            if actual != expected:
                raise RuntimeError("invalid synthetic replay record")

        receipts = {
            value[1]: value[0]
            for value in copied
            if "fired=1" in value[2]
        }
        sum(1 for value in copied if value[1] in receipts)

    return shots


def new_fire() -> int:
    next_shot = 0.0
    shots = 0

    for tick in range(TICKS):
        now = tick / TICKS_PER_SECOND
        if now + 1e-12 >= next_shot:
            shots += 1
            next_shot = now + (1.0 / SHOTS_PER_SECOND)

    return shots


def median_ns(run: Callable[[], int], rounds: int) -> int:
    values: list[int] = []
    for _ in range(rounds):
        start = time.perf_counter_ns()
        run()
        values.append(time.perf_counter_ns() - start)
    return int(statistics.median(values))


def main() -> None:
    for _ in range(20):
        old_fire()
        new_fire()

    old_ns = median_ns(old_fire, 200)
    new_ns = median_ns(new_fire, 5000)
    history_checks = sum(range(1, TICKS + 1))

    print(f"shots: old={old_fire()} new={new_fire()}")
    print(f"old median: {old_ns / 1_000_000:.3f} ms")
    print(f"new median: {new_ns / 1_000_000:.3f} ms")
    print(f"ratio: {old_ns / new_ns:.1f}x")
    print(f"old retained-record visits: {history_checks}")
    print(f"new timer checks: {TICKS}")


if __name__ == "__main__":
    main()
