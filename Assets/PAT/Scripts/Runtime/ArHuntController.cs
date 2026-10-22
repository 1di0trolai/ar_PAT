using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Pat.ArLab
{
    public sealed class ArHuntController : MonoBehaviour
    {
        public HuntSettings settings;
        public ARSession arSession;
        public ARTrackedImageManager imageManager;
        public ARAnchorManager anchorManager;
        public Camera arCamera;
        [HideInInspector] public string saveFileName = "pat-ar-lab.json";

        public GardenRepository Repository { get; private set; }
        public HuntSession Session { get; private set; }
        public GardenPlant Target { get; private set; }
        public PetDefinition Reward { get; private set; }
        public bool Practice { get; private set; }
        public bool Simulation { get; private set; }
        public bool MenuOpen { get; set; }
        public bool SimulatedTrackingLost { get; set; }
        public bool DebugVisible { get; set; }
        public bool AimedAtPet { get; private set; }
        public string Message { get; private set; } = "Đưa nhãn cây vào khung camera.";
        public string DetectedMarker { get; private set; } = "";
        public string Error { get; private set; } = "";
        public float RecognitionProgress => Mathf.Clamp01(recognitionSeconds / settings.recognitionHold);
        public bool CanCatch => Session.CanCatch && AimedAtPet && !MenuOpen && !applicationPaused;
        public bool CanSimulate => Application.isEditor || Debug.isDebugBuild;

        private ARAnchor anchor;
        private GameObject simulatedAnchor;
        private GameObject simulationEnvironment;
        private PetActor pet;
        private string candidate;
        private string simulatedMarker;
        private float recognitionSeconds;
        private float lostSeconds;
        private bool creatingAnchor;
        private bool applicationPaused;
        private int generation;
        private float orbit;
        private float simulatedLookHeight = 0.16f;
        private Coroutine arStartup;
        private readonly System.Collections.Generic.List<Material> simulationMaterials = new System.Collections.Generic.List<Material>();

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Repository = new GardenRepository(Path.Combine(Application.persistentDataPath, saveFileName));
            Session = new HuntSession(settings.scanDuration);
            Practice = settings.practiceByDefault;
            DebugVisible = settings.showDebugByDefault;
            Simulation = Application.isEditor;
            arSession.enabled = false;
            imageManager.enabled = false;
            anchorManager.enabled = false;
            if (!string.IsNullOrEmpty(Repository.LoadWarning)) Error = Repository.LoadWarning;
        }

        private void Start()
        {
            if (Simulation) SetupSimulation();
            else arStartup = StartCoroutine(StartAr());
            gameObject.AddComponent<LabHud>().Initialize(this);
        }

        private IEnumerator StartAr()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Message = "Cho phép camera để nhận diện nhãn cây.";
                var permissionFinished = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => permissionFinished = true;
                callbacks.PermissionDenied += _ => permissionFinished = true;
                callbacks.PermissionDeniedAndDontAskAgain += _ => permissionFinished = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
                while (!permissionFinished) yield return null;
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Error = "Camera chưa được cấp quyền. Mở Cài đặt của ứng dụng để cho phép, rồi quay lại.";
                    yield break;
                }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Error = "Camera chưa được cấp quyền. Hãy bật Camera trong Cài đặt rồi thử lại.";
                yield break;
            }
