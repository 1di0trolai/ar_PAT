# PAT AR Lab — Unity / C#

Prototype mobile độc lập để thử săn thú quanh **cây thật đã đăng ký**. Project nằm ngay trong `C:\Code\ar_PAT`, dùng Unity **6000.6.5f1**, AR Foundation / ARCore / ARKit **6.6.2**.

## Chạy ngay trong Unity Editor

1. Mở **Unity Hub → Projects → Add → Add project from disk**, chọn folder `C:\Code\ar_PAT`.
2. Mở bằng Unity `6000.6.5f1`, đợi import package xong.
3. Mở scene `Assets/PAT/Scenes/PlantHunt.unity`. Nếu thiếu scene/nhãn, chọn menu **PAT → 1. Setup or repair AR Lab**.
4. Bấm **Play**. Chọn Game view portrait **540 × 960** hoặc **1080 × 1920** để xem đúng giao diện mobile.
5. Dưới màn hình, giữ `PAT-001` và bấm **Thử nhãn**. App tự xác nhận Bé Mập, rồi đếm đủ **10 giây tracking hợp lệ**.
6. Thú xuất hiện trong không gian quanh cây mẫu. Giữ **chuột phải và kéo** để ngắm; hoặc dùng **A / D** xoay ngang và **W / S** ngắm cao/thấp (các phím mũi tên cũng dùng được). Khi thú vào dấu `+`, nút **Bắt thú** sáng lên.
7. Mở **Khu vườn → Pet đã bắt** để xem lịch sử. Mô phỏng luôn là luyện tập: có lịch sử nhưng không cộng kho, không trừ lượt ngày.

Editor hiển thị **môi trường 3D mô phỏng**, không dùng webcam thay thế ARCore/ARKit. Quét nhãn ngoài đời cần build lên điện thoại tương thích.

## Thử nhận diện nhiều cây

| Nhãn | Cây mặc định | Kết quả quét |
|---|---|---|
| `PAT-001` | Bé Mập / Monstera | Mở phiên săn cho Bé Mập |
| `PAT-002` | Minty / Bạc hà | Mở phiên săn cho Minty |
| `PAT-003` | Gai Nhỏ / Xương rồng | Mở phiên săn cho Gai Nhỏ |
| `PAT-004` đến `PAT-006` | Chưa liên kết | Bị từ chối cho đến khi đăng ký |

Để thử cây mới: chọn `PAT-004` → **Thử nhãn** → xác nhận app báo chưa đăng ký → mở **Khu vườn → Cây & nhãn** → chọn nhãn còn trống, nhập tên/loài cây và bấm **Liên kết cây với nhãn** → đóng Khu vườn, quét lại nhãn. Mỗi nhãn chỉ liên kết một cây trong vườn của tài khoản lab.

**Gỡ nhãn** có xác nhận và giữ lại lịch sử săn. Nhãn đã gỡ không thể mở phiên săn cho đến khi được liên kết lại.

## In nhãn và quét bằng điện thoại

- Mở `Markers/printable-labels.html`, hoặc menu **PAT → 3. Open printable plant labels**. Đây chỉ là tài liệu để in, không phải ứng dụng web.
- In ở **100% / Actual size**, tắt **Fit to page**. Khung ảnh phải đúng **12 × 12 cm**. Có đường thước 10 cm để kiểm tra bản in.
- Đây là **nhãn ảnh có đặc trưng riêng, không phải QR**. Không thay bằng QR tự tạo: Unity phải dùng đúng ảnh trong `PlantMarkers.asset`.
- Đặt nhãn **phẳng cạnh chân chậu**, trong cùng vùng với cây. Không dán uốn cong quanh chậu, không che ảnh, ưu tiên giấy ít phản chiếu.
- Đưa nhãn vào camera ở khoảng cách đủ gần để thấy rõ. App cần khoảng 0,8 giây nhận diện liên tục trước khi tự khóa cây.
- Sau đó giữ camera mở và dịch nhẹ sang hai bên trong khoảng 10 giây. Điểm neo cho phép nhãn tạm ra khỏi khung khi soi tìm thú.
- Tracking yếu, camera quá gần/quá xa hoặc hướng vào nhãn cây khác sẽ **tạm dừng**. Mất theo dõi liên tục 12 giây thì phiên bị hủy, yêu cầu quét lại.
- Giữ nguyên chậu/nhãn trong một phiên săn. Nhãn nhận diện hồ sơ chậu; bản này không phân biệt sinh học hai cây giống nhau hay phát hiện việc chuyển nhãn sang cây khác.

