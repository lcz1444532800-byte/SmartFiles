# Validation

## English release

English coverage includes interface controls, confirmation buttons, notifications, app errors, statuses, storage labels, new built-in categories, documentation, and release notes. Existing filenames, custom labels, physical paths, keys, and history are retained.

The upgrade regression checks old saved task states, methods, confidence/source labels, existing category IDs, installer routing, review-folder exclusion, English defaults, and preservation of custom names.

Release verification on 2026-10-21: all **30 regression groups passed**. All four published projects built without warnings or errors. Six English UI pages and the minimum-size Settings view were rendered and checked. A 100-file synthetic UI batch reached 100 pending results while excluding the existing file; selection, input, and status text survived refreshes.

The installed English build was also verified: UI close exited without stopping Watcher; a new file started Worker and submitted a matching system notification; Worker and the model child exited after the configured idle timeout. Pause/resume excluded paused files, full exit released all app processes, and three injected Worker crashes left Watcher and the source intact while stopping retries. Settings and encrypted API-key files were unchanged during deployment.

## Earlier Windows x64 measurements

The following were measured on 2026-10-08, before the language update. They are not a new 10-minute benchmark of the English build.

| Check | Result |
|---|---|
| Watcher idle sampling | 600 seconds; 120 external samples |
| Working Set | Average 26.88 MiB; peak 31.16 MiB |
| CPU and process I/O | CPU time and all six I/O counters unchanged |
| Network endpoints | No IPv4/IPv6 TCP or UDP endpoints in three samples; no independent byte counter |
| UI close | UI exited; Watcher continued |
| Second batch | Worker restarted and submitted a matching Toast |
| Models | Loaded on demand; Worker and AIWorker exited when idle |
| GPU | CPU provider; no GPU counter instances for app PIDs |
| Crash isolation | Three failures; Watcher/source survived; retries stopped |
| Pause and full exit | Paused files stayed excluded; no processes after full exit |

Storage stress used 100,000 synthetic history records, not 100,000 real documents. Retention left 10,000 terminal records plus one active transaction, with a database of about 4.63 MiB. Logs stayed within 50 MiB; cache and model cache stayed within 100 MiB each.

Privacy checks cover limited PDF text, compressed thumbnails, no weight extraction from a 10 GiB sparse GGUF, ZIP names without bodies, no extra image when OCR is sufficient, bounded API fields, and real-path redaction. Transactions cover collisions, stale identities, partial-copy recovery, rollback, move-based undo, Unicode paths, and sparse files.

## Verification limits

Actual reboot, physical C: to D: operation, sudden power loss, drive removal, and manual notification clicks were not tested. Gemini has local protocol tests without a live-key acceptance test. Builds are unsigned; Smart App Control may block them. The app does not disable protection or claim Microsoft approval.
