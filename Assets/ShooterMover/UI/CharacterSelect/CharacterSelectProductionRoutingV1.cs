using System;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Contracts.Flow.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.CharacterSelect
{
    public sealed class UnityCharacterSelectionRouteSinkV1 :
        ICharacterSelectionRouteSinkV1
    {
        public const string MainMenuScenePath =
            "Assets/ShooterMover/Scenes/Menu/MainMenu.unity";
        public const string HubScenePath =
            "Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity";

        public void Accept(CharacterSelectionRouteResultV1 result)
        {
            if (result == null || result.Payload == null
                || !result.Payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid character-selection route result is required.",
                    nameof(result));
            }

            if (result.Status == CharacterSelectionRouteStatusV1.Confirmed)
            {
                HubReturnRouteContextV1.Capture(result.Payload);
                SceneManager.LoadScene(HubScenePath, LoadSceneMode.Single);
                return;
            }

            CharacterSelectionEntryRouteContextV1.Capture(result.Payload);
            SceneManager.LoadScene(MainMenuScenePath, LoadSceneMode.Single);
        }
    }

    [DefaultExecutionOrder(11000)]
    [DisallowMultipleComponent]
    public sealed class CharacterSelectProductionInstallerV1 : MonoBehaviour
    {
        public bool Applied { get; private set; }
        public string RejectionCode { get; private set; }

        private void Awake()
        {
            PlayerRouteProfilePayloadV1 payload;
            if (!CharacterSelectionEntryRouteContextV1.TryConsume(out payload))
            {
                RejectionCode = "character-selection-entry-context-missing";
                return;
            }

            CharacterSelectControllerV1 controller =
                GetComponent<CharacterSelectControllerV1>();
            if (controller == null)
            {
                RejectionCode = "character-selection-controller-missing";
                CharacterSelectionEntryRouteContextV1.Capture(payload);
                return;
            }

            controller.ConfigureForTests(
                payload,
                BuiltInCharacterSelectionCatalogV1.Create(),
                new UnityCharacterSelectionRouteSinkV1());
            Applied = true;
            RejectionCode = string.Empty;
        }
    }
}
