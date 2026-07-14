#!/usr/bin/env python3
"""Validate Shooter Mover's inward-only Unity assembly-definition graph."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


@dataclass(frozen=True)
class ExpectedAssembly:
    path: str
    name: str
    references: tuple[str, ...]
    no_engine_references: bool
    test_assembly: bool = False
    include_platforms: tuple[str, ...] = ()


EXPECTED: tuple[ExpectedAssembly, ...] = (
    ExpectedAssembly(
        "Assets/ShooterMover/Runtime/Domain/ShooterMover.Domain.asmdef",
        "ShooterMover.Domain",
        (),
        True,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Runtime/Contracts/ShooterMover.Contracts.asmdef",
        "ShooterMover.Contracts",
        ("ShooterMover.Domain",),
        True,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Runtime/Application/ShooterMover.Application.asmdef",
        "ShooterMover.Application",
        ("ShooterMover.Domain", "ShooterMover.Contracts"),
        True,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Runtime/UnityAdapters/ShooterMover.UnityAdapters.asmdef",
        "ShooterMover.UnityAdapters",
        ("ShooterMover.Domain", "ShooterMover.Contracts", "ShooterMover.Application"),
        False,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Content/Definitions/ShooterMover.Content.Definitions.asmdef",
        "ShooterMover.Content.Definitions",
        ("ShooterMover.Domain", "ShooterMover.Contracts", "ShooterMover.Application"),
        False,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Runtime/Presentation/ShooterMover.Presentation.asmdef",
        "ShooterMover.Presentation",
        (
            "ShooterMover.Domain",
            "ShooterMover.Contracts",
            "ShooterMover.Application",
            "ShooterMover.UnityAdapters",
        ),
        False,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Runtime/Bootstrap/ShooterMover.Bootstrap.asmdef",
        "ShooterMover.Bootstrap",
        (
            "ShooterMover.Domain",
            "ShooterMover.Contracts",
            "ShooterMover.Application",
            "ShooterMover.UnityAdapters",
            "ShooterMover.Presentation",
            "ShooterMover.Content.Definitions",
        ),
        False,
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Tests/EditMode/ShooterMover.Tests.EditMode.asmdef",
        "ShooterMover.Tests.EditMode",
        (
            "ShooterMover.Domain",
            "ShooterMover.Contracts",
            "ShooterMover.Application",
            "ShooterMover.UnityAdapters",
            "ShooterMover.Presentation",
            "ShooterMover.Content.Definitions",
            "ShooterMover.Bootstrap",
        ),
        False,
        test_assembly=True,
        include_platforms=("Editor",),
    ),
    ExpectedAssembly(
        "Assets/ShooterMover/Tests/PlayMode/ShooterMover.Tests.PlayMode.asmdef",
        "ShooterMover.Tests.PlayMode",
        (
            "ShooterMover.Domain",
            "ShooterMover.Contracts",
            "ShooterMover.Application",
            "ShooterMover.UnityAdapters",
            "ShooterMover.Presentation",
            "ShooterMover.Content.Definitions",
            "ShooterMover.Bootstrap",
        ),
        False,
        test_assembly=True,
    ),
)

# Lower numbers are more inward. Equal-rank references are allowed, but cycles are not.
PATH_LAYER_MARKERS: tuple[tuple[str, int], ...] = (
    ("/Runtime/Domain/", 0),
    ("/Runtime/Contracts/", 1),
    ("/Runtime/Application/", 2),
    ("/Runtime/UnityAdapters/", 3),
    ("/Content/", 3),
    ("/ContentPackages/", 3),
    ("/Runtime/Presentation/", 3),
    ("/Runtime/Bootstrap/", 4),
    ("/Tests/", 5),
)


def _normalise_reference(reference: object) -> str:
    if not isinstance(reference, str) or not reference.strip():
        raise ValueError(f"invalid assembly reference {reference!r}")
    if reference.startswith("GUID:"):
        raise ValueError(
            f"GUID-based reference {reference!r} is not allowed in the baseline; "
            "use the stable assembly name"
        )
    return reference


def _layer_for(path: Path) -> int | None:
    normalised = "/" + path.as_posix().lstrip("/")
    for marker, layer in PATH_LAYER_MARKERS:
        if marker in normalised:
            return layer
    return None


def _read_asmdef(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        raise ValueError(f"missing expected assembly definition: {path}") from None
    except json.JSONDecodeError as exc:
        raise ValueError(f"{path}: invalid JSON: {exc}") from None
    if not isinstance(value, dict):
        raise ValueError(f"{path}: top-level JSON value must be an object")
    return value


def _find_all_asmdefs(root: Path) -> list[Path]:
    assets_root = root / "Assets" / "ShooterMover"
    if not assets_root.exists():
        return []
    return sorted(assets_root.rglob("*.asmdef"))


def _check_expected(root: Path, failures: list[str]) -> None:
    seen_names: dict[str, Path] = {}

    for expected in EXPECTED:
        path = root / expected.path
        try:
            data = _read_asmdef(path)
        except ValueError as exc:
            failures.append(str(exc))
            continue

        name = data.get("name")
        if name != expected.name:
            failures.append(f"{expected.path}: expected name {expected.name!r}, got {name!r}")
            continue
        if name in seen_names:
            failures.append(
                f"duplicate assembly name {name!r}: {seen_names[name]} and {path}"
            )
            continue
        seen_names[name] = path

        try:
            references = tuple(_normalise_reference(item) for item in data.get("references", []))
        except ValueError as exc:
            failures.append(f"{expected.path}: {exc}")
            references = ()

        if references != expected.references:
            failures.append(
                f"{expected.path}: references must be {list(expected.references)!r}, "
                f"got {list(references)!r}"
            )

        actual_no_engine = data.get("noEngineReferences")
        if actual_no_engine is not expected.no_engine_references:
            failures.append(
                f"{expected.path}: noEngineReferences must be "
                f"{expected.no_engine_references}, got {actual_no_engine!r}"
            )

        optional_unity = data.get("optionalUnityReferences", [])
        if expected.test_assembly:
            if optional_unity != ["TestAssemblies"]:
                failures.append(
                    f"{expected.path}: test assembly must declare "
                    'optionalUnityReferences ["TestAssemblies"]'
                )
            if data.get("autoReferenced") is not False:
                failures.append(f"{expected.path}: test assembly must set autoReferenced false")
        elif optional_unity:
            failures.append(
                f"{expected.path}: production assembly must not reference Unity test assemblies"
            )

        include_platforms = tuple(data.get("includePlatforms", []))
        if include_platforms != expected.include_platforms:
            failures.append(
                f"{expected.path}: includePlatforms must be "
                f"{list(expected.include_platforms)!r}, got {list(include_platforms)!r}"
            )


def _load_global_graph(root: Path, failures: list[str]) -> dict[str, tuple[Path, tuple[str, ...]]]:
    graph: dict[str, tuple[Path, tuple[str, ...]]] = {}
    for path in _find_all_asmdefs(root):
        try:
            data = _read_asmdef(path)
            name = data.get("name")
            if not isinstance(name, str) or not name:
                raise ValueError(f"{path}: name must be a non-empty string")
            if name in graph:
                raise ValueError(
                    f"duplicate assembly name {name!r}: {graph[name][0]} and {path}"
                )
            references = tuple(
                _normalise_reference(item) for item in data.get("references", [])
            )
            graph[name] = (path, references)
        except ValueError as exc:
            failures.append(str(exc))
    return graph


def _check_inward_direction(
    root: Path,
    graph: dict[str, tuple[Path, tuple[str, ...]]],
    failures: list[str],
) -> None:
    for source_name, (source_path, references) in graph.items():
        source_relative = source_path.relative_to(root)
        source_layer = _layer_for(source_relative)
        if source_layer is None:
            failures.append(f"{source_relative}: cannot classify assembly layer")
            continue

        for target_name in references:
            target = graph.get(target_name)
            if target is None:
                if target_name.startswith("ShooterMover."):
                    failures.append(
                        f"{source_relative}: references missing internal assembly {target_name!r}"
                    )
                continue

            target_path, _ = target
            target_relative = target_path.relative_to(root)
            target_layer = _layer_for(target_relative)
            if target_layer is None:
                failures.append(f"{target_relative}: cannot classify assembly layer")
                continue
            if source_layer < target_layer:
                failures.append(
                    f"forbidden outward reference: {source_name} "
                    f"(layer {source_layer}) -> {target_name} (layer {target_layer})"
                )


def _check_cycles(
    graph: dict[str, tuple[Path, tuple[str, ...]]],
    failures: list[str],
) -> None:
    state: dict[str, int] = {}
    stack: list[str] = []
    reported: set[tuple[str, ...]] = set()

    def visit(name: str) -> None:
        marker = state.get(name, 0)
        if marker == 2:
            return
        if marker == 1:
            start = stack.index(name)
            cycle = tuple(stack[start:] + [name])
            if cycle not in reported:
                failures.append("assembly reference cycle: " + " -> ".join(cycle))
                reported.add(cycle)
            return

        state[name] = 1
        stack.append(name)
        for target in graph[name][1]:
            if target in graph:
                visit(target)
        stack.pop()
        state[name] = 2

    for name in sorted(graph):
        if state.get(name, 0) == 0:
            visit(name)


def validate(root: Path) -> list[str]:
    failures: list[str] = []
    _check_expected(root, failures)
    graph = _load_global_graph(root, failures)
    _check_inward_direction(root, graph, failures)
    _check_cycles(graph, failures)
    return failures


def _parse_args(argv: Iterable[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate Shooter Mover Unity assembly definitions."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="Repository root (defaults to two levels above this script).",
    )
    return parser.parse_args(list(argv))


def main(argv: Iterable[str] = ()) -> int:
    args = _parse_args(argv)
    root = args.root.resolve()
    failures = validate(root)

    if failures:
        print("Shooter Mover assembly graph validation FAILED:")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print(
        "Shooter Mover assembly graph validation passed: "
        f"{len(EXPECTED)} required assemblies, inward-only references, no cycles."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
