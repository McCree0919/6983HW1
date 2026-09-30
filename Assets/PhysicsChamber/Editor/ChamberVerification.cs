using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PhysicsChamber.Editor
{
    [InitializeOnLoad]
    public static class ChamberVerification
    {
        private const string Pending = "PhysicsChamber.VerificationPending";

        static ChamberVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                new GameObject("Verification runner").AddComponent<ChamberVerificationRunner>();
            };
        }

        // Intended for an isolated batch project, never replaces an unsaved user scene.
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only.");
            // Let Editor startup (including its Search index) finish before Play.
            EditorApplication.delayCall += () => EditorApplication.delayCall += BeginBatch;
        }

        private static void BeginBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PhysicsChamber.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
    }

    public sealed class ChamberVerificationRunner : MonoBehaviour
    {
        private Keyboard keyboard;
        private int unlockLogs;
        private int successLogs;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private bool previousRunInBackground;

        private IEnumerator Start()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Verification input is restricted to an isolated batch run.");
            previousRunInBackground = Application.runInBackground;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Chamber verification keyboard");
            keyboard.MakeCurrent();
            Application.logMessageReceived += CountRunLogs;
            yield return null;
            var checks = new Stack<IEnumerator>();
            checks.Push(Check());
            while (checks.Count > 0)
            {
                object next = null;
                try
                {
                    if (!checks.Peek().MoveNext())
                    {
                        checks.Pop();
                        continue;
                    }
                    next = checks.Peek().Current;
                    if (next is IEnumerator nested)
                    {
                        checks.Push(nested);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    CleanUp();
                    EditorApplication.Exit(1);
                    yield break;
                }
                yield return next;
            }
            CleanUp();
            Debug.Log("CHAMBER VERIFICATION PASSED");
            EditorApplication.Exit(0);
        }

        private void CountRunLogs(string message, string stack, LogType type)
        {
            if (message.StartsWith("Roadblock removed:", StringComparison.Ordinal)) unlockLogs++;
            if (message.StartsWith("Deposit success!", StringComparison.Ordinal)) successLogs++;
        }

        private void CleanUp()
        {
            Application.logMessageReceived -= CountRunLogs;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            Application.runInBackground = previousRunInBackground;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }

        private void OnDestroy() => CleanUp();

        private void Keys(params Key[] keys)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private IEnumerator Hold(float seconds, params Key[] keys)
        {
            Keys(keys);
            yield return new WaitForSeconds(seconds);
            Keys();
            yield return new WaitForSeconds(.15f);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("FAILED: " + message);
            Debug.Log("PASS: " + message);
        }

        private static void Place(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Physics.SyncTransforms();
        }

        private static void Describe(string phase, ChamberRun run)
        {
            var sensor = run.sensorVisual.GetComponent<Collider>();
            var floor = run.transform.Find("Floor").GetComponent<Collider>();
            Debug.Log($"DIAGNOSTIC {phase}: root={run.transform.position:F3}; " +
                $"actor body={run.actor.position:F3} transform={run.actor.transform.position:F3} " +
                $"bounds={run.actor.GetComponent<Collider>().bounds}; velocity={run.actor.linearVelocity:F3}; " +
                $"payload body={run.payload.position:F3} transform={run.payload.transform.position:F3} " +
                $"bounds={run.payload.GetComponent<Collider>().bounds}; " +
                $"sensor={sensor.bounds} trigger={sensor.isTrigger}; floor={floor.bounds}; " +
                $"unlocked={run.Unlocked} complete={run.Complete}");
        }

        private IEnumerator Check()
        {
            var run = FindAnyObjectByType<ChamberRun>();
            Require(run != null, "main scene creates a run");
            Require(!Physics.GetIgnoreLayerCollision(0, 0), "Default-layer physics collisions are enabled");
            Describe("first frame", run);
            yield return new WaitForFixedUpdate();
            Describe("first physics step", run);
            Require(run != null && !run.Unlocked && !run.Complete, "fresh run starts locked");
            yield return new WaitForSeconds(.75f);
            var floor = run.transform.Find("Floor").GetComponent<Collider>();
            RequireSupported(run.actor, floor, "Actor");
            RequireSupported(run.payload, floor, "Payload");
            Require(Mathf.Abs(run.transform.InverseTransformPoint(run.actor.position).z + 7f) < .1f &&
                Mathf.Abs(run.transform.InverseTransformPoint(run.payload.position).z + 4f) < .1f,
                "spawn settles in the intended start lane without drifting");
            Require(run.payload.mass != run.actor.mass && run.payload.linearDamping != run.actor.linearDamping &&
                run.payload.GetComponent<Collider>().sharedMaterial.dynamicFriction !=
                run.actor.GetComponent<Collider>().sharedMaterial.dynamicFriction,
                "actor and payload have distinct mass, damping and friction");

            foreach (Key key in new[] { Key.W, Key.A, Key.S, Key.D })
            {
                run.Restart();
                yield return new WaitForSeconds(.1f);
                Vector3 before = run.actor.position;
                yield return Hold(.3f, key);
                Vector3 direction = key == Key.W ? Vector3.forward : key == Key.S ? Vector3.back :
                    key == Key.A ? Vector3.left : Vector3.right;
                Require(Vector3.Dot(run.actor.position - before, direction) > .15f,
                    key + " keyboard input drives the actor through its controller (delta=" +
                    (run.actor.position - before).ToString("F3") + ")");
            }
            run.Restart();
            yield return new WaitForFixedUpdate();

            Require(!run.TryDeposit(run.payload), "early deposit rejected by state guard");
            var zone = FindAnyObjectByType<DepositZone>().GetComponent<BoxCollider>();
            Vector3 deposit = new Vector3(zone.bounds.center.x, floor.bounds.max.y + .65f, zone.bounds.center.z);
            Vector3 sensor = new Vector3(run.sensorVisual.bounds.center.x, floor.bounds.max.y + 1f,
                run.sensorVisual.bounds.center.z);
            Place(run.payload, deposit);
            yield return new WaitForSeconds(.15f);
            Require(!run.Complete, "real DepositZone overlap before unlock is rejected");
            int logsBefore = unlockLogs;
            Place(run.actor, sensor);
            Describe("actor placed at sensor", run);
            yield return new WaitForSeconds(.2f);
            Describe("after actor sensor physics", run);
            Require(run.Unlocked && !run.roadblock.activeSelf, "actor sensor removes physical roadblock");
            Require(unlockLogs == logsBefore + 1, "unlock emits exactly one roadblock-removed log");
            run.Unlock(run.actor);
            Require(unlockLogs == logsBefore + 1, "repeat sensor activation does not duplicate unlock log");
            Require(!run.Complete, "payload preloaded while locked cannot complete merely by unlocking");
            Require(!run.TryDeposit(run.actor), "actor is not accepted as payload");
            Place(run.payload, deposit + Vector3.left * 3f);
            yield return new WaitForSeconds(.15f);
            Place(run.payload, deposit);
            logsBefore = successLogs;
            yield return new WaitForSeconds(.15f);
            Require(run.Complete && run.Elapsed > 0f, "payload exit and reentry after unlock allows deposit");
            Require(successLogs == logsBefore + 1, "successful deposit emits exactly one elapsed-time log");
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(.15f);
            Require(run.Elapsed == elapsed && !run.TryDeposit(run.payload) && successLogs == logsBefore + 1,
                "success is one-shot and timer freezes");

            run.Restart();
            Require(!run.Unlocked && !run.Complete && run.roadblock.activeSelf, "restart resets gate and success");
            Require(run.actor.linearVelocity == Vector3.zero && run.payload.linearVelocity == Vector3.zero,
                "restart clears momentum");
            Require(run.Elapsed < .1f, "restart resets the attempt timer");
            Place(run.payload, new Vector3(sensor.x, floor.bounds.max.y + .65f, sensor.z));
            yield return new WaitForSeconds(.15f);
            Require(!run.Unlocked, "payload cannot activate actor sensor");

            // A locked gate must stop a real Rigidbody driven by keyboard input.
            run.Restart();
            Bounds gate = run.roadblock.GetComponent<Collider>().bounds;
            Place(run.actor, new Vector3(gate.min.x - 1.5f, floor.bounds.max.y + 1f, gate.center.z));
            yield return Hold(.8f, Key.D);
            Require(run.actor.GetComponent<Collider>().bounds.max.x <= gate.min.x + .08f && !run.Unlocked,
                "locked roadblock physically prevents crossing the first divider");

            run.Restart();
            Place(run.actor, sensor);
            yield return new WaitForSeconds(.15f);
            Place(run.payload, deposit + Vector3.right * (zone.bounds.extents.x));
            yield return new WaitForSeconds(.15f);
            Require(!run.Complete, "partial crate overlap is rejected");
            Place(run.payload, deposit);
            yield return new WaitForSeconds(.15f);
            Require(run.Complete, "unlocked full crate footprint is accepted");

            // The crate is never teleported during this route. Only the actor is placed
            // behind it between pushes to isolate the feasibility of each maze turn.
            run.Restart();
            yield return new WaitForSeconds(.1f); // Let the previous sensor overlap exit after reset.
            Place(run.actor, sensor);
            yield return new WaitForSeconds(.15f);
            Require(run.Unlocked && !run.Complete, "new physical route starts with sensor unlocked");
            if (run.payload.position.x < run.transform.position.x - 6.65f)
                yield return PushTo(run, Key.D, run.transform.position.x - 6.5f, "clear the sensor alcove");
            yield return PushTo(run, Key.W, run.transform.position.z + 7.7f, "north opening");
            yield return PushTo(run, Key.D, run.transform.position.x, "cross the removed gate");
            yield return PushTo(run, Key.S, run.transform.position.z - 7.7f, "middle corridor");
            yield return PushTo(run, Key.D, deposit.x, "south opening");
            yield return PushTo(run, Key.W, deposit.z, "final delivery");
            yield return new WaitForSeconds(.2f);
            Require(run.Complete && run.Elapsed > 5f,
                "keyboard-driven actor physically pushes the payload around both maze dividers into DepositZone");
            RequireSupported(run.actor, floor, "Actor after delivery");
            RequireSupported(run.payload, floor, "Payload after delivery");

            run.Restart();
            Keys();
            yield return new WaitForSeconds(.25f);
            RequireSupported(run.actor, floor, "Actor after restart");
            RequireSupported(run.payload, floor, "Payload after restart");
            Capture(1280, 800, "chamber-desktop.png");
            Capture(480, 800, "chamber-portrait.png");
        }

        private static void RequireSupported(Rigidbody body, Collider floor, string label)
        {
            Bounds bounds = body.GetComponent<Collider>().bounds;
            Require(Mathf.Abs(bounds.min.y - floor.bounds.max.y) < .08f &&
                bounds.min.x > floor.bounds.min.x && bounds.max.x < floor.bounds.max.x &&
                bounds.min.z > floor.bounds.min.z && bounds.max.z < floor.bounds.max.z,
                label + " rests on the chamber floor at its intended height");
            Require(Vector3.Distance(body.position, body.transform.position) < .08f,
                label + " transform and physics positions agree");
        }

        private IEnumerator PushTo(ChamberRun run, Key key, float destination, string label)
        {
            bool horizontal = key == Key.A || key == Key.D;
            Vector3 direction = key == Key.W ? Vector3.forward : key == Key.S ? Vector3.back :
                key == Key.A ? Vector3.left : Vector3.right;
            float sign = key == Key.S || key == Key.A ? -1f : 1f;
            Vector3 actorPosition = run.payload.position - direction * 1.35f;
            actorPosition.y += .35f;
            Place(run.actor, actorPosition);
            float deadline = Time.time + 16f;
            float remaining;
            do
            {
                float position = horizontal ? run.payload.position.x : run.payload.position.z;
                remaining = (destination - position) * sign;
                float speed = Vector3.Dot(run.payload.linearVelocity, direction);
                // Reduce speed near each turn so contact-driven motion does not overshoot.
                float targetSpeed = Mathf.Clamp(remaining * 1.8f, .25f, 2.2f);
                Keys(remaining > .12f && speed < targetSpeed ? new[] { key } : Array.Empty<Key>());
                yield return new WaitForFixedUpdate();
            }
            while (remaining > .12f && Time.time < deadline && !run.Complete);
            Keys();
            yield return new WaitForSeconds(.5f);
            float finalPosition = horizontal ? run.payload.position.x : run.payload.position.z;
            Require(Mathf.Abs(finalPosition - destination) < .5f || run.Complete,
                $"physical payload route: {label} (position={run.payload.position:F2})");
        }

        private static void Capture(int width, int height, string filename)
        {
            var camera = Camera.main;
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.orthographicSize = Mathf.Max(13f, 14f / camera.aspect);
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels32();
            int varied = 0;
            foreach (var pixel in pixels)
                if (Math.Abs(pixel.r - pixels[0].r) + Math.Abs(pixel.g - pixels[0].g) + Math.Abs(pixel.b - pixels[0].b) > 40) varied++;
            Require(varied > pixels.Length / 10, filename + " has nonblank rendered geometry");
            string directory = Path.GetFullPath("../ChamberVerificationResults");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Destroy(target);
            Destroy(texture);
        }
    }
}
