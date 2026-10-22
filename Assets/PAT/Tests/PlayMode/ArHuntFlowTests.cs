using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace Pat.ArLab.Tests
{
    public sealed class ArHuntFlowTests
    {
        private GameObject rig;
        private ArHuntController controller;
        private HuntSettings settings;
        private string savePath;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            rig = new GameObject("Test lab rig");
            rig.SetActive(false);
            settings = ScriptableObject.CreateInstance<HuntSettings>();
            settings.scanDuration = 1;
            settings.recognitionHold = 0.1f;
            var cameraObject = new GameObject("Test AR camera");
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<ARCameraManager>().enabled = false;
            cameraObject.AddComponent<ARCameraBackground>().enabled = false;
            cameraObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled = false;
            var origin = rig.AddComponent<XROrigin>();
            origin.Camera = camera;
            var images = rig.AddComponent<ARTrackedImageManager>();
            images.enabled = false;
            var anchors = rig.AddComponent<ARAnchorManager>();
            anchors.enabled = false;
            var session = rig.AddComponent<ARSession>();
            session.enabled = false;
            controller = rig.AddComponent<ArHuntController>();
            controller.settings = settings;
            controller.imageManager = images;
            controller.anchorManager = anchors;
            controller.arSession = session;
            controller.arCamera = camera;
            controller.saveFileName = "pat-playtest-" + System.Guid.NewGuid().ToString("N") + ".json";
            savePath = Path.Combine(Application.persistentDataPath, controller.saveFileName);
            rig.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(rig);
            Object.Destroy(settings);
            yield return null;
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
        }

        [UnityTest]
        public IEnumerator UnregisteredMarkerIsRejectedAndRegistrationUnlocksTheCorrectPlant()
        {
            Assert.That(rig.GetComponentInChildren<Canvas>(), Is.Not.Null);
            controller.SimulateMarker(4);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.Target, Is.Null);
            Assert.That(controller.Session.Phase, Is.EqualTo(HuntPhase.Searching));
            var plant = controller.Repository.Register("PAT-004", "Sen Đá Test", "Succulent");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.Target.id, Is.EqualTo(plant.id));
            Assert.That(controller.Session.Phase, Is.EqualTo(HuntPhase.Scanning));
        }

        [UnityTest]
        public IEnumerator TrackingLossAndGardenMenuPauseTheActualController()
        {
            controller.SimulateMarker(1);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.Target.name, Is.EqualTo("Bé Mập"));
            controller.SimulatedTrackingLost = true;
            var before = controller.Session.ScanSeconds;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.Session.ScanSeconds, Is.EqualTo(before));
            Assert.That(controller.Session.Paused, Is.True);
            controller.SimulatedTrackingLost = false;
            controller.GetComponent<LabHud>().OpenGarden();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.Session.ScanSeconds, Is.EqualTo(before));
            controller.GetComponent<LabHud>().CloseGarden();
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(controller.Session.Phase, Is.EqualTo(HuntPhase.ReadyToCatch));
            Assert.That(rig.GetComponentInChildren<PetActor>(), Is.Null, "Pet is attached to world anchor, not the moving camera rig.");
            Assert.That(Object.FindAnyObjectByType<PetActor>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AimAndCatchRecordsOnePracticeReceiptWithoutAwardingInventory()
        {
            controller.SimulateMarker(1);
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(controller.Session.Phase, Is.EqualTo(HuntPhase.ReadyToCatch));
            var pet = Object.FindAnyObjectByType<PetActor>();
            Assert.That(pet, Is.Not.Null);
            // Feed actual Input System mouse events into the same camera controls used by the player.
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                // Pet spawn and bobbing are random. Correct the actual view instead of assuming
                // its initial height or assuming an input event is consumed in exactly one frame.
                for (var attempt = 0; attempt < 12 && !controller.CanCatch; attempt++)
                {
                    var target = pet.transform.position;
                    var camera = controller.arCamera.transform;
                    var desiredYaw = Mathf.Atan2(-target.x, -target.z) * Mathf.Rad2Deg;
                    var currentYaw = Mathf.Atan2(-camera.position.x, -camera.position.z) * Mathf.Rad2Deg;
                    var radius = new Vector2(target.x, target.z).magnitude;
                    var desiredHeight = 0.6f + (target.y - 0.6f) * 1.05f / (1.05f - radius);
                    var horizontalForward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
                    var travel = -Vector3.Dot(Vector3.ProjectOnPlane(camera.position, Vector3.up), horizontalForward) / horizontalForward.sqrMagnitude;
                    var currentHeight = camera.position.y + camera.forward.y * travel;
                    mouse.MakeCurrent();
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        buttons = 2,
                        delta = new Vector2(Mathf.DeltaAngle(currentYaw, desiredYaw) / 0.12f, (desiredHeight - currentHeight) / 0.002f)
                    });
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return null;
                    yield return null;
                }
                Assert.That(controller.CanCatch, Is.True,
                    $"Aim must hit pet. Camera={controller.arCamera.transform.position}, forward={controller.arCamera.transform.forward}, pet={pet.transform.position}");
                controller.CatchPet();
                controller.CatchPet();
                Assert.That(controller.Session.Phase, Is.EqualTo(HuntPhase.Completed));
                Assert.That(controller.Repository.Data.hunts.Count, Is.EqualTo(1));
                Assert.That(controller.Repository.Data.hunts[0].plantId, Is.EqualTo("plant-map"));
                Assert.That(controller.Repository.Data.hunts[0].practice, Is.True);
                Assert.That(controller.Repository.Data.pets, Is.Empty);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator CapturePortraitPreview()
        {
            controller.SimulateMarker(2);
            yield return new WaitForSecondsRealtime(0.4f);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var camera = controller.arCamera;
            const int width = 540;
            const int height = 960;
            var render = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var oldAspect = camera.aspect;
            camera.targetTexture = render;
            camera.aspect = width / (float)height;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("TestResults");
            File.WriteAllBytes("TestResults/preview-scan.png", texture.EncodeToPNG());
            controller.GetComponent<LabHud>().OpenGarden();
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes("TestResults/preview-garden.png", texture.EncodeToPNG());
            camera.targetTexture = previousTarget;
            camera.aspect = oldAspect;
            RenderTexture.active = previousActive;
            Object.Destroy(texture);
            Object.Destroy(render);
        }
    }
}
