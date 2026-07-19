using System;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Progression.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Skills
{
    /// <summary>
    /// Artwork-backed Skills presentation. Production may show a disconnected state
    /// without constructing preview XP/SKILL authorities; connected sessions still use
    /// the existing SkillsScreenSessionV1.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class SkillsSceneController :
        MonoBehaviour,
        ISkillsScreenPresenterV1
    {
        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;
        private const int ColumnCount = 4;
        private const float CardHeight = 154f;
        private const float CardGap = 10f;

        [SerializeField] private TextAsset skillsBackplateAsset;
        [SerializeField] private bool enableStandalonePreview;
        [SerializeField, Range(1, 100)] private int previewPlayerLevel = 20;
        [SerializeField] private string backSceneName = "MainMenu";

        private SkillsScreenSessionV1 session;
        private ISkillsScreenNavigationPortV1 navigationPort;
        private PlayerRouteProfilePayloadV1 disconnectedPayload;
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
        public bool IsDisconnected { get { return visible && session == null; } }

        private void Awake()
        {
            EnsureBackplateTexture();
            // Standalone preview is intentionally not composed here. Production flow
            // must inject real authorities or explicitly request disconnected mode.
        }

        private void Update()
        {
            if (!visible) return;
            bool keyboardBack = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.backspaceKey.wasPressedThisFrame);
            bool gamepadBack = Gamepad.current != null
                && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (keyboardBack || gamepadBack) Back();
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureBackplateTexture();
            EnsureStyles();
            if (session != null) projection = session.CurrentProjection;

            int previousDepth = GUI.depth;
            GUI.depth = -900;
            Rect canvas = DrawBackplate();
            DrawHeader(canvas);
            if (session == null) DrawDisconnected(canvas);
            else DrawSkills(canvas);
            GUI.depth = previousDepth;
        }

        public void Show(
            SkillsScreenSessionV1 presentedSession,
            ISkillsScreenNavigationPortV1 presentedNavigationPort)
        {
            session = presentedSession
                ?? throw new ArgumentNullException(nameof(presentedSession));
            navigationPort = presentedNavigationPort
                ?? throw new ArgumentNullException(nameof(presentedNavigationPort));
            disconnectedPayload = null;
            projection = session.CurrentProjection;
            lastAllocation = null;
            backDispatched = false;
            visible = true;
            enabled = true;
        }

        public void ShowDisconnected(
            PlayerRouteProfilePayloadV1 routePayload,
            ISkillsScreenNavigationPortV1 presentedNavigationPort)
        {
            disconnectedPayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The Skills route payload is invalid.",
                    nameof(routePayload));
            }

            navigationPort = presentedNavigationPort
                ?? throw new ArgumentNullException(nameof(presentedNavigationPort));
            session = null;
            projection = null;
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
            EnsureConnectedSession();
            lastAllocation = session.Allocate(operationId, skillId);
            projection = lastAllocation.Projection;
            return lastAllocation;
        }

        public bool Back()
        {
            if (backDispatched || navigationPort == null) return false;
            PlayerRouteProfilePayloadV1 payload;
            if (session == null)
            {
                payload = disconnectedPayload;
            }
            else
            {
                SkillsScreenBackResultV1 result = session.Back();
                payload = result.RoutePayload;
            }

            backDispatched = true;
            visible = false;
            navigationPort.ReturnToHub(payload);
            return true;
        }

        private void EnsureConnectedSession()
        {
            if (session == null)
            {
                throw new InvalidOperationException(
                    "The Skills screen requires injected XP and SKILL authorities.");
            }
        }

        private Rect DrawBackplate()
        {
            float scale = Mathf.Min(
                Screen.width / DesignWidth,
                Screen.height / DesignHeight);
            if (scale <= 0f) scale = 1f;
            Rect canvas = new Rect(
                (Screen.width - DesignWidth * scale) * 0.5f,
                (Screen.height - DesignHeight * scale) * 0.5f,
                DesignWidth * scale,
                DesignHeight * scale);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            if (backplateTexture != null)
            {
                GUI.DrawTexture(
                    canvas,
                    backplateTexture,
                    ScaleMode.StretchToFill,
                    true);
            }
            return canvas;
        }

        private void DrawHeader(Rect canvas)
        {
            if (GUI.Button(
                ScaleRect(canvas, new Rect(22f, 18f, 126f, 48f)),
                "BACK"))
            {
                Back();
            }

            GUI.Label(
                ScaleRect(canvas, new Rect(164f, 15f, 535f, 48f)),
                "SKILLS",
                titleStyle);
            string totals = projection == null
                ? "AUTHORITY COMPOSITION REQUIRED"
                : "LEVEL " + projection.PlayerLevel
                    + "    POINTS "
                    + projection.AvailableSkillPoints
                    + " / " + projection.TotalSkillPoints
                    + "    SPENT "
                    + projection.SpentSkillPoints;
            GUI.Label(
                ScaleRect(canvas, new Rect(710f, 18f, 548f, 38f)),
                totals,
                headerStyle);
            GUI.Label(
                ScaleRect(canvas, new Rect(164f, 57f, 1094f, 34f)),
                session == null
                    ? "No preview rank or skill authority was created."
                    : lastAllocation == null
                        ? "Select an available skill to allocate one real skill point."
                        : FormatStatus(lastAllocation),
                statusStyle);
        }

        private void DrawDisconnected(Rect canvas)
        {
            GUI.Label(
                ScaleRect(canvas, new Rect(170f, 190f, 940f, 150f)),
                "The real Skills screen is active, but no XP/SKILL authority "
                + "composition has been supplied. No preview ranks or skill points "
                + "were created.",
                statusStyle);
            GUI.Label(
                ScaleRect(canvas, new Rect(170f, 355f, 940f, 70f)),
                disconnectedPayload == null
                    ? string.Empty
                    : disconnectedPayload.SelectedCharacterStableId
                        + " / "
                        + disconnectedPayload.LoadoutProfileStableId,
                smallStyle);
        }

        private void DrawSkills(Rect canvas)
        {
            if (projection == null)
            {
                return;
            }

            Rect viewport = ScaleRect(
                canvas,
                new Rect(24f, 104f, 1232f, 588f));
            float scale = canvas.width / DesignWidth;
            float cardWidth = ((viewport.width / scale)
                - ((ColumnCount - 1) * CardGap) - 18f)
                / ColumnCount;
            int rowCount =
                (projection.Skills.Count + ColumnCount - 1)
                / ColumnCount;
            Rect content = new Rect(
                0f,
                0f,
                viewport.width - 18f,
                rowCount * (CardHeight + CardGap) * scale);

            scrollPosition = GUI.BeginScrollView(
                viewport,
                scrollPosition,
                content);
            for (int index = 0;
                index < projection.Skills.Count;
                index++)
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

        private void DrawSkillCard(
            Rect card,
            SkillsScreenSkillProjectionV1 skill)
        {
            GUI.Box(card, GUIContent.none);
            float inset = Mathf.Max(
                6f,
                card.width * 0.035f);
            float line = Mathf.Max(
                16f,
                card.height * 0.135f);
            float width = card.width - inset * 2f;

            GUI.Label(
                new Rect(
                    card.x + inset,
                    card.y + inset,
                    width,
                    line),
                skill.DisplayName,
                headerStyle);
            GUI.Label(
                new Rect(
                    card.x + inset,
                    card.y + inset + line,
                    width,
                    line),
                skill.SkillId,
                smallStyle);
            GUI.Label(
                new Rect(
                    card.x + inset,
                    card.y + inset + line * 2f,
                    width,
                    line),
                "RANK " + skill.CurrentRank
                + " / " + skill.MaximumRank
                + "    "
                + skill.State.ToString().ToUpperInvariant(),
                bodyStyle);
            GUI.Label(
                new Rect(
                    card.x + inset,
                    card.y + inset + line * 3f,
                    width,
                    line),
                "REQ: " + skill.PrerequisiteLabel,
                smallStyle);
            GUI.Label(
                new Rect(
                    card.x + inset,
                    card.y + inset + line * 4f,
                    width,
                    line * 2f),
                skill.Description,
                smallStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = skill.CanAllocate;
            string buttonLabel = skill.CanAllocate
                ? "ALLOCATE"
                : BlockLabel(skill.AllocationBlockCode);
            if (GUI.Button(
                new Rect(
                    card.x + inset,
                    card.yMax - line - inset,
                    width,
                    line),
                buttonLabel))
            {
                AllocateSkill(
                    skill.SkillId,
                    CreateOperationId(skill.SkillId));
            }
            GUI.enabled = previousEnabled;
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
                + (routeToken.Length == 0
                    ? "unbound"
                    : routeToken)
                + "."
                + (skillId ?? "unknown")
                + "."
                + Guid.NewGuid().ToString("N");
        }

        private static string FormatStatus(
            SkillsScreenAllocationResultV1 result)
        {
            SkillMutationFactV1 fact = result.MutationFact;
            switch (fact.Status)
            {
                case SkillMutationStatusV1.Applied:
                    return fact.SkillId
                        + " increased to rank "
                        + fact.CurrentRank
                        + ".";
                case SkillMutationStatusV1.DuplicateNoChange:
                    return "Duplicate operation ignored; no additional point was spent.";
                case SkillMutationStatusV1.InsufficientPoints:
                    return "Insufficient skill points.";
                case SkillMutationStatusV1.PrerequisiteMissing:
                    return "Missing prerequisite for "
                        + fact.SkillId
                        + ".";
                case SkillMutationStatusV1.RankCapped:
                    return fact.SkillId
                        + " is already at maximum rank.";
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
                case "skill-prerequisite-missing":
                    return "LOCKED";
                case "skill-rank-capped":
                    return "CAPPED";
                case "skill-points-insufficient":
                    return "NO POINTS";
                default:
                    return "UNAVAILABLE";
            }
        }

        private void EnsureBackplateTexture()
        {
            if (backplateTexture != null
                || skillsBackplateAsset == null
                || skillsBackplateAsset.bytes.Length == 0)
            {
                return;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(
                    skillsBackplateAsset.text.Trim());
            }
            catch (FormatException)
            {
                return;
            }

            Texture2D loaded = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);
            if (ImageConversion.LoadImage(loaded, bytes, false))
            {
                backplateTexture = loaded;
            }
            else
            {
                Destroy(loaded);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
            };
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = true,
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
        }

        private static Rect ScaleRect(Rect canvas, Rect design)
        {
            float scale = canvas.width / DesignWidth;
            return new Rect(
                canvas.x + design.x * scale,
                canvas.y + design.y * scale,
                design.width * scale,
                design.height * scale);
        }

        private void OnDestroy()
        {
            if (backplateTexture != null)
            {
                Destroy(backplateTexture);
                backplateTexture = null;
            }
        }
    }
}