## Build Android

Máy hiện có Unity Editor nhưng **chưa có Android Build Support**. Trong Unity Hub:

1. **Installs → Unity 6000.6.5f1 → Add modules**.
2. Chọn **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK** và cài.
3. Dùng điện thoại hỗ trợ ARCore, bật Developer options / USB debugging nếu dùng Build and Run.
4. Mở project → **File → Build Profiles → Android → Switch Platform**.
5. Trong **Project Settings → XR Plug-in Management**, xác nhận Android có **ARCore** (đã được cấu hình tự động).
6. Chọn menu **PAT → Build Android APK (development)**. File đầu ra: `Builds/Android/PAT-AR-Lab.apk`. Hoặc dùng Build and Run trong Unity để cài qua USB.
7. Mở app trên điện thoại và cấp quyền camera. App dùng nhãn thật, không cần chạm chậu.

Build development có công cụ mô phỏng để debug. Bấm **Camera** để trở lại camera thật. Build thường không hiển thị công cụ mô phỏng.

Android đã đặt ARM64, IL2CPP, OpenGLES3 và min SDK 26; cần kiểm tra **Project Validation** nếu đổi phiên bản Unity/package. Chỉ tuyên bố AR hoạt động trên thiết bị sau khi chạy thử trên điện thoại thật.

## iOS

Đã cấu hình ARKit loader và mô tả quyền camera. Để build/cài iOS cần Unity iOS Build Support, **macOS + Xcode**, thiết bị hỗ trợ ARKit và signing của Apple. Luồng gameplay dùng chung C#; chưa xác nhận bằng thiết bị iOS trong phiên làm việc này.

## Luyện tập và lượt ngày

- Mặc định **Luyện tập** bật để thử nhiều lần, không cộng pet vào kho và không tiêu lượt ngày.
- Trên điện thoại dùng camera thật, chạm dòng **Luyện tập** để chuyển chế độ ngày: **một lượt chung cho tất cả cây của tài khoản**, không phải một lượt/cây.
- Chỉ tiêu lượt khi bắt thú hợp lệ và lưu kết quả thành công. Đóng camera, nhận diện thất bại hoặc hủy trước khi bắt không tiêu lượt.
- Ngày tính theo **UTC+7**, kết quả lưu cục bộ và giữ sau khi mở lại app. Khi nối backend, server phải quản lý quota và sinh reward; đồng hồ/JSON trên thiết bị không dùng làm cơ chế chống gian lận.
- Mô phỏng luôn là luyện tập, kể cả khi công tắc chế độ ngày đã tắt.

## Chỉnh gameplay / vibe code tiếp

Chọn `Assets/PAT/Data/HuntSettings.asset` trong Inspector:

| Trường | Mặc định | Tác dụng |
|---|---|---|
| Scan Duration | 10 giây | Thời gian quét hợp lệ cần tích lũy |
| Recognition Hold | 0,8 giây | Giữ nhãn ổn định trước khi khóa |
| Tracking Loss Reset | 12 giây | Hủy phiên nếu mất tracking quá lâu |
| Minimum / Maximum Camera Distance | 0,25 / 3 m | Khoảng cách camera được phép săn |
| Spawn Radius Minimum / Maximum | 0,20 / 0,45 m | Vùng xuất hiện quanh nhãn, tránh tâm chậu |
| Spawn Height | 0,20 m | Độ cao thú bay phía trên vị trí nhãn |
| Plant Center Offset | 0,0,0 | Dịch tâm vùng săn theo vị trí nhãn đặt cạnh chậu |
| Practice By Default | Bật | Mặc định luyện tập khi mở app |

