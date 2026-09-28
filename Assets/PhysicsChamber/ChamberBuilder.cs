using System.Collections.Generic;
using UnityEngine;

namespace PhysicsChamber
{
    public sealed class ChamberBuilder : MonoBehaviour
    {
        private readonly List<Object> resources = new List<Object>();
        private Material floor, wall, blue, amber, green, dark;

        private void Awake() => Build();

        private Material Paint(string label, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = label;
            material.color = color;
            resources.Add(material);
            return material;
        }

        private PhysicsMaterial Surface(string label, float friction)
        {
            var material = new PhysicsMaterial(label) {
                dynamicFriction = friction, staticFriction = friction,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f
            };
            resources.Add(material);
            return material;
        }

        private GameObject Block(string label, Vector3 position, Vector3 scale, Material material,
            bool collision = true, PrimitiveType shape = PrimitiveType.Cube)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = label;
            item.transform.SetParent(transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) item.GetComponent<Collider>().enabled = false;
            return item;
        }

        private Rigidbody Body(GameObject item, float mass, float damping, PhysicsMaterial surface)
        {
            item.GetComponent<Collider>().sharedMaterial = surface;
            var body = item.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = damping;
            body.angularDamping = 3f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return body;
        }

        private void Build()
        {
            floor = Paint("Porcelain", new Color(.68f, .73f, .75f));
            wall = Paint("Graphite", new Color(.22f, .26f, .27f));
            blue = Paint("Actor cyan", new Color(.03f, .65f, .86f));
            amber = Paint("Payload gold", new Color(1f, .63f, .08f));
            green = Paint("Deposit mint", new Color(.13f, .8f, .42f));
            dark = Paint("Gate coral", new Color(.91f, .19f, .24f));
            var run = gameObject.AddComponent<ChamberRun>();
            var ground = Block("Floor", new Vector3(0, -.25f, 0), new Vector3(24, .5f, 20), floor);
            ground.GetComponent<Collider>().sharedMaterial = Surface("Floor friction 0.45", .45f);
            Block("West boundary", new Vector3(-12, 1, 0), new Vector3(.5f, 2, 20), wall);
            Block("East boundary", new Vector3(12, 1, 0), new Vector3(.5f, 2, 20), wall);
            Block("North boundary", new Vector3(0, 1, 10), new Vector3(24, 2, .5f), wall);
            Block("South boundary", new Vector3(0, 1, -10), new Vector3(24, 2, .5f), wall);
            // Alternating openings form an S route. The only first crossing is the gate.
            Block("First divider", new Vector3(-4, 1, -2), new Vector3(.5f, 2, 16), wall);
            Block("Second divider", new Vector3(4, 1, 2), new Vector3(.5f, 2, 16), wall);
            Block("Sensor alcove", new Vector3(-10, 1, 1), new Vector3(3.5f, 2, .5f), wall);
            run.roadblock = Block("Roadblock", new Vector3(-4, 1, 8), new Vector3(.5f, 2, 4), dark);
            run.actor = Body(Block("Actor", new Vector3(-8, 1, -7), new Vector3(1, 1, 1), blue,
                true, PrimitiveType.Capsule), 2f, 1.4f, Surface("Actor friction 0.05", .05f));
            run.actor.gameObject.AddComponent<ActorController>();
            run.payload = Body(Block("Payload", new Vector3(-8, .65f, -4), new Vector3(1.3f, 1.3f, 1.3f), amber),
                3.5f, .65f, Surface("Payload friction 0.18", .18f));
            for (int i = -1; i <= 1; i += 2)
            {
                var band = Block("Crate band", Vector3.zero, Vector3.one, wall, false);
                band.transform.SetParent(run.payload.transform, false);
                band.transform.localPosition = new Vector3(i * .28f, 0, 0);
                band.transform.localScale = new Vector3(.08f, 1.02f, 1.02f);
            }
            var sensor = Block("Unlock sensor", new Vector3(-9, .12f, 4), new Vector3(2.4f, .24f, 2.4f), dark);
            sensor.GetComponent<BoxCollider>().isTrigger = true;
            sensor.AddComponent<ChamberSensor>().run = run;
            run.sensorVisual = sensor.GetComponent<Renderer>();
            run.unlockedMaterial = green;
            var deposit = Block("DepositZone", new Vector3(8, .12f, 7), new Vector3(3.4f, .24f, 3.4f), green);
            deposit.GetComponent<BoxCollider>().isTrigger = true;
            deposit.AddComponent<DepositZone>().run = run;
            // Trigger volumes extend above the thin visible pads.
            sensor.GetComponent<BoxCollider>().size = new Vector3(1, 12, 1);
            deposit.GetComponent<BoxCollider>().size = new Vector3(1, 12, 1);
            for (int x = -11; x <= 11; x += 2)
                Block("Floor joint", new Vector3(x, .006f, 0), new Vector3(.025f, .008f, 19.5f), wall, false);
            for (int z = -9; z <= 9; z += 2)
                Block("Floor joint", new Vector3(0, .006f, z), new Vector3(23.5f, .008f, .025f), wall, false);

            var cameraObject = new GameObject("Chamber Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 30, -13);
            cameraObject.transform.LookAt(Vector3.zero);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 15.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.085f, .105f, .11f);
            cameraObject.AddComponent<ChamberCamera>();
            var lightObject = new GameObject("Chamber Light", typeof(Light));
            lightObject.transform.SetParent(transform);
            lightObject.transform.rotation = Quaternion.Euler(55, -25, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        private void OnDestroy()
        {
            foreach (var resource in resources) if (resource != null) Destroy(resource);
        }
    }
}
