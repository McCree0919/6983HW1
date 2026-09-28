using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsChamber
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ActorController : MonoBehaviour
    {
        public float driveForce = 38f;
        public float maximumSpeed = 4.5f;
        private Rigidbody body;
        private Vector2 movement;

        private void Awake() => body = GetComponent<Rigidbody>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            movement = keyboard == null ? Vector2.zero : new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            movement = Vector2.ClampMagnitude(movement, 1f);
        }

        private void FixedUpdate()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 desired = new Vector3(movement.x, 0f, movement.y) * maximumSpeed;
            Vector3 correction = (desired - new Vector3(velocity.x, 0f, velocity.z)) * 12f;
            body.AddForce(Vector3.ClampMagnitude(correction, driveForce), ForceMode.Force);
        }

        private void OnDisable() => movement = Vector2.zero;
    }
}
