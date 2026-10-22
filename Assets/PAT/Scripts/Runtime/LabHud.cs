using System;
using UnityEngine;
using UnityEngine.UI;

namespace Pat.ArLab
{
    /// <summary>Native Unity canvas, generated once; no web view or browser dependency.</summary>
    public sealed class LabHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.08f, 0.16f, 0.13f);
        private static readonly Color Paper = new Color(0.97f, 0.96f, 0.91f);
        private static readonly Color Mint = new Color(0.79f, 0.93f, 0.63f);
        private static readonly Color Muted = new Color(0.63f, 0.72f, 0.65f);
        private ArHuntController controller;
        private RectTransform safeArea;
        private RectTransform root;
        private Font font;
        private Text status;
        private Text subtitle;
        private Text progressLabel;
        private Text plantLabel;
        private Text badge;
        private Text reticle;
        private Text debug;
        private Text error;
        private Text catchLabel;
        private Text modeLabel;
        private Text simLabel;
        private Image progressFill;
        private Button catchButton;
        private GameObject modal;
        private RectTransform modalContent;
        private bool collectionTab;
        private int selectedMarker = 1;
        private Text selectedMarkerLabel;
        private InputField nameInput;
        private InputField speciesInput;
        private Text formError;
        private int lastWidth;
        private int lastHeight;
        private Rect lastSafeArea;

        public void Initialize(ArHuntController huntController)
        {
            controller = huntController;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("PAT HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = controller.arCamera;
            canvas.planeDistance = 0.2f;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root = canvasObject.GetComponent<RectTransform>();
            safeArea = Rect("Safe area", root, Vector2.zero, Vector2.one);
            ApplySafeArea();
            BuildHud();
        }

        private void BuildHud()
        {
            var header = Panel("Header", safeArea, new Vector2(0, 1), new Vector2(1, 1), new Color(Ink.r, Ink.g, Ink.b, 0.94f));
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 228);
            Label(header, "P A T   /   A R   L A B", 27, Mint, new Vector2(42, 32), new Vector2(650, 42));
            Label(header, "Soi quanh một mầm xanh", 45, Paper, new Vector2(42, 88), new Vector2(920, 66));
            badge = Label(header, "", 22, Muted, new Vector2(42, 164), new Vector2(650, 36));
            ActionButton(header, "Khu vườn", new Vector2(-42, 32), new Vector2(265, 62), OpenGarden, true);

            var focus = Rect("Focus", safeArea, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f));
            focus.sizeDelta = new Vector2(480, 420);
            foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
            {
                var x = Panel("Focus corner x", focus, corner, corner, Mint);
                x.sizeDelta = new Vector2(65, 5);
                x.pivot = corner;
                var y = Panel("Focus corner y", focus, corner, corner, Mint);
                y.sizeDelta = new Vector2(5, 65);
                y.pivot = corner;
            }
            reticle = Label(focus, "+", 76, Paper, Vector2.zero, new Vector2(150, 150));
            var reticleRect = reticle.rectTransform;
            reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.pivot = new Vector2(0.5f, 0.5f);
            reticleRect.anchoredPosition = Vector2.zero;
            reticle.alignment = TextAnchor.MiddleCenter;
            // Actual targeting uses the camera center. Keep this plus at the same center.
            reticleRect.SetParent(root, false);
            reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.anchoredPosition = Vector2.zero;

            var bottom = Panel("Hunt card", safeArea, Vector2.zero, new Vector2(1, 0), Paper);
            bottom.pivot = new Vector2(0.5f, 0);
            bottom.sizeDelta = new Vector2(0, controller.CanSimulate ? 605 : 505);
            plantLabel = Label(bottom, "CHƯA XÁC ĐỊNH CÂY", 25, new Color(0.35f, 0.47f, 0.35f), new Vector2(42, 28), new Vector2(980, 42));
            status = Label(bottom, "Nhận diện cây của bạn", 45, Ink, new Vector2(42, 82), new Vector2(985, 72));
            subtitle = Label(bottom, "Đưa nhãn riêng của chậu cây vào camera.", 29, new Color(0.35f, 0.42f, 0.36f), new Vector2(42, 166), new Vector2(990, 100));
            subtitle.alignment = TextAnchor.UpperLeft;
            var progressTrack = Panel("Scan track", bottom, new Vector2(0, 1), new Vector2(1, 1), new Color(0.85f, 0.88f, 0.80f));
            progressTrack.pivot = new Vector2(0, 1);
            progressTrack.offsetMin = new Vector2(42, -287);
            progressTrack.offsetMax = new Vector2(-42, -274);
            progressFill = Panel("Scan fill", progressTrack, Vector2.zero, new Vector2(0, 1), new Color(0.32f, 0.51f, 0.29f)).GetComponent<Image>();
            progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;
            progressLabel = Label(bottom, "0 / 10 giây hợp lệ", 23, new Color(0.35f, 0.42f, 0.36f), new Vector2(42, 301), new Vector2(580, 37));
            catchButton = ActionButton(bottom, "Đang chờ nhãn cây", new Vector2(42, 358), new Vector2(720, 85), HandlePrimary);
            catchLabel = catchButton.GetComponentInChildren<Text>();
            ActionButton(bottom, "Thử lại", new Vector2(-42, 358), new Vector2(225, 85), () =>
            {
                if (!controller.Simulation && !string.IsNullOrEmpty(controller.Error)) controller.RetryCamera();
                else controller.ResetHunt();
            }, true);
            var modeButton = ActionButton(bottom, "", new Vector2(42, 458), new Vector2(720, 46), () => controller.SetPractice(!controller.Practice));
            modeButton.GetComponent<Image>().color = Color.clear;
            modeLabel = modeButton.GetComponentInChildren<Text>();
            modeLabel.fontSize = 22;
            modeLabel.alignment = TextAnchor.MiddleLeft;
            if (controller.CanSimulate) BuildSimulationControls(bottom);

            error = Label(safeArea, "", 24, new Color(1f, 0.79f, 0.48f), new Vector2(42, 244), new Vector2(990, 140));
            error.alignment = TextAnchor.UpperLeft;
            debug = Label(safeArea, "", 20, Paper, new Vector2(42, 395), new Vector2(980, 180));
            debug.alignment = TextAnchor.UpperLeft;
            var debugButton = ActionButton(header, "Thông số", new Vector2(-42, 165), new Vector2(265, 40), () => controller.DebugVisible = !controller.DebugVisible, true);
            debugButton.GetComponent<Image>().color = Color.clear;
            debugButton.GetComponentInChildren<Text>().color = Muted;
        }

        private void BuildSimulationControls(RectTransform bottom)
        {
            simLabel = Label(bottom, "MÔ PHỎNG • không dùng camera thật", 19, new Color(0.48f, 0.40f, 0.23f), new Vector2(42, 511), new Vector2(995, 31));
            ActionButton(bottom, "<", new Vector2(42, 552), new Vector2(65, 42), () => CycleMarker(-1));
            selectedMarkerLabel = Label(bottom, LabCatalog.MarkerId(selectedMarker), 22, Ink, new Vector2(125, 553), new Vector2(155, 40));
            ActionButton(bottom, ">", new Vector2(290, 552), new Vector2(65, 42), () => CycleMarker(1));
            ActionButton(bottom, "Thử nhãn", new Vector2(376, 552), new Vector2(210, 42), () => controller.SimulateMarker(selectedMarker));
            ActionButton(bottom, "Mất tracking", new Vector2(608, 552), new Vector2(240, 42), () => controller.SimulatedTrackingLost = !controller.SimulatedTrackingLost);
            ActionButton(bottom, "Camera", new Vector2(-42, 552), new Vector2(140, 42), controller.RetryCamera, true);
        }

        private void Update()
        {
            if (controller == null || controller.Session == null || status == null) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea) ApplySafeArea();
            var session = controller.Session;
            reticle.gameObject.SetActive(!controller.MenuOpen);
            var completed = session.Phase == HuntPhase.Completed;
            var seeking = session.Phase == HuntPhase.Searching;
            status.text = completed ? "Một người bạn mới!" : seeking ? "Nhận diện cây của bạn"
                : session.Phase == HuntPhase.Scanning ? session.Paused ? "Chờ theo dõi ổn định" : "Đang tìm dấu chân…"
                : "Có thú ở quanh đây!";
            subtitle.text = controller.Message;
            plantLabel.text = controller.Target == null ? "NHÃN RIÊNG • ĐÚNG CÂY TRONG VƯỜN"
                : controller.Target.name.ToUpperInvariant() + "  /  " + controller.Target.markerId;
            badge.text = controller.Simulation ? "MÔ PHỎNG TRONG EDITOR / DEV BUILD" : "CAMERA AR • NHẬN DIỆN BẰNG NHÃN";
            progressFill.rectTransform.anchorMax = new Vector2(seeking ? controller.RecognitionProgress : session.Progress, 1);
            progressLabel.text = seeking ? "Xác nhận nhãn trước khi mở phiên săn"
                : $"{session.ScanSeconds:0.0} / {session.Duration:0} giây hợp lệ";
            catchLabel.text = completed ? "Quét một cây khác" : seeking ? "Đang chờ nhãn cây"
                : session.Phase == HuntPhase.Scanning ? "Đang quét…" : "Bắt thú";
            catchButton.interactable = completed || controller.CanCatch;
            reticle.color = controller.AimedAtPet ? Mint : Paper;
            modeLabel.text = controller.Practice || controller.Simulation
                ? "● Luyện tập: không giới hạn, không cộng kho"
                : "● Chế độ ngày: 1 lượt chung / tài khoản";
            if (simLabel != null) simLabel.text = controller.Simulation
                ? "MÔ PHỎNG • Chuột phải kéo / A,D xoay / W,S ngắm cao thấp"
                : "CÔNG CỤ DEV • Nhãn thử chỉ chạy trong mô phỏng";
            error.text = controller.Error;
            debug.text = controller.DebugVisible
                ? $"Nhãn: {controller.DetectedMarker}\nTrạng thái: {session.Phase} | Tạm dừng: {session.Paused}\nTracking AR: {UnityEngine.XR.ARFoundation.ARSession.state}\nCây: {controller.Target?.id ?? "chưa có"}"
                : "";
        }

        private void HandlePrimary()
        {
            if (controller.Session.Phase == HuntPhase.Completed) controller.ResetHunt();
            else controller.CatchPet();
        }

        private void CycleMarker(int direction)
        {
            selectedMarker = (selectedMarker - 1 + direction + LabCatalog.MarkerCount) % LabCatalog.MarkerCount + 1;
            selectedMarkerLabel.text = LabCatalog.MarkerId(selectedMarker);
        }

        public void OpenGarden()
        {
            controller.MenuOpen = true;
            if (modal != null) Destroy(modal);
            modal = Panel("Garden modal", safeArea, Vector2.zero, Vector2.one, Ink).gameObject;
            var modalRect = modal.GetComponent<RectTransform>();
            Label(modalRect, "Khu vườn của tôi", 48, Paper, new Vector2(42, 38), new Vector2(790, 85));
            ActionButton(modalRect, "Đóng", new Vector2(-42, 43), new Vector2(180, 65), CloseGarden, true);
            Label(modalRect, "Mỗi cây dùng một nhãn riêng. Chưa liên kết = chưa được săn.", 27, Muted, new Vector2(42, 142), new Vector2(990, 86));
            ActionButton(modalRect, "Cây & nhãn", new Vector2(42, 248), new Vector2(465, 65), () => { collectionTab = false; OpenGarden(); });
            ActionButton(modalRect, "Pet đã bắt", new Vector2(-42, 248), new Vector2(465, 65), () => { collectionTab = true; OpenGarden(); }, true);

            var viewport = Rect("Scroll viewport", modalRect, new Vector2(0, 0), new Vector2(1, 1));
            viewport.offsetMin = new Vector2(42, 30);
            viewport.offsetMax = new Vector2(-42, -345);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            modalContent = Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1));
            modalContent.pivot = new Vector2(0.5f, 1);
            scroll.viewport = viewport;
            scroll.content = modalContent;
            if (collectionTab) BuildCollection();
            else BuildGarden();
        }

        private void BuildGarden()
        {
            var y = 0f;
            foreach (var plant in controller.Repository.Data.plants)
            {
                var captured = plant;
                var card = Panel("Plant " + plant.markerId, modalContent, new Vector2(0, 1), new Vector2(1, 1), new Color(0.14f, 0.23f, 0.18f));
                card.pivot = new Vector2(0.5f, 1);
                card.sizeDelta = new Vector2(0, 150);
                card.anchoredPosition = new Vector2(0, -y);
                Label(card, plant.name, 36, Paper, new Vector2(25, 16), new Vector2(690, 55));
                Label(card, plant.species + "  •  " + plant.markerId, 25, Mint, new Vector2(25, 83), new Vector2(690, 45));
                ActionButton(card, "Gỡ nhãn", new Vector2(-18, 49), new Vector2(210, 55), () => OpenRemoveConfirmation(captured), true);
                y += 173;
            }
            Label(modalContent, "THÊM CÂY / LIÊN KẾT NHÃN CÒN TRỐNG", 25, Mint, new Vector2(0, y + 15), new Vector2(990, 55));
            y += 85;
            var freeMarkers = new System.Collections.Generic.List<string>();
            for (var i = 1; i <= LabCatalog.MarkerCount; i++)
                if (controller.Repository.FindByMarker(LabCatalog.MarkerId(i)) == null) freeMarkers.Add(LabCatalog.MarkerId(i));
            if (freeMarkers.Count == 0)
            {
                Label(modalContent, "Cả 6 nhãn đã có cây. Gỡ một nhãn để liên kết lại.", 27, Paper, new Vector2(0, y), new Vector2(970, 90));
                modalContent.sizeDelta = new Vector2(0, y + 140);
                return;
            }
            var markerIndex = 0;
            var markerButton = ActionButton(modalContent, "Nhãn: " + freeMarkers[0] + "  >", new Vector2(0, y), new Vector2(990, 65), null);
            markerButton.onClick.AddListener(() =>
            {
                markerIndex = (markerIndex + 1) % freeMarkers.Count;
                markerButton.GetComponentInChildren<Text>().text = "Nhãn: " + freeMarkers[markerIndex] + "  >";
            });
            y += 89;
            nameInput = Field(modalContent, "Tên cây, ví dụ: Sen Đá Nhỏ", y);
            y += 89;
            speciesInput = Field(modalContent, "Loài cây (không bắt buộc)", y);
            y += 95;
            ActionButton(modalContent, "Liên kết cây với nhãn", new Vector2(0, y), new Vector2(990, 75), () =>
            {
                try
                {
                    controller.Repository.Register(freeMarkers[markerIndex], nameInput.text, speciesInput.text);
                    controller.ResetHunt();
                    OpenGarden();
                }
                catch (Exception exception) { formError.text = exception.Message; }
            });
            y += 92;
            formError = Label(modalContent, "", 25, new Color(1f, 0.72f, 0.43f), new Vector2(0, y), new Vector2(990, 85));
            y += 100;
            Label(modalContent, "Nhãn mẫu nằm trong Markers/printable-labels.html.\nIn đúng khung ảnh 12 × 12 cm, đặt phẳng cạnh chân chậu.", 25, Muted, new Vector2(0, y), new Vector2(990, 140));
            modalContent.sizeDelta = new Vector2(0, y + 175);
        }

        private void BuildCollection()
        {
            var y = 0f;
            foreach (var pet in LabCatalog.Pets)
            {
                var owned = controller.Repository.Data.pets.Find(entry => entry.petId == pet.id);
                Label(modalContent, pet.name + "  ×" + (owned?.quantity ?? 0), 35, Paper, new Vector2(0, y), new Vector2(990, 70));
                Label(modalContent, pet.rarity, 25, Mint, new Vector2(0, y + 74), new Vector2(990, 50));
                y += 158;
            }
            Label(modalContent, "LƯỢT SĂN GẦN ĐÂY", 26, Mint, new Vector2(0, y + 12), new Vector2(990, 60));
            y += 90;
            var hunts = controller.Repository.Data.hunts;
            for (var i = hunts.Count - 1; i >= Mathf.Max(0, hunts.Count - 12); i--)
            {
                var hunt = hunts[i];
                var plant = controller.Repository.Data.plants.Find(entry => entry.id == hunt.plantId);
                var pet = Array.Find(LabCatalog.Pets, entry => entry.id == hunt.petId);
                Label(modalContent, $"{hunt.localDate} • {(hunt.practice ? "Luyện tập" : "Lượt ngày")}\n{plant?.name ?? "Cây đã gỡ"} → {pet?.name ?? hunt.petId}", 26, Paper, new Vector2(0, y), new Vector2(990, 105));
                y += 125;
            }
            if (hunts.Count == 0) Label(modalContent, "Chưa có lượt săn nào. Hãy quét nhãn cây để bắt đầu.", 28, Muted, new Vector2(0, y), new Vector2(990, 110));
            modalContent.sizeDelta = new Vector2(0, y + 150);
        }

        private void OpenRemoveConfirmation(GardenPlant plant)
        {
            var confirm = Panel("Confirm remove", modal.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Ink);
            Label(confirm, "Gỡ liên kết " + plant.name + "?", 43, Paper, new Vector2(42, 350), new Vector2(990, 130));
            Label(confirm, "Lịch sử săn vẫn được giữ. Nhãn sẽ không mở phiên săn cho đến khi được liên kết lại.", 30, Muted, new Vector2(42, 515), new Vector2(990, 170));
            ActionButton(confirm, "Giữ cây", new Vector2(42, 740), new Vector2(465, 85), () => Destroy(confirm.gameObject));
            ActionButton(confirm, "Gỡ liên kết", new Vector2(-42, 740), new Vector2(465, 85), () =>
            {
                try
                {
                    controller.Repository.RemovePlant(plant.id);
                    controller.ResetHunt();
                    OpenGarden();
                }
                catch (Exception exception) { Debug.LogException(exception); }
            }, true);
        }

        public void CloseGarden()
        {
            if (modal != null) Destroy(modal);
            controller.MenuOpen = false;
        }

        private InputField Field(RectTransform parent, string placeholder, float y)
        {
            var panel = Panel("Input", parent, new Vector2(0, 1), new Vector2(0, 1), Paper);
            panel.pivot = new Vector2(0, 1);
            panel.sizeDelta = new Vector2(990, 68);
            panel.anchoredPosition = new Vector2(0, -y);
            var input = panel.gameObject.AddComponent<InputField>();
            input.characterLimit = 60;
            var text = Label(panel, "", 28, Ink, new Vector2(20, 7), new Vector2(945, 54));
            var hint = Label(panel, placeholder, 27, new Color(0.45f, 0.5f, 0.45f), new Vector2(20, 7), new Vector2(945, 54));
            input.textComponent = text;
            input.placeholder = hint;
            return input;
        }

        private void ApplySafeArea()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / lastWidth, lastSafeArea.yMin / lastHeight);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / lastWidth, lastSafeArea.yMax / lastHeight);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        private static RectTransform Rect(string elementName, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(elementName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Panel(string elementName, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(elementName, parent, min, max);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text Label(Transform parent, string content, int size, Color color, Vector2 position, Vector2 dimensions)
        {
            var rect = Rect("Text", parent, new Vector2(0, 1), new Vector2(0, 1));
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = dimensions;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button ActionButton(Transform parent, string caption, Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action, bool right = false)
        {
            var anchor = new Vector2(right ? 1 : 0, 1);
            var rect = Panel("Button " + caption, parent, anchor, anchor, right ? new Color(0.24f, 0.35f, 0.28f) : Mint);
            rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = dimensions;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.88f, 1f, 0.79f);
            colors.pressedColor = new Color(0.58f, 0.77f, 0.47f);
            colors.disabledColor = new Color(0.73f, 0.76f, 0.67f, 0.6f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            var label = Label(rect, caption, dimensions.y <= 48 ? 22 : 28, right ? Paper : Ink, Vector2.zero, dimensions);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
