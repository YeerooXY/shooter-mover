using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// DEMO-CUTOVER-001 composition adapter. The retained Stage 1 controller supplies
    /// scene-authored Unity presentation only; this component connects the accepted
    /// player, weapon, enemy, room, mission-result and flow authorities into one loop.
    /// </summary>
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private void ProjectCurrentRoom(bool movePlayer)
        {
            if (rooms == null || rooms.CurrentRoomStableId == null) return;
            bool entry = rooms.CurrentRoomStableId
                == Level1AuthorableRoomDefinitionV1.EntryRoomStableId;

            entryRoomRoot.SetActive(entry);
            terminalRoomRoot.SetActive(!entry);
            if (entry)
            {
                controller.MobileBlasterDroid.ActivateSession();
                controller.TurretPackage.Deactivate();
            }
            else
            {
                controller.MobileBlasterDroid.DeactivateSession();
                controller.TurretPackage.Activate();
            }

            if (movePlayer && controller.PlayerBody != null)
            {
                Vector2 spawn = rooms.GetCurrentSpawnPosition();
                controller.PlayerBody.position = spawn;
                controller.PlayerTransform.position = spawn;
                controller.PlayerBody.linearVelocity = Vector2.zero;
                controller.PlayerBody.angularVelocity = 0f;
            }

            if (entry)
            {
                MirrorDoor(
                    controller.EntryExitDoor,
                    IsDoorOpen(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId));
            }
            else
            {
                MirrorDoor(
                    controller.TerminalExitDoor,
                    IsDoorOpen(Level1AuthorableRoomDefinitionV1.FinalDoorStableId));
            }
        }

        private bool IsDoorOpen(StableId doorStableId)
        {
            RoomDoorInstance2D door;
            return rooms.TryGetSpawnedDoor(doorStableId, out door)
                && door != null
                && door.IsOpen;
        }

        private static void MirrorDoor(
            ShooterMover.ContentPackages.Environment.Doors.DoorController2D door,
            bool open)
        {
            if (door == null) return;
            if (open)
            {
                door.NotifyInteractionRequested();
            }
            else
            {
                door.Close();
            }
        }

        private void HandleRoomTraversal()
        {
            Vector3 position = controller.PlayerTransform.position;
            bool entry = rooms.CurrentRoomStableId
                == Level1AuthorableRoomDefinitionV1.EntryRoomStableId;
            if (entry)
            {
                if (Mathf.Abs(position.y) <= DoorLaneHalfWidth
                    && position.x >= DoorTraversalX
                    && IsDoorOpen(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId))
                {
                    RoomLiveOperationResultV1 result = rooms.Traverse(
                        TraversalOperation("forward"),
                        Level1AuthorableRoomDefinitionV1.ForwardExitStableId);
                    if (result.Status == RoomLiveOperationStatusV1.Applied)
                    {
                        ProjectCurrentRoom(true);
                    }
                }

                return;
            }

            if (position.x <= -DoorTraversalX
                && Mathf.Abs(position.y) <= DoorLaneHalfWidth
                && IsDoorOpen(Level1AuthorableRoomDefinitionV1.ReturnDoorStableId))
            {
                RoomLiveOperationResultV1 result = rooms.Traverse(
                    TraversalOperation("return"),
                    Level1AuthorableRoomDefinitionV1.ReturnExitStableId);
                if (result.Status == RoomLiveOperationStatusV1.Applied)
                {
                    ProjectCurrentRoom(true);
                }
                return;
            }

            if (position.x >= DoorTraversalX
                && Mathf.Abs(position.y) <= DoorLaneHalfWidth
                && IsDoorOpen(Level1AuthorableRoomDefinitionV1.FinalDoorStableId))
            {
                rooms.Traverse(
                    TraversalOperation("final"),
                    Level1AuthorableRoomDefinitionV1.FinalExitStableId);
            }
        }

        private StableId TraversalOperation(string kind)
        {
            return StableId.Create(
                "operation",
                "demo-cutover-" + kind + "-g"
                    + controller.RestartGeneration.ToString(CultureInfo.InvariantCulture)
                    + "-" + HashToken(
                        controller.PlayerTransform.position.ToString("R")));
        }

        private void CommitPendingExperienceRewards()
        {
            for (int index = 0; index < pendingEnemyRewards.Count; index++)
            {
                PendingEnemyReward pending = pendingEnemyRewards[index];
                EnemyExperienceRewardFactV1 reward =
                    enemyRewards.ProcessDestruction(
                        runStableId,
                        pending.EnemyDefinitionStableId,
                        1,
                        pending.Destruction);
                if (reward == null || !reward.Changed) continue;
                ParticipantRunStats stats;
                if (!participantStats.TryGetValue(
                        pending.ParticipantStableId,
                        out stats))
                {
                    stats = new ParticipantRunStats(
                        pending.ParticipantStableId);
                    participantStats.Add(
                        pending.ParticipantStableId,
                        stats);
                }
                stats.Experience += reward.ExperienceAmount;
            }
            pendingEnemyRewards.Clear();
        }

        private void HandleFinalExitReached()
        {
            if (ending) return;
            ending = true;
            effectEmitter.ClearEmittedEffects();
            CommitPendingExperienceRewards();

            var holdingsSnapshot = holdings.ExportSnapshot();
            EndMissionRunCommandV1 command = EndMissionRunCommandV1.Create(
                StableId.Create(
                    "operation",
                    "demo-cutover-end-g"
                        + controller.RestartGeneration.ToString(CultureInfo.InvariantCulture)),
                runStableId,
                profile.Payload,
                MissionRunCompletionStateV1.Completed,
                missionResults.Sequence,
                holdings.Sequence,
                holdingsSnapshot.Fingerprint,
                missionPort.OpeningSequence,
                missionPort.OpeningFingerprint);
            MissionRunAuthorityResultV1 result = missionResults.EndRun(command);
            if (result == null || !result.Succeeded || result.ResultPayload == null)
            {
                ending = false;
                diagnostic = result == null
                    ? "The mission-result authority returned no result."
                    : result.RejectionCode;
                return;
            }

            StableId participantId = controller.PlayerRunParticipantId;
            ParticipantRunStats stats;
            if (!participantStats.TryGetValue(participantId, out stats))
            {
                stats = new ParticipantRunStats(participantId);
            }

            pendingResults = new Stage1ReadOnlyResultsProjectionV1(
                result.ResultPayload,
                profile.DisplayName,
                DisplayClass(profile.Payload.LoadoutProfileStableId),
                experience.CurrentState.Level,
                participantId,
                stats.Kills,
                stats.Experience,
                0L,
                0L);
            if (!flow.Transitions.TryLoadSubflow(ProductionFlowScenePathsV1.Results))
            {
                ending = false;
                diagnostic = "ProductionFlowCoordinatorV1 rejected the Results route.";
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 430f, 190f), GUI.skin.box);
            GUILayout.Label("LEVEL 1 — PRODUCTION LOOP", titleStyle);
            if (!initialized)
            {
                GUILayout.Label(
                    string.IsNullOrEmpty(diagnostic)
                        ? "Connecting accepted runtime authorities..."
                        : diagnostic,
                    bodyStyle);
                GUILayout.EndArea();
                return;
            }

            string roomName = rooms.CurrentRoomStableId
                == Level1AuthorableRoomDefinitionV1.EntryRoomStableId
                    ? "Room 1: Moving Droid"
                    : "Room 2: Blaster Turret";
            string weaponName = WeaponDisplayNames[weapons.SelectedSlotIndex];
            GUILayout.Label(roomName, bodyStyle);
            GUILayout.Label(
                "HP " + controller.PlayerHealth
                + "   Weapon " + weaponName
                + "   Slot " + (weapons.SelectedSlotIndex + 1),
                smallStyle);
            GUILayout.Label(
                "1 Blaster   2 Shotgun   3 Rocket Launcher   4 Flamethrower",
                smallStyle);
            GUILayout.Label(
                controller.IsPlayerDead
                    ? "Destroyed — press R to restart through PlayerActorAuthority."
                    : "Move, aim with the mouse, hold left click/Space to fire. Press R to restart.",
                smallStyle);
            if (!string.IsNullOrEmpty(diagnostic))
            {
                GUILayout.Label(diagnostic, smallStyle);
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
            };
        }

        private void OnDestroy()
        {
            if (rooms != null)
            {
                rooms.FinalExitReached -= HandleFinalExitReached;
            }
            if (activeComposition == this)
            {
                activeComposition = null;
            }
            projectedRoomEnemies.Clear();
            for (int index = 0; index < runtimeAssets.Count; index++)
            {
                if (runtimeAssets[index] != null)
                {
                    Destroy(runtimeAssets[index]);
                }
            }
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T value = roots[index].GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static TextAsset ReadResultsBackground(
            ProductionResultsControllerV1 controller)
        {
            if (controller == null) return null;
            FieldInfo field = typeof(ProductionResultsControllerV1).GetField(
                "resultsBackgroundAsset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(controller) as TextAsset;
        }

        private static string DisplayClass(StableId loadoutProfileStableId)
        {
            string value = loadoutProfileStableId == null
                ? string.Empty
                : loadoutProfileStableId.ToString();
            int separator = value.LastIndexOf('.');
            string token = separator >= 0 ? value.Substring(separator + 1) : value;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                token.Replace('-', ' '));
        }

        private static string HashToken(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    builder.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        private Sprite RuntimeSprite(string key, Color color)
        {
            Sprite cached;
            if (projectileSprites.TryGetValue(key, out cached)
                && cached != null)
            {
                return cached;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "DEMO-CUTOVER-001 Projectile Pixel " + key;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            projectileSprites[key] = sprite;
            runtimeAssets.Add(texture);
            runtimeAssets.Add(sprite);
            return sprite;
        }
    }
}
