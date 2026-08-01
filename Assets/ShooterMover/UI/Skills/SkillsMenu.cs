using System;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Progression.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Skills
{
    /// <summary>
    /// Artwork-backed Skills presentation. Production uses ranked-skills V2; the original
    /// V1 session remains available only for compatibility fixtures and focused tests.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class SkillsMenu :
        MonoBehaviour,
        ISkillsScreenPresenter
    {
        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;
        private const int ColumnCount = 4;
        private const float CardHeight = 154f;
        private const float CardGap = 10f;

        [SerializeField] private TextAsset skillsBackplateAsset;
        [SerializeField] private bool enableStandalonePreview;

        private SkillsScreenSession session;
        private RankedSkillsScreenSession rankedSession;
        private ISkillsScreenNavigationPort navigationPort;
        private PlayerRouteProfilePayload disconnectedPayload;
        private SkillsScreenView projection;
        private SkillsScreenAllocationResult lastAllocation;
        private string unavailableReason = string.Empty;
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
        public SkillsScreenView CurrentProjection { get { return projection; } }
        public SkillsScreenAllocationResult LastAllocation { get { return lastAllocation; } }
        public bool HasBackplateAsset { get { return skillsBackplateAsset != null; } }
        public bool IsDisconnected { get { return visible && session == null && rankedSession == null; } }
        public bool IsRankedV2Connected { get { return visible && rankedSession != null; } }
        public string UnavailableReason { get { return unavailableReason; } }

        private void Awake()
        {
            EnsureBackplateTexture();
            // Standalone preview is intentionally not composed here. Production flow
            // must inject the selected character graph or explicitly present unavailable state.
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
            if (rankedSession != null) projection = rankedSession.CurrentProjection;
            else if (session != null) projection = session.CurrentProjection;

            int previousDepth = GUI.depth;
            GUI.depth = -900;
            Rect canvas = DrawBackplate();
            DrawHeader(canvas);
            if (session == null && rankedSession == null) DrawDisconnected(canvas);
            else DrawSkills(canvas);
            GUI.depth = previousDepth;
        }

        public void Show(
            SkillsScreenSession presentedSession,
            ISkillsScreenNavigationPort presentedNavigationPort)
        {
            session = presentedSession
                ?? throw new ArgumentNullException(nameof(presentedSession));
            rankedSession = null;
            navigationPort = presentedNavigationPort
                ?? throw new ArgumentNullException(nameof(presentedNavigationPort));
            disconnectedPayload = null;
            projection = session.CurrentProjection;
            lastAllocation = null;
            unavailableReason = string.Empty;
            backDispatched = false;
            visible = true;
            enabled = true;
        }

        public void ShowRanked(
            RankedSkillsScreenSession presentedSession,
            ISkillsScreenNavigationPort presentedNavigationPort)
        {
            rankedSession = presentedSession
                ?? throw new ArgumentNullException(nameof(presentedSession));
            session = null;
            navigationPort = presentedNavigationPort
                ?? throw new ArgumentNullException(nameof(presentedNavigationPort));
            disconnectedPayload = null;
            projection = rankedSession.CurrentProjection;
            lastAllocation = null;
            unavailableReason = string.Empty;
            backDispatched = false;
            visible = true;
            enabled = true;
        }

        public void ShowDisconnected(
            PlayerRouteProfilePayload routePayload,
            ISkillsScreenNavigationPort presentedNavigationPort)
        {
            ShowUnavailable(
                routePayload,
                presentedNavigationPort,
                "skills-v2-active-character-graph-unavailable");
        }

        public void ShowUnavailable(
            PlayerRouteProfilePayload routePayload,
            ISkillsScreenNavigationPort presentedNavigationPort,
            string rejectionCode)
        {
            disconnectedPayload = routePayload;
            if (routePayload != null && !routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The Skills route payload is invalid.",
                    nameof(routePayload));
            }
            navigationPort = presentedNavigationPort
                ?? throw new ArgumentNullException(nameof(presentedNavigationPort));
            session = null;
            rankedSession = null;
            projection = null;
            lastAllocation = null;
            unavailableReason = string.IsNullOrWhiteSpace(rejectionCode)
                ? "skills-v2-unavailable"
                : rejectionCode.Trim();
            backDispatched = false;
            visible = true;
            enabled = true;
        }

        public void Hide()
        {
            visible = false;
        }

        public void ConfigureForTests(
            SkillsScreenSession configuredSession,
            ISkillsScreenNavigationPort configuredNavigationPort)
        {
            Show(configuredSession, configuredNavigationPort);
        }

        public void ConfigureRankedV2ForTests(
            RankedSkillsScreenSession configuredSession,
            ISkillsScreenNavigationPort configuredNavigationPort)
        {
            ShowRanked(configuredSession, configuredNavigationPort);
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

        public SkillsScreenAllocationResult AllocateSkill(
            string skillId,
            string operationId)
        {
            if (session == null)
                throw new InvalidOperationException(
                    "The legacy Skills session is not connected.");
            lastAllocation = session.Allocate(operationId, skillId);
            projection = lastAllocation.Projection;
            return lastAllocation;
        }

        public SkillsScreenAllocationResult AllocateRankedSkill(string skillId)
        {
            if (rankedSession == null)
                throw new InvalidOperationException(
                    "The ranked Skills V2 session is not connected.");
            lastAllocation = rankedSession.Allocate(skillId);
            projection = lastAllocation.Projection;
            return lastAllocation;
        }

        public bool Back()
        {
            if (backDispatched || navigationPort == null) return false;
            PlayerRouteProfilePayload payload;
            if (rankedSession != null)
                payload = rankedSession.Back().RoutePayload;
            else if (session != null)
                payload = session.Back().RoutePayload;
            else
                payload = disconnectedPayload;

            backDispatched = true;
            visible = false;
            navigationPort.ReturnToHub(payload);
            return true;
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
                ? "SKILLS UNAVAILABLE"
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

            bool disconnected = session == null && rankedSession == null;
            string status = disconnected
                ? "No valid active selected-character graph: " + unavailableReason
                : lastAllocation == null
                    ? "Select a skill to request one authoritative rank allocation."
                    : FormatStatus(lastAllocation);
            GUI.Label(
                ScaleRect(canvas, new Rect(164f, 57f, 1094f, 34f)),
                status,
                statusStyle);
        }

        private void DrawDisconnected(Rect canvas)
        {
            GUI.Label(
                ScaleRect(canvas, new Rect(170f, 190f, 940f, 150f)),
                "Skills V2 requires the real active selected-character graph. "
                + "No preview profile, point budget, or rank state was created.",
                statusStyle);
            GUI.Label(
                ScaleRect(canvas, new Rect(170f, 355f, 940f, 90f)),
                (disconnectedPayload == null
                    ? string.Empty
                    : disconnectedPayload.SelectedCharacterStableId
                        + " / "
                        + disconnectedPayload.LoadoutProfileStableId)
                    + "\n"
                    + unavailableReason,
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
            SkillsScreenSkillView skill)
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
            // V2 keeps blocked cards clickable so the trusted session can return the exact
            // authoritative rejection. Legacy fixtures retain their original disabled UI.
            GUI.enabled = rankedSession != null || skill.CanAllocate;
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
                if (rankedSession != null)
                    AllocateRankedSkill(skill.SkillId);
                else
                    AllocateSkill(skill.SkillId, CreateOperationId(skill.SkillId));
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
            SkillsScreenAllocationResult result)
        {
            SkillMutationFact fact = result.MutationFact;
            switch (fact.Status)
            {
                case SkillMutationStatus.Applied:
                    return fact.SkillId
                        + " increased to rank "
                        + fact.CurrentRank
                        + ".";
                case SkillMutationStatus.DuplicateNoChange:
                    return "Duplicate operation ignored; no additional point was spent.";
                case SkillMutationStatus.InsufficientPoints:
                    return "Insufficient skill points.";
                case SkillMutationStatus.PrerequisiteMissing:
                    return "Missing prerequisite for "
                        + fact.SkillId
                        + ".";
                case SkillMutationStatus.CategoryInvestmentMissing:
                    return "Category investment requirement is not satisfied.";
                case SkillMutationStatus.RankCapped:
                    return fact.SkillId
                        + " is already at maximum rank.";
                case SkillMutationStatus.UnknownSkill:
                    return "Unknown skill identity.";
                case SkillMutationStatus.InvalidRequest:
                    return "Allocation rejected: "
                        + (string.IsNullOrEmpty(fact.RejectionCode)
                            ? "invalid request"
                            : fact.RejectionCode);
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
                case "skill-category-investment-missing":
                    return "GATED";
                case "skill-class-ineligible":
                    return "CLASS LOCKED";
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
