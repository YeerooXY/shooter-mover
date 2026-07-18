using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.LevelSelection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.PlaySelection
{
    public sealed class UnityPlaySelectionRouteAdapterV1 :
        IPlaySelectionRouteAdapterV1
    {
        public const string HubScenePath =
            "Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity";
        public const string LevelSelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity";

        public void Present(
            PlaySelectionRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null || !payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable Play route payload is required.",
                    nameof(payload));
            }

            if (route == PlaySelectionRouteV1.LevelSelection)
            {
                LevelSelectionRouteContextV1.CaptureEntry(
                    payload,
                    StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));
                SceneManager.LoadScene(
                    LevelSelectionScenePath,
                    LoadSceneMode.Single);
                return;
            }

            if (route == PlaySelectionRouteV1.Hub)
            {
                HubReturnRouteContextV1.Capture(payload);
                SceneManager.LoadScene(HubScenePath, LoadSceneMode.Single);
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    [DefaultExecutionOrder(11000)]
    [DisallowMultipleComponent]
    public sealed class PlaySelectionProductionInstallerV1 : MonoBehaviour
    {
        public bool Applied { get; private set; }
        public string RejectionCode { get; private set; }

        private void Awake()
        {
            PlayerRouteProfilePayloadV1 payload;
            if (!PlaySelectionEntryRouteContextV1.TryConsume(out payload))
            {
                RejectionCode = "play-selection-entry-context-missing";
                return;
            }

            PlaySelectionControllerV1 controller =
                GetComponent<PlaySelectionControllerV1>();
            if (controller == null)
            {
                RejectionCode = "play-selection-controller-missing";
                PlaySelectionEntryRouteContextV1.Capture(payload);
                return;
            }

            controller.Configure(
                payload,
                PlayModeCatalogDefinitionV1.CreateDefaultCatalog(),
                new UnityPlaySelectionRouteAdapterV1());
            Applied = true;
            RejectionCode = string.Empty;
        }
    }
}
