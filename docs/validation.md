# 验证记录

发布源码复验（2026-10-21）：四个程序项目编译零警告、零错误，29/29 回归再次通过；以下性能数据仍来自此前的 10 分钟实测。

以下为 2026-10-08 在 Windows 11 x64 上完成的本地验证，未宣称所有 Windows 配置均已验证。

- 回归：29/29 通过。
- Watcher 空闲观测 600 秒，平均 Working Set 26.88 MiB，最高 31.16 MiB；CPU 时间及进程 I/O 六项计数无增加。
- UI 关闭即退出；Watcher 继续工作；第二批文件按需重新启动 Worker。
- Worker / OCR / embedding 模型空闲退出；系统通知提交、暂停恢复、完全退出与三次崩溃转人工均通过。
- 10 万条合成历史保留压力测试：10000 条终态加 1 条活动事务，数据库约 4.63 MiB；日志 50 MiB、缓存和模型缓存各 100 MiB。
- PDF 有限片段、压缩缩略图、OCR 足够时不附图、ZIP 只取名称、10 GiB 稀疏模型不读取权重、AI 请求路径脱敏均通过。

尚未验证：实际系统重启、人工点击通知、真实物理 C→D、断电/突然掉盘。Gemini 没有使用真实密钥做联网验收，仅本地协议测试。
程序尚未进行可信代码签名，Windows 智能应用控制可能拦截未签名构建；项目不会自动关闭系统防护。

测试组：
- Invalid settings preserve live and persisted configuration
- Same-volume move preserves file ID and content
- Collision never overwrites
- Changed file cannot be moved from stale plan
- Move outside taxonomy rejected
- AI path traversal / unknown IDs / NaN rejected
- Reserved device names / ADS / trailing dot rejected
- Undo restores identity; repeated undo rejected
- Undo refuses later modifications
- Cross-volume algorithm: verified copy and source removal
- Source deletion failure rolls back published copy
- Crash after native rename recovers without retry
- Crash after publication rolls back only owned identical copy
- Owned partial copy recovery removes temp and preserves source
- 12 GiB sparse same-volume move: no copy and same ID
- 200 native renames preserve exact Unicode paths
- Strict cross-volume hash and undo
- 5000 old files and 1000 timestamp changes stay excluded
- Browser .crdownload rename admitted exactly once
- Old temporary download renamed after arming stays excluded
- Deleted fresh file does not produce a result
- 100 new image files deduplicate and batch intake
- Exclusive writer at Created is retried until completion
- Local installer recommendation under 300 ms
- One correction does not become an absolute rule
- Source discovery is read-only and returns configured paths
- API privacy / concurrency / injection / bounded retry
- End-to-end deleted file / late TTL / explicit approval
- 100000-record retention / storage caps / model and ZIP privacy
