# Design research

SmartFiles is independently authored in C#/.NET. Public references informed design choices; their implementations were not copied.

| Reference | Reviewed license / scope | Design influence |
|---|---|---|
| [hyperfield/ai-file-sorter](https://github.com/hyperfield/ai-file-sorter) | AGPL-3.0; README and classification behavior | Preview, narrowed candidates, learning, branch approval |
| [adityarya24/file-organizer](https://github.com/adityarya24/file-organizer) | MIT; README and organizer | Download suffixes, stability, locks, collisions, undo; polling was not adopted |
| [jishnudutta/JishAtlas](https://github.com/jishnudutta/JishAtlas) | GPL-3.0; README and apply workflow | AI proposes; local validation executes; no organized-copy mode |
| [RCushmaniii/ai-filesense](https://github.com/RCushmaniii/ai-filesense) | Public README/structure only; proprietary | Preview, undo, recovery, SQLite, limited excerpts |
| [SeyiDan/smart-file-organizer](https://github.com/SeyiDan/smart-file-organizer) | README and entry point; no clear SPDX license in reviewed metadata | Embedding routing and dry-run concepts; no unrestricted deep clustering |

## Models

- **BGE-small-zh-v1.5:** MIT model card; Xenova ONNX INT8 conversion, approximately 24 MB. Revision and LFS SHA-256 are pinned in the manifest. Local WordPiece, CLS pooling, and normalization support ranking, not calibrated probabilities. An English interface does not remove Chinese file-content support.
- **OCR:** existing PP-OCRv4 ONNX detection, recognition, and dictionary resources are read from `D:\DeepPickAI\models`, without copying or modifying that installation, Python, or another GUI. Axis-aligned detection has limited rotated-document support.
- **MobileCLIP:** source-code MIT licensing does not override the separate research-only model-weight license. Those weights were not downloaded or bundled; no MobileCLIP performance result is claimed.
- **TinyCLIP:** the ONNX-community INT8 conversion of ViT-8M/16 + Text-3M, approximately 24 MB, is the MIT-licensed replacement. CPU inference, memory behavior, and low-score fallback were tested. Outputs are coarse, uncalibrated category scores.

Sources:

- [BGE model card](https://huggingface.co/BAAI/bge-small-zh-v1.5)
- [BGE ONNX conversion](https://huggingface.co/Xenova/bge-small-zh-v1.5)
- [MobileCLIP weight license](https://github.com/apple-aiml/ml-mobileclip/blob/main/LICENSE_MODELS)
- [TinyCLIP upstream](https://github.com/microsoft/Cream/tree/main/TinyCLIP)
- [TinyCLIP ONNX conversion](https://huggingface.co/onnx-community/TinyCLIP-ViT-8M-16-Text-3M-YFCC15M-ONNX)

## Runtime decisions

FileSystemWatcher avoids drivers, services, administrator rights, and idle scanning. Identity baselines exclude existing files. Worker isolation protects monitoring from parser/model failures. NativeAOT was considered; the build environment lacked MSVC tools, so a trimmed, self-contained .NET 8 Watcher was chosen for stability.

The D: runtime home is deliberate. Cross-drive moves use verified publication, source removal, and rollback. A language update does not authorize renaming or reorganizing existing user files.
