using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.ARKit;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

namespace Pat.ArLab.Editor
{
    public static class LabProjectBuilder
    {
        public const string ScenePath = "Assets/PAT/Scenes/PlantHunt.unity";
        public const string SettingsPath = "Assets/PAT/Data/HuntSettings.asset";
        public const string LibraryPath = "Assets/PAT/Data/PlantMarkers.asset";

        [MenuItem("PAT/1. Setup or repair AR Lab")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/PAT/Data");
            Directory.CreateDirectory("Assets/PAT/Scenes");
            Directory.CreateDirectory("Assets/PAT/Markers");
            Directory.CreateDirectory("Markers");
            AssetDatabase.Refresh();
            ConfigurePlayer();
            ConfigureXr();
            var library = GenerateMarkers();
            var settings = AssetDatabase.LoadAssetAtPath<HuntSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<HuntSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            if (!File.Exists(ScenePath)) CreateScene(library, settings);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("PAT_SETUP_OK: six image markers, Android/iOS XR loaders and PlantHunt scene are ready.");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "PAT Lab";
            PlayerSettings.productName = "PAT AR Lab";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pat.arlab");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.pat.arlab");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.cameraUsageDescription = "PAT dùng camera để nhận diện nhãn cây của bạn và tìm thú AR quanh cây.";
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var handler = player.FindProperty("activeInputHandler");
            if (handler != null) { handler.intValue = 1; player.ApplyModifiedPropertiesWithoutUndo(); }
            AddShader("Standard");
        }

