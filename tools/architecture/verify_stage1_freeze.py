#!/usr/bin/env python3
"""Source-level architecture freeze for retained Stage 1 migration surfaces."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

DEFAULT_MANIFEST = "docs/architecture/stage1/stage1_migration_responsibilities_v1.json"
TYPE_CONTROLLER = "Stage1VisibleSliceController"
TYPE_COMPOSITION = "Stage1PlayableLoopCompositionV1"
CONFIG = {
    "composition_partial_glob": "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1*.cs",
    "pickup_partial_glob": "Assets/ShooterMover/Production/Stage1/Stage1RunPickupBootstrap2D*.cs",
    "prop_pickup_partial_glob": "Assets/ShooterMover/Production/Stage1/Stage1RunPickupPropBootstrap2D*.cs",
    "retained_source_prefixes": [
        "Assets/ShooterMover/Production/Stage1/",
        "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs",
        "Assets/ShooterMover/Production/Content/Stage1TerminalDropContentV1.cs",
        "Assets/ShooterMover/ContentPackages/Props/DestructibleProps/Stage1DestructiblePropIntegration.cs",
    ],
    "ordinary_content_roots": [
        "Assets/ShooterMover/Runtime/Content/Definitions/",
        "Assets/ShooterMover/Production/Content/Definitions/",
        "Assets/ShooterMover/Resources/",
    ],
    "authorities": [
        "RunSessionAggregateV1", "RunSessionAuthorityV1", "MoneyWalletService",
        "ScrapWalletServiceV1", "PlayerHoldingsService", "RewardApplicationServiceV1",
        "StrongboxOpeningServiceV1", "MissionRunResultAuthorityV1",
        "PlayerActorAuthority", "EnemyActorAuthority", "RoomRuntimeAuthority",
    ],
    "persistence": [
        "IAccountSnapshotPersistence", "ICharacterSnapshotPersistence", "AtomicStore",
        "PersistPreparedCustody", "SaveAsync", "PlayerPrefs",
    ],
    "reward": [
        "Random.", "UnityEngine.Random", "probability", "weighted", "drop chance",
        "ChooseReward", "SelectReward",
    ],
    "weapon": [
        "switch (weapon", "switch(weapon", "WeaponDefinitionId ==",
        "WeaponDefinitionId !=", "weaponName ==", "weaponName !=", 'case "weapon.',
    ],
    "discovery": [
        "SceneManager.sceneLoaded +=", "FindFirstObjectByType<", "FindAnyObjectByType<",
        "FindObjectsByType<", "FindObjectOfType<", "FindObjectsOfType<",
    ],
    "name": [
        "gameObject.name", "transform.name", '.Find("', "GameObject.Find(",
        "Room 1", "Room 2", "room number",
    ],
    "content": [
        "Register(", "DefinitionId", "Catalog", 'StableId.Parse("weapon.',
        'StableId.Parse("enemy.', 'StableId.Parse("prop.',
    ],
}


class AuditError(RuntimeError):
    pass


@dataclass(frozen=True)
class Finding:
    rule: str
    path: str
    detail: str

    def render(self) -> str:
        return f"{self.rule}: {self.path}: {self.detail}"


def git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", *args], cwd=root, text=True, check=False,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    if result.returncode:
        raise AuditError(
            f"git {' '.join(args)} failed ({result.returncode}): {result.stderr.strip()}"
        )
    return result.stdout


def load_manifest(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise AuditError(f"Cannot read migration manifest {path}: {error}") from error
    if value.get("schema") != "stage1-migration-responsibility-manifest-v1":
        raise AuditError("Unsupported Stage 1 migration manifest schema.")
    return value


def source_records(manifest: dict) -> dict[str, dict]:
    fields = manifest["source_record_fields"]
    return {
        source_id: dict(zip(fields, values))
        for source_id, values in manifest["source_files"].items()
    }


def target_records(manifest: dict) -> list[dict]:
    fields = manifest["target_fields"]
    return [dict(zip(fields, values)) for values in manifest["frozen_targets"]]


def debt_records(manifest: dict) -> list[dict]:
    fields = manifest["debt_fields"]
    return [dict(zip(fields, values)) for values in manifest["known_retained_debt"]]


def retirement_records(manifest: dict) -> list[dict]:
    fields = manifest["retirement_target_fields"]
    return [dict(zip(fields, values)) for values in manifest["retirement_targets"]]


def normalize(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def all_source_records(manifest: dict) -> list[dict]:
    return [source_records(manifest)[key] for key in sorted(source_records(manifest))]


def is_retained_path(path: str, manifest: dict | None = None) -> bool:
    for prefix in CONFIG["retained_source_prefixes"]:
        if (prefix.endswith(".cs") and path == prefix) or (
            not prefix.endswith(".cs") and path.startswith(prefix)
        ):
            return True
    return False


def is_stage1_production_path(path: str) -> bool:
    name = Path(path).name
    return "/Stage1/" in path or name.startswith("Stage1") or "Stage1" in name


def unique_ids(items: Sequence[dict], key: str, label: str) -> None:
    values = [item.get(key) for item in items]
    if any(not value for value in values):
        raise AuditError(f"{label} entry is missing {key}.")
    duplicates = sorted({value for value in values if values.count(value) > 1})
    if duplicates:
        raise AuditError(f"{label} entries must be represented exactly once: {duplicates}")


def validate_manifest_plan(manifest: dict) -> None:
    retirement = retirement_records(manifest)
    required_types = {
        "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
        "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1",
    }
    if {item["type"] for item in retirement} != required_types:
        raise AuditError("Both retained controller and composition must be retirement targets.")
    composition = next(item for item in retirement if item["type"].endswith(TYPE_COMPOSITION))
    if TYPE_CONTROLLER not in composition["prohibited_outcome"]:
        raise AuditError("The plan must reject moving controller behavior into the composition.")
    required_boundaries = {
        "Stage1SceneInstaller2D", "Stage1RunLoopDriver2D",
        "InventoryWeaponEffectDamageRouter2D", "Stage1RoomFlowController2D",
        "Stage1EnemyTerminalPickupConsumerV1", "Stage1PropTerminalPickupConsumerV1",
        "RunPickupLifecycleProjection2D", "Stage1LegacyScenePresentation2D",
    }
    missing = required_boundaries - set(manifest["replacement_boundaries"])
    if missing:
        raise AuditError(f"Missing replacement boundaries: {sorted(missing)}")
    expected = [
        "STAGE1-FREEZE-001", "ROOM-JSON-LIVE-001",
        "STAGE1-RUNTIME-DECOMPOSE-A-001", "STAGE1-RUNTIME-DECOMPOSE-B-001",
        "LEVEL1-CONTROLLER-RETIRE-001",
    ]
    if manifest["split_sequence"] != expected:
        raise AuditError("Stage 1 split sequence does not match the frozen plan.")
    unique_ids(debt_records(manifest), "id", "known retained debt")
    responsibilities = []
    sources = source_records(manifest)
    for target in target_records(manifest):
        responsibilities.extend(
            {"id": pair[0]} for pair in target["responsibilities"]
        )
        expected_lines = sum(
            sources[source_id]["approximate_line_count"]
            for source_id in dict.fromkeys(target["source_ids"])
        )
        if target["approximate_source_line_count"] != expected_lines:
            raise AuditError(
                f"{target['type']} line-count summary drift: expected "
                f"{expected_lines}, got {target['approximate_source_line_count']}"
            )
    unique_ids(responsibilities, "id", "migration responsibility")


def validate_source_tree(root: Path, manifest: dict) -> None:
    errors: list[str] = []
    sources = source_records(manifest)
    for source_id, record in sources.items():
        path = root / record["path"]
        if not path.is_file():
            errors.append(f"missing frozen source {source_id}: {record['path']}")
            continue
        text = path.read_text(encoding="utf-8")
        if len(text.splitlines()) != record["approximate_line_count"]:
            errors.append(
                f"line-count drift {record['path']}: expected "
                f"{record['approximate_line_count']}, got {len(text.splitlines())}"
            )
        blob = git(root, "hash-object", "--", str(path)).strip()
        if blob != record["git_blob_sha"]:
            errors.append(
                f"blob drift {record['path']}: expected {record['git_blob_sha']}, got {blob}; "
                "update the baseline and migration plan intentionally after extraction"
            )
    target_sources = {
        target["type"]: {sources[item]["path"] for item in target["source_ids"]}
        for target in target_records(manifest)
    }
    for type_name, pattern in (
        (TYPE_COMPOSITION, CONFIG["composition_partial_glob"]),
        ("Stage1RunPickupBootstrap2D", CONFIG["pickup_partial_glob"]),
        ("Stage1RunPickupPropBootstrap2D", CONFIG["prop_pickup_partial_glob"]),
    ):
        actual = {normalize(path, root) for path in root.glob(pattern) if path.is_file()}
        expected = set()
        for name, paths in target_sources.items():
            if name.endswith(type_name):
                expected |= {path for path in paths if Path(path).name.startswith(type_name)}
        if actual != expected:
            errors.append(
                f"{type_name} inventory drift; unlisted={sorted(actual-expected)}, "
                f"removed={sorted(expected-actual)}"
            )
    if errors:
        raise AuditError("\n".join(errors))


def validate_known_debt(root: Path, manifest: dict) -> None:
    sources = source_records(manifest)
    for debt in debt_records(manifest):
        record = sources[debt["source_id"]]
        path = root / record["path"]
        if not path.is_file():
            raise AuditError(f"Known debt path is absent: {debt['id']}: {record['path']}")
        if re.search(debt["anchor_regex"], path.read_text(encoding="utf-8"), re.MULTILINE) is None:
            raise AuditError(
                f"Known debt anchor no longer exists: {debt['id']}; update the baseline intentionally"
            )
        if not debt["replacement_owner"] or not debt["retirement_task"]:
            raise AuditError(f"Known debt lacks replacement plan: {debt['id']}")


def iter_cs_files(root: Path) -> Iterable[tuple[str, str]]:
    assets = root / "Assets/ShooterMover"
    if not assets.is_dir():
        raise AuditError("Assets/ShooterMover is unavailable.")
    for path in sorted(assets.rglob("*.cs")):
        relative = normalize(path, root)
        if "/Tests/" in relative or "/Editor/" in relative:
            continue
        yield relative, path.read_text(encoding="utf-8")


def scan_scene_loaded_subscriptions(sources: Iterable[tuple[str, str]]) -> list[Finding]:
    pattern = re.compile(r"SceneManager\.sceneLoaded\s*\+=\s*([A-Za-z_][A-Za-z0-9_]*)")
    return [
        Finding("scene-loaded-subscription", path, match.group(1))
        for path, text in sources for match in pattern.finditer(text)
    ]


def scan_private_stage1_reflection(sources: Iterable[tuple[str, str]]) -> list[Finding]:
    targets = re.compile(
        r"typeof\s*\(\s*(Stage1VisibleSliceController|Stage1PlayableLoopCompositionV1)\s*\)"
    )
    member = re.compile(r"\.Get(Field|Method|Property)\s*\(\s*\"([^\"]+)\"")
    findings: list[Finding] = []
    for path, text in sources:
        for target in targets.finditer(text):
            window = text[target.start():target.start()+700]
            if "BindingFlags.NonPublic" not in window:
                continue
            found = member.search(window)
            detail = f"{target.group(1)}.{found.group(2) if found else '<private-member>'}"
            findings.append(Finding("private-stage1-reflection", path, detail))
    return findings


def validate_inventory_findings(
    findings: Sequence[Finding], expected: set[tuple[str, str]], rule: str
) -> None:
    actual = {(item.path, item.detail) for item in findings}
    if actual != expected:
        raise AuditError(
            f"{rule} inventory drift; new={sorted(actual-expected)}, removed={sorted(expected-actual)}"
        )


def declaration_interfaces(text: str, type_name: str) -> list[str]:
    match = re.search(rf"\bclass\s+{re.escape(type_name)}\s*:\s*(.*?)\{{", text, re.DOTALL)
    if not match:
        return []
    raw = re.sub(r"\s+", " ", match.group(1)).strip()
    names = [item.strip().split("<", 1)[0].split(".")[-1] for item in raw.split(",")]
    return [name for name in names if name and name != "MonoBehaviour"]


def validate_declared_interfaces(
    text: str, type_name: str, expected: Sequence[str], label: str
) -> None:
    actual = declaration_interfaces(text, type_name)
    if actual != list(expected):
        raise AuditError(f"{label} interface drift: expected {list(expected)}, got {actual}")


def validate_interface_baselines(root: Path, manifest: dict) -> None:
    checks = [
        (
            "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs",
            TYPE_CONTROLLER,
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
            "controller gameplay authority",
        ),
        (
            "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.cs",
            TYPE_COMPOSITION,
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1",
            "composition authority/persistence",
        ),
    ]
    for path, type_name, key, label in checks:
        validate_declared_interfaces(
            (root / path).read_text(encoding="utf-8"), type_name,
            manifest["interface_baselines"][key], label,
        )


def added_lines(root: Path, base: str, head: str) -> list[tuple[str, str]]:
    output = git(root, "diff", "--unified=0", "--no-ext-diff", f"{base}...{head}", "--", "*.cs")
    current = ""
    result: list[tuple[str, str]] = []
    for line in output.splitlines():
        if line.startswith("+++ b/"):
            current = line[6:]
        elif line.startswith("+") and not line.startswith("+++"):
            result.append((current, line[1:]))
    return result


def scan_added_line_violations(
    lines: Iterable[tuple[str, str]], manifest: dict | None = None
) -> list[Finding]:
    findings: list[Finding] = []
    authority = re.compile(r"\bnew\s+(" + "|".join(map(re.escape, CONFIG["authorities"])) + r")\b")
    aggregate = re.compile(r"\bnew\s+RunSessionAggregateV1\s*\(")
    marker_groups = [
        ("new-retained-persistence", CONFIG["persistence"]),
        ("new-stage1-reward-selection", CONFIG["reward"]),
        ("new-stage1-weapon-switch", CONFIG["weapon"]),
        ("new-stage1-global-discovery", CONFIG["discovery"]),
        ("new-name-or-hierarchy-decision", CONFIG["name"]),
    ]
    for path, line in lines:
        if not is_retained_path(path, manifest):
            continue
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        lowered = stripped.lower()
        if aggregate.search(stripped):
            findings.append(Finding("duplicate-run-session-aggregate", path, stripped))
        match = authority.search(stripped)
        if match:
            findings.append(Finding("new-retained-authority-construction", path, match.group(1)))
        for rule, markers in marker_groups:
            if any(marker.lower() in lowered for marker in markers):
                findings.append(Finding(rule, path, stripped))
        if Path(path).name in {"Stage1VisibleSliceController.cs", "Stage1PlayableLoopCompositionV1.cs"}:
            if any(marker.lower() in lowered for marker in CONFIG["content"]):
                findings.append(Finding("new-content-registration-in-retained-controller", path, stripped))
    return findings


def scan_full_direct_aggregate_creation(root: Path, manifest: dict) -> list[Finding]:
    pattern = re.compile(r"\bnew\s+RunSessionAggregateV1\s*\(")
    return [
        Finding("duplicate-run-session-aggregate", path, "direct construction")
        for path, text in iter_cs_files(root)
        if is_retained_path(path, manifest) and pattern.search(text)
    ]


def ordinary_content_path_is_decoupled(path: str, manifest: dict | None = None) -> bool:
    return any(path.startswith(root) for root in CONFIG["ordinary_content_roots"]) and not is_retained_path(path, manifest)


def run_audit(root: Path, manifest_path: Path, head: str) -> None:
    manifest = load_manifest(manifest_path)
    validate_manifest_plan(manifest)
    validate_source_tree(root, manifest)
    validate_known_debt(root, manifest)
    validate_interface_baselines(root, manifest)
    sources = list(iter_cs_files(root))
    source_map = source_records(manifest)
    expected_hooks = {
        (source_map[item[1]]["path"], item[2])
        for item in manifest["scene_loaded_subscription_inventory"]
        for _ in range(item[3])
    }
    stage1_hooks = [
        finding for finding in scan_scene_loaded_subscriptions(sources)
        if is_stage1_production_path(finding.path)
    ]
    validate_inventory_findings(
        stage1_hooks, expected_hooks, "SceneManager.sceneLoaded"
    )
    expected_reflection = {
        (source_map[item[2]]["path"], f"{item[1].split('.')[-1]}.{item[4]}")
        for item in manifest["production_reflection_inventory"]
        for _ in range(item[6])
    }
    validate_inventory_findings(
        scan_private_stage1_reflection(sources), expected_reflection, "Stage 1 private reflection"
    )
    aggregate = scan_full_direct_aggregate_creation(root, manifest)
    if aggregate:
        raise AuditError("Retained classes directly create RunSessionAggregateV1:\n" + "\n".join(x.render() for x in aggregate))
    base = manifest["launch_main_sha"]
    git(root, "merge-base", "--is-ancestor", base, head)
    violations = scan_added_line_violations(added_lines(root, base, head), manifest)
    if violations:
        raise AuditError("New Stage 1 migration violations:\n" + "\n".join(x.render() for x in violations))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()
    root = Path(git(Path.cwd(), "rev-parse", "--show-toplevel").strip())
    manifest_path = root / args.manifest
    run_audit(root, manifest_path, args.head)
    manifest = load_manifest(manifest_path)
    print(
        f"STAGE1-FREEZE-001 passed: {len(source_records(manifest))} source files, "
        f"{len(debt_records(manifest))} debt entries, 0 new violations."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AuditError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
