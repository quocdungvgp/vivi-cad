using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ViViCad.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

// Khai báo lớp chứa lệnh: AutoCAD chỉ quét lớp này thay vì cả assembly → NETLOAD nhanh hơn.
[assembly: CommandClass(typeof(ViViCad.Commands.CoreCommands))]

namespace ViViCad.Commands
{
    public class CoreCommands
    {
        /// <summary>
        /// Lệnh test CORE: (1) chạy Validator, (2) ghi dữ liệu mẫu, (3) đọc lại và so khớp.
        /// Dữ liệu mẫu ghi thật vào DWG — chỉ chạy trên bản vẽ thử, không chạy trên hồ sơ khách.
        /// </summary>
        [CommandMethod("VIVI_INIT")]
        public void ViViInit()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // Bước 1 — Trạm kiểm lâm. Thiếu chuẩn là dừng, không làm gì tiếp.
            if (!ViViValidator.ValidateTemplate()) return;
            ed.WriteMessage("\n[ViVi] Template hợp lệ.");

            // Bước 2 — Ghi dữ liệu mẫu qua GlobalDataManager (không đụng XRecord trực tiếp).
            var dummy = new HoSoData
            {
                MaHoSo = "TEST-0001",
                TenCDT = "Nguyễn Văn Thử",
                DiaChi = "123 Đường Thử, Phường Bến Thành, TP.HCM",
                TongDienTich = 123.45
            };
            GlobalDataManager.SaveHoSo(db, dummy);

            // Bước 3 — Đọc lại từ bản vẽ và so từng trường.
            HoSoData loaded = GlobalDataManager.GetHoSo(db);
            bool khop = loaded.MaHoSo == dummy.MaHoSo
                        && loaded.TenCDT == dummy.TenCDT
                        && loaded.DiaChi == dummy.DiaChi
                        && loaded.TongDienTich == dummy.TongDienTich;

            ed.WriteMessage("\n[ViVi] Đọc lại từ DWG:");
            ed.WriteMessage("\n  MaHoSo       : " + loaded.MaHoSo);
            ed.WriteMessage("\n  TenCDT       : " + loaded.TenCDT);
            ed.WriteMessage("\n  DiaChi       : " + loaded.DiaChi);
            ed.WriteMessage("\n  TongDienTich : " + (loaded.TongDienTich.HasValue ? loaded.TongDienTich.Value.ToString("0.00") + " m²" : "(chưa tính)"));
            ed.WriteMessage(khop
                ? "\n[ViVi] PASS — ghi/đọc XRecord khớp. Lưu file, đóng, mở lại rồi chạy VIVI_HOSO để kiểm tra dữ liệu còn."
                : "\n[ViVi] FAIL — dữ liệu đọc lại không khớp dữ liệu vừa ghi.");
        }

        /// <summary>Chỉ ĐỌC hồ sơ đang lưu trong bản vẽ — dùng để kiểm tra dữ liệu còn sau khi đóng/mở lại file.</summary>
        [CommandMethod("VIVI_HOSO")]
        public void ViViHoSo()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            HoSoData hs = GlobalDataManager.GetHoSo(doc.Database);
            if (hs.IsEmpty)
            {
                doc.Editor.WriteMessage("\n[ViVi] Bản vẽ này chưa gắn hồ sơ.");
                return;
            }

            doc.Editor.WriteMessage("\n[ViVi] Hồ sơ " + hs.MaHoSo + " — " + hs.TenCDT + " — " + hs.DiaChi
                                    + " — " + (hs.TongDienTich.HasValue ? hs.TongDienTich.Value.ToString("0.00") + " m²" : "chưa tính diện tích"));
        }
    }
}
