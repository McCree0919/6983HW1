using UnityEngine;

namespace PhysicsChamber
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChamberCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            var camera = GetComponent<Camera>();
            camera.orthographicSize = Mathf.Max(13f, 14f / camera.aspect);
        }
    }
}
