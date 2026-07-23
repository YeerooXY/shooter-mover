#!/usr/bin/env python3
"""Regression and end-to-end fixtures for verify_stage1_freeze.py."""
from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).with_name("verify_stage1_freeze.py")
SPEC = importlib.util.spec_from_file_location("verify_stage1_freeze", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
audit = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = audit
SPEC.loader.exec_module(audit)


REPLACEMENTS = [
    "Stage1SceneInstaller2D", "Stage1RunLoopDriver2D",
    "InventoryWeaponEffectDamageRouter2D", "Stage1RoomFlowController2D",
    "Stage1EnemyTerminalPickupConsumerV1", "Stage1PropTerminalPickupConsumerV1",
    "RunPickupLifecycleProjection2D", "Stage1LegacyScenePresentation2D",
]


def fixture_manifest() -> dict:
    approved = []
    for type_name in REPLACEMENTS:
        if type_name == "Stage1RunLoopDriver2D":
            approved.append([
                type_name,
                "narrow lifecycle coordinator",
                "observe lifecycle; forward typed commands; request restart/end",
                "player weapon enemy room reward durable transfer Results persistence",
                "STAGE1-RUNTIME-DECOMPOSE-A-001",
            ])
        else:
            approved.append([
                type_name, "focused replacement", "typed projection ports",
                "unrelated authority persistence", "STAGE1-RUNTIME-DECOMPOSE-B-001",
            ])
    return {
        "schema": "stage1-migration-responsibility-manifest-v1",
        "launch_main_sha": "0" * 40,
        "retirement_target_fields": ["type", "required_outcome", "prohibited_outcome"],
        "retirement_targets": [
            [
                "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
                "retire",
                "Do not move Stage1VisibleSliceController into Stage1PlayableLoopCompositionV1",
            ],
            [
                "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1",
                "retire",
                "Do not move Stage1VisibleSliceController into this type",
            ],
        ],
        "replacement_boundaries": list(REPLACEMENTS),
        "approved_replacement_type_fields": [
            "type", "role", "allowed_ownership", "forbidden_ownership", "task",
        ],
        "approved_replacement_types": approved,
        "canonical_owner_fields": [
            "owner_id", "existing_types_or_boundary", "responsibility",
        ],
        "canonical_owners": [
            ["player-scene-input", ["Level1PlayerRuntimeSceneAdapterV1"], "player"],
            ["inventory-weapon-runtime", ["InventoryWeaponRuntimeComposition"], "weapon"],
            ["enemy-runtime-scheduler", ["EnemyAttackPatternLiveSchedulerV1"], "enemy"],
            ["room-authority", ["RoomRuntimeComposition2D"], "room"],
            ["run-session-authority", ["RunSessionAggregateV1"], "run"],
            ["collected-run-transfer", ["ProductionCollectedRunRewardPersistenceV2"], "transfer"],
            ["results-flow", ["ProductionCollectedRunRewardResultsBridge"], "results"],
        ],
        "split_sequence": [
            "STAGE1-FREEZE-001", "ROOM-JSON-LIVE-001",
            "STAGE1-RUNTIME-DECOMPOSE-A-001", "STAGE1-RUNTIME-DECOMPOSE-B-001",
            "ABILITY-RUNTIME-001", "LEVEL1-CONTROLLER-RETIRE-001",
        ],
        "source_record_fields": ["path", "approximate_line_count", "git_blob_sha"],
        "source_files": {},
        "target_fields": [
            "type", "source_ids", "approximate_source_line_count",
            "interfaces", "lifecycle", "mutable_state",
            "discovery", "reflection", "responsibilities", "destination",
        ],
        "frozen_targets": [],
        "debt_fields": [
            "id", "source_id", "anchor_regex", "replacement_owner", "retirement_task",
        ],
        "known_retained_debt": [],
        "scene_hook_fields": ["type", "source_id", "callback", "count"],
        "scene_loaded_subscription_inventory": [],
        "reflection_fields": [
            "caller", "target", "source_id", "kind", "member", "flags", "count",
        ],
        "production_reflection_inventory": [],
        "interface_baselines": {
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController": [],
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1": [],
        },
    }


class TemporaryAuditRepository:
    controller_path = (
        "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs"
    )
    composition_path = (
        "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.cs"
    )
    legacy_path = "Assets/ShooterMover/Production/Stage1/Stage1LegacyDebt.cs"

    def __init__(self, include_legacy: bool = False) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.manifest = fixture_manifest()
        self._git("init", "-q")
        self._git("config", "user.email", "fixture@example.com")
        self._git("config", "user.name", "Fixture")
        self.write(
            self.controller_path,
            "namespace ShooterMover.TestSupport.VisibleSlice\n"
            "{\n"
            "    public sealed class Stage1VisibleSliceController : UnityEngine.MonoBehaviour { }\n"
            "}\n",
        )
        self.write(
            self.composition_path,
            "namespace ShooterMover.UnityAdapters.Production.Stage1\n"
            "{\n"
            "    public sealed partial class Stage1PlayableLoopCompositionV1 : UnityEngine.MonoBehaviour { }\n"
            "}\n",
        )
        if include_legacy:
            self.write(
                self.legacy_path,
                "namespace ShooterMover.UnityAdapters.Production.Stage1\n"
                "{\n"
                "    internal static class Stage1LegacyDebt\n"
                "    {\n"
                "        internal const string LegacyAnchor = \"legacy-anchor\";\n"
                "    }\n"
                "}\n",
            )
        self.commit("launch")
        self.base = self._git("rev-parse", "HEAD").strip()
        self.manifest["launch_main_sha"] = self.base
        self.add_source("s-controller", self.controller_path)
        self.add_source("s-composition", self.composition_path)
        self.manifest["frozen_targets"] = [
            self.target(
                "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
                ["s-controller"],
                "controller.existing",
            ),
            self.target(
                "ShooterMover.UnityAdapters.Production.Stage1.Stage1PlayableLoopCompositionV1",
                ["s-composition"],
                "composition.existing",
            ),
        ]
        if include_legacy:
            self.add_source("s-legacy", self.legacy_path)
            self.manifest["frozen_targets"].append(
                self.target(
                    "ShooterMover.UnityAdapters.Production.Stage1.Stage1LegacyDebt",
                    ["s-legacy"],
                    "legacy.existing",
                )
            )
            self.manifest["known_retained_debt"] = [[
                "legacy.debt", "s-legacy", "legacy-anchor",
                "Stage1SceneInstaller2D", "STAGE1-RUNTIME-DECOMPOSE-A-001",
            ]]
        self.manifest_path = self.root / "manifest.json"
        self.save_manifest()

    def close(self) -> None:
        self.temp.cleanup()

    def _git(self, *args: str) -> str:
        result = subprocess.run(
            ["git", *args], cwd=self.root, text=True, check=True,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        )
        return result.stdout

    def write(self, path: str, content: str) -> None:
        absolute = self.root / path
        absolute.parent.mkdir(parents=True, exist_ok=True)
        absolute.write_text(content, encoding="utf-8")

    def commit(self, message: str) -> None:
        self._git("add", "-A")
        self._git("commit", "-q", "-m", message)

    def blob(self, path: str) -> str:
        return self._git("hash-object", "--", path).strip()

    def add_source(self, source_id: str, path: str) -> None:
        text = (self.root / path).read_text(encoding="utf-8")
        self.manifest["source_files"][source_id] = [
            path, len(text.splitlines()), self.blob(path),
        ]

    def refresh_source(self, source_id: str) -> None:
        path = self.manifest["source_files"][source_id][0]
        self.add_source(source_id, path)
        for row in self.manifest["frozen_targets"]:
            data = dict(zip(self.manifest["target_fields"], row))
            if source_id in data["source_ids"]:
                data["approximate_source_line_count"] = sum(
                    self.manifest["source_files"][item][1]
                    for item in dict.fromkeys(data["source_ids"])
                )
                row[:] = [data[field] for field in self.manifest["target_fields"]]

    def target(self, type_name: str, source_ids: list[str], responsibility: str) -> list:
        lines = sum(self.manifest["source_files"][item][1] for item in source_ids)
        return [
            type_name, source_ids, lines, [], [], [], [], [],
            [[responsibility, "Stage1SceneInstaller2D"]], "delete",
        ]

    def save_manifest(self) -> None:
        self.manifest_path.write_text(json.dumps(self.manifest), encoding="utf-8")

    def audit(self) -> None:
        self.save_manifest()
        audit.run_audit(self.root, self.manifest_path, "HEAD")


class Stage1FreezeFixtureTests(unittest.TestCase):
    def test_new_scene_loaded_installer_is_rejected(self) -> None:
        findings = audit.scan_scene_loaded_subscriptions([
            ("Assets/ShooterMover/Production/Stage1/NewInstaller.cs",
             "SceneManager.sceneLoaded += InstallAnotherStage1;")
        ])
        with self.assertRaises(audit.AuditError):
            audit.validate_inventory_findings(
                findings, audit.Counter(), "SceneManager.sceneLoaded"
            )

    def test_new_private_reflection_is_rejected(self) -> None:
        source = """
        typeof(Stage1PlayableLoopCompositionV1)
            .GetMethod("HiddenOwner", BindingFlags.Instance | BindingFlags.NonPublic);
        """
        findings = audit.scan_private_stage1_reflection([
            ("Assets/ShooterMover/Production/Stage1/BadReflection.cs", source)
        ])
        with self.assertRaises(audit.AuditError):
            audit.validate_inventory_findings(
                findings, audit.Counter(), "Stage 1 private reflection"
            )

    def test_name_based_gameplay_decision_is_rejected(self) -> None:
        manifest = fixture_manifest()
        path = "Assets/ShooterMover/Production/Stage1/BadDecision.cs"
        findings = audit.scan_added_block_violations(
            [audit.AddedBlock(path,
                'if (target.gameObject.name == "Room 2 Boss") return;',
                'if (target.gameObject.name == "Room 2 Boss") return;')],
            manifest,
            {path: "namespace X.Stage1 { class BadDecision {} }"},
        )
        self.assertTrue(any(item.rule == "new-name-or-hierarchy-decision" for item in findings))

    def test_multiline_weapon_definition_switch_is_rejected(self) -> None:
        manifest = fixture_manifest()
        path = "Assets/ShooterMover/Production/Stage1/WeaponDecision.cs"
        added = """switch (
            weapon.DefinitionId)
        {
            case WeaponIds.Blaster:
                break;
        }"""
        findings = audit.scan_added_block_violations(
            [audit.AddedBlock(path, added, added)], manifest,
            {path: "namespace X.Stage1 { class WeaponDecision {} }"},
        )
        self.assertTrue(any(item.rule == "new-stage1-weapon-switch" for item in findings))

    def test_unknown_current_authority_type_is_rejected(self) -> None:
        manifest = fixture_manifest()
        path = "Assets/ShooterMover/Production/Stage1/NewOwner.cs"
        added = "receipts = new CollectedRunRewardTransferReceiptAuthorityV1();"
        findings = audit.scan_added_block_violations(
            [audit.AddedBlock(path, added, added)], manifest,
            {path: "namespace X.Stage1 { class NewOwner {} }"},
        )
        self.assertTrue(any(
            item.rule == "new-retained-authority-construction" for item in findings
        ))

    def test_aliased_authority_construction_is_rejected(self) -> None:
        manifest = fixture_manifest()
        path = "Assets/ShooterMover/Production/Stage1/AliasOwner.cs"
        full = (
            "using ReceiptOwner = Foo.CollectedRunRewardTransferReceiptAuthorityV1;\n"
            "namespace X.Stage1 { class AliasOwner { void M() { var x = new ReceiptOwner(); } } }"
        )
        findings = audit.scan_added_block_violations(
            [audit.AddedBlock(path, "var x = new ReceiptOwner();", full)],
            manifest, {path: full},
        )
        self.assertTrue(any(
            item.rule == "new-retained-authority-construction" for item in findings
        ))

    def test_partial_class_interface_is_aggregated(self) -> None:
        sources = [
            ("A.cs", "public partial class Stage1PlayableLoopCompositionV1 : MonoBehaviour {}"),
            ("B.cs", "public partial class Stage1PlayableLoopCompositionV1 : INewRewardAuthority {}"),
        ]
        self.assertEqual(
            ["INewRewardAuthority"],
            audit.aggregate_type_interfaces(sources, "Stage1PlayableLoopCompositionV1"),
        )

    def test_manifest_uses_source_records_for_retained_paths(self) -> None:
        manifest = fixture_manifest()
        manifest["source_files"] = {"x": ["Elsewhere/Stage1Debt.cs", 1, "abc"]}
        self.assertTrue(audit.is_retained_path("Elsewhere/Stage1Debt.cs", manifest))
        self.assertFalse(audit.is_retained_path(
            "Assets/ShooterMover/Production/Stage1/Unlisted.cs", manifest
        ))

    def test_end_to_end_passing_audit(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            repo.audit()
        finally:
            repo.close()

    def test_end_to_end_unlisted_stage1_coordinator_fails(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            repo.write(
                "Assets/ShooterMover/Production/Stage1/Stage1SomethingCoordinator.cs",
                "namespace ShooterMover.UnityAdapters.Production.Stage1\n"
                "{ internal sealed class Stage1SomethingCoordinator { } }\n",
            )
            repo.commit("bad coordinator")
            with self.assertRaisesRegex(audit.AuditError, "unlisted Stage 1 production source"):
                repo.audit()
        finally:
            repo.close()

    def test_end_to_end_stage1_class_outside_legacy_prefixes_fails(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            repo.write(
                "Assets/ShooterMover/Runtime/Flow/UnexpectedCoordinator.cs",
                "namespace ShooterMover.Runtime.Flow\n"
                "{ internal sealed class Stage1OutsideCoordinator { } }\n",
            )
            repo.commit("outside stage1 class")
            with self.assertRaisesRegex(audit.AuditError, "unlisted Stage 1 production source"):
                repo.audit()
        finally:
            repo.close()

    def test_end_to_end_partial_interface_fails_even_when_inventoried(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            path = (
                "Assets/ShooterMover/Production/Stage1/"
                "Stage1PlayableLoopCompositionV1.NewAuthority.cs"
            )
            repo.write(
                path,
                "namespace ShooterMover.UnityAdapters.Production.Stage1\n"
                "{ public sealed partial class Stage1PlayableLoopCompositionV1 : INewRewardAuthority { } }\n",
            )
            repo.commit("partial interface")
            repo.add_source("s-partial", path)
            repo.manifest["frozen_targets"][1][1].append("s-partial")
            repo.manifest["frozen_targets"][1][2] += len(
                (repo.root / path).read_text().splitlines()
            )
            with self.assertRaisesRegex(audit.AuditError, "interface drift"):
                repo.audit()
        finally:
            repo.close()

    def test_end_to_end_multiline_weapon_switch_fails_after_baseline_refresh(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            original = (repo.root / repo.composition_path).read_text()
            replacement = original.replace(
                "    public sealed partial class Stage1PlayableLoopCompositionV1 : UnityEngine.MonoBehaviour { }",
                """    public sealed partial class Stage1PlayableLoopCompositionV1 : UnityEngine.MonoBehaviour
    {
        private void Bad(Weapon weapon)
        {
            switch (
                weapon.DefinitionId)
            {
                case WeaponIds.Blaster:
                    break;
            }
        }
    }""",
            )
            repo.write(repo.composition_path, replacement)
            repo.commit("multiline weapon switch")
            repo.refresh_source("s-composition")
            with self.assertRaisesRegex(audit.AuditError, "new-stage1-weapon-switch"):
                repo.audit()
        finally:
            repo.close()

    def test_end_to_end_unknown_authority_fails_after_baseline_refresh(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            original = (repo.root / repo.composition_path).read_text()
            replacement = original.replace(
                " { }",
                " { private object x = new CollectedRunRewardTransferReceiptAuthorityV1(); }",
            )
            repo.write(repo.composition_path, replacement)
            repo.commit("unknown authority")
            repo.refresh_source("s-composition")
            with self.assertRaisesRegex(
                audit.AuditError, "new-retained-authority-construction"
            ):
                repo.audit()
        finally:
            repo.close()

    def test_end_to_end_ordinary_content_passes(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            repo.write(
                "Assets/ShooterMover/Runtime/Content/Definitions/Enemies/NewEnemyDefinition.cs",
                "namespace ShooterMover.Runtime.Content.Definitions.Enemies\n"
                "{ public sealed class NewEnemyDefinition { } }\n",
            )
            repo.commit("ordinary content")
            repo.audit()
        finally:
            repo.close()

    def test_end_to_end_approved_replacement_source_passes(self) -> None:
        repo = TemporaryAuditRepository()
        try:
            repo.write(
                "Assets/ShooterMover/Production/Stage1/Stage1SceneInstaller2D.cs",
                "namespace ShooterMover.UnityAdapters.Production.Stage1\n"
                "{ internal sealed class Stage1SceneInstaller2D { } }\n",
            )
            repo.commit("approved installer")
            repo.audit()
        finally:
            repo.close()

    def test_genuine_source_deletion_with_manifest_and_debt_update_passes(self) -> None:
        repo = TemporaryAuditRepository(include_legacy=True)
        try:
            (repo.root / repo.legacy_path).unlink()
            repo.commit("delete extracted debt")
            del repo.manifest["source_files"]["s-legacy"]
            repo.manifest["frozen_targets"] = [
                row for row in repo.manifest["frozen_targets"]
                if "s-legacy" not in row[1]
            ]
            repo.manifest["known_retained_debt"] = []
            repo.audit()
        finally:
            repo.close()

    def test_driver_concrete_owner_field_is_rejected(self) -> None:
        sources = [(
            "Assets/ShooterMover/Production/Stage1/Stage1RunLoopDriver2D.cs",
            "namespace X.Stage1 { class Stage1RunLoopDriver2D { "
            "private InventoryWeaponRuntimeComposition weapons; } }",
        )]
        with self.assertRaisesRegex(audit.AuditError, "exceeds its narrow lifecycle role"):
            audit.validate_driver_policy(sources, fixture_manifest())

    def test_duplicate_debt_id_is_rejected(self) -> None:
        manifest = fixture_manifest()
        duplicate = [
            "duplicate.debt", "s01", "unused", "Stage1SceneInstaller2D",
            "STAGE1-RUNTIME-DECOMPOSE-A-001",
        ]
        manifest["known_retained_debt"] = [duplicate, list(duplicate)]
        with self.assertRaises(audit.AuditError):
            audit.validate_manifest_plan(manifest)


if __name__ == "__main__":
    unittest.main(verbosity=2)
