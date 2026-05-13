using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace WinForm部署yolo.ONNX推理模型
{
    internal class DAL
    {
    }

    public class DefectRecord
    {
        public int Id { get; set; }
        public string DetectionTime { get; set; }
        public string DefectType { get; set; }
        public double Confidence { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string ImagePath { get; set; }
        public string FrameInfo { get; set; }
        public int BoundingBoxX { get; set; }
        public int BoundingBoxY { get; set; }
        public int BoundingBoxWidth { get; set; }
        public int BoundingBoxHeight { get; set; }
    }

    public class DefectRecordDAL
    {
        private string connectionString;
        private string dbPath;

        public DefectRecordDAL(string dbPath)
        {
            this.dbPath = dbPath;
            connectionString = $"Data Source={dbPath};Version=3;";
            CreateTableIfNotExists();
        }

        private void CreateTableIfNotExists()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS DefectRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DetectionTime TEXT NOT NULL,
                    DefectType TEXT NOT NULL,
                    Confidence REAL NOT NULL,
                    Latitude REAL,
                    Longitude REAL,
                    ImagePath TEXT,
                    FrameInfo TEXT,
                    BoundingBoxX INTEGER,
                    BoundingBoxY INTEGER,
                    BoundingBoxWidth INTEGER,
                    BoundingBoxHeight INTEGER
                )";
                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void Insert(DefectRecord record)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string insertQuery = @"
                INSERT INTO DefectRecords 
                (DetectionTime, DefectType, Confidence, Latitude, Longitude, ImagePath, FrameInfo, BoundingBoxX, BoundingBoxY, BoundingBoxWidth, BoundingBoxHeight) 
                VALUES (@time, @type, @conf, @lat, @lon, @img, @frame, @bx, @by, @bw, @bh)";
                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@time", record.DetectionTime);
                    command.Parameters.AddWithValue("@type", record.DefectType);
                    command.Parameters.AddWithValue("@conf", record.Confidence);
                    command.Parameters.AddWithValue("@lat", (object)record.Latitude ?? DBNull.Value);
                    command.Parameters.AddWithValue("@lon", (object)record.Longitude ?? DBNull.Value);
                    command.Parameters.AddWithValue("@img", (object)record.ImagePath ?? DBNull.Value);
                    command.Parameters.AddWithValue("@frame", (object)record.FrameInfo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@bx", record.BoundingBoxX);
                    command.Parameters.AddWithValue("@by", record.BoundingBoxY);
                    command.Parameters.AddWithValue("@bw", record.BoundingBoxWidth);
                    command.Parameters.AddWithValue("@bh", record.BoundingBoxHeight);
                    command.ExecuteNonQuery();
                }
            }
        }

        public List<DefectRecord> GetAllRecords()
        {
            var records = new List<DefectRecord>();
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM DefectRecords ORDER BY Id DESC";
                using (var command = new SQLiteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new DefectRecord
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            DetectionTime = reader["DetectionTime"].ToString(),
                            DefectType = reader["DefectType"].ToString(),
                            Confidence = Convert.ToDouble(reader["Confidence"]),
                            Latitude = reader["Latitude"] == DBNull.Value ? null : (double?)Convert.ToDouble(reader["Latitude"]),
                            Longitude = reader["Longitude"] == DBNull.Value ? null : (double?)Convert.ToDouble(reader["Longitude"]),
                            ImagePath = reader["ImagePath"] == DBNull.Value ? null : reader["ImagePath"].ToString(),
                            FrameInfo = reader["FrameInfo"] == DBNull.Value ? null : reader["FrameInfo"].ToString(),
                            BoundingBoxX = Convert.ToInt32(reader["BoundingBoxX"]),
                            BoundingBoxY = Convert.ToInt32(reader["BoundingBoxY"]),
                            BoundingBoxWidth = Convert.ToInt32(reader["BoundingBoxWidth"]),
                            BoundingBoxHeight = Convert.ToInt32(reader["BoundingBoxHeight"])
                        });
                    }
                }
            }
            return records;
        }

        public void DeleteRecord(int id)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "DELETE FROM DefectRecords WHERE Id = @id";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void ClearAllRecords()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "DELETE FROM DefectRecords";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public int GetRecordCount()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM DefectRecords";
                using (var command = new SQLiteCommand(query, connection))
                {
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public string ExportToCsv(string exportPath)
        {
            var records = GetAllRecords();
            var sb = new StringBuilder();
            sb.AppendLine("序号,检测时间,缺陷类型,置信度,纬度,经度,图像路径,帧信息,边界框X,边界框Y,边界框宽度,边界框高度");
            foreach (var record in records)
            {
                sb.AppendLine($"{record.Id},{record.DetectionTime},{record.DefectType},{record.Confidence:F4},{record.Latitude},{record.Longitude},{record.ImagePath},{record.FrameInfo},{record.BoundingBoxX},{record.BoundingBoxY},{record.BoundingBoxWidth},{record.BoundingBoxHeight}");
            }
            File.WriteAllText(exportPath, sb.ToString(), Encoding.UTF8);
            return exportPath;
        }

        public string ExportToJson(string exportPath)
        {
            var records = GetAllRecords();
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                var comma = i < records.Count - 1 ? "," : "";
                string latStr = r.Latitude.HasValue ? r.Latitude.Value.ToString() : "null";
                string lonStr = r.Longitude.HasValue ? r.Longitude.Value.ToString() : "null";
                string imgPath = r.ImagePath == null ? "null" : "\"" + r.ImagePath.Replace("\\", "\\\\") + "\"";
                sb.AppendLine("{\"id\":" + r.Id + ",\"detectionTime\":\"" + r.DetectionTime + "\",\"defectType\":\"" + r.DefectType + "\",\"confidence\":" + r.Confidence.ToString("F4") + ",\"latitude\":" + latStr + ",\"longitude\":" + lonStr + ",\"imagePath\":" + imgPath + ",\"frameInfo\":\"" + (r.FrameInfo ?? "") + "\",\"boundingBox\":{\"x\":" + r.BoundingBoxX + ",\"y\":" + r.BoundingBoxY + ",\"width\":" + r.BoundingBoxWidth + ",\"height\":" + r.BoundingBoxHeight + "}}" + comma);
            }
            sb.AppendLine("]");
            File.WriteAllText(exportPath, sb.ToString(), Encoding.UTF8);
            return exportPath;
        }
    }
}
