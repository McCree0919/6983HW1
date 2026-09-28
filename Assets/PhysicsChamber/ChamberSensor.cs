using UnityEngine;

namespace PhysicsChamber
{
    public sealed class ChamberSensor : MonoBehaviour
    {
        public ChamberRun run;
        private void OnTriggerEnter(Collider other) => run.Unlock(other.attachedRigidbody);
    }
}
