using System;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ViViCad.Core;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

// Mỗi lớp lệnh phải tự khai báo: AutoCAD chỉ quét đúng những lớp được đánh dấu.
// Thiếu dòng này là NETLOAD xong gõ lệnh báo "Unknown command".
[assembly: CommandClass(typeof(ViViCad.Commands.TemplateCommands))]

namespace ViViCad.Commands
{
    public class TemplateCommands
    {
        /// <summary>
        /// In ra toàn bộ tên chuẩn (Layer / DimStyle / TextStyle / Block + tag) của bản vẽ đang mở
        /// và ghi ra file .txt cạnh bản vẽ để dán lại vào chat.
        ///
        /// KHÔNG gọi ValidateTemplate() ở đầu như các lệnh khác: lệnh này sinh ra để soi cả bản vẽ
        /// CHƯA đạt chuẩn (biết thiếu gì mà bù). Nó chỉ đọc, không vẽ, nên không có gì để chặn.
        /// </summary>
        [CommandMethod("VIVI_KIEM_TEMPLATE")]
        public void ViViKiemTemplate()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            string baoCao = TemplateInfo.Build(doc.Database);

            // In gọn ra dòng lệnh: bản đầy đủ nằm trong file, in hết ra đây sẽ dính phân trang "Press ENTER".
            ValidationResult kq = ViViValidator.Check(doc.Database);
            ed.WriteMessage("\n[ViVi] Đã đọc tên chuẩn của bản vẽ: " + doc.Name);
            ed.WriteMessage(kq.IsValid
                ? "\n[ViVi] Bản vẽ ĐỦ Layer/Block bắt buộc — qua được Validator."
                : "\n[ViVi] THIẾU — Layer: " + Gom(kq.MissingLayers) + " | Block: " + Gom(kq.MissingBlocks));

            string duongDan = GhiRaFile(doc, baoCao, ed);
            if (duongDan != null)
                ed.WriteMessage("\n[ViVi] Báo cáo đầy đủ: " + duongDan + "\n[ViVi] Mở file đó, copy hết nội dung rồi dán vào chat.");
        }

        private static string Gom(System.Collections.Generic.List<string> ds)
            => ds.Count == 0 ? "(đủ)" : string.Join(", ", ds);

        /// <summary>
        /// Ghi cạnh bản vẽ; bản vẽ chưa lưu (Drawing1.dwg) thì không có thư mục thật nên ghi ra Desktop.
        /// UTF-8 CÓ BOM: thiếu BOM là Notepad mở ra tiếng Việt thành ký tự rác, owner tưởng lệnh hỏng.
        /// </summary>
        private static string GhiRaFile(Document doc, string noiDung, Editor ed)
        {
            try
            {
                string thuMuc = null;
                try { thuMuc = Path.GetDirectoryName(doc.Name); } catch { /* tên bản vẽ chưa lưu */ }

                if (string.IsNullOrEmpty(thuMuc) || !Directory.Exists(thuMuc))
                    thuMuc = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                string ten = Path.GetFileNameWithoutExtension(doc.Name);
                if (string.IsNullOrEmpty(ten)) ten = "BanVe";

                string duongDan = Path.Combine(thuMuc, ten + "-vivi-template-info.txt");
                File.WriteAllText(duongDan, noiDung, new UTF8Encoding(true));
                return duongDan;
            }
            // Phải ghi rõ System.Exception: có using Autodesk.AutoCAD.Runtime thì chữ "Exception" trần
            // là lỗi biên dịch CS0104 (Runtime.Exception của AutoCAD trùng tên). Bắt System.Exception là
            // bắt luôn cả hai, vì lỗi của AutoCAD cũng kế thừa từ nó.
            catch (System.Exception ex)
            {
                // Không có quyền ghi thì vẫn phải cho owner lấy được nội dung → in thẳng ra dòng lệnh.
                ed.WriteMessage("\n[ViVi] Không ghi được file (" + ex.Message + "). Nội dung đầy đủ:\n" + noiDung);
                return null;
            }
        }
    }
}
