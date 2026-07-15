using System.Collections.Generic;
using ShooterMover.Domain.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    /// <summary>Temporary Stage 1 four-slot HUD and bounded procedural cue player.</summary>
    [DisallowMultipleComponent]
    public sealed class Stage1WeaponStatusStrip : MonoBehaviour
    {
        [SerializeField] private bool autoLoadRepresentativeFixture = true;
        [SerializeField] private bool reducedEffects;
        [SerializeField] private bool playTemporaryAudio = true;
        [SerializeField] private bool logFixtureProof = true;

        private Stage1WeaponPresentationProjector projector;
        private Stage1WeaponPresentationFrame current;
        private Stage1WeaponPresentationFrame previous;
        private Stage1WeaponTemporaryAudio temporaryAudio;
        private GUIStyle title;
        private GUIStyle text;
        private GUIStyle critical;

        public Stage1WeaponPresentationFrame CurrentFrame { get { return current; } }
        public bool ReducedEffects { get { return reducedEffects; } }

        private void Awake()
        {
            projector = new Stage1WeaponPresentationProjector();
            temporaryAudio = new Stage1WeaponTemporaryAudio(transform);
            if (autoLoadRepresentativeFixture) LoadRepresentativeFixture();
        }

        public void SetReducedEffects(bool value)
        {
            reducedEffects = value;
        }

        public void SetTemporaryAudioEnabled(bool value)
        {
            playTemporaryAudio = value;
            if (!value && temporaryAudio != null) temporaryAudio.Stop();
        }

        public void Present(FourMountStatusSnapshot snapshot)
        {
            PresentWithUnavailableCues(snapshot, null);
        }

        public void PresentWithUnavailableCues(
            FourMountStatusSnapshot snapshot,
            IEnumerable<string> unavailableCueIds)
        {
            if (projector == null) projector = new Stage1WeaponPresentationProjector();
            current = projector.Project(
                snapshot,
                previous,
                Stage1WeaponPresentationOptions.Create(reducedEffects, unavailableCueIds));
            previous = current;

            if (playTemporaryAudio && Application.isPlaying)
            {
                if (temporaryAudio == null)
                    temporaryAudio = new Stage1WeaponTemporaryAudio(transform);
                temporaryAudio.Play(current.BuildCuePlan());
            }
        }

        [ContextMenu("WP-010/Load Four-Slot Fixture")]
        public void LoadRepresentativeFixture()
        {
            Present(Stage1WeaponPresentationFixture.CreateRepresentativeSnapshot());
            Log("four-slot-fixture");
        }

        [ContextMenu("WP-010/Load Empowered Spend Fixture")]
        public void LoadEmpoweredSpendFixture()
        {
            previous = null;
            Present(Stage1WeaponPresentationFixture.CreateBeforeSpendSnapshot());
            Present(Stage1WeaponPresentationFixture.CreateRepresentativeSnapshot());
            Log("empowered-spend-fixture");
        }

        [ContextMenu("WP-010/Load Reduced-Effects Fixture")]
        public void LoadReducedEffectsFixture()
        {
            reducedEffects = true;
            previous = null;
            Present(Stage1WeaponPresentationFixture.CreateRepresentativeSnapshot());
            Log("reduced-effects-fixture");
        }

        [ContextMenu("WP-010/Load Ricochet Identity Fixture")]
        public void LoadRicochetIdentityFixture()
        {
            reducedEffects = false;
            previous = null;
            Present(Stage1WeaponPresentationFixture.CreateRicochetIdentitySnapshot());
            Log("ricochet-identity-fixture");
        }

        public string BuildDebugText()
        {
            return current == null ? "wp010=no-snapshot" : current.ToTraceString();
        }

        public string BuildPriorityCapture()
        {
            return current == null
                ? "wp010=no-cue-plan"
                : current.BuildCuePlan().ToTraceString();
        }

        private void OnGUI()
        {
            if (current == null) return;
            EnsureStyles();

            const float margin = 14f;
            const float gap = 6f;
            const float height = 170f;
            float width = Mathf.Max(760f, Screen.width - margin * 2f);
            float slotWidth = (width - gap * 3f) / 4f;
            float startX = (Screen.width - width) * 0.5f;
            float y = Screen.height - height - margin;
            for (int index = 0; index < current.Count; index++)
            {
                DrawSlot(
                    new Rect(startX + index * (slotWidth + gap), y, slotWidth, height),
                    current.GetByStableIndex(index));
            }
        }

        private void DrawSlot(Rect rect, Stage1WeaponSlotPresentation slot)
        {
            GUI.Box(rect, GUIContent.none);
            Rect line = new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 20f);
            Color previousColor = GUI.color;
            GUI.color = slot.Accent;
            GUI.Label(line, "S" + slot.StableSlotNumber + " [" + slot.Glyph + "] " + slot.Label, title);
            GUI.color = previousColor;

            line.y += 23f;
            GUI.Label(line, slot.Pattern + " / " + slot.State, critical);
            line.y += 20f;
            GUI.Label(line, slot.StateDetail, text);
            line.y += 20f;
            GUI.Label(line, slot.Mode, critical);
            line.y += 21f;
            GUI.Label(line, slot.Power, text);

            if (slot.HasPower)
            {
                line.y += 19f;
                line.height = 8f;
                GUI.Box(line, GUIContent.none);
                Rect fill = new Rect(
                    line.x + 1f,
                    line.y + 1f,
                    (line.width - 2f) * Mathf.Clamp01((float)slot.PowerLevel),
                    6f);
                GUI.color = slot.Accent;
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = previousColor;
            }

            line.y += 14f;
            line.height = 20f;
            GUI.Label(line, First(slot.Fault, slot.ReferenceWarning, slot.PowerChange), critical);
            line.y += 22f;
            string effectText = reducedEffects
                ? "FX REDUCED " + slot.Glyph
                : "FX " + new string('*', Mathf.Max(0, slot.Pulses)) + " " + slot.Pattern;
            GUI.Label(line, effectText, title);
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            text = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                clipping = TextClipping.Clip,
            };
            critical = new GUIStyle(text) { fontStyle = FontStyle.Bold };
        }

        private void Log(string fixture)
        {
            if (logFixtureProof)
                Debug.Log("WP-010 " + fixture + "\n" + BuildDebugText() + "\n" + BuildPriorityCapture(), this);
        }

        private static string First(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value;
            return string.Empty;
        }

        private void OnDestroy()
        {
            if (temporaryAudio != null) temporaryAudio.Dispose();
        }
    }
}