#endif
            Message = "Đang khởi động camera AR…";
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
            {
                Error = "Thiết bị chưa hỗ trợ AR hoặc chưa có dịch vụ AR. Kiểm tra ARCore/ARKit.";
                yield break;
            }
            arSession.enabled = true;
            anchorManager.enabled = true;
            imageManager.enabled = true;
            Error = "";
            Message = "Đưa nhãn cây vào khung camera.";
        }

        private void Update()
        {
            if (Session == null) return;
            if (Simulation) UpdateSimulationCamera();
            if (applicationPaused || MenuOpen) return;

            var tracked = Simulation ? !SimulatedTrackingLost : ARSession.state == ARSessionState.SessionTracking;
            FindVisibleMarker(out var visibleId, out var visiblePose);
            DetectedMarker = visibleId ?? "";

            if (Session.Phase == HuntPhase.Searching)
            {
                ObserveMarker(visibleId, visiblePose, tracked);
                return;
            }
            if (Session.Phase == HuntPhase.Completed) return;

            var root = AnchorTransform;
            var anchorTracking = Simulation ? root != null : anchor != null && anchor.trackingState == TrackingState.Tracking;
            var distance = root == null ? float.MaxValue : Vector3.Distance(arCamera.transform.position, root.position);
            var changedTarget = !string.IsNullOrEmpty(visibleId) && visibleId != Target.markerId;
            var valid = tracked && anchorTracking && !changedTarget
                && distance >= settings.minimumCameraDistance && distance <= settings.maximumCameraDistance;
            var reason = changedTarget ? "Bạn đang hướng vào nhãn khác. Quay lại " + Target.name + "."
                : !tracked || !anchorTracking ? "Mất theo dõi. Di chuyển chậm và đưa nhãn cây vào camera."
                : distance < settings.minimumCameraDistance ? "Camera quá gần. Lùi lại một chút."
                : "Camera quá xa cây. Tiến lại gần một chút.";

            Session.Tick(Time.unscaledDeltaTime, valid, reason);
            lostSeconds = valid ? 0f : lostSeconds + Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (lostSeconds >= settings.trackingLossReset)
            {
                ResetHunt();
                Message = "Đã mất theo dõi quá lâu. Hãy quét lại nhãn cây.";
                return;
            }

            if (Session.Phase == HuntPhase.ReadyToCatch && pet == null) SpawnPet();
            if (pet != null)
            {
                var ray = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                AimedAtPet = valid && Physics.Raycast(ray, out var hit, settings.maximumCameraDistance)
                    && hit.collider.GetComponentInParent<PetActor>() == pet;
                pet.SetHighlighted(AimedAtPet);
                pet.gameObject.SetActive(valid);
            }
            Message = Session.Paused ? reason : Session.Phase == HuntPhase.Scanning
                ? "Giữ cây trong vùng quét, dịch camera nhẹ sang hai bên."
                : AimedAtPet ? "Đã ngắm trúng! Bấm Bắt thú." : "Đã tìm thấy thú! Soi quanh cây và đưa thú vào tâm ngắm.";
        }

        private void FindVisibleMarker(out string id, out Pose pose)
        {
            id = null;
            pose = Pose.identity;
            if (Simulation)
            {
                if (!SimulatedTrackingLost && !string.IsNullOrEmpty(simulatedMarker))
                {
                    id = simulatedMarker;
                    pose = new Pose(Vector3.zero, Quaternion.identity);
                }
                return;
            }
            var bestCenterDistance = float.MaxValue;
            foreach (var image in imageManager.trackables)
            {
                if (image.trackingState != TrackingState.Tracking) continue;
                var viewport = arCamera.WorldToViewportPoint(image.transform.position);
                if (viewport.z <= 0 || viewport.x < 0.05f || viewport.x > 0.95f || viewport.y < 0.05f || viewport.y > 0.95f) continue;
                var centerDistance = Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
                if (centerDistance >= bestCenterDistance) continue;
                bestCenterDistance = centerDistance;
                id = image.referenceImage.name;
                pose = new Pose(image.transform.position, image.transform.rotation);
            }
        }

        private void ObserveMarker(string id, Pose pose, bool tracked)
        {
            if (creatingAnchor) return;
            if (!tracked || string.IsNullOrEmpty(id))
            {
                candidate = null;
                recognitionSeconds = 0;
                Message = tracked ? "Đưa nhãn cây vào khung camera." : "Di chuyển camera chậm để nhận diện không gian.";
                return;
            }
            var plant = Repository.FindByMarker(id);
            if (plant == null)
            {
                candidate = null;
                recognitionSeconds = 0;
                Message = $"Nhãn {id} chưa liên kết với cây trong vườn. Mở Khu vườn để đăng ký.";
                return;
            }
            if (!Practice && !Simulation && Repository.UsedToday(DateTimeOffset.UtcNow))
            {
                Message = "Bạn đã dùng lượt săn chung hôm nay. Bật Luyện tập để tiếp tục thử.";
                return;
            }
            if (candidate != id) { candidate = id; recognitionSeconds = 0; }
            recognitionSeconds += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Message = "Đang xác nhận " + plant.name + "…";
            if (recognitionSeconds >= settings.recognitionHold) LockPlant(plant, pose);
        }

        private async void LockPlant(GardenPlant plant, Pose pose)
        {
            creatingAnchor = true;
            var attemptGeneration = generation;
            try
            {
                if (Simulation)
                {
                    simulatedAnchor = new GameObject("Simulated plant anchor");
                    simulatedAnchor.transform.SetPositionAndRotation(pose.position, Quaternion.identity);
                }
                else
                {
                    var result = await anchorManager.TryAddAnchorAsync(new Pose(pose.position, Quaternion.identity));
                    if (this == null || generation != attemptGeneration)
                    {
                        if (result.status.IsSuccess() && result.value != null) Destroy(result.value.gameObject);
                        return;
                    }
                    if (!result.status.IsSuccess() || result.value == null)
                    {
                        Message = "Chưa khóa được vị trí. Giữ nhãn rõ hơn và thử lại.";
                        return;
                    }
                    anchor = result.value;
                }
                if (generation != attemptGeneration) return;
                Target = plant;
                Session.Begin(plant.id);
                lostSeconds = 0;
                Message = $"Đã nhận diện {plant.name}. Bắt đầu quét {settings.scanDuration:0} giây.";
            }
            catch (Exception exception)
            {
                if (this != null) { Error = "Không tạo được điểm neo: " + exception.Message; Debug.LogException(exception); }
            }
            finally { if (this != null && generation == attemptGeneration) creatingAnchor = false; }
        }

        private Transform AnchorTransform => Simulation ? simulatedAnchor == null ? null : simulatedAnchor.transform
            : anchor == null ? null : anchor.transform;

        private void SpawnPet()
        {
            Reward = LabCatalog.RollPet(UnityEngine.Random.value);
            var root = new GameObject("Pet - " + Reward.id);
            root.transform.SetParent(AnchorTransform, false);
            // Face the player-side semicircle; stay away from the center occupied by the pot.
            var cameraDirection = Vector3.ProjectOnPlane(arCamera.transform.position - AnchorTransform.position, Vector3.up).normalized;
            var angle = UnityEngine.Random.Range(-65f, 65f);
            var offset = Quaternion.Euler(0, angle, 0) * cameraDirection * UnityEngine.Random.Range(settings.spawnRadiusMinimum, settings.spawnRadiusMaximum);
            root.transform.localPosition = settings.plantCenterOffset + offset + Vector3.up * settings.spawnHeight;
            root.transform.rotation = Quaternion.LookRotation(-cameraDirection, Vector3.up);
            pet = root.AddComponent<PetActor>();
            pet.Initialize(Reward);
        }

        public void CatchPet()
        {
            if (!CanCatch || Reward == null || Target == null) return;
            try
            {
                Repository.RecordCatch(Session.RequestId, Target.id, Reward.id, Practice || Simulation, DateTimeOffset.UtcNow);
                Session.Complete();
                if (pet != null) Destroy(pet.gameObject);
                AimedAtPet = false;
                Message = $"Đã bắt {Reward.name} ({Reward.rarity}) quanh {Target.name}!";
                Error = "";
            }
            catch (Exception exception) { Error = "Chưa lưu được kết quả: " + exception.Message; }
        }

        public void ResetHunt()
        {
            generation++;
            creatingAnchor = false;
            if (pet != null) Destroy(pet.gameObject);
            if (anchor != null) Destroy(anchor.gameObject);
            if (simulatedAnchor != null) Destroy(simulatedAnchor);
            Target = null;
            Reward = null;
            candidate = null;
            recognitionSeconds = 0;
            AimedAtPet = false;
            lostSeconds = 0;
            Session.Reset();
            Message = "Đưa nhãn cây vào khung camera.";
            Error = "";
        }

        public void SetPractice(bool value)
        {
            if (value == Practice) return;
            Practice = value;
            ResetHunt();
        }

        public void SimulateMarker(int index)
        {
            if (!CanSimulate) return;
            if (!Simulation)
            {
                if (arStartup != null) StopCoroutine(arStartup);
                Simulation = true;
                arSession.enabled = false;
                imageManager.enabled = false;
                anchorManager.enabled = false;
                SetupSimulation();
            }
            ResetHunt();
            simulatedMarker = LabCatalog.MarkerId(index);
            SimulatedTrackingLost = false;
        }

        private void SetupSimulation()
        {
            var poseDriver = arCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (poseDriver != null) poseDriver.enabled = false;
            arCamera.GetComponent<ARCameraBackground>().enabled = false;
            arCamera.GetComponent<ARCameraManager>().enabled = false;
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = new Color(0.13f, 0.19f, 0.17f);
            simulationEnvironment = new GameObject("Editor practice environment");
            CreateProp("Ground", PrimitiveType.Cube, new Vector3(0, -0.10f, 0), new Vector3(3, 0.1f, 3), new Color(0.32f, 0.38f, 0.33f));
            CreateProp("Pot", PrimitiveType.Cylinder, new Vector3(0, 0.035f, 0), new Vector3(0.18f, 0.09f, 0.18f), new Color(0.77f, 0.55f, 0.38f));
            CreateProp("Stem", PrimitiveType.Cylinder, new Vector3(0, 0.24f, 0), new Vector3(0.018f, 0.16f, 0.018f), new Color(0.32f, 0.53f, 0.29f));
            for (var i = 0; i < 5; i++)
            {
                var angle = i * 1.8f;
                CreateProp("Leaf", PrimitiveType.Sphere, new Vector3(Mathf.Sin(angle) * 0.08f, 0.20f + i * 0.035f, Mathf.Cos(angle) * 0.08f), new Vector3(0.16f, 0.035f, 0.09f), new Color(0.37f, 0.65f, 0.34f));
            }
            orbit = 0;
            simulatedLookHeight = 0.16f;
            UpdateSimulationCamera();
        }

        private void CreateProp(string propName, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var prop = GameObject.CreatePrimitive(type);
            prop.name = propName;
            prop.transform.SetParent(simulationEnvironment.transform, false);
            prop.transform.localPosition = position;
            prop.transform.localScale = scale;
            var material = new Material(Shader.Find("Standard")) { color = color };
            simulationMaterials.Add(material);
            prop.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(prop.GetComponent<Collider>());
        }

        private void UpdateSimulationCamera()
        {
            if (Keyboard.current != null && !MenuOpen)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) orbit -= Time.unscaledDeltaTime * 65f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) orbit += Time.unscaledDeltaTime * 65f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) simulatedLookHeight += Time.unscaledDeltaTime * 0.5f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) simulatedLookHeight -= Time.unscaledDeltaTime * 0.5f;
            }
            if (Mouse.current != null && Mouse.current.rightButton.isPressed && !MenuOpen)
            {
                orbit += Mouse.current.delta.ReadValue().x * 0.12f;
                simulatedLookHeight += Mouse.current.delta.ReadValue().y * 0.002f;
            }
            simulatedLookHeight = Mathf.Clamp(simulatedLookHeight, -0.8f, 1.5f);
            arCamera.transform.position = Quaternion.Euler(0, orbit, 0) * new Vector3(0, 0.60f, -1.05f);
            arCamera.transform.LookAt(new Vector3(0, simulatedLookHeight, 0));
        }

        public void RetryCamera()
        {
            ResetHunt();
            if (Simulation)
            {
                Simulation = false;
                if (simulationEnvironment != null) Destroy(simulationEnvironment);
                arCamera.transform.localPosition = Vector3.zero;
                arCamera.transform.localRotation = Quaternion.identity;
                arCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled = true;
                arCamera.GetComponent<ARCameraBackground>().enabled = true;
                arCamera.GetComponent<ARCameraManager>().enabled = true;
            }
            if (arStartup != null) StopCoroutine(arStartup);
            arStartup = StartCoroutine(StartAr());
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            recognitionSeconds = 0;
            if (Session != null) Session.Tick(0, false, "Đưa nhãn cây vào camera để tiếp tục.");
        }

        private void OnDestroy()
        {
            generation++;
            if (anchor != null) Destroy(anchor.gameObject);
            if (simulatedAnchor != null) Destroy(simulatedAnchor);
            if (simulationEnvironment != null) Destroy(simulationEnvironment);
            foreach (var material in simulationMaterials) if (material != null) Destroy(material);
        }
    }
}
