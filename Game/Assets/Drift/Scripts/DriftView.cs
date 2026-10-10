using UnityEngine;
using UnityEngine.InputSystem;

namespace Drift
{
    // An invisible test viewpoint, not a selectable or rendered player character.
    public sealed class DriftView : MonoBehaviour
    {
        private CharacterController body;
        private DriftSettings settings;
        private float pitch, verticalSpeed;
        private float speedMultiplier = 1;
        private readonly Collider[] headroomHits = new Collider[16];
        public Camera Camera { get; private set; }
        public void Initialize(Transform ship, DriftSettings configuration)
        {
            settings = configuration; transform.SetParent(ship, false);
            Vector3 vesselScale = ship.lossyScale;
            transform.localScale = new Vector3(1f / vesselScale.x, 1f / vesselScale.y, 1f / vesselScale.z);
            body = gameObject.AddComponent<CharacterController>();
            body.height = 1.8f; body.center = Vector3.up * .9f; body.radius = .3f; body.stepOffset = .25f;
            Camera = new GameObject("Test View Camera").AddComponent<Camera>();
            Camera.transform.SetParent(transform, false); Camera.transform.localPosition = Vector3.up * 1.6f;
            Camera.nearClipPlane = .06f; Camera.farClipPlane = 1000; Camera.fieldOfView = 75;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.30f, .45f, .49f);
            Camera.gameObject.AddComponent<AudioListener>(); Camera.tag = "MainCamera";
            ResetView();
        }
        public void ResetView()
        {
            body.enabled = false; transform.localPosition = new Vector3(0, 1.1f, -8);
            body.height = 1.8f; body.center = Vector3.up * .9f;
            transform.localRotation = Quaternion.Euler(0, 180, 0); pitch = 0; verticalSpeed = 0;
            Camera.transform.localRotation = Quaternion.identity; Camera.transform.localPosition = Vector3.up * 1.6f; body.enabled = true;
        }
        public void MoveTo(Vector3 shipLocalPosition)
        {
            body.enabled = false;
            transform.localPosition = shipLocalPosition;
            verticalSpeed = 0;
            body.enabled = true;
        }
        public void SetCharacterSpeed(int speed) => speedMultiplier = Mathf.Clamp(speed / 10f, .5f, 2f);
        public void Preview()
        {
            body.enabled = false;
            transform.localPosition = new Vector3(-12, 8, -19);
            transform.LookAt(transform.parent.TransformPoint(new Vector3(0, 1.5f, 2)));
            Camera.transform.localPosition = Vector3.zero; Camera.transform.localRotation = Quaternion.identity;
        }
        public void Tick(float dt, bool helm, DriftWorld world)
        {
            if (Mouse.current != null)
            {
                Vector2 mouse = Mouse.current.delta.ReadValue() * .085f;
                transform.Rotate(0, mouse.x, 0);
                pitch = Mathf.Clamp(pitch - mouse.y, -80, 80);
                Camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            if (helm) return;
            if (world.IsInWater(transform.position))
            {
                body.height = 1.45f; body.center = Vector3.up * .725f;
                Camera.transform.localPosition = Vector3.up * 1.25f;
                Vector2 swimInput = new Vector2((settings.Held(Control.Right) ? 1 : 0) - (settings.Held(Control.Left) ? 1 : 0),
                    (settings.Held(Control.Forward) ? 1 : 0) - (settings.Held(Control.Back) ? 1 : 0));
                swimInput = Vector2.ClampMagnitude(swimInput, 1);
                float verticalInput = (settings.Held(Control.Jump) ? 1 : 0) - (settings.Held(Control.Crouch) ? 1 : 0);
                Vector3 swimDirection = transform.right * swimInput.x + transform.forward * swimInput.y;
                float swimSpeed = (settings.Held(Control.Sprint) ? 3.8f : 2.6f) * speedMultiplier;
                float floatHeight = world.WaterSurfaceY - .78f;
                float buoyancy = Mathf.Clamp((floatHeight - transform.position.y) * 3.5f - verticalSpeed * 2.2f, -2.2f, 2.2f);
                float targetVerticalSpeed = Mathf.Approximately(verticalInput, 0) ? buoyancy : verticalInput * 2.4f;
                verticalSpeed = Mathf.MoveTowards(verticalSpeed, targetVerticalSpeed, 8f * dt);
                body.Move((swimDirection * swimSpeed + Vector3.up * verticalSpeed) * dt);
                if (transform.position.y < world.WaterSurfaceY - 30f) ResetView();
                return;
            }
            bool crouch = settings.Held(Control.Crouch);
            float height = crouch ? 1.15f : 1.8f;
            if (height > body.height)
            {
                // Check only the extra headroom, rather than the capsule already on the floor.
                Vector3 head = transform.position + Vector3.up * 1.52f;
                int hits = Physics.OverlapSphereNonAlloc(head, .26f, headroomHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++) if (headroomHits[i] != body) { height = body.height; break; }
            }
            body.height = height; body.center = Vector3.up * (height * .5f);
            Camera.transform.localPosition = Vector3.up * (height - .2f);
            Vector2 input = new Vector2((settings.Held(Control.Right) ? 1 : 0) - (settings.Held(Control.Left) ? 1 : 0), (settings.Held(Control.Forward) ? 1 : 0) - (settings.Held(Control.Back) ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (body.isGrounded && settings.Pressed(Control.Jump) && !crouch) verticalSpeed = 5.7f;
            verticalSpeed -= 18 * dt;
            float speed = (crouch ? 1.8f : settings.Held(Control.Sprint) ? 5.4f : 3.2f) * speedMultiplier;
            body.Move(((transform.right * input.x + transform.forward * input.y) * speed + Vector3.up * verticalSpeed) * dt);
            if (transform.position.y < world.WaterSurfaceY - 30f) ResetView();
        }
        public DriftInteractable Target()
        {
            if (Physics.Raycast(Camera.transform.position, Camera.transform.forward, out var hit, 3.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponent<DriftInteractable>();
            return null;
        }
    }
}
