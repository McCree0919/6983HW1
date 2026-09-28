using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            EditorSceneManager.OpenScene("Assets/Scenes/PhysicsChamber.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
    }

    public sealed class ChamberVerificationRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            var checks = Check();
            while (true)
            {
                object next;
                try
                {
                    if (!checks.MoveNext()) break;
                    next = checks.Current;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorApplication.Exit(1);
                    yield break;
                }
                yield return next;
            }
            Debug.Log("CHAMBER VERIFICATION PASSED");
            EditorApplication.Exit(0);
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
            Physics.SyncTransforms();
        }

        private IEnumerator Check()
        {
            var run = FindAnyObjectByType<ChamberRun>();
            yield return new WaitForFixedUpdate();
            Require(run != null && !run.Unlocked && !run.Complete, "fresh run starts locked");
            Require(!run.TryDeposit(run.payload), "early deposit rejected by state guard");
            Place(run.payload, new Vector3(8, .65f, 7));
            yield return new WaitForSeconds(.15f);
            Require(!run.Complete, "real DepositZone overlap before unlock is rejected");
            run.Restart();
            Place(run.payload, new Vector3(-9, .65f, 4));
            yield return new WaitForSeconds(.15f);
            Require(!run.Unlocked, "payload cannot activate actor sensor");
            run.Restart();
            Place(run.actor, new Vector3(-9, 1, 4));
            yield return new WaitForSeconds(.15f);
            Require(run.Unlocked && !run.roadblock.activeSelf, "actor sensor removes physical roadblock");
            Require(!run.TryDeposit(run.actor), "actor is not accepted as payload");
            Place(run.payload, new Vector3(9.7f, .65f, 7));
            yield return new WaitForSeconds(.15f);
            Require(!run.Complete, "partial crate overlap is rejected");
            Place(run.payload, new Vector3(8, .65f, 7));
            yield return new WaitForSeconds(.15f);
            Require(run.Complete && run.Elapsed > 0f, "unlocked full crate deposit completes with elapsed time");
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(.15f);
            Require(run.Elapsed == elapsed && !run.TryDeposit(run.payload), "success is one-shot and timer freezes");
            run.Restart();
            Require(!run.Unlocked && !run.Complete && run.roadblock.activeSelf, "restart resets gate and success");
            Require(run.actor.linearVelocity == Vector3.zero && run.payload.linearVelocity == Vector3.zero,
                "restart clears momentum");

            // Exercise contact forces without moving the payload transform.
            run.actor.GetComponent<ActorController>().enabled = false;
            Vector3 initial = run.payload.position;
            for (int i = 0; i < 80; i++)
            {
                yield return new WaitForFixedUpdate();
                run.actor.AddForce(Vector3.forward * 38f, ForceMode.Force);
            }
            Require(run.payload.position.z > initial.z + 1f, "actor contact physically pushes heavier payload");
            run.Restart();
            run.actor.GetComponent<ActorController>().enabled = true;
            yield return null;
            Capture(1280, 800, "chamber-desktop.png");
            Capture(480, 800, "chamber-portrait.png");
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
