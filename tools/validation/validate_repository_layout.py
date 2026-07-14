#!/usr/bin/env python3
"""Validate Shooter Mover repository roots and exclusive ownership rules."""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Mapping, Sequence


REQUIRED_DOCUMENTS = (
    Path("Assets/ShooterMover/README.md"),
    Path("tools/README.md"),
    Path("docs/architecture/FILE_OWNERSHIP.md"),
)

REQUIRED_ROOTS = frozenset(
    {
        "Assets/",
        "Assets/ShooterMover/",
        "Assets/ShooterMover/Runtime/",
        "Assets/ShooterMover/Runtime/Domain/",
        "Assets/ShooterMover/Runtime/Contracts/",
        "Assets/ShooterMover/Runtime/Application/",
        "Assets/ShooterMover/Runtime/UnityAdapters/",
        "Assets/ShooterMover/Runtime/Bootstrap/",
        "Assets/ShooterMover/Runtime/Presentation/",
        "Assets/ShooterMover/Content/",
        "Assets/ShooterMover/Content/Definitions/",
        "Assets/ShooterMover/Content/SharedModules/",
        "Assets/ShooterMover/ContentPackages/",
        "Assets/ShooterMover/ContentPackages/Weapons/",
        "Assets/ShooterMover/ContentPackages/Enemies/",
        "Assets/ShooterMover/ContentPackages/Rooms/",
        "Assets/ShooterMover/ContentPackages/Encounters/",
        "Assets/ShooterMover/ContentPackages/Environment/",
        "Assets/ShooterMover/Generated/",
        "Assets/ShooterMover/Scenes/",
        "Assets/ShooterMover/Scenes/Bootstrap/",
        "Assets/ShooterMover/Scenes/MenuHub/",
        "Assets/ShooterMover/Scenes/Prototypes/",
        "Assets/ShooterMover/Scenes/Factory/",
        "Assets/ShooterMover/Scenes/Tests/",
        "Assets/ShooterMover/UI/",
        "Assets/ShooterMover/Localization/",
        "Assets/ShooterMover/Tests/",
        "Assets/ShooterMover/Tests/EditMode/",
        "Assets/ShooterMover/Tests/PlayMode/",
        "Assets/ShooterMover/Tests/Performance/",
        "Assets/ShooterMover/TestSupport/",
        "Assets/ShooterMover/Settings/",
        "Assets/ShooterMover/Settings/Rendering/",
        "Packages/",
        "ProjectSettings/",
        ".github/",
        ".github/workflows/",
        "tools/",
        "tools/validation/",
        "tools/generation/",
        "tools/build/",
        "docs/",
        "docs/architecture/",
        "docs/verification/",
        "docs/toolchain/",
        "docs/art-pipeline/",
        "source-assets/",
        "source-assets/manifests/",
        "source-assets/export-recipes/",
        "source-assets/reference/",
        "assembly/",
        "assembly/generated/",
    }
)

REQUIRED_OWNERSHIP_RULES = frozenset(
    {
        "scenes",
        "prefabs",
        "scriptable-objects",
        "shared-modules",
        "central-tables",
        "generated-registries",
        "tools",
    }
)

APPROVED_GENERATED_ROOTS = frozenset(
    {
        "Assets/ShooterMover/Generated/",
        "assembly/generated/",
    }
)

ALLOWED_CREATION_RULES = frozenset(
    {
        "tracked-marker",
        "tracked-or-create-by-owning-task",
        "create-by-owning-task",
        "create-by-generator-owner",
        "tracked-unity-baseline",
        "create-by-verification-owner",
        "create-by-art-pipeline-owner",
        "tracked-lifecycle-root",
        "tracked-workflow-output",
    }
)

IGNORED_DIRECTORY_NAMES = frozenset(
    {
        ".git",
        ".idea",
        ".vs",
        ".vscode",
        "__pycache__",
        "Library",
        "Temp",
        "Logs",
        "Obj",
        "UserSettings",
        "Build",
        "Builds",
    }
)

MARKER_RE = re.compile(
    r"<!--\s*(?P<kind>layout-root|ownership-rule|exclusive-owner|generated-output)"
    r"\s+(?P<attributes>.*?)\s*-->"
)
ATTRIBUTE_RE = re.compile(r'(?P<name>[A-Za-z0-9_-]+)="(?P<value>[^"]*)"')


@dataclass(frozen=True)
class Marker:
    kind: str
    attributes: Mapping[str, str]
    source: Path
    line: int


@dataclass(frozen=True)
class ExclusiveScope:
    pattern: str
    owner: str
    source: Path
    line: int

    @property
    def is_subtree(self) -> bool:
        return self.pattern.endswith("/**")

    @property
    def base(self) -> str:
        raw_base = (
            self.pattern[:-3].rstrip("/")
            if self.is_subtree
            else self.pattern.rstrip("/")
        )
        return raw_base.casefold()


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate accepted repository roots and file-ownership markers."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="Repository root. Defaults to the parent containing tools/.",
    )
    return parser.parse_args(argv)


