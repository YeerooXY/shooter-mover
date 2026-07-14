using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.RamDroid
{
    /// <summary>
    /// Temporary package-owned warning. Readability comes from explicit text and a
    /// geometric scale pulse; color may support it but is never the sole signal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RamDroidTemporaryPresentation : MonoBehaviour
    {
        [SerializeField] private Transform warningRoot;
        [SerializeField] private TextMesh warningText;

        private RamDroidDefinition definition;
        private bool warningVisible;

        public bool IsWarningVisible
        {
            get { return warningVisible; }
        }

        public string WarningLabel
        {
            get { return warningText == null ? string.Empty : warningText.text; }
        }

        public bool UsesTextCue
        {
            get { return warningText != null && !string.IsNullOrWhiteSpace(warningText.text); }
        }

        public bool UsesShapePulse
        {
            get { return warningRoot != null; }
        }

        public bool UsesColorOnly
        {
            get { return false; }
        }

        public void Configure(RamDroidDefinition packageDefinition)
        {
            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
            }

            packageDefinition.ValidateOrThrow();
            if (warningRoot == null || warningText == null)
            {
                throw new InvalidOperationException(
                    "Ram Droid temporary presentation requires its package-owned warning objects.");
            }

            definition = packageDefinition;
            warningText.text = packageDefinition.WarningLabel;
            EnsureTemporaryFont();
            UpdateWarning(false, 0d);
        }

        public void UpdateWarning(bool visible, double simulationTimeSeconds)
        {
            if (double.IsNaN(simulationTimeSeconds)
                || double.IsInfinity(simulationTimeSeconds)
                || simulationTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTimeSeconds));
            }

            warningVisible = visible;
            if (warningRoot == null)
            {
                return;
            }

            warningRoot.gameObject.SetActive(visible);
            if (!visible || definition == null)
            {
                warningRoot.localScale = Vector2.one;
                return;
            }

            double phase = simulationTimeSeconds
                * definition.WarningPulseFrequency
                * Math.PI
                * 2d;
            float pulse = 1f
                + ((float)Math.Sin(phase) * definition.WarningPulseAmplitude);
            warningRoot.localScale = Vector2.one * pulse;
        }

        private void EnsureTemporaryFont()
        {
            if (warningText.font != null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                return;
            }

            warningText.font = font;
            MeshRenderer renderer = warningText.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = font.material;
            }
        }

        private void OnDisable()
        {
            warningVisible = false;
            if (warningRoot != null)
            {
                warningRoot.gameObject.SetActive(false);
            }
        }
    }
}
