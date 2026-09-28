using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsChamber
{
    public sealed class ChamberRun : MonoBehaviour
    {
        public Rigidbody actor;
        public Rigidbody payload;
        public GameObject roadblock;
        public Renderer sensorVisual;
        public Material unlockedMaterial;
        public bool Unlocked { get; private set; }
        public bool Complete { get; private set; }
        public float Elapsed => Complete ? finalTime : Time.timeSinceLevelLoad - startedAt;
        private float startedAt;
        private float finalTime;
        private Vector3 actorStart;
        private Vector3 payloadStart;
        private Material lockedMaterial;

        private void Start()
        {
            actorStart = actor.position;
            payloadStart = payload.position;
            lockedMaterial = sensorVisual.sharedMaterial;
            startedAt = 0f;
        }

        public void Unlock(Rigidbody source)
        {
            if (source != actor || Unlocked || Complete) return;
            roadblock.SetActive(false);
            Unlocked = true;
            sensorVisual.sharedMaterial = unlockedMaterial;
            Debug.Log("Roadblock removed: actor activated the sensor.", this);
        }

        public bool TryDeposit(Rigidbody source)
        {
            if (!Unlocked || Complete || source != payload) return false;
            finalTime = Time.timeSinceLevelLoad - startedAt;
            Complete = true;
            Debug.Log($"Deposit success! Elapsed time from Play/restart: {finalTime:F2} seconds.", this);
            return true;
        }

        public void Restart()
        {
            ResetBody(actor, actorStart);
            ResetBody(payload, payloadStart);
            roadblock.SetActive(true);
            sensorVisual.sharedMaterial = lockedMaterial;
            Unlocked = false;
            Complete = false;
            startedAt = Time.timeSinceLevelLoad;
            Physics.SyncTransforms();
        }

        private static void ResetBody(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
        }

        private void OnGUI()
        {
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var status = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUI.Label(new Rect(18, 12, Screen.width - 36, 32), "TRANSFER / 01", heading);
            GUI.Label(new Rect(18, 48, Screen.width - 36, 28),
                Complete ? $"DEPOSIT ACCEPTED   {Elapsed:F2} s" : $"{(Unlocked ? "GATE OPEN" : "GATE LOCKED")}   {Elapsed:F2} s", status);
            if (GUI.Button(new Rect(Screen.width - 98, 14, 80, 30), "Restart")) Restart();
        }
    }
}
