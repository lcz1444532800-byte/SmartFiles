# SmartFiles

Windows 新下载、接收文件分类助手。只接纳启用监听后产生的新文件事件，给出分类建议，经用户确认后整理；旧文件不动。

## 直接下载

[Windows x64 运行包与 SHA-256 校验文件](https://github.com/lcz1444532800-byte/SmartFiles/releases/latest)。解压至 `D:\`，运行 `D:\SmartFilesApp\Release\SmartFiles.exe`。这是便携运行包，包含正式 BGE/TinyCLIP 模型和必要运行时；OCR 资源依赖见下文。

已有安装请先退出 SmartFiles 并备份配置，再更新程序文件；不要删除自己的 `Data` 或用户分类目录。

## 运行架构

- **Watcher** 等待 Windows 文件事件，是唯一长期后台进程，不加载 WPF、OCR、ONNX 或网络模块。
- **Worker** 按需提取信息、运行本地模型和调用 API，默认空闲 45 秒退出，可配置 30–600 秒。模型子进程随任务释放。
- **SmartFiles.exe** 是 WPF 界面，默认关闭窗口即退出。系统通知可打开具体任务。

开机自启只运行 `Release\Watcher\SmartFiles.Watcher.exe --startup`，不会加载完整 UI。关闭窗口后驻留托盘默认禁用。

## 构建和运行

需要 Windows 11 x64、.NET 8 SDK、Git；当前运行目录设计为 **`D:\SmartFilesApp`**，分类根目录为 **`D:\SmartFiles`**。本版本没有跨平台运行支持。

在空的 `D:\SmartFilesApp` 中克隆此仓库，然后执行：

```powershell
.\build.ps1 -Publish
.\Release\SmartFiles.exe
```

如果本机已安装 SmartFiles，先备份自己的配置，使用独立目录检出、构建并核对差异。不要以 `git clean` 清除已有安装目录中的私有数据。发布输出中的 `Watcher`、`Worker`、`AIWorker` 子目录必须保留，不能把裁剪 Watcher 的运行库覆盖到 UI 根目录。

打开界面后检查文件来源，再启用监听；默认不扫描或整理旧文件。开机自启可在设置中开启。D 盘不可用时不会回退到 C 盘。新 C 盘下载经确认后可跨卷移动并删除源文件，旧文件仍被排除。

本仓库不含个人 API 密钥、数据库、用户文件、开发 SDK 或模型权重。DeepSeek、Gemini 和 Chat Completions 兼容 API 可在设置中接入；密钥按服务地址使用 Windows DPAPI 加密保存，不能随仓库共享。

## 本地 AI

本地文本使用 BGE-small-zh ONNX INT8，视觉使用 MIT 许可 TinyCLIP。模型来源、固定 revision 和权重 SHA-256 位于 `Models/*/manifest.json`。需要从对应上游版本准备 ONNX 模型和 tokenizer 文件，再放入相应模型目录；仓库不会自动下载模型。

OCR 当前只读复用 `D:\DeepPickAI\models` 中的 PP-OCRv4 资源。没有这些资源时应关闭本地 AI 或允许降级。MobileCLIP 权重未打包；其独立研究许可不被源代码 MIT 许可替代。模型细节见 [设计调研](docs/research-notes.md)。

## 隐私与存储

第一版**没有完整文件上传能力**。AI 仅接收必要的文件名、metadata、有限文档/OCR 文本、本地候选；必要时才附上长边最多 768 px 的内存压缩缩略图。OCR 足够时不额外附图。不会向 AI 发送真实目标目录，响应只能返回本地校验的分类 ID 和安全名称建议。

默认日志上限 50 MiB，缓存与模型缓存各 100 MiB；历史保留最近 10000 条或 180 天，活动恢复事务除外。撤销使用身份校验后的 Move，不复制用户文件建立备份。设置提供自身占用统计和安全缓存清理。

## 测试与已知限制

```powershell
.\run-tests.ps1
```

测试使用 `D:\SmartFilesApp\TestData` 中的合成文件，并将结果写入 `D:\SmartFilesApp\DevArtifacts`；运行前确保 D 盘可用。上次本地验收 29/29 通过，Watcher 10 分钟平均约 26.88 MiB。详见 [验证记录](docs/validation.md) 与 [架构](docs/architecture.md)。

程序目前未使用可信代码签名。Windows 智能应用控制可能拦截新构建；公开 GitHub 仓库或 Release 不等于微软审核或可信签名，项目不会自动关闭系统防护。实际重启、断电/掉盘、真实物理跨盘和人工通知点击尚未完成验收。

## 许可证

项目代码暂未指定开源许可证。公开可见不代表授予再分发或商用许可；第三方组件和模型继续遵守各自许可证，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 与 `licenses/`。
