#!/usr/bin/env python3
"""Fixture tests for tools/architecture/verify_stage1_freeze.py."""
from __future__ import annotations

import importlib.util
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


def fixture_manifest() -> dict:
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
        "replacement_boundaries": [
            "Stage1SceneInstaller2D", "Stage1RunLoopDriver2D",
            "InventoryWeaponEffectDamageRouter2D", "Stage1RoomFlowController2D",
            "Stage1EnemyTerminalPickupConsumerV1", "Stage1PropTerminalPickupConsumerV1",
            "RunPickupLifecycleProjection2D", "Stage1LegacyScenePresentation2D",
        ],
        "split_sequence": [
            "STAGE1-FREEZE-001", "ROOM-JSON-LIVE-001",
            "STAGE1-RUNTIME-DECOMPOSE-A-001", "STAGE1-RUNTIME-DECOMPOSE-B-001",
            "LEVEL1-CONTROLLER-RETIRE-001",
        ],
        "source_record_fields": ["path", "approximate_line_count", "git_blob_sha"],
        "source_files": {},
        "target_fields": [
            "type", "source_ids", "approximate_source_line_count",
            "interfaces", "lifecycle", "mutable_state",
            "discovery", "reflection", "responsibilities", "destination",
        ],
        "frozen_targets": [
            [
                "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController",
                [], 0, [], [], [], [], [],
                [["controller.existing", "Stage1SceneInstaller2D"]],
                "delete",
            ]
        ],
        "debt_fields": ["id", "source_id", "anchor_regex", "replacement_owner", "retirement_task"],
        "known_retained_debt": [],
        "scene_hook_fields": ["type", "source_id", "callback", "count"],
        "scene_loaded_subscription_inventory": [],
        "reflection_fields": ["caller", "target", "source_id", "kind", "member", "flags", "count"],
        "production_reflection_inventory": [],
        "interface_baselines": {},
    }


class Stage1FreezeFixtureTests(unittest.TestCase):
    def test_new_scene_loaded_installer_is_rejected(self) -> None:
        findings = audit.scan_scene_loaded_subscriptions([
            ("Assets/ShooterMover/Production/Stage1/NewInstaller.cs",
             "SceneManager.sceneLoaded += InstallAnotherStage1;")
        ])
        self.assertEqual(1, len(findings))
        with self.assertRaises(audit.AuditError):
            audit.validate_inventory_findings(findings, set(), "SceneManager.sceneLoaded")

    def test_new_private_reflection_is_rejected(self) -> None:
        source = """
        typeof(Stage1PlayableLoopCompositionV1)
            .GetMethod("HiddenOwner", BindingFlags.Instance | BindingFlags.NonPublic);
        """
        findings = audit.scan_private_stage1_reflection([
            ("Assets/ShooterMover/Production/Stage1/BadReflection.cs", source)
        ])
        self.assertEqual(1, len(findings))
        with self.assertRaises(audit.AuditError):
            audit.validate_inventory_findings(findings, set(), "Stage 1 private reflection")

    def test_name_based_gameplay_decision_is_rejected(self) -> None:
        findings = audit.scan_added_line_violations([
            ("Assets/ShooterMover/Production/Stage1/BadDecision.cs",
             'if (target.gameObject.name == "Room 2 Boss") return;')
        ], fixture_manifest())
        self.assertTrue(any(item.rule == "new-name-or-hierarchy-decision" for item in findings))

    def test_new_controller_gameplay_interface_is_rejected(self) -> None:
        source = """
        public sealed class Stage1VisibleSliceController :
            MonoBehaviour, IGeneralCombatHudStateSource, INewRewardAuthority { }
        """
        with self.assertRaises(audit.AuditError):
            audit.validate_declared_interfaces(
                source, "Stage1VisibleSliceController",
                ["IGeneralCombatHudStateSource"], "fixture controller",
            )

    def test_ordinary_content_definition_is_decoupled(self) -> None:
        path = "Assets/ShooterMover/Runtime/Content/Definitions/Enemies/NewEnemyDefinition.json"
        self.assertTrue(audit.ordinary_content_path_is_decoupled(path, fixture_manifest()))
        self.assertEqual([], audit.scan_added_line_violations(
            [(path, '"definition_id": "enemy.fixture"')], fixture_manifest()
        ))

    def test_removed_debt_is_allowed_after_baseline_update(self) -> None:
        manifest = fixture_manifest()
        with tempfile.TemporaryDirectory() as directory:
            audit.validate_known_debt(Path(directory), manifest)

    def test_new_responsibility_without_plan_fails(self) -> None:
        findings = audit.scan_added_line_violations([
            ("Assets/ShooterMover/Production/Stage1/NewWalletOwner.cs",
             "wallet = new MoneyWalletService();")
        ], fixture_manifest())
        self.assertTrue(any(item.rule == "new-retained-authority-construction" for item in findings))

    def test_known_debt_must_be_represented_exactly_once(self) -> None:
        manifest = fixture_manifest()
        duplicate = [
            "duplicate.debt", "s01", "unused", "Stage1SceneInstaller2D",
            "STAGE1-RUNTIME-DECOMPOSE-A-001",
        ]
        manifest["known_retained_debt"] = [duplicate, list(duplicate)]
        with self.assertRaises(audit.AuditError):
            audit.validate_manifest_plan(manifest)

    def test_direct_run_aggregate_creation_is_rejected(self) -> None:
        findings = audit.scan_added_line_violations([
            ("Assets/ShooterMover/Production/Stage1/NewRunOwner.cs",
             "run = new RunSessionAggregateV1(arguments);")
        ], fixture_manifest())
        self.assertTrue(any(item.rule == "duplicate-run-session-aggregate" for item in findings))


if __name__ == "__main__":
    unittest.main(verbosity=2)