def normalize_root(value: str) -> str:
    normalized = value.strip().replace("\\", "/").strip("/")
    return f"{normalized}/" if normalized else ""


def normalize_pattern(value: str) -> str:
    normalized = value.strip().replace("\\", "/")
    while "//" in normalized:
        normalized = normalized.replace("//", "/")
    return normalized.removeprefix("./").rstrip("/")


def read_markers(repo_root: Path) -> tuple[list[Marker], list[str]]:
    markers: list[Marker] = []
    errors: list[str] = []

    for relative_path in REQUIRED_DOCUMENTS:
        document = repo_root / relative_path
        if not document.is_file():
            errors.append(f"missing required ownership document: {relative_path.as_posix()}")
            continue

        for line_number, line in enumerate(
            document.read_text(encoding="utf-8").splitlines(), start=1
        ):
            match = MARKER_RE.search(line)
            if not match:
                continue

            attributes = {
                attribute.group("name"): attribute.group("value")
                for attribute in ATTRIBUTE_RE.finditer(match.group("attributes"))
            }
            markers.append(
                Marker(
                    kind=match.group("kind"),
                    attributes=attributes,
                    source=relative_path,
                    line=line_number,
                )
            )

    return markers, errors


def marker_location(marker: Marker) -> str:
    return f"{marker.source.as_posix()}:{marker.line}"


def validate_roots(repo_root: Path, markers: Iterable[Marker]) -> list[str]:
    errors: list[str] = []
    declared: dict[str, Marker] = {}

    for marker in markers:
        if marker.kind != "layout-root":
            continue

        raw_path = marker.attributes.get("path", "")
        creation = marker.attributes.get("creation", "")
        path = normalize_root(raw_path)

        if not path:
            errors.append(f"{marker_location(marker)} layout-root has no path")
            continue
        if path in declared:
            errors.append(
                f"{marker_location(marker)} duplicates layout-root {path} "
                f"from {marker_location(declared[path])}"
            )
            continue
        if creation not in ALLOWED_CREATION_RULES:
            errors.append(
                f"{marker_location(marker)} layout-root {path} has unknown "
                f"creation rule {creation!r}"
            )
        declared[path] = marker

    unknown = sorted(set(declared) - REQUIRED_ROOTS)
    for path in unknown:
        errors.append(
            f"{marker_location(declared[path])} declares unknown accepted root {path}"
        )

    for path in sorted(REQUIRED_ROOTS):
        on_disk = (repo_root / path.rstrip("/")).exists()
        if not on_disk and path not in declared:
            errors.append(
                f"required root is missing and has no documented creation rule: {path}"
            )

    return errors


def validate_required_rules(markers: Iterable[Marker]) -> list[str]:
    errors: list[str] = []
    declared: dict[str, Marker] = {}

    for marker in markers:
        if marker.kind != "ownership-rule":
            continue
        rule_id = marker.attributes.get("id", "").strip()
        mode = marker.attributes.get("mode", "").strip()
        if not rule_id:
            errors.append(f"{marker_location(marker)} ownership-rule has no id")
            continue
        if not mode:
            errors.append(
                f"{marker_location(marker)} ownership-rule {rule_id} has no mode"
            )
        if rule_id in declared:
            errors.append(
                f"{marker_location(marker)} duplicates ownership-rule {rule_id} "
                f"from {marker_location(declared[rule_id])}"
            )
        declared[rule_id] = marker

    for rule_id in sorted(REQUIRED_OWNERSHIP_RULES - set(declared)):
        errors.append(f"missing required ownership-rule marker: {rule_id}")

    return errors


def scopes_overlap(left: ExclusiveScope, right: ExclusiveScope) -> bool:
    if not left.is_subtree and not right.is_subtree:
        return left.base == right.base
    if left.is_subtree and right.is_subtree:
        return (
            left.base == right.base
            or left.base.startswith(f"{right.base}/")
            or right.base.startswith(f"{left.base}/")
        )
    subtree = left if left.is_subtree else right
    exact = right if left.is_subtree else left
    return exact.base == subtree.base or exact.base.startswith(f"{subtree.base}/")


