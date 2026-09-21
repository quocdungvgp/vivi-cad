using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace ViViCad.Core
{
    /// <summary>
    /// Đọc tên chuẩn đang có thật trong bản vẽ: Layer / DimStyle / TextStyle / Block (+ tag thuộc tính).
    ///
    /// Sinh ra để chấm dứt việc đoán tên: luật 5.2 cấm bịa tên Layer/Block, mà danh sách trong
    /// ViViValidator lại phải khớp từng ký tự với ViVi_Template.dwt. Mở template rồi chạy lệnh này
    /// là có danh sách thật để dán vào RequiredLayers/RequiredBlocks và để M1 gọi đúng tên.
    ///
    /// TUYỆT ĐỐI chỉ đọc: không tạo, không sửa, không xoá bản ghi nào. Transaction vẫn Commit()
    /// theo quy ước chung (luật 2.1.4) — không có gì để ghi nên Commit ở đây là rẻ nhất.
    /// </summary>
    public static class TemplateInfo
    {
        /// <summary>Dựng báo cáo dạng văn bản thuần cho bản vẽ đang truyền vào.</summary>
        public static string Build(Database db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            var sb = new StringBuilder();
            sb.AppendLine("=== ViVi — TÊN CHUẨN ĐANG CÓ TRONG BẢN VẼ ===");
            sb.AppendLine("Thời điểm đọc: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sb.AppendLine();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                ThamSoBanVe(db, tr, sb);
                DanhSachLayer(db, tr, sb);
                DanhSachDimStyle(db, tr, sb);
                DanhSachTextStyle(db, tr, sb);
                DanhSachBlock(db, tr, sb);
                DoiChieuValidator(db, sb);

                tr.Commit();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Biến hệ thống quyết định tỷ lệ chữ/Dim. M1 phải nhận DimScale từ đây chứ không hằng số hoá.
        /// INSUNITS nói bản vẽ đang tính bằng mm hay m — sai chỗ này là M4 trả diện tích lệch 1.000.000 lần.
        /// </summary>
        private static void ThamSoBanVe(Database db, Transaction tr, StringBuilder sb)
        {
            sb.AppendLine("0. THAM SỐ BẢN VẼ");
            sb.AppendLine("   - DIMSCALE hiện hành : " + db.Dimscale.ToString("0.###"));
            sb.AppendLine("   - LTSCALE            : " + db.Ltscale.ToString("0.###"));
            sb.AppendLine("   - INSUNITS           : " + db.Insunits + " (Millimeters/Meters... — đơn vị chèn block)");
            sb.AppendLine("   - MEASUREMENT        : " + db.Measurement);
            sb.AppendLine("   - DimStyle hiện hành : " + TenBanGhi(tr, db.Dimstyle));
            sb.AppendLine("   - TextStyle hiện hành: " + TenBanGhi(tr, db.Textstyle));
            sb.AppendLine("   - Layer hiện hành    : " + TenBanGhi(tr, db.Clayer));
            sb.AppendLine();
        }

        private static void DanhSachLayer(Database db, Transaction tr, StringBuilder sb)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var dong = new List<string>();

            foreach (ObjectId id in layerTable)
            {
                if (id.IsErased) continue;
                var lop = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);

                var co = new List<string>();
                if (lop.IsFrozen) co.Add("đóng băng");
                if (lop.IsLocked) co.Add("khoá");
                if (lop.IsOff) co.Add("tắt");
                if (!lop.IsPlottable) co.Add("không in");

                dong.Add(string.Format("   - {0,-28} | màu {1,-3} | nét {2,-14} | dày {3}{4}",
                    lop.Name,
                    lop.Color.IsByAci ? lop.Color.ColorIndex.ToString() : lop.Color.ToString(),
                    TenBanGhi(tr, lop.LinetypeObjectId),
                    lop.LineWeight,
                    co.Count > 0 ? " | " + string.Join(", ", co) : ""));
            }

            sb.AppendLine("1. LAYER (" + dong.Count + ")");
            XuatSapXep(sb, dong);
        }

        /// <summary>
        /// In kèm các biến DIM quan trọng: chốt tên DimStyle xong vẫn phải biết cỡ chữ/mũi tên
        /// thì M1 mới rải Dim ra đúng tỷ lệ. DIMLFAC là hệ số đổi đơn vị đo — lệch là sai số trên bản vẽ in.
        /// </summary>
        private static void DanhSachDimStyle(Database db, Transaction tr, StringBuilder sb)
        {
            var dimTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            var dong = new List<string>();

            foreach (ObjectId id in dimTable)
            {
                if (id.IsErased) continue;
                var ds = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);

                dong.Add(string.Format("   - {0,-28} | DIMSCALE {1,-8} | chữ {2,-7} | mũi tên {3,-7} | DIMLFAC {4,-7} | {5} số lẻ | kiểu chữ {6}",
                    ds.Name,
                    ds.Dimscale.ToString("0.###"),
                    ds.Dimtxt.ToString("0.###"),
                    ds.Dimasz.ToString("0.###"),
                    ds.Dimlfac.ToString("0.###"),
                    ds.Dimdec,
                    TenBanGhi(tr, ds.Dimtxsty)));
            }

            sb.AppendLine("2. DIMSTYLE (" + dong.Count + ")");
            XuatSapXep(sb, dong);
        }

        private static void DanhSachTextStyle(Database db, Transaction tr, StringBuilder sb)
        {
            var styleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            var dong = new List<string>();

            foreach (ObjectId id in styleTable)
            {
                if (id.IsErased) continue;
                var st = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);

                // Style không tên là style của Dim/Mleader sinh ra, không dùng để gọi tên.
                string ten = string.IsNullOrEmpty(st.Name) ? "(không tên)" : st.Name;

                dong.Add(string.Format("   - {0,-28} | font {1,-22} | cao {2,-7} | rộng {3}",
                    ten,
                    string.IsNullOrEmpty(st.FileName) ? "(trống)" : st.FileName,
                    st.TextSize.ToString("0.###"),
                    st.XScale.ToString("0.###")));
            }

            sb.AppendLine("3. TEXTSTYLE (" + dong.Count + ")");
            XuatSapXep(sb, dong);
        }

        /// <summary>
        /// Bỏ qua block bố cục (Model/Paper space) và block vô danh (*U12, *X3 — do hatch/dim sinh ra):
        /// chúng không phải thứ gọi được bằng tên. Block động và tag thuộc tính in kèm vì M3 (VIVI_CaoDo)
        /// và M5 (khung tên A3) phải điền đúng tag, đoán sai tag là điền vào chỗ không tồn tại.
        /// </summary>
        private static void DanhSachBlock(Database db, Transaction tr, StringBuilder sb)
        {
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var dong = new List<string>();

            foreach (ObjectId id in blockTable)
            {
                if (id.IsErased) continue;
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsLayout || btr.IsAnonymous) continue;

                var co = new List<string>();
                if (btr.IsDynamicBlock) co.Add("block động");
                if (btr.IsFromExternalReference) co.Add("XREF");

                var tag = new List<string>();
                foreach (ObjectId entId in btr)
                {
                    var attDef = tr.GetObject(entId, OpenMode.ForRead) as AttributeDefinition;
                    if (attDef != null && !attDef.Constant) tag.Add(attDef.Tag);
                }
                if (tag.Count > 0) co.Add("tag: " + string.Join(", ", tag));

                dong.Add(string.Format("   - {0,-28}{1}", btr.Name, co.Count > 0 ? " | " + string.Join(" | ", co) : ""));
            }

            sb.AppendLine("4. BLOCK gọi được bằng tên (" + dong.Count + ")");
            XuatSapXep(sb, dong);
        }

        /// <summary>Đối chiếu ngay với danh sách bắt buộc, để biết bản vẽ đang mở có phải template chuẩn không.</summary>
        private static void DoiChieuValidator(Database db, StringBuilder sb)
        {
            ValidationResult kq = ViViValidator.Check(db);

            sb.AppendLine("5. ĐỐI CHIẾU VỚI ViViValidator");
            sb.AppendLine("   - Layer bắt buộc: " + string.Join(", ", ViViValidator.RequiredLayers));
            sb.AppendLine("   - Block bắt buộc: " + string.Join(", ", ViViValidator.RequiredBlocks));
            sb.AppendLine(kq.IsValid
                ? "   => ĐỦ. Bản vẽ này qua được Validator."
                : "   => THIẾU. Layer: " + Liet(kq.MissingLayers) + " | Block: " + Liet(kq.MissingBlocks));
            sb.AppendLine();
            sb.AppendLine("Dán toàn bộ nội dung này vào chat để chốt tên cho Validator (DimStyle) và M1 (lưới trục).");
        }

        private static string Liet(List<string> ds) => ds.Count == 0 ? "(đủ)" : string.Join(", ", ds);

        /// <summary>Sắp theo tên, không phân biệt hoa thường — đọc bằng mắt cho dễ dò.</summary>
        private static void XuatSapXep(StringBuilder sb, List<string> dong)
        {
            foreach (string d in dong.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(d);
            sb.AppendLine();
        }

        /// <summary>Lấy tên của một bản ghi bảng ký hiệu qua ObjectId; rỗng/không hợp lệ thì trả chuỗi báo rõ.</summary>
        private static string TenBanGhi(Transaction tr, ObjectId id)
        {
            if (id.IsNull || id.IsErased) return "(không có)";

            var record = tr.GetObject(id, OpenMode.ForRead) as SymbolTableRecord;
            if (record == null) return "(không đọc được)";

            return string.IsNullOrEmpty(record.Name) ? "(không tên)" : record.Name;
        }
    }
}
