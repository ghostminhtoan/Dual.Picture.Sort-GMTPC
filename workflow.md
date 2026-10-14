# Workflow Chuẩn - Dual Picture Sort - GMTPC (Avalonia C#)

Tài liệu quy tắc, tiêu chuẩn kỹ thuật và luồng xử lý cho dự án Dual Picture Sort - GMTPC.

## 1. Quy tắc cốt lõi
- Luôn trả lời bằng tiếng Việt.
- Luôn luôn sử dụng skill `teamwork-preview-protocol` và `prompt-optimizer` cho mọi tác vụ code.
- YAGNI & KISS: Thiết kế tối giản, sạch sẽ, giải quyết trực diện bài toán sắp xếp và kiểm tra ảnh 2 panel song song. Tận dụng tối đa Avalonia UI controls và .NET standard libraries.
- Kiến trúc MVVM: Dùng CommunityToolkit.Mvvm (ObservableObject, RelayCommand...).
- Build dứt điểm đạt `0 error, 0 warning`.
- Sau khi code xong: tự động commit local và cung cấp mã hash commit cho người dùng.
- TUYỆT ĐỐI CHỈ COMMIT LOCAL, KHÔNG ĐƯỢC PUSH GITHUB trừ khi người dùng yêu cầu rõ ràng.
- Luôn luôn đưa trạng thái sau khi code:
  - commit local: *<mã hash>*
  - commit github: "không"
  - path exe: *<đường dẫn file exe sau build>*

## 2. Kiến trúc & Nền tảng
- Nền tảng: Avalonia UI (v12.x / net10.0), chạy đa nền tảng: Desktop (Windows, Linux, macOS) và Mobile (Android).
- Projects:
  - `DualPictureSort/DualPictureSort.csproj`: Shared core UI & ViewModels (Views, ViewModels, Models, Services, Styles).
  - `DualPictureSort.Desktop/DualPictureSort.Desktop.csproj`: Entry point chạy trên Desktop (Windows, Linux).
  - `DualPictureSort.Android/DualPictureSort.Android.csproj`: Entry point chạy trên Android.
- Build target cho Desktop:
  - `dotnet build DualPictureSort.Desktop/DualPictureSort.Desktop.csproj -c Release`
  - Output exe: `DualPictureSort.Desktop\bin\Release\net10.0\DualPictureSort.Desktop.exe`

## 3. Luồng nghiệp vụ & Tiêu chuẩn giao diện
- **Chức năng chính**: Sắp xếp/lọc ảnh vào thư mục sorted.
  - Người dùng chọn thư mục gốc (ví dụ: `comic\ranma fanart\`).
  - Thư mục đích tự động xác định là `[TênThưMụcGốc]-sorted` (ví dụ: `comic\ranma fanart\ranma fanart-sorted\` hoặc cho phép chọn thư mục đích).
  - Giao diện gồm 2 panel song song (Dual Panel / Side-by-side):
    - **Panel bên trái**: Thư mục gốc (chưa lọc / còn lại). Hiển thị danh sách thumbnail / tên file / kích thước / tổng số ảnh.
    - **Panel bên phải**: Thư mục đã lọc ảnh (`...-sorted`). Hiển thị danh sách ảnh đã chuyển sang, đếm số lượng.
  - Khi người dùng click/chọn ảnh (hoặc bấm phím tắt / nút chuyển): Ảnh sẽ tự động di chuyển (hoặc sao chép tùy chọn, mặc định di chuyển) sang folder sorted.
  - Hỗ trợ di chuyển ngược lại (Unsort / Move back) nếu chọn nhầm.
  - Có khung Preview ảnh lớn xem chi tiết chất lượng cao khi click vào ảnh ở bất kỳ panel nào (hoặc zoom, so sánh).
  - Giao diện chuẩn dark mode hiện đại, mượt mà, hỗ trợ responsive thích ứng cả màn hình Desktop và Mobile (Android).