def validate_exclusive_ownership(markers: Iterable[Marker]) -> list[str]:
    errors: list[str] = []
    scopes: list[ExclusiveScope] = []

    for marker in markers:
        if marker.kind != "exclusive-owner":
            continue
        pattern = normalize_pattern(marker.attributes.get("pattern", ""))
        owner = marker.attributes.get("owner", "").strip()
        if not pattern:
            errors.append(f"{marker_location(marker)} exclusive-owner has no pattern")
            continue
        if not owner:
            errors.append(
                f"{marker_location(marker)} exclusive-owner {pattern} has no owner"
            )
            continue
        if "*" in pattern and (
            not pattern.endswith("/**") or pattern.count("*") != 2
        ):
            errors.append(
                f"{marker_location(marker)} exclusive-owner pattern {pattern!r} "
                "may use only a trailing /** wildcard"
            )
            continue
        scopes.append(
            ExclusiveScope(
                pattern=pattern,
                owner=owner,
                source=marker.source,
                line=marker.line,
            )
        )

    for index, left in enumerate(scopes):
        for right in scopes[index + 1 :]:
            if left.owner != right.owner and scopes_overlap(left, right):
                errors.append(
                    "conflicting exclusive ownership: "
                    f"{left.pattern} ({left.owner}, {left.source}:{left.line}) overlaps "
                    f"{right.pattern} ({right.owner}, {right.source}:{right.line})"
                )

    return errors


def validate_generated_markers(markers: Iterable[Marker]) -> list[str]:
    errors: list[str] = []
    declared: dict[str, Marker] = {}

    for marker in markers:
        if marker.kind != "generated-output":
            continue
        path = normalize_root(marker.attributes.get("path", ""))
        mode = marker.attributes.get("mode", "").strip()
        owner = marker.attributes.get("owner", "").strip()

        if not path:
            errors.append(f"{marker_location(marker)} generated-output has no path")
            continue
        if path in declared:
            errors.append(
                f"{marker_location(marker)} duplicates generated-output {path} "
                f"from {marker_location(declared[path])}"
            )
        if path not in APPROVED_GENERATED_ROOTS:
            errors.append(
                f"{marker_location(marker)} declares unknown generated-output location {path}"
            )
        if mode != "regenerate-only":
            errors.append(
                f"{marker_location(marker)} generated-output {path} must use "
                'mode="regenerate-only"'
            )
        if not owner:
            errors.append(
                f"{marker_location(marker)} generated-output {path} has no owner"
            )
        declared[path] = marker

    for path in sorted(APPROVED_GENERATED_ROOTS - set(declared)):
        errors.append(f"missing generated-output marker for approved root: {path}")

    return errors


def is_below(relative_path: Path, root: str) -> bool:
    normalized_path = relative_path.as_posix().strip("/")
    normalized_root = root.strip("/")
    return normalized_path == normalized_root or normalized_path.startswith(
        f"{normalized_root}/"
    )


def iter_repository_paths(repo_root: Path) -> Iterable[Path]:
    for current, directories, files in os.walk(repo_root):
        directories[:] = sorted(
            directory
            for directory in directories
            if directory not in IGNORED_DIRECTORY_NAMES
        )
        current_path = Path(current)
        for directory in directories:
            yield (current_path / directory).relative_to(repo_root)
        for filename in sorted(files):
            yield (current_path / filename).relative_to(repo_root)


def validate_generated_locations(repo_root: Path) -> list[str]:
    errors: list[str] = []
    unknown_roots: set[str] = set()

    for relative_path in iter_repository_paths(repo_root):
        parts = relative_path.parts
        generated_indices = [
            index for index, part in enumerate(parts) if part.casefold() == "generated"
        ]
        for index in generated_indices:
            candidate = Path(*parts[: index + 1])
            if not any(
                is_below(candidate, approved)
                for approved in APPROVED_GENERATED_ROOTS
            ):
                unknown_roots.add(f"{candidate.as_posix()}/")

    for path in sorted(unknown_roots):
        errors.append(f"unknown generated-output location found on disk: {path}")

    return errors


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    repo_root = args.root.resolve()

    if not (repo_root / "project_workspace.json").is_file():
        print(
            f"ERROR: {repo_root} does not look like the Shooter Mover repository "
            "(project_workspace.json is missing).",
            file=sys.stderr,
        )
        return 2

    markers, errors = read_markers(repo_root)
    errors.extend(validate_roots(repo_root, markers))
    errors.extend(validate_required_rules(markers))
    errors.extend(validate_exclusive_ownership(markers))
    errors.extend(validate_generated_markers(markers))
    errors.extend(validate_generated_locations(repo_root))

    if errors:
        print("Shooter Mover repository layout validation FAILED:", file=sys.stderr)
        for error in sorted(set(errors)):
            print(f"  - {error}", file=sys.stderr)
        return 1

    root_count = sum(marker.kind == "layout-root" for marker in markers)
    ownership_count = sum(marker.kind == "exclusive-owner" for marker in markers)
    print(
        "Shooter Mover repository layout validation passed: "
        f"{len(REQUIRED_ROOTS)} required roots, "
        f"{root_count} documented root rules, "
        f"{len(REQUIRED_OWNERSHIP_RULES)} required ownership categories, "
        f"{ownership_count} exclusive patterns, "
        f"{len(APPROVED_GENERATED_ROOTS)} regenerate-only output roots."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
