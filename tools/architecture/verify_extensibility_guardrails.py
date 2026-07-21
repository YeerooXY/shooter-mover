#!/usr/bin/env python3
"""Fail closed when EXTENSIBILITY-GUARDRAILS-001 leaks into production registration code."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

LAUNCH_SHA = "7b2dfb1dadb13a6d8c0631a56d10fc44f3080472"

ALLOWED_PATHS = {
    "Assets/ShooterMover/Tests/EditMode/ExtensibilityGuardrailsV1Tests.cs",
    "Assets/ShooterMover/Tests/EditMode/ExtensibilityGuardrailsV1Tests.cs.meta",
    "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_enemy_catalog_v1.json",
    "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_enemy_catalog_v1.json.meta",
    "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_access_v1.json",
    "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_access_v1.json.meta",
    "docs/authoring/EXTENSIBILITY_CONTENT_CHECKLIST_V1.md",
    "docs/verification/EXTENSIBILITY_GUARDRAILS_001.md",
    "tools/architecture/verify_extensibility_guardrails.py",
}

REQUIRED_FIXTURES = {
    "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_enemy_catalog_v1.json",
    "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_access_v1.json",
}

FORBIDDEN_PREFIXES = (
    "Assets/ShooterMover/Runtime/EnemyRuntimeComposition/",
    "Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/",
    "Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/",
)

FORBIDDEN_BASENAMES = {
    "Stage1VisibleSliceController.cs",
    "Stage1PlayableLoopCompositionV1.cs",
    "Stage1PlayableLoopCompositionV1.Catalogs.cs",
}


def git(*args: str) -> str:
    completed = subprocess.run(
        ["git", *args],
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(args)} failed ({completed.returncode}):\n"
            f"{completed.stderr.strip()}"
        )
    return completed.stdout


def changed_paths(base: str, head: str) -> list[tuple[str, str]]:
    output = git("diff", "--name-status", "--find-renames", f"{base}...{head}")
    result: list[tuple[str, str]] = []
    for raw_line in output.splitlines():
        if not raw_line.strip():
            continue
        fields = raw_line.split("\t")
        status = fields[0]
        path = fields[-1]
        result.append((status, path))
    return result


def validate_json(path: Path) -> None:
    try:
        with path.open("r", encoding="utf-8") as handle:
            json.load(handle)
    except (OSError, json.JSONDecodeError) as error:
        raise RuntimeError(f"Invalid fixture JSON at {path}: {error}") from error


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default=LAUNCH_SHA)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()

    git("merge-base", "--is-ancestor", args.base, args.head)
    changes = changed_paths(args.base, args.head)
    if not changes:
        raise RuntimeError("The guardrail proof diff is empty.")

    changed_names = {path for _, path in changes}
    unexpected = sorted(changed_names - ALLOWED_PATHS)
    if unexpected:
        raise RuntimeError(
            "Content proof edited paths outside its owned fixture/test/doc/tool boundary:\n"
            + "\n".join(f"  - {path}" for path in unexpected)
        )

    non_additions = sorted(
        f"{status}\t{path}" for status, path in changes if not status.startswith("A")
    )
    if non_additions:
        raise RuntimeError(
            "Ordinary content proof must be additive only:\n"
            + "\n".join(f"  - {entry}" for entry in non_additions)
        )

    forbidden = sorted(
        path
        for path in changed_names
        if path.startswith(FORBIDDEN_PREFIXES)
        or Path(path).name in FORBIDDEN_BASENAMES
    )
    if forbidden:
        raise RuntimeError(
            "Forbidden central/runtime paths changed:\n"
            + "\n".join(f"  - {path}" for path in forbidden)
        )

    missing = sorted(REQUIRED_FIXTURES - changed_names)
    if missing:
        raise RuntimeError(
            "Required fixture files are missing from the diff:\n"
            + "\n".join(f"  - {path}" for path in missing)
        )

    repository_root = Path(git("rev-parse", "--show-toplevel").strip())
    for fixture in sorted(REQUIRED_FIXTURES):
        validate_json(repository_root / fixture)

    print(f"EXTENSIBILITY-GUARDRAILS-001 passed for {len(changes)} additive paths.")
    for status, path in changes:
        print(f"{status}\t{path}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
