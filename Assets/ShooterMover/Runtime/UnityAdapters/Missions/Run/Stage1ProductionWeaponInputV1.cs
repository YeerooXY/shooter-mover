using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UnityAdapters.Missions.Run
{
    /// <summary>
    /// Production-only keyboard bridge for the four routed weapon slots. It submits
    /// commands to the run binding and keeps no independent selection or loadout state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage1ProductionWeaponInputV1 : MonoBehaviour
    {
        [SerializeField] private Stage1ProductionRunSceneAdapterV1 runAdapter;

        public Stage1ProductionRunSceneAdapterV1 RunAdapter
        {
            get { return runAdapter; }
        }

        public void Configure(Stage1ProductionRunSceneAdapterV1 adapter)
        {
            runAdapter = adapter;
        }

        private void Awake()
        {
            if (runAdapter == null)
            {
                runAdapter = GetComponent<Stage1ProductionRunSceneAdapterV1>();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || runAdapter == null || !runAdapter.IsConfigured)
            {
                return;
            }

            int requestedSlot = ReadRequestedSlot(keyboard);
            if (requestedSlot >= 0)
            {
                runAdapter.SelectWeaponSlot(requestedSlot);
            }
        }

        internal static int ReadRequestedSlot(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame
                || keyboard.numpad1Key.wasPressedThisFrame)
            {
                return 0;
            }

            if (keyboard.digit2Key.wasPressedThisFrame
                || keyboard.numpad2Key.wasPressedThisFrame)
            {
                return 1;
            }

            if (keyboard.digit3Key.wasPressedThisFrame
                || keyboard.numpad3Key.wasPressedThisFrame)
            {
                return 2;
            }

            if (keyboard.digit4Key.wasPressedThisFrame
                || keyboard.numpad4Key.wasPressedThisFrame)
            {
                return 3;
            }

            return -1;
        }
    }
}
