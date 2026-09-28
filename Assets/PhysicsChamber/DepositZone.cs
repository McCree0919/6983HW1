using UnityEngine;

namespace PhysicsChamber
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DepositZone : MonoBehaviour
    {
        public ChamberRun run;
        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == run.payload && !run.Unlocked)
                Debug.Log("Deposit rejected: roadblock is still locked.", this);
            CheckDeposit(other);
        }

        private void OnTriggerStay(Collider other) => CheckDeposit(other);

        private void CheckDeposit(Collider other)
        {
            if (other.attachedRigidbody != run.payload) return;
            Bounds zone = GetComponent<BoxCollider>().bounds;
            Bounds crate = other.bounds;
            // Require the entire crate footprint, not just a grazing corner.
            if (crate.min.x >= zone.min.x && crate.max.x <= zone.max.x &&
                crate.min.z >= zone.min.z && crate.max.z <= zone.max.z)
                run.TryDeposit(other.attachedRigidbody);
        }
    }
}