Các file chính:

```text
Assets/PAT/
  Scenes/PlantHunt.unity              Scene mở để Play / build
  Data/HuntSettings.asset             Thông số gameplay
  Data/PlantMarkers.asset             Thư viện 6 ảnh nhận diện
  Markers/                           Ảnh dùng khi build mobile
  Scripts/Core/HuntSession.cs         State machine và tiến trình scan
  Scripts/Core/GardenRepository.cs    Cây, liên kết nhãn, kho, quota, idempotency
  Scripts/Core/LabModels.cs           Model và danh mục pet
  Scripts/Runtime/ArHuntController.cs Camera, marker, anchor, gameplay
  Scripts/Runtime/PetActor.cs         Pet 3D đơn giản bằng primitive
  Scripts/Runtime/LabHud.cs           Canvas native, Khu vườn, kho, debug
  Editor/LabProjectBuilder.cs         Tạo/repair project, nhãn và build
  Tests/                             Kiểm tra nghiệp vụ và luồng chạy
Markers/                             Bản PNG + tài liệu in nhãn
scripts/Run-Lab.ps1                   Setup / chạy test / build bằng terminal
```

Muốn thay pet: sửa danh mục trong `LabModels.cs` và phần hình thể trong `PetActor.cs`, hoặc thay bằng prefab 3D riêng. Thú hiện là vật thể ảo do gameplay sinh ra quanh điểm neo, chưa có AI phát hiện sinh vật thật. Chưa có che khuất theo chiều sâu, nên thú có thể hiện phía trước chậu thay vì bị lá/chậu che.

## Kiểm tra tự động

Đóng **Editor của project ar_PAT** trước khi chạy batch command (có thể giữ Editor project khác đang mở).

```powershell
cd C:\Code\ar_PAT
powershell -ExecutionPolicy Bypass -File .\scripts\Run-Lab.ps1 -Task Setup
powershell -ExecutionPolicy Bypass -File .\scripts\Run-Lab.ps1 -Task AllTests
```

Để chạy PlayMode với graphics và xuất ảnh preview vào `TestResults/`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Run-Lab.ps1 -Task PlayMode -Graphics
```

Có thể dùng **Window → General → Test Runner → EditMode / PlayMode → Run All** trong Editor. Test bao gồm: từ chối nhãn chưa đăng ký, đăng ký nhiều cây, giữ đúng mục tiêu, dừng thời gian khi mất tracking/mở vườn, không cộng pet hoặc lưu hai lần khi retry, giới hạn lượt chung, đổi ngày UTC+7 và lưu dữ liệu sau khi mở lại.

Dữ liệu lab lưu ở `Application.persistentDataPath/pat-ar-lab.json`, có bản `.bak` khi cập nhật. Trên Windows thường là `%USERPROFILE%\AppData\LocalLow\PAT Lab\PAT AR Lab\`. Test dùng file riêng, không sửa vườn đang dùng. Không xóa file save để reset nếu chưa sao lưu dữ liệu cần giữ.

## Tích hợp PAT sau này

Project hiện chạy độc lập. Đầu vào sau này là người dùng, danh sách cây/nhãn và quyền săn; đầu ra là `{ requestId, plantId, petId, completedAtUtc }`. Backend xác nhận quyền sở hữu và quota trước khi cộng pet. Có thể nhúng Unity thành màn hình native toàn màn hình trong app PAT; giai đoạn đó cần native build riêng, không chạy Unity qua Expo Go.

Tài liệu chính thức: [AR Foundation](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.6/manual/index.html), [ARCore Augmented Images](https://developers.google.com/ar/develop/augmented-images), [Unity as a Library](https://docs.unity3d.com/6000.6/Documentation/Manual/UnityasaLibrary.html).
