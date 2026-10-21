# Third-party notices

SmartFiles application code is independently authored. Third-party runtime/model components retain their respective licenses. License texts are under licenses/ and model provenance/checksums under Models/*/manifest.json.

| Component | Version / provenance | License |
|---|---|---|
| .NET / WPF runtime | 8.0.31, self-contained Windows x64 | MIT; Microsoft runtime third-party notices |
| Portable .NET SDK (development only) | 8.0.425, Microsoft ZIP SHA-512 checked | Microsoft .NET SDK distribution terms; not included in end-user ZIP |
| Microsoft.Data.Sqlite / Core | 8.0.11 | MIT |
| SQLitePCLRaw family | 2.1.6 | Apache-2.0 |
| SQLite engine | bundled e_sqlite3 | Public domain; SQLitePCLRaw wrapper license remains applicable |
| PdfPig | 0.1.9 | Apache-2.0 |
| Microsoft.ML.OnnxRuntime CPU | 1.20.1 | MIT, retain bundled notices |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT |
| BAAI/bge-small-zh-v1.5 | Xenova ONNX INT8 conversion, pinned manifest | Base model MIT; conversion source noted |
| TinyCLIP ViT-8M/16 Text-3M | onnx-community INT8 conversion, pinned manifest | MIT; Microsoft/Cream/TinyCLIP |
| Existing PP-OCRv4 det/rec models | read-only reference to D:\DeepPickAI\models | Apache-2.0; existing RapidOCR model manifest retained in original installation |

No Python environment, GPU runtime, MobileCLIP weights, general-purpose LLM, or third-party organizer implementation is bundled.
MobileCLIP source-code MIT licensing does not override its separate research-only model-weight license.

Research-only references (no implementation copied):
hyperfield/ai-file-sorter (AGPL-3.0); adityarya24/file-organizer (MIT); jishnudutta/JishAtlas (GPL-3.0); RCushmaniii/ai-filesense (proprietary public product description); SeyiDan/smart-file-organizer (no clear SPDX license returned by repository API).

Upstream:
https://github.com/dotnet/runtime
https://github.com/dotnet/wpf
https://github.com/dotnet/efcore
https://github.com/ericsink/SQLitePCL.raw
https://sqlite.org/copyright.html
https://github.com/UglyToad/PdfPig
https://github.com/microsoft/onnxruntime
https://github.com/FlagOpen/FlagEmbedding
https://huggingface.co/Xenova/bge-small-zh-v1.5
https://github.com/microsoft/Cream/tree/main/TinyCLIP
https://huggingface.co/onnx-community/TinyCLIP-ViT-8M-16-Text-3M-YFCC15M-ONNX
https://github.com/RapidAI/RapidOCR
