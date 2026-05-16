<div align="center">

# YOLOv8-OnnxDetection

**基于 YOLOv8 ONNX 的路面缺陷检测系统**

C# WinForms 桌面应用，加载 YOLOv8 导出的 ONNX 模型，
实现道路裂缝与坑洼的目标检测，支持图片/视频双模式推理。

[![.NET](https://img.shields.io/badge/.NET_Framework-4.7.2-512BD4.svg)](https://dotnet.microsoft.com/)
[![ONNX Runtime](https://img.shields.io/badge/ONNX_Runtime-1.23-FF6F00.svg)](https://onnxruntime.ai/)
[![OpenCvSharp](https://img.shields.io/badge/OpenCvSharp-4.11-green.svg)](https://github.com/shimat/opencvsharp)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

</div>

---

## 系统展示

| 主界面 — 模型加载与推理配置 | 图片检测 — 裂缝识别结果 |
|:---:|:---:|
| ![主界面](img/screenshot1.png) | ![图片检测](img/screenshot2.png) |

---

## 核心功能

### YOLOv8 ONNX 推理

纯 C# 手写推理管线，不依赖 YOLOv8 原生库：

```
输入图像 → Resize 640×640 → BGR→RGB + /255 归一化 → DenseTensor[1,3,640,640]
    → ONNX Runtime 推理 → 输出[1,8,8400] → 置信度过滤(>0.3) → NMS(IoU=0.5)
    → 坐标缩放回原图 → 绘制检测框
```

- DenseTensor 逐像素填充实现 BGR→RGB 转换 + 归一化
- 从 `[1,8,8400]` 原始输出手动解析 bbox + confidence，实现类别感知 NMS
- ONNX Runtime 默认 CPU 推理，无需 GPU

### 检测类别

基于 RDD2020 数据集，4 类路面缺陷：

| 编号 | 类型 | 标注颜色 |
|------|------|---------|
| D00 | 纵向裂缝 | 绿色 |
| D10 | 横向裂缝 | 蓝色 |
| D20 | 龟裂/坑槽 | 红色 |
| D40 | 修补/坑洼 | 青色 |

### 图片/视频双模式

- **图片模式**：单张推理，结果预览，自动保存检测图像（JPEG）
- **视频模式**：VideoCapture 逐帧推理 + VideoWriter 导出结果视频（AVI/MJPG），进度条同步，检测到缺陷的帧自动保存为关键帧

### 数据持久化

SQLite 自动建表存储检测记录（缺陷类型、置信度、坐标、时间），支持 CSV / JSON 双格式导出。

---

## 技术架构

```
┌────────────────────────────────────────────┐
│  Form1 — 左栏（控制面板）+ 右栏（预览+日志） │
├────────────────────────────────────────────┤
│  预处理（NCHW 张量）→ ONNX 推理 → NMS → 绘制 │
├────────────────────────────────────────────┤
│  DefectRecordDAL → SQLite                  │
└────────────────────────────────────────────┘
```

| 设计决策 | 实现方式 |
|---------|---------|
| 纯 C# 推理 | DenseTensor 手动填充，BGR→RGB + /255 归一化 |
| NMS 手写 | 类别感知 IoU NMS，从 [1,8,8400] 原始输出解析 |
| 视频逐帧 | OpenCvSharp VideoCapture + MJPG VideoWriter |
| 关键帧提取 | 有缺陷的帧自动保存 JPEG |
| 深色主题 | 全窗体统一 RGB 配色 |

---

## 技术栈

| 分类 | 技术 |
|------|------|
| 框架 | .NET Framework 4.7.2 |
| UI | WinForms（深色主题） |
| 推理引擎 | ONNX Runtime 1.23（CPU） |
| 图像处理 | OpenCvSharp4 4.11 |
| 数据库 | System.Data.SQLite |
| 模型格式 | YOLOv8 → ONNX（输入 `images` [1,3,640,640]，输出 `output0` [1,8,8400]） |

---

## 快速开始

### 环境要求

- Visual Studio 2022
- .NET Framework 4.7.2
- YOLOv8 导出的 `.onnx` 模型文件（4 类，输入 640×640）

### 运行步骤

```bash
git clone https://github.com/NothingLikeYouthRoam/YOLOv8-OnnxDetection.git
# 用 Visual Studio 打开 .sln，还原 NuGet 包，编译运行
```

### 模型准备

```python
from ultralytics import YOLO

model = YOLO("best.pt")
model.export(format="onnx", imgsz=640)
```

将生成的 `best.onnx` 放到任意位置，在界面「模型设置」中选择即可。

---

## 项目结构

```
├── Program.cs                   # 入口
├── Form1.cs                     # 主窗体（推理 + 绘制 + UI 事件）
│   ├── 预处理 → ONNX 推理入口
│   ├── 后处理（解析 + NMS）
│   └── 图片/视频双模式流程
├── Form1.Designer.cs            # UI 布局（深色主题）
├── DAL.cs                       # SQLite CRUD + CSV/JSON 导出
└── Properties/                  # 程序集信息 + 资源
```

---

## License

MIT License
