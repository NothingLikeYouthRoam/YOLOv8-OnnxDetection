using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinForm部署yolo.ONNX推理模型
{
    public partial class Form1 : Form
    {
        private InferenceSession inferenceSession;
        private int width = 640;
        private int height = 640;
        private float fidence = 0.3f;
        private DefectRecordDAL dal;
        private string outputDirectory;
        private List<DefectRecord> currentDetectionResults = new List<DefectRecord>();
        private double? currentLatitude;
        private double? currentLongitude;
        private string[] classNames = { "D00", "D10", "D20", "D40" };
        
        private VideoWriter videoWriter;
        private string keyFramesDir;
        private int videoFrameCount;
        private bool currentFrameHasDetection;
        
        public Form1()
        {
            InitializeComponent();
            InitializeDefectDAL();
            InitializeDefaultOutputPath();
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            UpdateRecordCount();
        }

        private void InitializeDefectDAL()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(appDir, "defect_records.db");
            dal = new DefectRecordDAL(dbPath);
        }

        private void InitializeDefaultOutputPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            outputDirectory = Path.Combine(appDir, "DetectionResults");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            textBox6.Text = outputDirectory;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "选择ONNX模型";
            openFileDialog.InitialDirectory = "d:/";
            openFileDialog.Filter = "ONNX文件|*.onnx";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = false;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = openFileDialog.FileName;
                inferenceSession = new InferenceSession(openFileDialog.FileName);
            }
        }

        private void button5_Click(object sender, System.EventArgs e)
        {
            if (inferenceSession == null)
            {
                MessageBox.Show("当前未选择模型");
            }else if (textBox5.Text == "")
            {
                MessageBox.Show("请选择图片");
            }else
            {
                RadioButton selectedRadio = groupBox2.Controls.
                    OfType<RadioButton>().FirstOrDefault(r => r.Checked);
                switch (selectedRadio.Name)
                {
                    case "radioButton1":
                        ReasoningPicture();
                        break;
                    case "radioButton2":
                        ProcessVideos();
                        break;
                    case "radioButton3":
                        break;
                }

            }
        }

        private async void ProcessVideos()
        {
            button5.Enabled = false;
            button5.Text = "处理中...";
            button5.BackColor = System.Drawing.Color.White;
            try
            {
                progressBar1.Value = 0;
                progressBar1.Maximum = 100;
                await ChuLiShiPing();
            }
            finally
            {
                button5.Enabled = true;
                button5.Text = "开始推理";
                button5.BackColor = System.Drawing.Color.FromArgb(43, 60, 78);
            }
        }

        private async Task ChuLiShiPing()
        {
            await Task.Run(() =>
            {
                string videoName = Path.GetFileNameWithoutExtension(textBox5.Text);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                keyFramesDir = Path.Combine(outputDirectory, videoName + "_keyframes_" + timestamp);
                string videoPath = Path.Combine(outputDirectory, videoName + "_processed_" + timestamp + ".avi");
                
                if (checkBox1.Checked)
                {
                    Directory.CreateDirectory(keyFramesDir);
                }

                using (VideoCapture capture = new VideoCapture(textBox5.Text))
                {
                    int totalFrames = (int)capture.Get(VideoCaptureProperties.FrameCount);
                    int frameWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                    int frameHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
                    double fps = capture.Get(VideoCaptureProperties.Fps);

                    if (checkBox1.Checked)
                    {
                        videoWriter = new VideoWriter(videoPath, VideoWriter.FourCC('M', 'J', 'P', 'G'), fps, new Size(frameWidth, frameHeight));
                    }

                    List<Mat> res = new List<Mat>();
                    Mat frame = new Mat();
                    Mat processedFrame = new Mat();
                    int i = 0;
                    int keyFrameCount = 0;

                    while (capture.Read(frame) && !frame.Empty())
                    {
                        processedFrame = frame.Clone();
                        currentFrameHasDetection = false;
                        videoFrameCount = i;
                        TuiLiDanZhangTuPian(processedFrame, null, true);
                        res.Add(processedFrame);

                        if (checkBox1.Checked && videoWriter.IsOpened())
                        {
                            videoWriter.Write(processedFrame);
                            if (currentFrameHasDetection)
                            {
                                string keyFramePath = Path.Combine(keyFramesDir, "keyframe_" + i.ToString("D6") + ".jpg");
                                Cv2.ImWrite(keyFramePath, processedFrame);
                                keyFrameCount++;
                            }
                        }

                        frame = new Mat();
                        i++;
                        if (i % Math.Max(1, totalFrames / 100) == 0)
                        {
                            int progress = (i * 100) / totalFrames;
                            Invoke(new Action(() => {
                                progressBar1.Value = progress;
                                label9.Text = "处理中... " + progress + "%";
                            }));
                        }
                    }

                    if (checkBox1.Checked && videoWriter.IsOpened())
                    {
                        videoWriter.Release();
                    }

                    int recordCount = dal.GetRecordCount();
                    Invoke(new Action(() => {
                        progressBar1.Value = 100;
                        label9.Text = "视频处理完成";
                        UpdateRecordCount();
                    }));

                    string msg;
                    if (checkBox1.Checked)
                    {
                        msg = "视频处理完成！已保存完整视频和 " + keyFrameCount + " 个关键帧";
                    }
                    else
                    {
                        msg = "视频处理完成，共记录 " + recordCount + " 条检测信息";
                    }
                    Invoke(new Action(() => textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n")));

                    BoFang(res, fps);
                }
            });
        }

        private void BoFang(List<Mat> res, double fps)
        {
            int delay = (int)(1000 / fps);

            Task.Run(() =>
            {
                foreach (Mat mat in res)
                {
                    pictureBox1.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat);
                    Thread.Sleep(delay);
                }
            });
        }

        private void button2_Click(object sender, System.EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "选择输入路径";
            openFileDialog.InitialDirectory = "d:/";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = false;
            openFileDialog.RestoreDirectory = true;
            RadioButton selectedRadio = groupBox2.Controls.
                OfType<RadioButton>().FirstOrDefault(r => r.Checked);
            if (selectedRadio.Name == "radioButton1")
            {
                openFileDialog.Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.ico";
                
            }else if ( selectedRadio.Name == "radioButton2")
            {
                openFileDialog.Filter = "视频文件|*.mp4;*.avi;*.mov;*.wmv";
            }
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBox5.Text = openFileDialog.FileName;
            }
        }

        private void ReasoningPicture()
        {
            Mat img = new Mat(textBox5.Text);
            string imagePath = textBox5.Text;
            currentDetectionResults.Clear();
            TuiLiDanZhangTuPian(img, imagePath);
            pictureBox1.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(img);
            int detectedCount = currentDetectionResults.Count;
            
            if (checkBox1.Checked && !string.IsNullOrEmpty(outputDirectory))
            {
                string fileName = "preview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".jpg";
                string savePath = Path.Combine(outputDirectory, fileName);
                Cv2.ImWrite(savePath, img);
                textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] 预览图已保存: " + savePath + "\r\n");
            }
            
            if (detectedCount > 0)
            {
                textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] 图片检测完成，共检测到 " + detectedCount + " 个缺陷：\r\n");
                foreach (var record in currentDetectionResults)
                {
                    dal.Insert(record);
                    textBox7.AppendText("  - 类型: " + record.DefectType + ", 置信度: " + (record.Confidence * 100).ToString("F1") + "%, 位置: (" + record.BoundingBoxX + "," + record.BoundingBoxY + ")\r\n");
                }
                UpdateRecordCount();
            }
            else
            {
                textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] 图片检测完成，未检测到缺陷\r\n");
            }
        }

        private void TuiLiDanZhangTuPian(Mat img, string imagePath = null, bool isVideo = false)
        {
            Mat resized = new Mat();
            Cv2.Resize(img, resized, new Size(width, height));

            DenseTensor<float> input = new DenseTensor<float>(new int[] { 1, 3, width, height });
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Vec3b vec = resized.At<Vec3b>(i, j);
                    input[0, 0, i, j] = vec[2] / 255.0f;
                    input[0, 1, i, j] = vec[1] / 255.0f;
                    input[0, 2, i, j] = vec[0] / 255.0f;
                }
            }

            List<NamedOnnxValue> inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", input)
            };

            using (var res = inferenceSession.Run(inputs))
            {
                var output = res.FirstOrDefault(x => x.Name == "output0")?.AsTensor<float>();
                Draw(output, img, imagePath, isVideo);
            }
        }

        private void Draw(Tensor<float> output, Mat img, string imagePath = null, bool isVideo = false)
        {
            int count = output.Dimensions[1];
            int zongShu = output.Dimensions[2];
            int leiBieZongShu = count - 4;

            var allBoxes = new List<BoxInfo>();
            
            for (int i = 0; i < zongShu; i++)
            {
                int id = 0;
                float maxFidence = 0f;
                for (int j = leiBieZongShu; j < count; j++)
                {
                    if (maxFidence < output[0, j, i])
                    {
                        maxFidence = output[0, j, i];
                        id = j - 4;
                    }
                }
                if (maxFidence <= fidence)
                {
                    continue;
                }
                float centerX = output[0, 0, i];
                float centerY = output[0, 1, i];
                float boxWidth = output[0, 2, i];
                float boxHeight = output[0, 3, i];

                float scaleX = img.Width / 640f;
                float scaleY = img.Height / 640f;

                int x1 = (int)((centerX - boxWidth / 2) * scaleX);
                int y1 = (int)((centerY - boxHeight / 2) * scaleY);
                int w = (int)(boxWidth * scaleX);
                int h = (int)(boxHeight * scaleY);

                x1 = Math.Max(0, Math.Min(x1, img.Width - 1));
                y1 = Math.Max(0, Math.Min(y1, img.Height - 1));
                w = Math.Max(1, Math.Min(w, img.Width - x1));
                h = Math.Max(1, Math.Min(h, img.Height - y1));

                allBoxes.Add(new BoxInfo { X = x1, Y = y1, Width = w, Height = h, ClassId = id, Confidence = maxFidence });
            }

            var nmsBoxes = NMS(allBoxes, 0.5f);

            foreach (var box in nmsBoxes)
            {
                DrawDetection(img, box.X, box.Y, box.Width, box.Height, box.ClassId, box.Confidence, imagePath, isVideo);
            }
        }

        private List<BoxInfo> NMS(List<BoxInfo> boxes, float iouThreshold)
        {
            if (boxes.Count == 0) return boxes;

            var sortedBoxes = boxes.OrderByDescending(b => b.Confidence).ToList();
            var keep = new List<BoxInfo>();

            foreach (var box in sortedBoxes)
            {
                bool shouldKeep = true;
                foreach (var kept in keep)
                {
                    if (box.ClassId == kept.ClassId && CalculateIoU(box, kept) > iouThreshold)
                    {
                        shouldKeep = false;
                        break;
                    }
                }
                if (shouldKeep)
                {
                    keep.Add(box);
                }
            }

            return keep;
        }

        private float CalculateIoU(BoxInfo a, BoxInfo b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 < x1 || y2 < y1) return 0;

            float intersection = (x2 - x1) * (y2 - y1);
            float union = a.Width * a.Height + b.Width * b.Height - intersection;

            return intersection / union;
        }

        private class BoxInfo
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int ClassId { get; set; }
            public float Confidence { get; set; }
        }

        private void DrawDetection(Mat image, int x, int y, int width, int height, int classId, float confidence, string imagePath, bool isVideo = false)
        {
            Scalar[] classColors = new Scalar[]
            {
                new Scalar(0, 255, 0),
                new Scalar(255, 0, 0),
                new Scalar(0, 0, 255),
                new Scalar(255, 255, 0)
            };

            Scalar boxColor = classColors[classId];
            string className = classNames[classId];

            Cv2.Rectangle(image,
                         new Rect(x, y, width, height),
                         boxColor,
                         2);

            string label = className + ": " + confidence.ToString("F2");

            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);

            Cv2.Rectangle(image,
                         new Rect(x, y - textSize.Height - 5, textSize.Width, textSize.Height + 5),
                         boxColor,
                         -1);

            Cv2.PutText(image,
                       label,
                       new Point(x, y - 5),
                       HersheyFonts.HersheySimplex,
                       0.5,
                       new Scalar(255, 255, 255),
                       1);

            if (isVideo)
            {
                currentFrameHasDetection = true;
                
                var record = new DefectRecord
                {
                    DetectionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DefectType = className,
                    Confidence = confidence,
                    Latitude = currentLatitude,
                    Longitude = currentLongitude,
                    ImagePath = null,
                    FrameInfo = "帧" + videoFrameCount,
                    BoundingBoxX = x,
                    BoundingBoxY = y,
                    BoundingBoxWidth = width,
                    BoundingBoxHeight = height
                };
                dal.Insert(record);
            }
            else
            {
                var record = new DefectRecord
                {
                    DetectionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DefectType = className,
                    Confidence = confidence,
                    Latitude = currentLatitude,
                    Longitude = currentLongitude,
                    ImagePath = imagePath,
                    BoundingBoxX = x,
                    BoundingBoxY = y,
                    BoundingBoxWidth = width,
                    BoundingBoxHeight = height
                };
                currentDetectionResults.Add(record);
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            fidence = Convert.ToSingle(textBox2.Text);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "导出CSV文件";
            saveFileDialog.Filter = "CSV文件|*.csv";
            saveFileDialog.FileName = "defect_records_" + DateTime.Now.ToString("yyyyMMdd") + ".csv";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    dal.ExportToCsv(saveFileDialog.FileName);
                    MessageBox.Show("CSV导出成功！\n文件路径: " + saveFileDialog.FileName, "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] CSV导出成功: " + saveFileDialog.FileName + "\r\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "导出JSON文件";
            saveFileDialog.Filter = "JSON文件|*.json";
            saveFileDialog.FileName = "defect_records_" + DateTime.Now.ToString("yyyyMMdd") + ".json";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    dal.ExportToJson(saveFileDialog.FileName);
                    MessageBox.Show("JSON导出成功！\n文件路径: " + saveFileDialog.FileName, "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] JSON导出成功: " + saveFileDialog.FileName + "\r\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有检测记录吗？", "确认清空", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                dal.ClearAllRecords();
                UpdateRecordCount();
                textBox7.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] 已清空所有检测记录\r\n");
                MessageBox.Show("记录已清空！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void UpdateRecordCount()
        {
            int count = dal.GetRecordCount();
            label13.Text = "记录: " + count + " 条";
        }

        private void button3_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.Description = "选择输出目录";
            folderDialog.SelectedPath = outputDirectory;
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                outputDirectory = folderDialog.SelectedPath;
                textBox6.Text = outputDirectory;
            }
        }
    }
}
