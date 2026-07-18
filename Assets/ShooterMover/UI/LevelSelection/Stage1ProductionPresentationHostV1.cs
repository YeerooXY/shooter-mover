using System;
using UnityEngine;

namespace ShooterMover.UI.LevelSelection
{
    /// <summary>
    /// Production-owned lifecycle boundary in front of the retained Stage 1 presentation.
    /// The retained implementation remains presentation-only and may run only after the
    /// production composition root has accepted the route and authority handoff.
    /// </summary>
    [DefaultExecutionOrder(-9500)]
    [DisallowMultipleComponent]
    public sealed class Stage1ProductionPresentationHostV1 : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour retainedPresentation;

        public MonoBehaviour RetainedPresentation
        {
            get { return retainedPresentation; }
        }

        public bool HasRetainedPresentation
        {
            get { return retainedPresentation != null; }
        }

        public bool IsPresentationEnabled
        {
            get
            {
                return retainedPresentation != null
                    && retainedPresentation.enabled;
            }
        }

        public void ConfigureForTests(MonoBehaviour presentation)
        {
            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (ReferenceEquals(presentation, this))
            {
                throw new ArgumentException(
                    "The production presentation host cannot retain itself.",
                    nameof(presentation));
            }

            if (retainedPresentation != null
                && !ReferenceEquals(retainedPresentation, presentation))
            {
                throw new InvalidOperationException(
                    "A different retained Stage 1 presentation is already configured.");
            }

            retainedPresentation = presentation;
        }

        public void SetPresentationEnabled(bool value)
        {
            if (retainedPresentation == null)
            {
                throw new InvalidOperationException(
                    "The retained Stage 1 presentation is not configured.");
            }

            retainedPresentation.enabled = value;
        }

        private void OnDisable()
        {
            if (retainedPresentation != null)
            {
                retainedPresentation.enabled = false;
            }
        }
    }
}
