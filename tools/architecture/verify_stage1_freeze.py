#!/usr/bin/env python3
"""Architecture freeze audit for STAGE1-FREEZE-001.

The audit separates explicitly inventoried migration debt from new violations.
It is deliberately source-level and engine-independent so it can run before Unity.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

DEFAULT_MANIFEST = (
    "docs/architecture/stage1/stage1_migration_responsibilities_v1.json"
)
TYPE_CONTROLLER = "Stage1VisibleSliceController"
TYPE_COMPOSITION = "Stage1PlayableLoopCompositionV1"


class AuditError(RuntimeError):
    pass


@dataclass(frozen=True)
class Finding:
    rule: str
    path: str
    detail: str

    def render(self) -> str:
        return f"{self.rule}: {self.path}: {self.detail}"


def git(repository_root: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", *args],
        cwd=repository_root,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if completed.returncode != 0:
        raise AuditError(
            f"git {' '.join(args)} failed ({completed.returncode}): "
            f"{completed.stderr.strip()}"
        )
    return completed.stdout


def load_manifest(path: Path) -> dict:
    try:
        with path.open("r", encoding="utf-8") as handle:
            manifest = json.load(handle)
    except (OSError, json.JSONDecodeError) as error:
        raise AuditError(f"Cannot read migration manifest {path}: {error}") from error
    if manifest.get("schema") != "stage1-migration-responsibility-manifest-v1":
        raise AuditError("Unsupported Stage 1 migration manifest schema.")
    return manifest


def normalize_path(path: Path, repository_root: Path) -> str:
    return path.resolve().relative_to(repository_root.resolve()).as_posix()


def all_source_records(manifest: dict) -> list[dict]:
    records: dict[str, dict] = {}
    for target in manifest.get("frozen_targets", []):
        for record in target.get("source_files", []):
            path = record["path"]
            existing = records.get(path)
            if existing is not None and existing != record:
                raise AuditError(f"Conflicting source baseline entries for {path}.")
            records[path] = record
    return [records[path] for path in sorted(records)]


def is_retained_path(path: str, manifest: dict) -> bool:
    for prefix in manifest["guardrail_configuration"]["retained_source_prefixes"]:
        if prefix.endswith(".cs"):
            if path == prefix:
                return True
        elif path.startswith(prefix):
            return True
    return False


def expected_partial_paths(manifest: dict, type_prefix: str) -> set[str]:
    return {
        record["path"]
        for record in all_source_records(manifest)
        if Path(record["path"]).name.startswith(type_prefix)
    }


def discover_paths(repository_root: Path, pattern: str) -> set[str]:
    return {
        normalize_path(path, repository_root)
        for path in repository_root.glob(pattern)
        if path.is_file()
    }


def git_blob_sha(repository_root: Path, path: Path) -> str:
    return git(repository_root, "hash-object", "--", str(path)).strip()


def validate_source_tree(repository_root: Path, manifest: dict) -> None:
    errors: list[str] = []
    for record in all_source_records(manifest):
        relative = record["path"]
        path = repository_root / relative
        if not path.is_file():
            errors.append(f"missing frozen source: {relative}")
            continue
        text = path.read_text(encoding="utf-8")
        line_count = len(text.splitlines())
        if line_count != record["approximate_line_count"]:
            errors.append(
                f"line-count drift: {relative}: "
                f"expected {record['approximate_line_count']}, got {line_count}"
            )
        actual_blob = git_blob_sha(repository_root, path)
        if actual_blob != record["git_blob_sha"]:
            errors.append(
                f"blob drift: {relative}: expected {record['git_blob_sha']}, "
                f"got {actual_blob}. Remove extracted debt from the manifest or "
                "update the baseline and migration plan intentionally."
            )

    config = manifest["guardrail_configuration"]
    for type_prefix, key in (
        (TYPE_COMPOSITION, "composition_partial_glob"),
        ("Stage1RunPickupBootstrap2D", "pickup_partial_glob"),
    ):
        actual = discover_paths(repository_root, config[key])
        expected = expected_partial_paths(manifest, type_prefix)
        if actual != expected:
            errors.append(
                f"{type_prefix} partial inventory drift:\n"
                f"  missing from manifest: {sorted(actual - expected)}\n"
                f"  no longer in tree: {sorted(expected - actual)}"
            )

    if errors:
        raise AuditError("\n".join(errors))


def unique_ids(items: Sequence[dict], key: str, label: str) -> None:
    seen: set[str] = set()
    duplicates: set[str] = set()
    for item in items:
        value = item.get(key)
        if not value:
            raise AuditError(f"{label} entry is missing {key}.")
        if value in seen:
            duplicates.add(value)
        seen.add(value)
    if duplicates:
        raise AuditError(
            f"{label} entries must be represented exactly once: "
            + ", ".join(sorted(duplicates))
        )


def validate_manifest_plan(manifest: dict) -> None:
    retirement = manifest.get("retirement_targets", [])
    retired_types = {
        item.get("fully_qualified_type_name")
        for item in retirement
    }
    required = {
        "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
        "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1",
    }
    if retired_types != required:
        raise AuditError(
            "Both retained controller and retained composition must be explicit, "
            "separate retirement targets."
        )
    composition = next(
        item for item in retirement
        if item["fully_qualified_type_name"].endswith(TYPE_COMPOSITION)
    )
    if TYPE_CONTROLLER not in composition.get("prohibited_outcome", ""):
        raise AuditError(
            "The migration plan must explicitly reject moving the controller into "
            "the retained composition."
        )

    required_boundaries = {
        "Stage1SceneInstaller2D",
        "Stage1RunLoopDriver2D",
        "InventoryWeaponEffectDamageRouter2D",
        "Stage1RoomFlowController2D",
        "Stage1EnemyTerminalPickupConsumerV1",
        "Stage1PropTerminalPickupConsumerV1",
        "RunPickupLifecycleProjection2D",
        "Stage1LegacyScenePresentation2D",
    }
    actual_boundaries = set(manifest.get("replacement_boundaries", []))
    missing = required_boundaries - actual_boundaries
    if missing:
        raise AuditError(
            "Missing intended replacement boundaries: "
            + ", ".join(sorted(missing))
        )

    expected_sequence = [
        "STAGE1-FREEZE-001",
        "ROOM-JSON-LIVE-001",
        "STAGE1-RUNTIME-DECOMPOSE-A-001",
        "STAGE1-RUNTIME-DECOMPOSE-B-001",
        "LEVEL1-CONTROLLER-RETIRE-001",
    ]
    if manifest.get("split_sequence") != expected_sequence:
        raise AuditError("Stage 1 split sequence does not match the frozen plan.")

    debt = manifest.get("known_retained_debt", [])
    unique_ids(debt, "id", "known retained debt")
    responsibilities: list[dict] = []
    for target in manifest.get("frozen_targets", []):
        responsibilities.extend(target.get("current_gameplay_responsibilities", []))
    unique_ids(responsibilities, "id", "migration responsibility")


def validate_known_debt(repository_root: Path, manifest: dict) -> None:
    for debt in manifest.get("known_retained_debt", []):
        path = repository_root / debt["path"]
        if not path.is_file():
            raise AuditError(
                f"Known debt path is absent: {debt['id']}: {debt['path']}. "
                "If the responsibility was removed, remove the debt entry and "
                "update the baseline intentionally."
            )
        text = path.read_text(encoding="utf-8")
        if re.search(debt["anchor_regex"], text, re.MULTILINE) is None:
            raise AuditError(
                f"Known debt anchor no longer exists: {debt['id']}. "
                "Update the manifest intentionally after extraction/deletion."
            )
        if not debt.get("replacement_owner") or not debt.get("retirement_task"):
            raise AuditError(
                f"Known debt lacks replacement plan: {debt['id']}."
            )


def iter_cs_files(repository_root: Path) -> Iterable[tuple[str, str]]:
    assets = repository_root / "Assets" / "ShooterMover"
    if not assets.is_dir():
        raise AuditError("Assets/ShooterMover is unavailable.")
    for path in sorted(assets.rglob("*.cs")):
        relative = normalize_path(path, repository_root)
        if "/Tests/" in relative or "/Editor/" in relative:
            continue
        yield relative, path.read_text(encoding="utf-8")


def scan_scene_loaded_subscriptions(
    sources: Iterable[tuple[str, str]],
) -> list[Finding]:
    findings: list[Finding] = []
    pattern = re.compile(
        r"SceneManager\.sceneLoaded\s*\+=\s*([A-Za-z_][A-Za-z0-9_]*)"
    )
    for path, text in sources:
        for match in pattern.finditer(text):
            findings.append(Finding("scene-loaded-subscription", path, match.group(1)))
    return findings


def scan_private_stage1_reflection(
    sources: Iterable[tuple[str, str]],
) -> list[Finding]:
    findings: list[Finding] = []
    target_pattern = re.compile(
        r"typeof\s*\(\s*(Stage1VisibleSliceController|"
        r"Stage1PlayableLoopCompositionV1)\s*\)"
    )
    member_pattern = re.compile(
        r"\.Get(Field|Method|Property)\s*\(\s*\"([^\"]+)\""
    )
    for path, text in sources:
        for target in target_pattern.finditer(text):
            window = text[target.start(): target.start() + 700]
            if "BindingFlags.NonPublic" not in window:
                continue
            member = member_pattern.search(window)
            detail = (
                f"{target.group(1)}.{member.group(2)}"
                if member is not None
                else f"{target.group(1)}.<private-member>"
            )
            findings.append(Finding("private-stage1-reflection", path, detail))
    return findings


def validate_inventory_findings(
    findings: Sequence[Finding],
    expected: set[tuple[str, str]],
    rule: str,
) -> None:
    actual = {(finding.path, finding.detail) for finding in findings}
    if actual != expected:
        raise AuditError(
            f"{rule} inventory drift:\n"
            f"  new/uninventoried: {sorted(actual - expected)}\n"
            f"  removed/stale baseline: {sorted(expected - actual)}"
        )


def declaration_interfaces(text: str, type_name: str) -> list[str]:
    pattern = re.compile(
        rf"\bclass\s+{re.escape(type_name)}\s*:\s*(.*?)\{{",
        re.DOTALL,
    )
    match = pattern.search(text)
    if match is None:
        return []
    raw = re.sub(r"\s+", " ", match.group(1)).strip()
    names = [
        item.strip().split("<", 1)[0].split(".")[-1]
        for item in raw.split(",")
        if item.strip()
    ]
    return [name for name in names if name != "MonoBehaviour"]


def validate_declared_interfaces(
    text: str,
    type_name: str,
    expected: Sequence[str],
    label: str,
) -> None:
    actual = declaration_interfaces(text, type_name)
    if actual != list(expected):
        raise AuditError(
            f"{label} interface drift: expected {list(expected)}, got {actual}."
        )


def validate_interface_baselines(repository_root: Path, manifest: dict) -> None:
    controller_path = (
        repository_root
        / "Assets/ShooterMover/TestSupport/VisibleSlice/"
        "Stage1VisibleSliceController.cs"
    )
    controller_text = controller_path.read_text(encoding="utf-8")
    expected_controller = manifest["interface_baselines"][
        "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController"
    ]
    validate_declared_interfaces(
        controller_text,
        TYPE_CONTROLLER,
        expected_controller,
        "Stage1VisibleSliceController gameplay authority",
    )

    composition_path = (
        repository_root
        / "Assets/ShooterMover/Production/Stage1/"
        "Stage1PlayableLoopCompositionV1.cs"
    )
    composition_text = composition_path.read_text(encoding="utf-8")
    expected_composition = manifest["interface_baselines"][
        "ShooterMover.UnityAdapters.Production.Stage1."
        "Stage1PlayableLoopCompositionV1"
    ]
    validate_declared_interfaces(
        composition_text,
        TYPE_COMPOSITION,
        expected_composition,
        "Stage1PlayableLoopCompositionV1 authority/persistence",
    )


def added_lines(repository_root: Path, base: str, head: str) -> list[tuple[str, str]]:
    output = git(
        repository_root,
        "diff",
        "--unified=0",
        "--no-ext-diff",
        f"{base}...{head}",
        "--",
        "*.cs",
    )
    result: list[tuple[str, str]] = []
    current_path = ""
    for line in output.splitlines():
        if line.startswith("+++ b/"):
            current_path = line[6:]
            continue
        if line.startswith("+") and not line.startswith("+++"):
            result.append((current_path, line[1:]))
    return result


def scan_added_line_violations(
    lines: Iterable[tuple[str, str]],
    manifest: dict,
) -> list[Finding]:
    config = manifest["guardrail_configuration"]
    findings: list[Finding] = []
    authority_pattern = re.compile(
        r"\bnew\s+(" +
        "|".join(
            re.escape(item)
            for item in config["forbidden_added_authority_constructors"]
        ) +
        r")\b"
    )
    run_aggregate_pattern = re.compile(r"\bnew\s+RunSessionAggregateV1\s*\(")
    reward_markers = tuple(
        marker.lower()
        for marker in config["forbidden_added_reward_selection_markers"]
    )
    weapon_markers = tuple(
        marker.lower()
        for marker in config["forbidden_added_weapon_switch_markers"]
    )
    persistence_markers = tuple(
        marker.lower()
        for marker in config["forbidden_added_persistence_markers"]
    )
    discovery_markers = tuple(
        marker.lower()
        for marker in config["forbidden_added_discovery_markers"]
    )
    name_markers = tuple(
        marker.lower()
        for marker in config["forbidden_added_name_or_hierarchy_markers"]
    )
    content_markers = tuple(
        marker.lower()
        for marker in config["content_registration_markers"]
    )

    for path, line in lines:
        if not is_retained_path(path, manifest):
            continue
        stripped = line.strip()
        lowered = stripped.lower()
        if not stripped or stripped.startswith("//"):
            continue
        if run_aggregate_pattern.search(stripped):
            findings.append(
                Finding("duplicate-run-session-aggregate", path, stripped)
            )
        authority = authority_pattern.search(stripped)
        if authority is not None:
            findings.append(
                Finding(
                    "new-retained-authority-construction",
                    path,
                    authority.group(1),
                )
            )
        if any(marker in lowered for marker in persistence_markers):
            findings.append(
                Finding("new-retained-persistence", path, stripped)
            )
        if any(marker in lowered for marker in reward_markers):
            findings.append(
                Finding("new-stage1-reward-selection", path, stripped)
            )
        if any(marker in lowered for marker in weapon_markers):
            findings.append(
                Finding("new-stage1-weapon-switch", path, stripped)
            )
        if any(marker in lowered for marker in discovery_markers):
            findings.append(
                Finding("new-stage1-global-discovery", path, stripped)
            )
        if any(marker in lowered for marker in name_markers):
            findings.append(
                Finding("new-name-or-hierarchy-decision", path, stripped)
            )
        if (
            Path(path).name in {
                "Stage1VisibleSliceController.cs",
                "Stage1PlayableLoopCompositionV1.cs",
            }
            and any(marker.lower() in lowered for marker in content_markers)
        ):
            findings.append(
                Finding("new-content-registration-in-retained-controller", path, stripped)
            )
    return findings


def scan_full_direct_aggregate_creation(
    repository_root: Path, manifest: dict
) -> list[Finding]:
    findings: list[Finding] = []
    pattern = re.compile(r"\bnew\s+RunSessionAggregateV1\s*\(")
    for path, text in iter_cs_files(repository_root):
        if not is_retained_path(path, manifest):
            continue
        for _ in pattern.finditer(text):
            findings.append(
                Finding("duplicate-run-session-aggregate", path, "direct construction")
            )
    return findings


def ordinary_content_path_is_decoupled(path: str, manifest: dict) -> bool:
    roots = manifest["guardrail_configuration"]["ordinary_content_roots"]
    return any(path.startswith(root) for root in roots) and not is_retained_path(
        path, manifest
    )


def run_audit(repository_root: Path, manifest_path: Path, head: str) -> None:
    manifest = load_manifest(manifest_path)
    validate_manifest_plan(manifest)
    validate_source_tree(repository_root, manifest)
    validate_known_debt(repository_root, manifest)
    validate_interface_baselines(repository_root, manifest)

    sources = list(iter_cs_files(repository_root))
    expected_hooks = {
        (item["path"], item["callback"])
        for item in manifest["scene_loaded_subscription_inventory"]
        for _ in range(item["expected_subscription_count"])
    }
    validate_inventory_findings(
        scan_scene_loaded_subscriptions(sources),
        expected_hooks,
        "SceneManager.sceneLoaded",
    )

    expected_reflection = {
        (item["path"], f"{item['target_type'].split('.')[-1]}.{item['member_name']}")
        for item in manifest["production_reflection_inventory"]
        for _ in range(item["expected_access_count"])
    }
    validate_inventory_findings(
        scan_private_stage1_reflection(sources),
        expected_reflection,
        "Stage 1 private reflection",
    )

    aggregate_findings = scan_full_direct_aggregate_creation(
        repository_root, manifest
    )
    if aggregate_findings:
        raise AuditError(
            "Retained Stage 1 classes directly construct RunSessionAggregateV1:\n"
            + "\n".join(item.render() for item in aggregate_findings)
        )

    base = manifest["launch_main_sha"]
    git(repository_root, "merge-base", "--is-ancestor", base, head)
    violations = scan_added_line_violations(
        added_lines(repository_root, base, head), manifest
    )
    if violations:
        raise AuditError(
            "New Stage 1 migration violations were introduced:\n"
            + "\n".join(item.render() for item in violations)
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()

    repository_root = Path(
        git(Path.cwd(), "rev-parse", "--show-toplevel").strip()
    )
    manifest_path = repository_root / args.manifest
    run_audit(repository_root, manifest_path, args.head)
    manifest = load_manifest(manifest_path)
    print(
        "STAGE1-FREEZE-001 passed: "
        f"{len(all_source_records(manifest))} source files, "
        f"{len(manifest['known_retained_debt'])} debt entries, "
        "0 new violations."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AuditError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
