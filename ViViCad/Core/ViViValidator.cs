using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ViViCad.Core
{
    /// <summary>
    /// Trạm kiểm lâm: mọi lệnh ViVi phải gọi ValidateTemplate() đầu tiên.
    /// Chỉ ĐỌC bản vẽ, tuyệt đối không tạo Layer/Block để vá chỗ thiếu.
    /// </summary>
    public static class ViViValidator
    {
        // Danh sách chuẩn của ViVi_Template.dwt. Thêm Layer/Block mới thì sửa ở đây, không rải tên cứng khắp nơi.
        public static readonly string[] RequiredLayers = { "VIVI_Dim", "VIVI_Tuong", "VIVI_KhungVien" };
        public static readonly string[] RequiredBlocks = { "VIVI_KhungTen_A3" };

        /// <summary>
        /// Kiểm tra bản vẽ hiện hành. Trả về true nếu đủ chuẩn.
        /// Thiếu thứ gì thì hiện Alert liệt kê hết và trả về false — lệnh gọi phải return ngay.
        /// </summary>
        public static bool ValidateTemplate()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            ValidationResult result = Check(doc.Database);
            if (result.IsValid) return true;

            AcApp.ShowAlertDialog(result.ToMessage());
            doc.Editor.WriteMessage("\n[ViVi] Lệnh bị chặn: bản vẽ không đúng ViVi_Template.dwt.");
            return false;
        }

        /// <summary>
        /// Phần kiểm tra thuần, không hiện giao diện — để module khác (M8, nút "Chúa" M6) tái dùng kết quả.
        /// </summary>
        public static ValidationResult Check(Database db)
        {
            var result = new ValidationResult();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                result.MissingLayers.AddRange(FindMissing(layerTable, RequiredLayers));
                result.MissingBlocks.AddRange(FindMissing(blockTable, RequiredBlocks));

                // Chỉ đọc nhưng vẫn Commit: rẻ hơn Abort và đúng quy ước "mọi Transaction kết thúc bằng Commit()".
                tr.Commit();
            }

            return result;
        }

        /// <summary>
        /// Thuật toán: duyệt bảng một lần, gom tên các bản ghi CÒN SỐNG vào HashSet không phân biệt hoa/thường
        /// (AutoCAD coi "VIVI_Dim" và "vivi_dim" là một), rồi đối chiếu danh sách bắt buộc.
        ///
        /// Không dùng SymbolTable.Has(name): hàm này vẫn trả true cho bản ghi đã bị xoá (erased) trong phiên,
        /// tức người dùng vừa PURGE layer xong mà validator vẫn báo đủ.
        /// Phép duyệt foreach mặc định đã bỏ qua bản ghi erased, nên kiểm tra thêm IsErased chỉ để chắc chắn.
        /// </summary>
        private static IEnumerable<string> FindMissing(SymbolTable table, IEnumerable<string> required)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId id in table)
            {
                if (id.IsErased) continue;
                // Đọc tên qua ObjectId khi đã có Transaction ngoài — không mở Transaction lồng.
                var record = (SymbolTableRecord)id.GetObject(OpenMode.ForRead);
                existing.Add(record.Name);
            }

            return required.Where(name => !existing.Contains(name));
        }
    }

    /// <summary>Kết quả kiểm tra template.</summary>
    public class ValidationResult
    {
        public List<string> MissingLayers { get; } = new List<string>();
        public List<string> MissingBlocks { get; } = new List<string>();

        public bool IsValid => MissingLayers.Count == 0 && MissingBlocks.Count == 0;

        public string ToMessage()
        {
            var lines = new List<string> { "BẢN VẼ KHÔNG ĐÚNG ViVi_Template.dwt — LỆNH ĐÃ BỊ CHẶN.", "" };

            if (MissingLayers.Count > 0)
                lines.Add("Thiếu Layer: " + string.Join(", ", MissingLayers));
            if (MissingBlocks.Count > 0)
                lines.Add("Thiếu Block: " + string.Join(", ", MissingBlocks));

            lines.Add("");
            lines.Add("Hãy mở bản vẽ mới từ ViVi_Template.dwt, hoặc chèn đúng Layer/Block từ template.");
            lines.Add("ViVi không tự tạo Layer/Block để vá.");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
