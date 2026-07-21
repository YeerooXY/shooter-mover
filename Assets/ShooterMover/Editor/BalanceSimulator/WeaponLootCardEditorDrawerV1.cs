using System.Globalization;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    public static class WeaponLootCardEditorDrawerV1
    {
        public static bool Draw(
            EquipmentInstance equipment,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            ref bool diagnosticsExpanded)
        {
            WeaponLootCardProjectionV1 card;
            string diagnostic;
            if (!WeaponLootCardProjectionV1.TryCreate(
                    equipment,
                    equipmentCatalog,
                    weaponCatalog,
                    out card,
                    out diagnostic))
            {
                EditorGUILayout.HelpBox(
                    "Weapon card unavailable. Generation result was not projected: "
                    + diagnostic,
                    MessageType.Error);
                return false;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(
                card.DisplayName.ToUpperInvariant(),
                EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                card.QualityLabel.ToUpperInvariant(),
                EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(card.TypeLine);
            EditorGUILayout.Space(4f);

            DrawStat("Damage", card.DamageText);
            DrawStat("Shots/sec", card.ShotsPerSecondText);
            DrawStat("DPS", card.DpsText);
            if (card.ShowsPierce)
            {
                DrawStat("Pierce", card.PierceText);
            }
            if (card.ShowsProjectileCount)
            {
                DrawStat("Projectiles", card.ProjectileCountText);
            }

            if (card.AugmentCapacity > 0)
            {
                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField(
                    card.AugmentSymbols,
                    EditorStyles.boldLabel);
            }

            diagnosticsExpanded = EditorGUILayout.Foldout(
                diagnosticsExpanded,
                "Diagnostics / identity",
                true);
            if (diagnosticsExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(
                    "Item level",
                    card.ItemLevel.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "Equipment instance",
                    card.EquipmentInstanceId.ToString());
                EditorGUILayout.LabelField(
                    "Equipment definition",
                    card.EquipmentDefinitionId.ToString());
                EditorGUILayout.LabelField(
                    "Runtime weapon reference",
                    card.RuntimeWeaponReferenceId.ToString());
                EditorGUILayout.LabelField(
                    "Weapon definition",
                    card.WeaponDefinitionId);
                EditorGUILayout.LabelField(
                    "Raw quality ID",
                    card.QualityId.ToString());
                EditorGUILayout.LabelField(
                    "Definition augment capacity",
                    card.AugmentCapacity.ToString(
                        CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "Installed augments",
                    equipment.Augments.Count.ToString(
                        CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "Equipment fingerprint",
                    card.EquipmentFingerprint);
                EditorGUILayout.LabelField(
                    "Card fingerprint",
                    card.Fingerprint);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            return true;
        }

        private static void DrawStat(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110f));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }
    }
}
