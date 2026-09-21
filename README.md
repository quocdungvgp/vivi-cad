# VIVI CAD ECOSYSTEM - MASTER ARCHITECTURE & CURRENT STATE

Verified local source: `main` · 21/09/2026.
Đây là bộ định tuyến kiến trúc cốt lõi cho C# AutoCAD Plugin (ViVi CAD). Tất cả Dev và AI (Claude/Cursor) **BẮT BUỘC** đọc file này trước khi viết bất kỳ dòng code nào.

---

## 1. TẦM NHÌN VÀ PHÂN VAI (CORE VISION)
Hệ sinh thái ViVi XPXD hoạt động dựa trên nguyên tắc **Single Source of Truth** (Một nguồn chân lý duy nhất).
- **App ViVi (Firebase): BỘ NÃO.** Chứa dữ liệu gốc (OCR Sổ hồng, Chủ đầu tư), tính toán quy chuẩn mật độ (`density-model.js`), lưu trữ Drive, phân quyền người dùng và **tự động sinh văn bản (Đơn Xin Phép - Word)**.
- **AutoCAD Plugin (C#): XƯỞNG CƠ KHÍ.** Chỉ chịu trách nhiệm đồ họa: Vẽ nét, tính diện tích, bắt lỗi kiến trúc (thang, ban công) và xuất bản vẽ PDF. CAD **KHÔNG** tự bịa dữ liệu pháp lý, **KHÔNG** tự sinh file Word.

---

## 2. BỘ LUẬT TỬ HUYỆT (STRICT RULES)

### 2.1. Luật Code AutoCAD C#
1. **100% Model Space:** Cấm tuyệt đối dùng Layout. Mọi thao tác dàn trang, rải Khung A3, Dim, Text đều nằm trên Model Space.
2. **Validator-First (Trạm kiểm lâm):** Mọi lệnh chạy phải quét kiểm tra file `ViVi_Template.dwt` hiện hành (có đủ Layer, DimStyle, Block gốc không). Thiếu -> Chặn lệnh ngay. **Cấm AI tự sinh layer rác để bù vào.**
3. **No Explode:** Tôn trọng thực thể. Cấm phá vỡ Block, Dim.
4. **Bọc Transaction:** Mọi hàm can thiệp Database CAD phải bọc trong `using (Transaction tr = ...)` và kết thúc bằng `tr.Commit()`. Quên là Fatal Error.

### 2.2. Luật Dữ Liệu & API
1. **Repository Pattern (`GlobalDataManager`):** Mọi Module (M4, M5, M8...) tuyệt đối không tự lấy dữ liệu ngoài, mà phải Get/Set thông qua class `GlobalDataManager`. Giai đoạn 1: Class này đọc/ghi vào XRecord của CAD. Giai đoạn 2: Nó sẽ kết nối API.
2. **App Check Bypass:** App ViVi đã bật reCAPTCHA v3. C# Desktop cấm nhúng Service Account. Giao tiếp API (Module 14) **BẮT BUỘC** phải đi qua **Firebase Cloud Functions** (dùng Admin SDK) bằng `idToken` để xác thực.

---

## 3. BẢN ĐỒ KIẾN TRÚC 14 MODULES

| Nhóm | Module / Chức năng | Đặc tả Luồng thực thi & Nhiệm vụ |
|---|---|---|
| **CORE** | **M0: Core (Trạm điều khiển)** | Chứa `ViViValidator` (Chặn template rác) và `GlobalDataManager` (Trạm trung chuyển Data bằng XRecord). |
| **DRAFTING** | **M1: Lưới Trục & Kích thước** | Rải trục tự nhận DimScale theo Block. |
| | **M2: Tổng MB & Ranh đất** | Vẽ ranh đất, tự động lật Text ghi chú nếu góc > 90 độ. |
| | **M3: Cao độ tự động** | Chèn Dynamic Block `VIVI_CaoDo` (có biến Flip). |
| **COMPUTE** | **M4: Auto Area (Diện tích)** | Thuật toán quét Polyline tàng hình, tự nhận diện lỗ thông tầng (Point in Polygon) để bóc khối lượng. |
| **LAYOUT** | **M5: Dàn trang Bento Box** | Tự rải Block Khung A3, đánh số trang tự động (Visibility: Bìa / Trong). |
| **UI/UX** | **M6: Ribbon & Master Button** | Tích hợp Menu Ribbon. Nút "Chúa": Bóp cò 1 phát chạy liên hoàn M4 -> M5 -> M8 -> M10. |
| **DELEGATED** | **M7: Xuất Word Đơn XPXD** | **[ĐÃ GẠCH BỎ]**. Giao lại toàn quyền cho Firebase App xử lý để đảm bảo tính đồng bộ pháp lý. |
| **AUDIT** | **M8: Cảnh sát pháp lý** | Bắt lỗi kiến trúc: Đụng đầu lọt lòng thang (<2m), ranh nước chảy (mái lòi ra ngoài), vượt mật độ cho phép. |
| **ANNOTATION**| **M9: Smart Tags** | Tự động ném cụm MText ghi chú pháp lý (Snap vào góc Khung A3). |
| **PUBLISH** | **M10: Auto-Plot 1-Click** | Background Plot. Quét tọa độ Khung A3 -> Nạp `ViVi_PlotStyle.ctb` -> Gom N trang ra 1 file PDF duy nhất. |
| **PURGE** | **M11: Máy Lọc Máu** | WBlockClone API. Bơm nét từ bản vẽ thiết kế ngoại lai rác rưởi sang file trắng `ViVi_Template`. Cấm dọn rác trên file gốc. |
| **GIS/BOQ** | **M12: Cầu nối VN-2000** | Copy/Paste X,Y -> Vẽ Polyline ranh. Export ranh từ CAD ra `GeoJSON` cho QField/QGIS. |
| | **M13: Khái toán nhanh (BOQ)**| Lấy diện tích từ M4, nhân hệ số đơn giá -> Sinh bảng Excel/Table báo giá ngay trên CAD. |
| **API SYNC** | **M14: Two-Way API Gateway** | **Giai đoạn 2.** Kéo OCR + Mật độ từ App đắp vào Khung tên CAD. Đẩy Diện tích CAD lên App để trigger sinh Word & ném Drive. |

---

## 4. NHẬT KÝ & TRẠNG THÁI SOURCE CODE (CẬP NHẬT: 21/09/2026)

**[X] ĐÃ HOÀN THÀNH (Cấm AI sửa lại nếu không có lệnh):**
- Quy hoạch xong kiến trúc toàn hệ thống.
- Chốt chiến lược lách App Check bằng Cloud Functions.
- Project C# Class Library đã khởi tạo (`ViViCad/ViViCad.csproj` — net48, x64, AutoCAD 2021).
- **M0 (Core)** — commit `7df1eed`. Build OK, **chưa chạy thử trong AutoCAD**.
  - `ViViValidator` (`ViViCad/Core/ViViValidator.cs`): `ValidateTemplate()`, `Check(Database)` → `ValidationResult` (`MissingLayers`, `MissingBlocks`, `IsValid`, `ToMessage()`); danh sách chuẩn `RequiredLayers`, `RequiredBlocks`.
  - `GlobalDataManager` (`ViViCad/Core/GlobalDataManager.cs`): `GetHoSo()`, `GetHoSo(Database)`, `SaveHoSo(HoSoData)`, `SaveHoSo(Database, HoSoData)`, `SetTongDienTich(Database, double)`, `Repository`.
  - `HoSoData`: `MaHoSo`, `TenCDT`, `DiaChi`, `TongDienTich` (m², `null` = chưa tính), `IsEmpty`.
  - `IHoSoRepository` / `XRecordHoSoRepository`: lưu ở NOD → `VIVI` → Xrecord `HOSO`.
  - Lệnh: `VIVI_INIT` (test Validator + ghi/đọc dữ liệu mẫu), `VIVI_HOSO` (chỉ đọc hồ sơ).
- **Lệnh đọc tên chuẩn** — commit `42b3ba7` (21/09). Build OK, **chưa chạy thử trong AutoCAD**.
  - `TemplateInfo` (`ViViCad/Core/TemplateInfo.cs`): `Build(Database)` → báo cáo văn bản gồm tham số bản vẽ (DIMSCALE, LTSCALE, INSUNITS, MEASUREMENT, Layer/DimStyle/TextStyle hiện hành), Layer (màu, nét, độ dày, đóng băng/khoá/tắt/không in), DimStyle (DIMSCALE, DIMTXT, DIMASZ, DIMLFAC, số lẻ, kiểu chữ), TextStyle (font, cao, rộng), Block gọi được bằng tên (+ block động, XREF, **tag thuộc tính**) và mục đối chiếu với `ViViValidator`.
  - `TemplateCommands` (`ViViCad/Commands/TemplateCommands.cs`): lệnh **`VIVI_KIEM_TEMPLATE`** — in tóm tắt ra dòng lệnh, ghi báo cáo đầy đủ ra `<tên bản vẽ>-vivi-template-info.txt` **cạnh file DWG** (bản vẽ chưa lưu thì ra Desktop), UTF-8 **có BOM**.
  - Lệnh này **cố ý không gọi `ValidateTemplate()`** ở đầu — nó sinh ra để soi cả bản vẽ chưa đạt chuẩn, và chỉ đọc nên không có gì để chặn.

**[ ] ĐANG LÀM / TREO (Focus cho phiên hiện tại):**
- **Chạy `VIVI_KIEM_TEMPLATE` trên `ViVi_Template.dwt`** rồi dán file kết quả vào chat. Đây là chốt chặn của hai việc dưới — làm cái này trước.
- **Chạy thử M0 trong AutoCAD:** `NETLOAD` → `VIVI_INIT` trên bản vẽ thử → lưu, đóng, mở lại → `VIVI_HOSO`.
- **Treo (đã có đường gỡ):** Validator chưa kiểm DimStyle (luật 2.1.2) — chờ tên DimStyle thật từ báo cáo trên.
- **Viết M1 (Lưới trục):** chờ **tên layer trục** + **tên block đầu trục** thật. `RequiredLayers` hiện mới có `VIVI_Dim`, `VIVI_Tuong`, `VIVI_KhungVien`; `RequiredBlocks` mới có `VIVI_KhungTen_A3` — chưa có gì cho trục, mà luật 5.2 cấm bịa tên còn luật 2.1.2 cấm tự tạo layer để vá.

### Nhật ký phiên 21/09/2026

- Thêm lệnh `VIVI_KIEM_TEMPLATE` (commit `42b3ba7`) — `dotnet build` PASS, 0 warning.
- **Vì sao làm cái này trước M1:** M1 cần gọi tên layer trục/block đầu trục, cái treo DimStyle cũng chỉ thiếu đúng một danh sách tên. Đoán tên là gãy luật 5.2, mà gãy thì Validator chặn lệnh ngay — nên bỏ 1 việc nhỏ để có tên thật rẻ hơn viết M1 rồi sửa lại tên khắp nơi.
- **Bẫy đã dính khi build, ghi để khỏi mất thời gian lại:** có `using Autodesk.AutoCAD.Runtime` thì viết `catch (Exception ex)` trần là **lỗi biên dịch CS0104** — AutoCAD có `Runtime.Exception` trùng tên `System.Exception`. Phải ghi rõ `catch (System.Exception ex)` (bắt cả hai, vì lỗi AutoCAD cũng kế thừa từ nó).
- Bẫy thứ hai đã chặn trước: mỗi lớp lệnh mới phải tự thêm `[assembly: CommandClass(typeof(...))]`. Thiếu là NETLOAD xong gõ lệnh ra "Unknown command", nhìn y như build hỏng.
- Báo cáo ghi UTF-8 **có BOM** vì Notepad thiếu BOM sẽ hiện tiếng Việt thành ký tự rác.
- `codemau.py` (đọc DXF bằng `ezdxf`) coi như **không cần nữa**: lệnh này đọc thẳng bản vẽ đang mở, không phải xuất DXF, không cần Python. Bản `codemau.py` trong thư mục App ViVi vẫn đang là khung rỗng (dòng gọi hàm bị comment).
- Máy đang làm có sẵn AutoCAD 2021 + .NET SDK + `csc.exe` của Visual Studio 18 → build ra `ViViCad/bin/Debug/net48/ViViCad.dll` bình thường.

### Nhật ký phiên 18/09/2026 (máy công ty)

**Đã làm theo thứ tự:**
1. Tạo repo **private** `github.com/quocdungvgp/vivi-cad`, tách hẳn khỏi repo App ViVi (`vividichvuCPXD`).
2. Commit `7df1eed` — M0 Core: `ViViValidator`, `GlobalDataManager`, lệnh `VIVI_INIT` / `VIVI_HOSO`.
3. Commit `2487582` — `.gitignore` (chặn `bin/`, `obj/`, khoá Firebase, `*.dwg`/`*.dxf`/`*.pdf` có dữ liệu khách; **không** chặn `ViVi_Template.dwt`) và `.gitattributes` (đánh dấu `.dwt`/`.dwg`/`.dll`/`.ctb`/`.pdf` là nhị phân).
4. Commit `e34e07f` — `README.md` này + `CLAUDE.md` (một dòng trỏ về README).

**Quyết định kỹ thuật đã chốt trong M0 (đừng đảo lại nếu không có lý do mới):**
- Lưu hồ sơ ở **Named Objects Dictionary** (`VIVI` → Xrecord `HOSO`), không ở Extension Dictionary của một đối tượng — xoá đối tượng là mất dữ liệu.
- Xrecord lưu theo cặp **khoá → giá trị**, có số phiên bản schema ở đầu → thêm trường mới ở GĐ2 không hỏng file cũ.
- `GlobalDataManager` **không giữ cache static**: AutoCAD mở nhiều bản vẽ cùng lúc, cache chung làm hồ sơ file A dính sang file B.
- Validator **không dùng `SymbolTable.Has()`** (vẫn trả true với layer vừa bị xoá trong phiên); duyệt bảng, so tên không phân biệt hoa/thường.
- `TongDienTich` lưu kiểu số thực, đơn vị **m²**; `null` = chưa tính, khác 0.
- `Save` tự khoá tài liệu (`LockDocument`) để sau này gọi được từ Ribbon/palette (M6).
- M14 để **Giai đoạn 2**; GĐ1 dữ liệu hồ sơ nhập tay qua `GlobalDataManager`.

**Môi trường build:**
- AutoCAD **2021** → `net48`, x64. Đường dẫn AutoCAD nằm ở `<AcadDir>` trong `ViViCad.csproj`. Máy khác là AutoCAD **2025+** thì phải chuyển sang .NET 8 (code M0 không đổi).
- Máy công ty: trình biên dịch của .NET SDK 10 lỗi *"Windows doesn't fully support CET"* → `.csproj` tự dùng `csc.exe` của Visual Studio 18 **nếu có**. Máy không có VS 18 thì bỏ qua, build bình thường.
- Build: `dotnet build ViViCad/ViViCad.csproj` → ra `ViViCad/bin/Debug/net48/ViViCad.dll` → trong AutoCAD gõ `NETLOAD`.

**Cách làm việc owner đã dặn:**
- File `.md` **ban đầu** do owner tự viết/đưa nội dung — AI không tự tạo file MD mới.
- Nhưng **khi đã bắt đầu làm việc**, AI **bắt buộc tự cập nhật** các file MD đang có (README mục 4) sau mỗi việc xong, không đợi nhắc (luật 5.3).
- Làm xong việc là AI **tự commit + đẩy lên GitHub**, không cần owner yêu cầu.
- Đổi máy: ngồi vào là `git pull` trước.

**Chưa có trên GitHub:** `codemau.py` (đọc Layer/TextStyle/DimStyle của DXF bằng Python `ezdxf`) chỉ nằm trong thư mục App ViVi ở máy công ty.

---

## 5. LUẬT TƯƠNG TÁC CHO AI (CURSOR / CLAUDE INSTRUCTIONS)
> **AI LƯU Ý: Đây là `.cursorrules` ngầm định. Mày phải tuân thủ tuyệt đối.**

1. **Chỉ làm theo Next Step:** Chỉ viết code cho task đang được đánh dấu ở mục ĐANG LÀM. Không code lố sang Module khác.
2. **Cấm bịa API:** Gọi đúng tên Layer/Block đã khai báo trong bảng. Nếu Fatal Error AutoCAD, không được tự mò random, phải yêu cầu User cung cấp Exception Log.
3. **Viết tới đâu, Lưu tới đó (Auto-Update):** Sau khi hoàn thành xong một hàm, một Class hoặc một Module, mày **BẮT BUỘC** phải tự động mở file này (`README.md`) ra, sửa mục "4. NHẬT KÝ & TRẠNG THÁI" (Chuyển task thành `[X]`, liệt kê tên Public Methods vừa viết, và đẩy task tiếp theo lên). Không được đợi User nhắc.
