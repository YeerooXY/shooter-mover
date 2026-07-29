using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    [DisallowMultipleComponent]
    public sealed class PlayerAimFacing2D : MonoBehaviour
    {
        private const float SpriteForwardDegrees = 180f;

        private Rigidbody2D body;
        private Camera gameplayCamera;
        private float desiredRotation;
        private bool hasAim;

        public bool TryBind(Rigidbody2D configuredBody)
        {
            if (configuredBody == null)
            {
                return false;
            }
            if (body != null && body != configuredBody)
            {
                return false;
            }

            Camera resolved = ResolveGameplayCamera(gameObject.scene);
            if (resolved == null)
            {
                return false;
            }

            body = configuredBody;
            gameplayCamera = resolved;
            body.freezeRotation = false;
            body.angularVelocity = 0f;
            return true;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (body == null || gameplayCamera == null || mouse == null)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = gameplayCamera.ScreenToWorldPoint(
                new Vector3(
                    screen.x,
                    screen.y,
                    -gameplayCamera.transform.position.z));
            Vector2 direction = new Vector2(
                world.x - body.position.x,
                world.y - body.position.y);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            desiredRotation = Mathf.Atan2(direction.y, direction.x)
                * Mathf.Rad2Deg
                - SpriteForwardDegrees;
            hasAim = true;
        }

        private void FixedUpdate()
        {
            if (body != null && hasAim)
            {
                body.SetRotation(desiredRotation);
            }
        }

        private void OnDisable()
        {
            hasAim = false;
            if (body != null)
            {
                body.angularVelocity = 0f;
            }
        }

        private static Camera ResolveGameplayCamera(Scene scene)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Camera resolved = null;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate == null
                    || !candidate.enabled
                    || candidate.gameObject.scene != scene)
                {
                    continue;
                }
                if (resolved != null)
                {
                    return null;
                }
                resolved = candidate;
            }
            return resolved;
        }
    }
}
