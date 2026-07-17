using System;
using System.Collections.Generic;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Skills
{
    /// <summary>
    /// Functional overlay for the passive skills_demo_screen backplate. Every displayed
    /// value and every hit target comes from code and the injected XP/SKILL authorities.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class SkillsSceneController : MonoBehaviour, ISkillsScreenPresenterV1
    {
        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;
        private const int ColumnCount = 4;
        private const float CardHeight = 154f;
        private const float CardGap = 10f;

        [Header("Passive presentation backplate")]
        [SerializeField] private TextAsset skillsBackplateAsset;
        [Header("Standalone authoring preview")]
        [SerializeField] private bool enableStandalonePreview = true;
        [SerializeField, Range(1, 100)] private int previewPlayerLevel = 20;
        [SerializeField] private string backSceneName = "MainMenu";

        private SkillsScreenSessionV1 session;
        private ISkillsScreenNavigationPortV1 navigationPort;
        private SkillsScreenProjectionV1 projection;
        private SkillsScreenAllocationResultV1 lastAllocation;
        private Texture2D backplateTexture;
        private Vector2 scrollPosition;
        private bool visible;
        private bool backDispatched;
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle statusStyle;

        public bool IsVisible { get { return visible; } }
        public SkillsScreenProjectionV1 CurrentProjection { get { return projection; } }
        public SkillsScreenAllocationResultV1 LastAllocation { get { return lastAllocation; } }
        public bool HasBackplateAsset { get { return skillsBackplateAsset != null; } }

        private void Awake()
        {
            EnsureBackplateTexture();
            if (enableStandalonePreview && session == null)
            {
                Show(CreateStandalonePreviewSession(), new SceneSkillsNavigationPortV1(backSceneName));
            }
        }

        private void Update()
        {
            if (!visible)
            {
                return;
            }

            bool keyboardBack = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            bool gamepadBack = Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardBack || gamepadBack)
            {
                Back();
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureBackplateTexture();
            EnsureStyles();
            projection = session.CurrentProjection;

            int previousDepth = GUI.depth;
            GUI.depth = -900;
            Rect canvas = DrawBackplate();
            DrawHeader(canvas);
            DrawSkills(canvas);
            GUI.depth = previousDepth;
        }

        private void OnDestroy()
        {
            if (backplateTexture != null)
            {
                Destroy(backplateTexture);
                backplateTexture = null;
            }
        }

        public void Show(
            SkillsScreenSessionV1 presentedSession,
            ISkillsScreenNavigationPortV1 presentedNavigationPort)
        {
            session = presentedSession ?? throw new ArgumentNullException(nameof(presentedSession));
            navigationPort = presentedNavigationPort
                ?? throw new ArgumentNullException(nameof(presentedNavigationPort));
            projection = session.CurrentProjection;
            lastAllocation = null;
            backDispatched = false;
            visible = true;
            enabled = true;
        }

        public void Hide()
        {
            visible = false;
        }

        public void ConfigureForTests(
            SkillsScreenSessionV1 configuredSession,
            ISkillsScreenNavigationPortV1 configuredNavigationPort)
        {
            enableStandalonePreview = false;
            Show(configuredSession, configuredNavigationPort);
        }

        public void ConfigureBackplateForTests(TextAsset asset)
        {
            skillsBackplateAsset = asset;
            if (backplateTexture != null)
            {
                Destroy(backplateTexture);
                backplateTexture = null;
            }
        }

        public SkillsScreenAllocationResultV1 AllocateSkill(
            string skillId,
            string operationId)
        {
            EnsureSession();
            lastAllocation = session.Allocate(operationId, skillId);
            projection = lastAllocation.Projection;
            return lastAllocation;
        }

        public bool Back()
        {
            if (backDispatched)
            {
                return false;
            }

            EnsureSession();
            SkillsScreenBackResultV1 result = session.Back();
            backDispatched = true;
            visible = false;
            navigationPort.ReturnToHub(result.RoutePayload);
            return true;
        }

        private void EnsureSession()
        {
            if (session == null || navigationPort == null)
            {
                throw new InvalidOperationException(
                    "The Skills screen requires injected XP, SKILL, route, and navigation composition.");
            }
        }

        private Rect DrawBackplate()
        {
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            if (scale <= 0f)
            {
                scale = 1f;
            }

            Rect canvas = new Rect(
                (Screen.width - (DesignWidth * scale)) * 0.5f,
                (Screen.height - (DesignHeight * scale)) * 0.5f,
                DesignWidth * scale,
                DesignHeight * scale);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            if (backplateTexture != null)
            {
                GUI.DrawTexture(canvas, backplateTexture, ScaleMode.StretchToFill, true);
            }
            else
            {
                GUI.Box(canvas, "SKILLS BACKPLATE NOT BOUND");
            }

            return canvas;
        }

        private void DrawHeader(Rect canvas)
        {
            if (GUI.Button(ScaleRect(canvas, new Rect(22f, 18f, 126f, 48f)), "BACK"))
            {
                Back();
            }

            GUI.Label(ScaleRect(canvas, new Rect(164f, 15f, 535f, 48f)), "SKILLS", titleStyle);
            string totals = projection == null
                ? "Disconnected"
                : "LEVEL " + projection.PlayerLevel
                    + "    POINTS " + projection.AvailableSkillPoints
                    + " / " + projection.TotalSkillPoints
                    + "    SPENT " + projection.SpentSkillPoints;
            GUI.Label(ScaleRect(canvas, new Rect(710f, 18f, 548f, 38f)), totals, headerStyle);
            GUI.Label(
                ScaleRect(canvas, new Rect(164f, 57f, 1094f, 34f)),
                lastAllocation == null
                    ? "Select an available skill to allocate one real skill point."
                    : FormatStatus(lastAllocation),
                statusStyle);
        }

        private void DrawSkills(Rect canvas)
        {
            if (projection == null)
            {
                GUI.Label(
                    ScaleRect(canvas, new Rect(180f, 180f, 920f, 100f)),
                    "No XP/SKILL authority composition is installed.",
                    titleStyle);
                return;
            }

            Rect viewport = ScaleRect(canvas, new Rect(24f, 104f, 1232f, 588f));
            float scale = canvas.width / DesignWidth;
            float cardWidth = ((viewport.width / scale) - ((ColumnCount - 1) * CardGap) - 18f)
                / ColumnCount;
            int rowCount = (projection.Skills.Count + ColumnCount - 1) / ColumnCount;
            Rect content = new Rect(
                0f,
                0f,
                viewport.width - 18f,
                rowCount * (CardHeight + CardGap) * scale);

            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, content);
            for (int index = 0; index < projection.Skills.Count; index++)
            {
                int column = index % ColumnCount;
                int row = index / ColumnCount;
                DrawSkillCard(
                    new Rect(
                        column * (cardWidth + CardGap) * scale,
                        row * (CardHeight + CardGap) * scale,
                        cardWidth * scale,
                        CardHeight * scale),
                    projection.Skills[index]);
            }
            GUI.EndScrollView();
        }

        private void DrawSkillCard(Rect card, SkillsScreenSkillProjectionV1 skill)
        {
            GUI.Box(card, GUIContent.none);
            float inset = Mathf.Max(6f, card.width * 0.035f);
            float line = Mathf.Max(16f, card.height * 0.135f);
            float width = card.width - (inset * 2f);

            GUI.Label(new Rect(card.x + inset, card.y + inset, width, line), skill.DisplayName, headerStyle);
            GUI.Label(new Rect(card.x + inset, card.y + inset + line, width, line), skill.SkillId, smallStyle);
            GUI.Label(
                new Rect(card.x + inset, card.y + inset + (line * 2f), width, line),
                "RANK " + skill.CurrentRank + " / " + skill.MaximumRank
                    + "    " + skill.State.ToString().ToUpperInvariant(),
                bodyStyle);
            GUI.Label(
                new Rect(card.x + inset, card.y + inset + (line * 3f), width, line),
                "REQ: " + skill.PrerequisiteLabel,
                smallStyle);
            GUI.Label(
                new Rect(card.x + inset, card.y + inset + (line * 4f), width, line * 2f),
                skill.Description,
                smallStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = skill.CanAllocate;
            string buttonLabel = skill.CanAllocate
                ? "ALLOCATE"
                : BlockLabel(skill.AllocationBlockCode);
            if (GUI.Button(
                new Rect(card.x + inset, card.yMax - line - inset, width, line),
                buttonLabel))
            {
                AllocateSkill(skill.SkillId, CreateOperationId(skill.SkillId));
            }
            GUI.enabled = previousEnabled;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = LabelStyle(30, FontStyle.Bold);
            headerStyle = LabelStyle(16, FontStyle.Bold);
            bodyStyle = LabelStyle(13, FontStyle.Normal);
            smallStyle = LabelStyle(11, FontStyle.Normal);
            statusStyle = LabelStyle(13, FontStyle.Bold);
        }

        private static GUIStyle LabelStyle(int size, FontStyle fontStyle)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = true,
            };
        }

        private void EnsureBackplateTexture()
        {
            if (backplateTexture != null || skillsBackplateAsset == null)
            {
                return;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(skillsBackplateAsset.text.Trim());
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "SKILLUI-001 skills_demo_screen backplate",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (!texture.LoadImage(bytes, true))
                {
                    Destroy(texture);
                    return;
                }

                backplateTexture = texture;
            }
            catch (FormatException)
            {
                backplateTexture = null;
            }
        }

        private SkillsScreenSessionV1 CreateStandalonePreviewSession()
        {
            var curve = new PlayerExperienceCurveV1(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
            var experience = new PlayerExperienceAuthorityV1(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.skills-preview"),
                    1,
                    new List<StableId>()));
            if (previewPlayerLevel > 1)
            {
                experience.Grant(new PlayerExperienceGrantRequestV1(
                    StableId.Parse("xp-source.skills-preview-bootstrap"),
                    checked((previewPlayerLevel - 1L) * 100L)));
            }

            var skills = new SkillProgressionAuthorityV1(
                SkillCatalogV1.CreateDefault(),
                experience.CurrentState.Level);
            PlayerRouteProfilePayloadV1 payload = PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.skills-preview"),
                StableId.Parse("loadout-profile.skills-preview"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.skills-preview-1"),
                    StableId.Parse("equipment-instance.skills-preview-2"),
                    StableId.Parse("equipment-instance.skills-preview-3"),
                    StableId.Parse("equipment-instance.skills-preview-4"),
                });
            return new SkillsScreenSessionV1(payload, experience, skills);
        }

        private string CreateOperationId(string skillId)
        {
            string fingerprint = projection == null
                ? string.Empty
                : projection.RoutePayload.Fingerprint;
            string routeToken = fingerprint.Length <= 16
                ? fingerprint
                : fingerprint.Substring(0, 16);
            return "skills-ui."
                + (routeToken.Length == 0 ? "unbound" : routeToken)
                + "."
                + (skillId ?? "unknown")
                + "."
                + Guid.NewGuid().ToString("N");
        }

        private static string FormatStatus(SkillsScreenAllocationResultV1 result)
        {
            SkillMutationFactV1 fact = result.MutationFact;
            switch (fact.Status)
            {
                case SkillMutationStatusV1.Applied:
                    return fact.SkillId + " increased to rank " + fact.CurrentRank + ".";
                case SkillMutationStatusV1.DuplicateNoChange:
                    return "Duplicate operation ignored; no additional point was spent.";
                case SkillMutationStatusV1.InsufficientPoints:
                    return "Insufficient skill points.";
                case SkillMutationStatusV1.PrerequisiteMissing:
                    return "Missing prerequisite for " + fact.SkillId + ".";
                case SkillMutationStatusV1.RankCapped:
                    return fact.SkillId + " is already at maximum rank.";
                case SkillMutationStatusV1.UnknownSkill:
                    return "Unknown skill identity.";
                case SkillMutationStatusV1.InvalidRequest:
                    return "Invalid allocation request.";
                default:
                    return fact.Status.ToString();
            }
        }

        private static string BlockLabel(string code)
        {
            switch (code)
            {
                case "skill-prerequisite-missing": return "LOCKED";
                case "skill-rank-capped": return "CAPPED";
                case "skill-points-insufficient": return "NO POINTS";
                default: return "UNAVAILABLE";
            }
        }

        private static Rect ScaleRect(Rect canvas, Rect source)
        {
            float scaleX = canvas.width / DesignWidth;
            float scaleY = canvas.height / DesignHeight;
            return new Rect(
                canvas.x + (source.x * scaleX),
                canvas.y + (source.y * scaleY),
                source.width * scaleX,
                source.height * scaleY);
        }

        private sealed class SceneSkillsNavigationPortV1 : ISkillsScreenNavigationPortV1
        {
            private readonly string sceneName;

            public SceneSkillsNavigationPortV1(string sceneName)
            {
                this.sceneName = sceneName ?? string.Empty;
            }

            public void ReturnToHub(PlayerRouteProfilePayloadV1 routePayload)
            {
                if (routePayload == null)
                {
                    throw new ArgumentNullException(nameof(routePayload));
                }

                if (!string.IsNullOrWhiteSpace(sceneName))
                {
                    SceneManager.LoadScene(sceneName);
                }
            }
        }
    }
}
