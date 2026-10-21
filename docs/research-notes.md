# SmartFiles 设计调研（2026-10-08）

SmartFiles 为独立 C#/.NET 实现。下列项目仅参考公开产品、架构与算法思想；未将 AGPL、GPL、专有或 MIT 项目的实现代码复制进产品。

| 项目 | 核对内容与许可 | 采用的思想 | 未采用 / 原因 |
|---|---|---|---|
| [hyperfield/ai-file-sorter](https://github.com/hyperfield/ai-file-sorter) | README、docs/categorization-behavior.md、CategorizationService.cpp；AGPL-3.0 | 应用前预览、白名单、先缩小大分类树候选、学习已确认决定、分支先批准 | 不复制代码，不使用常驻本地 LLM；本项目以稳定 Folder ID 而非自由路径作为输出契约 |
| [adityarya24/file-organizer](https://github.com/adityarya24/file-organizer) | README、master/organizer.py；MIT | 下载临时后缀、稳定性、锁释放、碰撞保护、撤销 | 源码 watch 实际使用轮询思路；SmartFiles 使用 FileSystemWatcher，不照搬轮询或全量整理 |
| [jishnudutta/JishAtlas](https://github.com/jishnudutta/JishAtlas) | README、apply.py；GPL-3.0 | AI 只生成计划，所有路径和执行由本地校验 | 不使用 organized-copy 模式，不复制源码，不自动下载通用 LLM |
| [RCushmaniii/ai-filesense](https://github.com/RCushmaniii/ai-filesense) | 仅公开 README 与文件结构；专有 | Preview、Undo、Crash Recovery、SQLite、少量片段、人工确认 | 不读取/复制专有实现，不扫描桌面或旧文档，不采用全部文件交给远程模型 |
| [SeyiDan/smart-file-organizer](https://github.com/SeyiDan/smart-file-organizer) | README、公开分类入口；API 未给出明确仓库许可 | embedding 路由、相似度候选、dry-run 与撤销思想 | README 披露过度细分问题；不采用自由聚类建目录，限制目录深度 16，新增分支须确认 |

## 模型决策

- BAAI/bge-small-zh-v1.5：官方模型卡标注 MIT。使用 Xenova 的 ONNX INT8 转换发布物，模型约 24 MB；固定 revision 和发布方 LFS SHA-256，记录于 Models/TextEmbedding/manifest.json。自实现 WordPiece、CLS pooling 与归一化。相似度仅用于排名，不直接当作经过校准的概率。
- OCR：只读检查 D:\DeepPickAI 后，确认已有 PP-OCRv4 ONNX 模型。SmartFiles 的独立 .NET Worker 读取其 det/rec 模型和字典，不启动 GUI、不复制模型、不修改该项目、不依赖 Python。采用独立的轴对齐检测框处理；倾斜/旋转文档识别能力有限。
- MobileCLIP2-S0 / MobileCLIP-S0：官方仓库代码 MIT，但 LICENSE_MODELS 为独立研究许可证，明确排除产品开发用途。没有下载/打包其权重，也没有声称完成其性能试验。
- 视觉备选：TinyCLIP ViT-8M/16 + Text-3M，ONNX-community INT8 转换，约 24 MB，模型页与上游标注 MIT。这是明确的方案替代，不是 MobileCLIP。已实际测试 CPU 推理、内存和低分降级。仅粗分类；分数未经任务数据集校准。

模型来源：
- https://huggingface.co/BAAI/bge-small-zh-v1.5
- https://huggingface.co/Xenova/bge-small-zh-v1.5
- https://github.com/apple-aiml-research/ml-mobileclip/blob/main/LICENSE_MODELS
- https://github.com/microsoft/Cream/tree/main/TinyCLIP
- https://huggingface.co/onnx-community/TinyCLIP-ViT-8M-16-Text-3M-YFCC15M-ONNX

## 文件安全设计

基线仅枚举已启用来源的文件身份，不读旧文件内容；Created 是新任务证据。Changed 仅重试已跟踪的下载；Renamed 仅接续启用后跟踪到的临时下载。溢出时停监控并提示，不补扫旧文件。

执行前重新核对卷序列号、文件 ID、长度和写入时间，拒绝 reparse point、硬链接、设备名称、ADS、越界路径。持有禁止写入/删除共享的源句柄及父目录句柄。同盘用 SetFileInformationByHandle 原生 rename；跨盘先复制到同目标卷暂存文件、校验，再发布/删除源；删除失败回滚本程序目标副本。

恢复只依据事务与身份。身份不匹配或未验证的半成品保留并标为需要人工检查，不猜测性删除。上述设计降低风险，不构成对断电、硬件故障或文件系统损坏的绝对保证。
