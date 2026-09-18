using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ViViCad.Core
{
    /// <summary>Dữ liệu hồ sơ gắn với MỘT bản vẽ.</summary>
    public class HoSoData
    {
        /// <summary>Mã hồ sơ bên App ViVi. GĐ1 nhập tay; GĐ2 (M14) dùng mã này để kéo dữ liệu gốc.</summary>
        public string MaHoSo { get; set; } = "";
        public string TenCDT { get; set; } = "";
        public string DiaChi { get; set; } = "";

        /// <summary>
        /// Tổng diện tích thiết kế, đơn vị m² (M4 tự đổi từ đơn vị bản vẽ sang m² trước khi ghi).
        /// null = chưa tính, khác với 0 m² — M8 không được coi "chưa tính" là "bằng 0".
        /// </summary>
        public double? TongDienTich { get; set; }

        public bool IsEmpty => string.IsNullOrEmpty(MaHoSo) && string.IsNullOrEmpty(TenCDT)
                               && string.IsNullOrEmpty(DiaChi) && !TongDienTich.HasValue;
    }

    /// <summary>
    /// Hợp đồng lưu/đọc hồ sơ. GĐ1: XRecordHoSoRepository (lưu trong DWG).
    /// GĐ2: thêm lớp đọc Firestore theo cùng interface này — M4/M5/M8 không phải sửa.
    /// </summary>
    public interface IHoSoRepository
    {
        HoSoData Load(Database db);
        void Save(Database db, HoSoData data);
    }

    /// <summary>
    /// Trạm trung chuyển dữ liệu DUY NHẤT. Module khác không được tự đọc XRecord hay gọi API.
    ///
    /// Không cache dữ liệu trong biến static: AutoCAD mở nhiều bản vẽ cùng lúc, cache chung sẽ làm
    /// hồ sơ của file A dính sang file B. Mọi hàm đều nhận Database (hoặc lấy bản vẽ hiện hành) và đọc thẳng
    /// từ bản vẽ đó — đọc XRecord rất rẻ.
    /// </summary>
    public static class GlobalDataManager
    {
        public static IHoSoRepository Repository { get; set; } = new XRecordHoSoRepository();

        public static HoSoData GetHoSo(Database db) => Repository.Load(db);

        public static void SaveHoSo(Database db, HoSoData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Repository.Save(db, data);
        }

        /// <summary>M4 dùng: chỉ cập nhật diện tích, giữ nguyên các trường khác.</summary>
        public static void SetTongDienTich(Database db, double dienTichM2)
        {
            if (double.IsNaN(dienTichM2) || double.IsInfinity(dienTichM2) || dienTichM2 < 0)
                throw new ArgumentOutOfRangeException(nameof(dienTichM2), "Diện tích phải là số không âm (m²).");

            HoSoData data = GetHoSo(db);
            data.TongDienTich = dienTichM2;
            SaveHoSo(db, data);
        }

        // Bản tiện dụng cho bản vẽ đang mở.
        public static HoSoData GetHoSo() => GetHoSo(ActiveDatabase());
        public static void SaveHoSo(HoSoData data) => SaveHoSo(ActiveDatabase(), data);

        private static Database ActiveDatabase()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("Không có bản vẽ nào đang mở.");
            return doc.Database;
        }
    }

    /// <summary>
    /// GĐ1: lưu hồ sơ ngay trong file DWG.
    ///
    /// Vị trí: Named Objects Dictionary (NOD) → DBDictionary "VIVI" → Xrecord "HOSO".
    /// Chọn NOD vì đây là chỗ chuẩn cho dữ liệu cấp BẢN VẼ: đi theo file khi lưu, Save As, gửi cho người khác.
    /// Extension Dictionary thì gắn vào từng đối tượng — xoá đối tượng là mất dữ liệu.
    ///
    /// Định dạng: chuỗi cặp (khoá Text, giá trị) + số phiên bản schema ở đầu.
    ///   (90 SchemaVersion) (1 "MaHoSo") (1 giá trị) (1 "TenCDT") (1 giá trị) ... (1 "TongDienTich") (40 số thực)
    /// Lưu theo khoá chứ không theo vị trí: GĐ2 thêm trường mới không làm hỏng file đã lưu ở GĐ1.
    /// Diện tích lưu kiểu Real (40), không lưu chuỗi — tránh lỗi dấu phẩy/chấm theo cài đặt vùng của Windows.
    /// </summary>
    public class XRecordHoSoRepository : IHoSoRepository
    {
        private const string DictName = "VIVI";
        private const string RecordName = "HOSO";
        private const int SchemaVersion = 1;

        private const string KeyMaHoSo = "MaHoSo";
        private const string KeyTenCDT = "TenCDT";
        private const string KeyDiaChi = "DiaChi";
        private const string KeyTongDienTich = "TongDienTich";

        public HoSoData Load(Database db)
        {
            var data = new HoSoData();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);

                if (nod.Contains(DictName))
                {
                    var viviDict = (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);
                    if (viviDict.Contains(RecordName))
                    {
                        var xrec = (Xrecord)tr.GetObject(viviDict.GetAt(RecordName), OpenMode.ForRead);
                        // Xrecord.Data trả về một ResultBuffer MỚI mỗi lần gọi → phải Dispose.
                        using (ResultBuffer rb = xrec.Data)
                        {
                            if (rb != null) Parse(rb.AsArray(), data);
                        }
                    }
                }

                tr.Commit();
            }

            return data;
        }

        public void Save(Database db, HoSoData data)
        {
            // Khoá tài liệu: bắt buộc khi ghi từ ngoài ngữ cảnh lệnh (Ribbon M6, palette).
            // Gọi từ trong CommandMethod thì khoá lồng vẫn an toàn.
            Document doc = AcApp.DocumentManager.GetDocument(db);
            using (doc != null ? doc.LockDocument() : null)
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary viviDict = GetOrCreateViViDict(db, tr);

                using (ResultBuffer rb = Build(data))
                {
                    if (viviDict.Contains(RecordName))
                    {
                        var xrec = (Xrecord)tr.GetObject(viviDict.GetAt(RecordName), OpenMode.ForWrite);
                        xrec.Data = rb; // Ghi đè toàn bộ — không để sót khoá cũ.
                    }
                    else
                    {
                        var xrec = new Xrecord { Data = rb };
                        viviDict.UpgradeOpen();
                        viviDict.SetAt(RecordName, xrec);
                        tr.AddNewlyCreatedDBObject(xrec, true);
                    }
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// Lấy dictionary "VIVI" trong NOD; chưa có thì tạo. Đây là vùng dữ liệu riêng của ViVi,
        /// không phải Layer/Block của template nên được phép tạo.
        /// </summary>
        private static DBDictionary GetOrCreateViViDict(Database db, Transaction tr)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);

            if (nod.Contains(DictName))
                return (DBDictionary)tr.GetObject(nod.GetAt(DictName), OpenMode.ForRead);

            var viviDict = new DBDictionary();
            nod.UpgradeOpen();
            nod.SetAt(DictName, viviDict);
            tr.AddNewlyCreatedDBObject(viviDict, true);
            return viviDict;
        }

        private static ResultBuffer Build(HoSoData data)
        {
            var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.Int32, SchemaVersion),
                new TypedValue((int)DxfCode.Text, KeyMaHoSo), new TypedValue((int)DxfCode.Text, data.MaHoSo ?? ""),
                new TypedValue((int)DxfCode.Text, KeyTenCDT), new TypedValue((int)DxfCode.Text, data.TenCDT ?? ""),
                new TypedValue((int)DxfCode.Text, KeyDiaChi), new TypedValue((int)DxfCode.Text, data.DiaChi ?? ""));

            // Chưa tính diện tích thì không ghi khoá này, để lúc đọc lại vẫn là null chứ không thành 0.
            if (data.TongDienTich.HasValue)
            {
                rb.Add(new TypedValue((int)DxfCode.Text, KeyTongDienTich));
                rb.Add(new TypedValue((int)DxfCode.Real, data.TongDienTich.Value));
            }

            return rb;
        }

        /// <summary>
        /// Đọc theo cặp (khoá, giá trị). Bỏ qua khoá lạ — file lưu bởi bản mới hơn vẫn mở được trên bản cũ.
        /// </summary>
        private static void Parse(TypedValue[] values, HoSoData data)
        {
            int i = 0;
            if (values.Length > 0 && values[0].TypeCode == (int)DxfCode.Int32) i = 1; // bỏ qua SchemaVersion

            for (; i + 1 < values.Length; i += 2)
            {
                string key = values[i].Value as string;
                object value = values[i + 1].Value;

                switch (key)
                {
                    case KeyMaHoSo: data.MaHoSo = value as string ?? ""; break;
                    case KeyTenCDT: data.TenCDT = value as string ?? ""; break;
                    case KeyDiaChi: data.DiaChi = value as string ?? ""; break;
                    case KeyTongDienTich:
                        if (value is double d) data.TongDienTich = d;
                        break;
                }
            }
        }
    }
}
