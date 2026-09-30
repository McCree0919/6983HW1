using UnityEngine;

namespace PhysicsChamber
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DepositZone : MonoBehaviour
    {
        public ChamberRun run;
        private BoxCollider zone;
        private bool enteredAfterUnlock;
        private int entryAttempt;

        private void Awake() => zone = GetComponent<BoxCollider>();

        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody != run.payload) return;
            entryAttempt = run.Attempt;
            enteredAfterUnlock = run.Unlocked;
            if (!enteredAfterUnlock)
                Debug.Log("Deposit rejected: roadblock is still locked.", this);
            CheckDeposit(other);
        }

        private void OnTriggerStay(Collider other) => CheckDeposit(other);

        private void OnTriggerExit(Collider other)
        {
            if (other.attachedRigidbody == run.payload) enteredAfterUnlock = false;
        }

        private void CheckDeposit(Collider other)
        {
            if (other.attachedRigidbody != run.payload || !enteredAfterUnlock ||
                entryAttempt != run.Attempt) return;
            Bounds destination = zone.bounds;
            Bounds crate = other.bounds;
            // Require the entire crate footprint, not just a grazing corner.
            if (crate.min.x >= destination.min.x && crate.max.x <= destination.max.x &&
                crate.min.z >= destination.min.z && crate.max.z <= destination.max.z)
                run.TryDeposit(other.attachedRigidbody);
        }
    }
}
