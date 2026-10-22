# Kiểm tra prototype PAT AR Lab

Ngày thực hiện: 22/10/2026 (Asia/Saigon).

Môi trường: Windows, Unity 6000.6.5f1, AR Foundation / ARCore / ARKit 6.6.2. Mã nguồn C# được biên dịch bằng Unity Editor thật.

## Kết quả

- Setup tạo scene, bộ marker, reference image library và loader Android/iOS thành công.
- **EditMode: 15/15 test đạt.** Kiểm tra scan đủ thời gian hợp lệ, mất tracking, frame stall, khóa đúng cây, nhãn chưa đăng ký, liên kết duy nhất, lưu dữ liệu sau restart, quota chung giữa nhiều cây, idempotency, luyện tập, ngày UTC+7, dữ liệu hỏng, kích thước marker và cấu hình provider.
- **PlayMode: 4/4 test đạt.** Chạy controller + UI thật trong Editor: từ chối nhãn chưa đăng ký rồi mở đúng cây sau khi đăng ký; tạm dừng khi mất tracking/mở Khu vườn; điều khiển chuột qua Input System để ngắm vào collider pet và bắt đúng một lần; xuất ảnh preview màn hình săn/khu vườn.
- Sáu marker `PAT-001` đến `PAT-006` đều đạt **100/100** qua `arcoreimg eval-img` trong package ARCore của Unity.
- `arcoreimg build-db` biên dịch thành công database sáu marker, có kích thước vật lý 0,12 m/ảnh.
- Đã xem ảnh render portrait để kiểm tra bố cục, chữ tiếng Việt, form liên kết nhãn và lớp hiển thị khi mở Khu vườn.

## Tự chạy lại

```powershell
cd C:\Code\ar_PAT
powershell -ExecutionPolicy Bypass -File .\scripts\Run-Lab.ps1 -Task AllTests -Graphics
```

Log ở `Logs/`; báo cáo NUnit XML và ảnh render ở `TestResults/`. Đây là output sinh lại được và đã được đưa vào `.gitignore`.

## Cần xác minh trên điện thoại

Máy hiện chưa cài Android Build Support/SDK/NDK/OpenJDK và chưa có ADB trên PATH. Chưa build APK hoặc kiểm tra camera/AR trên điện thoại thật. iOS cũng chưa được build và chạy trên thiết bị.

Điểm 100/100 của marker là chỉ số chất lượng ảnh tham chiếu, không phải cam kết xác suất nhận diện 100%. Khi có điện thoại, cần kiểm tra bản in đúng 12 cm, ánh sáng, góc nhìn, nhãn bị che, từ chối quyền camera, mất tracking, ra/vào nền, đổi nhãn cây và độ ổn định vị trí thú khi di chuyển.