        private static void AddShader(string shaderName)
        {
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            for (var i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            shaders.InsertArrayElementAtIndex(shaders.arraySize);
            shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader;
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureXr()
        {
            const string path = "Assets/PAT/Data/XRGeneralSettingsPerBuildTarget.asset";
            var all = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (all == null)
            {
                all = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(all, path);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, all, true);
            ConfigureLoader(all, BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
            ConfigureLoader(all, BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader");
            var arcore = ARCoreSettings.currentSettings;
            if (arcore != null)
            {
                arcore.requirement = ARCoreSettings.Requirement.Required;
                arcore.depth = ARCoreSettings.Requirement.Optional;
                EditorUtility.SetDirty(arcore);
            }
            var arkit = ARKitSettings.currentSettings;
            if (arkit != null)
            {
                arkit.requirement = ARKitSettings.Requirement.Required;
                EditorUtility.SetDirty(arkit);
            }
            EditorUtility.SetDirty(all);
        }

        private static void ConfigureLoader(XRGeneralSettingsPerBuildTarget all, BuildTargetGroup target, string loader)
        {
            if (!all.HasManagerSettingsForBuildTarget(target)) all.CreateDefaultManagerSettingsForBuildTarget(target);
            var general = all.SettingsForBuildTarget(target);
            general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, loader, target))
                throw new InvalidOperationException("Cannot configure XR loader: " + loader);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
        }

        public static void CreateScene(XRReferenceImageLibrary library, HuntSettings settings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sessionObject = new GameObject("AR Session");
            var session = sessionObject.AddComponent<ARSession>();
            session.enabled = false;
            sessionObject.AddComponent<ARInputManager>();

            var originObject = new GameObject("XR Origin");
            var origin = originObject.AddComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originObject.transform, false);
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 0;
            var cameraObject = new GameObject("AR Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.16f, 0.13f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            var poseDriver = cameraObject.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            poseDriver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            poseDriver.trackingStateInput = new InputActionProperty(new InputAction("Tracking State", InputActionType.Value, "<XRHMD>/trackingState"));
            origin.Camera = camera;

            var images = originObject.AddComponent<ARTrackedImageManager>();
            images.referenceLibrary = library;
            images.requestedMaxNumberOfMovingImages = 1;
            images.enabled = false;
            var anchors = originObject.AddComponent<ARAnchorManager>();
            anchors.enabled = false;
            var game = new GameObject("PAT - Hunt Controller");
            var controller = game.AddComponent<ArHuntController>();
            controller.settings = settings;
            controller.arSession = session;
            controller.imageManager = images;
            controller.anchorManager = anchors;
            controller.arCamera = camera;

            var light = new GameObject("Soft daylight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(42, -30, 0);
            RenderSettings.ambientLight = new Color(0.60f, 0.65f, 0.58f);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static XRReferenceImageLibrary GenerateMarkers()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            for (var index = 1; index <= LabCatalog.MarkerCount; index++)
            {
                var id = LabCatalog.MarkerId(index);
                var path = "Assets/PAT/Markers/" + id + ".png";
                if (!File.Exists(path))
                {
                    var texture = GenerateMarker(index);
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                File.Copy(path, "Markers/" + id + ".png", true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.SaveAndReimport();
                var existing = -1;
                for (var i = 0; i < library.count; i++) if (library[i].name == id) existing = i;
                if (existing < 0) { library.Add(); existing = library.count - 1; }
                library.SetName(existing, id);
                library.SetTexture(existing, AssetDatabase.LoadAssetAtPath<Texture2D>(path), false);
                library.SetSpecifySize(existing, true);
                library.SetSize(existing, Vector2.one * LabCatalog.MarkerWidthMeters);
            }
            EditorUtility.SetDirty(library);
            WritePrintableSheet();
            return library;
        }

        private static Texture2D GenerateMarker(int seed)
        {
            const int size = 768;
            var random = new System.Random(seed * 7919);
            var offsetX = (float)random.NextDouble() * 500;
            var offsetY = (float)random.NextDouble() * 500;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var colors = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var value = Mathf.PerlinNoise(offsetX + u * 12, offsetY + v * 12) * 0.55f
                    + Mathf.PerlinNoise(offsetY + u * 47, offsetX + v * 47) * 0.30f
                    + Mathf.PerlinNoise(offsetX + u * 120, offsetY + v * 120) * 0.15f;
                value = Mathf.Clamp01((value - 0.25f) * 2.0f);
                var gray = (byte)Mathf.RoundToInt(value * 255);
                colors[y * size + x] = new Color32(gray, gray, gray, 255);
            }
            // Irregular, nonrepeating silhouettes over multiscale texture produce stable feature points.
            for (var shape = 0; shape < 65; shape++)
            {
                var cx = random.Next(25, size - 25);
                var cy = random.Next(25, size - 25);
                var radius = random.Next(9, 48);
                var gray = (byte)(random.Next(2) == 0 ? 18 : 240);
                for (var y = Math.Max(0, cy - radius); y < Math.Min(size, cy + radius); y++)
                for (var x = Math.Max(0, cx - radius); x < Math.Min(size, cx + radius); x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var inside = shape % 3 == 0 ? Math.Abs(dx) + Math.Abs(dy) < radius : dx * dx + dy * dy < radius * radius;
                    if (inside) colors[y * size + x] = new Color32(gray, gray, gray, 255);
                }
            }
            texture.SetPixels32(colors);
            texture.Apply();
            return texture;
        }

        private static void WritePrintableSheet()
        {
            var html = new StringBuilder("<!doctype html><html lang='vi'><meta charset='utf-8'><title>PAT — Nhãn cây</title><style>body{font:16px sans-serif;color:#18372b}section{break-after:page;page-break-after:always;padding:10mm}img{width:120mm;height:120mm;display:block}h1{font-size:22pt}.ruler{width:100mm;border-top:2px solid;margin-top:10mm}@page{size:A4;margin:10mm}@media print{.intro{display:none}}</style><div class='intro'><h1>Nhãn thử nghiệm PAT AR</h1><p>In ở tỉ lệ 100% / Actual size, không Fit to page. Khung ảnh phải rộng đúng 12 cm. Đây là marker ảnh, không phải mã QR. Đặt phẳng cạnh chậu, không uốn quanh chậu. Chỉ dùng để in.</p></div>");
            for (var i = 1; i <= LabCatalog.MarkerCount; i++)
            {
                var id = LabCatalog.MarkerId(i);
                var label = i == 1 ? "Bé Mập" : i == 2 ? "Minty" : i == 3 ? "Gai Nhỏ" : "Chưa đăng ký — liên kết trong Khu vườn";
                html.Append($"<section><h1>{id} / {label}</h1><p>Ảnh 12 × 12 cm. Giữ ảnh rõ, không che góc.</p><img src='{id}.png' alt='{id}'><div class='ruler'>Thước kiểm tra: đường này dài 10 cm</div><p>PAT AR LAB · Mỗi nhãn chỉ liên kết một cây.</p></section>");
            }
            html.Append("</html>");
            File.WriteAllText("Markers/printable-labels.html", html.ToString(), Encoding.UTF8);
        }

        [MenuItem("PAT/2. Open hunt scene")]
        public static void OpenScene() => EditorSceneManager.OpenScene(ScenePath);

        [MenuItem("PAT/3. Open printable plant labels")]
        public static void OpenLabels() => Application.OpenURL(new Uri(Path.GetFullPath("Markers/printable-labels.html")).AbsoluteUri);

        [MenuItem("PAT/Build Android APK (development)")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Cài Android Build Support + SDK/NDK/OpenJDK cho Unity 6000.6.5f1 trong Unity Hub trước.");
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/PAT-AR-Lab.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Android build failed; xem Console.");
        }
    }
}
