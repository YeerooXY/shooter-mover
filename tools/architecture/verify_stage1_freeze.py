#!/usr/bin/env python3
"""Source-level architecture freeze for retained Stage 1 migration surfaces."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

DEFAULT_MANIFEST = "docs/architecture/stage1/stage1_migration_responsibilities_v1.json"
TYPE_CONTROLLER = "Stage1VisibleSliceController"
TYPE_COMPOSITION = "Stage1PlayableLoopCompositionV1"
TYPE_DRIVER = "Stage1RunLoopDriver2D"
CONFIG = {
    "ordinary_content_roots": [
        "Assets/ShooterMover/Runtime/Content/Definitions/",
        "Assets/ShooterMover/Production/Content/Definitions/",
        "Assets/ShooterMover/Resources/",
    ],
    "content_registration": [
        r"\bRegister\s*\(",
        r"\bStableId\.Parse\s*\(\s*\"(?:weapon|enemy|prop|room)\.",
        r"\b(?:Weapon|Enemy|Prop|Room)DefinitionId\b",
        r"\b(?:Weapon|Enemy|Prop|Room)Catalog\b",
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


@dataclass(frozen=True)
class AddedBlock:
    path: str
    added_text: str
    context_text: str


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


def table_records(manifest: dict, fields_key: str, rows_key: str) -> list[dict]:
    fields = manifest.get(fields_key, [])
    return [dict(zip(fields, values)) for values in manifest.get(rows_key, [])]


def source_records(manifest: dict) -> dict[str, dict]:
    fields = manifest["source_record_fields"]
    return {
        source_id: dict(zip(fields, values))
        for source_id, values in manifest["source_files"].items()
    }


def target_records(manifest: dict) -> list[dict]:
    return table_records(manifest, "target_fields", "frozen_targets")


def debt_records(manifest: dict) -> list[dict]:
    return table_records(manifest, "debt_fields", "known_retained_debt")


def retirement_records(manifest: dict) -> list[dict]:
    return table_records(manifest, "retirement_target_fields", "retirement_targets")


def approved_replacement_records(manifest: dict) -> list[dict]:
    return table_records(
        manifest, "approved_replacement_type_fields", "approved_replacement_types"
    )


def canonical_owner_records(manifest: dict) -> list[dict]:
    return table_records(manifest, "canonical_owner_fields", "canonical_owners")


def normalize(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def collapse(text: str) -> str:
    without_block_comments = re.sub(r"/\*.*?\*/", " ", text, flags=re.DOTALL)
    without_line_comments = re.sub(r"//[^\n]*", " ", without_block_comments)
    return re.sub(r"\s+", " ", without_line_comments).strip()


def is_retained_path(path: str, manifest: dict | None = None) -> bool:
    """Return true only for sources explicitly inventoried as retained debt."""
    if manifest is None:
        return False
    return path in {record["path"] for record in source_records(manifest).values()}


def is_ordinary_content_path(path: str, manifest: dict | None = None) -> bool:
    return any(path.startswith(root) for root in CONFIG["ordinary_content_roots"])


def is_stage1_source(path: str, text: str = "") -> bool:
    """Classify Stage 1 source by path, namespace, or declarations—not folder allowlists."""
    lowered = path.lower()
    if "/tests/" in lowered or "/editor/" in lowered:
        return False
    stem = Path(path).stem
    if re.search(r"Stage1(?!\d)", stem) or "/stage1/" in lowered:
        return True
    if re.search(r"\bnamespace\s+[A-Za-z0-9_.]*\.Stage1(?:\b|\.)", text):
        return True
    if re.search(
        r"\b(?:class|struct|record|interface)\s+Stage1(?!\d)[A-Za-z0-9_]*",
        text,
    ):
        return True
    return False


def governed_stage1_change(path: str, text: str, manifest: dict) -> bool:
    if is_retained_path(path, manifest):
        return True
    if is_ordinary_content_path(path, manifest) and not is_stage1_source(path, text):
        return False
    return is_stage1_source(path, text)


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
    if not any(
        item["type"].endswith(TYPE_CONTROLLER)
        and TYPE_COMPOSITION in item["prohibited_outcome"]
        for item in retirement
    ):
        raise AuditError("The plan must reject moving controller behavior into the composition.")

    required_boundaries = {
        "Stage1SceneInstaller2D", TYPE_DRIVER,
        "InventoryWeaponEffectDamageRouter2D", "Stage1RoomFlowController2D",
        "Stage1EnemyTerminalPickupConsumerV1", "Stage1PropTerminalPickupConsumerV1",
        "RunPickupLifecycleProjection2D", "Stage1LegacyScenePresentation2D",
    }
    boundaries = set(manifest.get("replacement_boundaries", []))
    missing = required_boundaries - boundaries
    if missing:
        raise AuditError(f"Missing replacement boundaries: {sorted(missing)}")

    approved = approved_replacement_records(manifest)
    unique_ids(approved, "type", "approved replacement type")
    if {item["type"] for item in approved} != boundaries:
        raise AuditError(
            "Approved replacement type records must exactly match replacement_boundaries."
        )
    driver = next(item for item in approved if item["type"] == TYPE_DRIVER)
    allowed = driver["allowed_ownership"].lower()
    forbidden = driver["forbidden_ownership"].lower()
    for token in ("observe lifecycle", "forward typed commands", "restart/end"):
        if token not in allowed:
            raise AuditError(f"{TYPE_DRIVER} policy is missing narrow allowance: {token}")
    for token in (
        "player", "weapon", "enemy", "room", "reward", "durable transfer",
        "results", "persistence",
    ):
        if token not in forbidden:
            raise AuditError(f"{TYPE_DRIVER} policy is missing forbidden ownership: {token}")

    canonical = canonical_owner_records(manifest)
    unique_ids(canonical, "owner_id", "canonical owner")
    required_owner_ids = {
        "player-scene-input", "inventory-weapon-runtime", "enemy-runtime-scheduler",
        "room-authority", "run-session-authority", "collected-run-transfer",
        "results-flow",
    }
    if required_owner_ids - {item["owner_id"] for item in canonical}:
        raise AuditError("Canonical owner map is incomplete.")

    expected = [
        "STAGE1-FREEZE-001", "ROOM-JSON-LIVE-001",
        "STAGE1-RUNTIME-DECOMPOSE-A-001", "STAGE1-RUNTIME-DECOMPOSE-B-001",
        "ABILITY-RUNTIME-001", "LEVEL1-CONTROLLER-RETIRE-001",
    ]
    if manifest["split_sequence"] != expected:
        raise AuditError("Stage 1 integration sequence does not match the frozen plan.")

    unique_ids(debt_records(manifest), "id", "known retained debt")
    responsibilities: list[dict] = []
    sources = source_records(manifest)
    for target in target_records(manifest):
        responsibilities.extend({"id": pair[0]} for pair in target["responsibilities"])
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


def declared_type_names(text: str) -> set[str]:
    return set(re.findall(
        r"\b(?:class|struct|record|interface)\s+([A-Za-z_][A-Za-z0-9_]*)",
        text,
    ))


def validate_source_tree(root: Path, manifest: dict) -> None:
    errors: list[str] = []
    sources = source_records(manifest)
    for source_id, record in sources.items():
        path = root / record["path"]
        if not path.is_file():
            errors.append(f"missing frozen source {source_id}: {record['path']}")
            continue
        text = path.read_text(encoding="utf-8")
        actual_lines = len(text.splitlines())
        if actual_lines != record["approximate_line_count"]:
            errors.append(
                f"line-count drift {record['path']}: expected "
                f"{record['approximate_line_count']}, got {actual_lines}"
            )
        blob = git(root, "hash-object", "--", str(path)).strip()
        if blob != record["git_blob_sha"]:
            errors.append(
                f"blob drift {record['path']}: expected {record['git_blob_sha']}, got {blob}; "
                "update the baseline and migration plan intentionally after extraction"
            )
    if errors:
        raise AuditError("\n".join(errors))


def validate_known_debt(root: Path, manifest: dict) -> None:
    sources = source_records(manifest)
    for debt in debt_records(manifest):
        if debt["source_id"] not in sources:
            raise AuditError(
                f"Known debt references an unlisted source: {debt['id']}: {debt['source_id']}"
            )
        record = sources[debt["source_id"]]
        path = root / record["path"]
        if not path.is_file():
            raise AuditError(f"Known debt path is absent: {debt['id']}: {record['path']}")
        if re.search(
            debt["anchor_regex"], path.read_text(encoding="utf-8"), re.MULTILINE
        ) is None:
            raise AuditError(
                f"Known debt anchor no longer exists: {debt['id']}; "
                "update the baseline intentionally"
            )
        if not debt["replacement_owner"] or not debt["retirement_task"]:
            raise AuditError(f"Known debt lacks replacement plan: {debt['id']}")


def iter_cs_files(root: Path) -> Iterable[tuple[str, str]]:
    assets = root / "Assets/ShooterMover"
    if not assets.is_dir():
        raise AuditError("Assets/ShooterMover is unavailable.")
    for path in sorted(assets.rglob("*.cs")):
        relative = normalize(path, root)
        lowered = relative.lower()
        if "/tests/" in lowered or "/editor/" in lowered:
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
            window = text[target.start():target.start() + 1200]
            if "BindingFlags.NonPublic" not in window:
                continue
            found = member.search(window)
            detail = f"{target.group(1)}.{found.group(2) if found else '<private-member>'}"
            findings.append(Finding("private-stage1-reflection", path, detail))
    return findings


def validate_inventory_findings(
    findings: Sequence[Finding], expected: Counter[tuple[str, str]], rule: str
) -> None:
    actual = Counter((item.path, item.detail) for item in findings)
    if actual != expected:
        new = list((actual - expected).elements())
        removed = list((expected - actual).elements())
        raise AuditError(f"{rule} inventory drift; new={sorted(new)}, removed={sorted(removed)}")


def declaration_interfaces(text: str, type_name: str) -> list[str]:
    pattern = re.compile(
        rf"\bclass\s+{re.escape(type_name)}(?:\s*<[^{{>]+>)?\s*"
        rf"(?::\s*(?P<bases>[^{{]+))?\{{",
        re.DOTALL,
    )
    interfaces: list[str] = []
    for match in pattern.finditer(text):
        raw = collapse(match.group("bases") or "")
        if not raw:
            continue
        for item in raw.split(","):
            name = item.strip().split("<", 1)[0].split(".")[-1]
            if name and name not in {"MonoBehaviour", "ScriptableObject"}:
                interfaces.append(name)
    return interfaces


def aggregate_type_interfaces(
    sources: Iterable[tuple[str, str]], type_name: str
) -> list[str]:
    return sorted({
        interface
        for _, text in sources
        for interface in declaration_interfaces(text, type_name)
    })


def validate_interface_baselines(
    sources: Sequence[tuple[str, str]], manifest: dict
) -> None:
    checks = [
        (
            TYPE_CONTROLLER,
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
            "controller gameplay authority",
        ),
        (
            TYPE_COMPOSITION,
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1",
            "composition authority/persistence",
        ),
    ]
    baselines = manifest.get("interface_baselines", {})
    for type_name, key, label in checks:
        if key not in baselines:
            raise AuditError(f"Missing interface baseline for {key}")
        expected = sorted({item.split(".")[-1] for item in baselines[key]})
        actual = aggregate_type_interfaces(sources, type_name)
        if actual != expected:
            raise AuditError(f"{label} interface drift: expected {expected}, got {actual}")


def changed_cs_statuses(root: Path, base: str, head: str) -> list[tuple[str, str]]:
    output = git(
        root, "diff", "--name-status", "--find-renames", f"{base}...{head}",
        "--", "*.cs",
    )
    result: list[tuple[str, str]] = []
    for raw in output.splitlines():
        if not raw.strip():
            continue
        fields = raw.split("\t")
        result.append((fields[0], fields[-1]))
    return result


def validate_new_stage1_source_inventory(
    root: Path, manifest: dict, base: str, head: str
) -> None:
    retained = {record["path"] for record in source_records(manifest).values()}
    approved = {item["type"] for item in approved_replacement_records(manifest)}
    errors: list[str] = []
    for status, path in changed_cs_statuses(root, base, head):
        if not (status.startswith("A") or status.startswith("R")):
            continue
        absolute = root / path
        if not absolute.is_file():
            continue
        text = absolute.read_text(encoding="utf-8")
        if not is_stage1_source(path, text):
            continue
        if path in retained:
            continue
        declared = declared_type_names(text)
        approved_declared = declared & approved
        unapproved_stage1 = {
            name for name in declared
            if re.match(r"Stage1(?!\d)", name) and name not in approved
        }
        if len(approved_declared) != 1 or unapproved_stage1:
            errors.append(
                f"unlisted Stage 1 production source {path}; approved={sorted(approved_declared)}, "
                f"unapproved-stage1-types={sorted(unapproved_stage1)}. Add it as exact retained "
                "debt or declare exactly one approved replacement boundary."
            )
    if errors:
        raise AuditError("\n".join(errors))


def parse_added_blocks(root: Path, base: str, head: str) -> list[AddedBlock]:
    output = git(
        root, "diff", "--unified=3", "--no-ext-diff", "--find-renames",
        f"{base}...{head}", "--", "*.cs",
    )
    blocks: list[AddedBlock] = []
    path = ""
    in_hunk = False
    added: list[str] = []
    context: list[str] = []

    def flush() -> None:
        nonlocal added, context
        if path and added:
            blocks.append(AddedBlock(path, "\n".join(added), "\n".join(context)))
        added = []
        context = []

    for line in output.splitlines():
        if line.startswith("diff --git "):
            flush()
            in_hunk = False
        elif line.startswith("+++ b/"):
            path = line[6:]
        elif line.startswith("@@"):
            flush()
            in_hunk = True
        elif in_hunk:
            if line.startswith("+") and not line.startswith("+++"):
                value = line[1:]
                added.append(value)
                context.append(value)
            elif line.startswith(" "):
                context.append(line[1:])
            elif line.startswith("-"):
                continue
    flush()
    return blocks


def alias_map(text: str) -> dict[str, str]:
    return {
        alias: target
        for alias, target in re.findall(
            r"\busing\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([A-Za-z0-9_.]+)\s*;",
            text,
        )
    }


def simple_type_name(value: str) -> str:
    value = value.split("<", 1)[0].rstrip("?[]")
    return value.split(".")[-1]


def is_mutable_owner_type(value: str) -> bool:
    name = simple_type_name(value)
    patterns = (
        r".*Authority(?:V\d+)?$",
        r".*Aggregate(?:V\d+)?$",
        r".*Wallet(?:Service)?(?:V\d+)?$",
        r".*HoldingsService(?:V\d+)?$",
        r".*Reward.*Service(?:V\d+)?$",
        r".*Strongbox.*Service(?:V\d+)?$",
        r".*Persistence(?:V\d+)?$",
        r".*Repository(?:V\d+)?$",
        r".*Store(?:V\d+)?$",
        r".*Scheduler(?:V\d+)?$",
        r".*RuntimeComposition(?:V\d+)?$",
    )
    return any(re.fullmatch(pattern, name) for pattern in patterns)


def constructed_types(text: str) -> list[str]:
    return re.findall(r"\bnew\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?:<[^;(){}]+>)?\s*\(", text)


def scan_added_block_violations(
    blocks: Iterable[AddedBlock], manifest: dict, file_texts: dict[str, str]
) -> list[Finding]:
    findings: list[Finding] = []
    for block in blocks:
        full_text = file_texts.get(block.path, "")
        if not governed_stage1_change(block.path, full_text, manifest):
            continue
        added = collapse(block.added_text)
        combined = collapse(block.context_text)
        if not added:
            continue
        aliases = alias_map(full_text)

        for constructed in constructed_types(added):
            resolved = aliases.get(simple_type_name(constructed), constructed)
            simple = simple_type_name(resolved)
            if simple == "RunSessionAggregateV1":
                findings.append(
                    Finding("duplicate-run-session-aggregate", block.path, simple)
                )
            elif is_mutable_owner_type(resolved):
                findings.append(
                    Finding("new-retained-authority-construction", block.path, simple)
                )

        if re.search(
            r"\bclass\s+\w+[^{:]*:\s*[^\{]*(?:Persistence|Save|Store|Repository)",
            combined,
            re.IGNORECASE,
        ) or re.search(
            r"\b(?:private|protected|public|internal)\s+(?:static\s+)?(?:readonly\s+)?"
            r"[A-Za-z0-9_.]*(?:Persistence|SaveStore|Repository)[A-Za-z0-9_.]*\s+\w+",
            added,
            re.IGNORECASE,
        ) or re.search(r"\.(?:SaveAsync|Persist|WriteSave|CommitSave)\s*\(", added):
            findings.append(Finding("new-retained-persistence", block.path, added[:220]))

        reward_signal = re.search(
            r"\b(?:Random|probabilit\w*|weight(?:ed|s)?|roll\w*|choose\w*|select\w*)\b",
            added,
            re.IGNORECASE,
        )
        reward_context = re.search(
            r"\b(?:reward|drop|strongbox|loot)\w*\b", combined, re.IGNORECASE
        )
        if reward_signal and reward_context:
            findings.append(
                Finding("new-stage1-reward-selection", block.path, added[:220])
            )

        weapon_switch = re.search(
            r"\bswitch\s*\(\s*[^)]{0,500}(?:weapon[A-Za-z0-9_.]*DefinitionId|"
            r"WeaponDefinitionId|weapon\s*\.\s*DefinitionId)",
            combined,
            re.IGNORECASE,
        ) or re.search(
            r"\bcase\s+[^:]{0,200}(?:weapon\.|WeaponDefinitionId|\"weapon\.)",
            combined,
            re.IGNORECASE,
        )
        if weapon_switch and re.search(r"weapon|DefinitionId|case|switch", added, re.IGNORECASE):
            findings.append(
                Finding("new-stage1-weapon-switch", block.path, added[:220])
            )

        if re.search(
            r"SceneManager\.sceneLoaded\s*\+=|Find(?:First|Any)?Object(?:s)?ByType\s*<|"
            r"FindObject(?:s)?OfType\s*<|GameObject\.Find\s*\(",
            added,
        ):
            findings.append(
                Finding("new-stage1-global-discovery", block.path, added[:220])
            )

        name_token = re.search(
            r"(?:gameObject|transform|hierarchy|room)\s*\.\s*name\b|"
            r"transform\.Find\s*\(|GameObject\.Find\s*\(|\broom\s+number\b|"
            r"\bRoom\s*[0-9]+\b",
            combined,
            re.IGNORECASE,
        )
        decision = re.search(r"\b(?:if|switch|while)\s*\(|\?[^:]+:", combined)
        if name_token and decision and re.search(
            r"name|Find|room|Room", added, re.IGNORECASE
        ):
            findings.append(
                Finding("new-name-or-hierarchy-decision", block.path, added[:220])
            )

        basename = Path(block.path).name
        if basename.startswith(TYPE_CONTROLLER) or basename.startswith(TYPE_COMPOSITION):
            if any(re.search(pattern, added) for pattern in CONFIG["content_registration"]):
                findings.append(
                    Finding(
                        "new-content-registration-in-retained-controller",
                        block.path,
                        added[:220],
                    )
                )
    return findings


def scan_full_direct_aggregate_creation(root: Path, manifest: dict) -> list[Finding]:
    pattern = re.compile(r"\bnew\s+RunSessionAggregateV1\s*\(")
    return [
        Finding("duplicate-run-session-aggregate", path, "direct construction")
        for path, text in iter_cs_files(root)
        if is_retained_path(path, manifest) and pattern.search(text)
    ]


def validate_driver_policy(sources: Sequence[tuple[str, str]], manifest: dict) -> None:
    driver_sources = [(path, text) for path, text in sources if TYPE_DRIVER in declared_type_names(text)]
    if not driver_sources:
        return
    forbidden_methods = re.compile(
        r"\b(?:HandleWeaponInput|SelectWeapon|Apply\w*Damage|Generate\w*Reward|"
        r"ComposeEnemy|TickEnemy|Persist\w*|PresentResults|Navigate\w*|ReadCombatInput|"
        r"MovePlayer)\s*\("
    )
    field_pattern = re.compile(
        r"\b(?:private|protected|public|internal)\s+(?:static\s+)?(?:readonly\s+)?"
        r"(?P<type>[A-Z][A-Za-z0-9_.]*(?:<[^;=]+>)?)\s+[a-z_][A-Za-z0-9_]*\s*(?:;|=)"
    )
    findings: list[Finding] = []
    concrete_canonical = {
        "Level1PlayerRuntimeSceneAdapterV1", "InventoryWeaponRuntimeComposition",
        "InventoryBackedWeaponExecutionAdapter", "EnemyAttackPatternLiveSchedulerV1",
        "RoomRuntimeComposition2D", "ProductionCollectedRunRewardPersistenceV2",
        "MissionRunResultAuthorityV1", "ProductionCollectedRunRewardResultsBridge",
    }
    for path, text in driver_sources:
        for match in field_pattern.finditer(text):
            field_type = simple_type_name(match.group("type"))
            if (
                field_type in concrete_canonical or is_mutable_owner_type(field_type)
            ) and re.match(r"I[A-Z]", field_type) is None:
                findings.append(
                    Finding("run-loop-driver-concrete-owner-field", path, field_type)
                )
        for constructed in constructed_types(text):
            if is_mutable_owner_type(constructed):
                findings.append(
                    Finding(
                        "run-loop-driver-owner-construction", path,
                        simple_type_name(constructed),
                    )
                )
        for match in forbidden_methods.finditer(text):
            findings.append(
                Finding("run-loop-driver-gameplay-implementation", path, match.group(0))
            )
    if findings:
        raise AuditError(
            f"{TYPE_DRIVER} exceeds its narrow lifecycle role:\n"
            + "\n".join(item.render() for item in findings)
        )


def file_text_map(root: Path, statuses: Sequence[tuple[str, str]]) -> dict[str, str]:
    result: dict[str, str] = {}
    for _, path in statuses:
        absolute = root / path
        if absolute.is_file():
            result[path] = absolute.read_text(encoding="utf-8")
    return result


def run_audit(root: Path, manifest_path: Path, head: str) -> None:
    manifest = load_manifest(manifest_path)
    validate_manifest_plan(manifest)
    validate_source_tree(root, manifest)
    validate_known_debt(root, manifest)

    base = manifest["launch_main_sha"]
    git(root, "merge-base", "--is-ancestor", base, head)
    statuses = changed_cs_statuses(root, base, head)
    validate_new_stage1_source_inventory(root, manifest, base, head)

    sources = list(iter_cs_files(root))
    validate_interface_baselines(sources, manifest)
    validate_driver_policy(sources, manifest)

    source_map = source_records(manifest)
    expected_hooks: Counter[tuple[str, str]] = Counter()
    for item in manifest["scene_loaded_subscription_inventory"]:
        expected_hooks[(source_map[item[1]]["path"], item[2])] += item[3]
    stage1_hooks = [
        finding for finding in scan_scene_loaded_subscriptions(sources)
        if is_stage1_source(
            finding.path,
            next(text for path, text in sources if path == finding.path),
        )
    ]
    validate_inventory_findings(stage1_hooks, expected_hooks, "SceneManager.sceneLoaded")

    expected_reflection: Counter[tuple[str, str]] = Counter()
    for item in manifest["production_reflection_inventory"]:
        expected_reflection[(
            source_map[item[2]]["path"],
            f"{item[1].split('.')[-1]}.{item[4]}",
        )] += item[6]
    validate_inventory_findings(
        scan_private_stage1_reflection(sources),
        expected_reflection,
        "Stage 1 private reflection",
    )

    aggregate = scan_full_direct_aggregate_creation(root, manifest)
    if aggregate:
        raise AuditError(
            "Retained classes directly create RunSessionAggregateV1:\n"
            + "\n".join(item.render() for item in aggregate)
        )

    blocks = parse_added_blocks(root, base, head)
    violations = scan_added_block_violations(
        blocks, manifest, file_text_map(root, statuses)
    )
    if violations:
        raise AuditError(
            "New Stage 1 migration violations:\n"
            + "\n".join(item.render() for item in violations)
        )


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
