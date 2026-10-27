# Memory / GC audit — 2026-10-27

Confirmed allocation churn and unbounded remembered-topic retention were fixed. Tracked closed viewer, handler and player objects were collectible after idle/drain. **This does not establish that the entire application is leak-free:** managed heap and private bytes still trend upward under accelerated navigation, including photo-only isolation. Residual retaining roots require heap/native allocation analysis.

## Changes in 1.7.1
- Memoize HistoryPager's album/post index until a new batch arrives. Previously each LoadedCount binding read rebuilt all posts/captions/arrays. A regression with 3,000 posts and 100 count reads failed before the fix; now these reads allocate under 100 KB. Separate tests check invalidation and album boundaries.
- Read FetchedAt directly instead of allocating/sorting a history snapshot for the status label.
- LRU remembered-history budget: 8 topics and 20,000 raw messages combined. Oversized active topics are not cached. Saved history reloads from disk on revisiting. Failed/canceled partial growth is pruned on cache access/leaving a topic. Active history remains expandable for search/paging; message count is not an exact byte budget, particularly for long captions.
- Skip disk deserialization when a pager is already cached. Stream JSON instead of duplicating entire JSON strings.
- Copy compressed media streams to unique temporary files instead of ToArray, then atomically move to cache. Canceled partial files cannot become valid cache entries.
- Unregister ZoomImageHandler native size/view/pointer events and detach content on disconnect. Dispose each decoder stream's cancellation source once; pending reads use a captured token safely.
- No normal-path forced GC, no image downsampling, no card/grid/global style changes.

## Measurements
Offline demo on Windows, release MAUI/WinUI. Each cycle opens an image, opens the video, plays/pauses, switches image, closes the viewer. Every five cycles records managed heap estimate, private bytes, working set, handle count and weak references. Forced GC checks collectibility; it changes natural GC timing. Final run includes 10 seconds idle and another drain measurement.

Final-behavior diagnostic, MiB rounded:

| Phase | Managed after GC | Private bytes | Working set | Viewer pages alive |
|---|---:|---:|---:|---:|
| 5 cycles | 11.1 | 261.7 | 334.7 | 2 |
| 20 cycles | 15.7 | 322.0 | 405.3 | 1 |
| 40 cycles | 23.8 | 389.2 | 474.3 | 1 |
| 40 cycles, idle + drain | 22.4 | 301.1 | 386.3 | 0 |

After drain all tracked resource categories were zero: viewer, session, image view/handler/native wrapper, video handler/native wrapper, player, video source, root layout and toolbar button/handler. The running async probe can temporarily hold the latest page. Registered Topic card count stayed bounded (12 in this run).

An intermediate 160-cycle run reached 64.9 MiB managed, 523.8 MiB private and 603.9 MiB working set before idle drain. A photo-only 40-cycle run also grew (30.7 MiB managed / 302.3 MiB private); video alone does not explain the trend. Collection of managed wrappers does not prove every native allocation was freed. Do not compare an idle result against a busy baseline as a percentage improvement.

Raw JSONL under `memory-1.7.1/`: baseline-40, intermediate-160, photo-only-40 and final-idle-40. Builds differ as fixes/instrumentation were added; these are not a controlled percentage benchmark.

## Limits / interpretation
- No heap-dump tool was installed locally; two dotnet-gcdump installation attempts failed against NuGet feeds. Residual retaining roots remain unidentified; this report does not blame a specific MAUI/WinUI component without a heap trace.
- Samples are small. A 6000×4000 32-bit decoded image needs about 91.6 MiB of pixels before decoder/GPU overhead. Large originals still produce legitimate peaks; image quality was not silently reduced.
- Video block cache remains bounded at 64 × 256 KiB = 16 MiB per active source; Windows decoder/GPU memory is extra. The 256 MiB thumbnail disk-cache limit is not a RAM limit.
- .NET may retain heap pages and Windows may retain native caches after collection. Working set alone does not diagnose a managed leak. Routine forced GC adds pauses and cannot release referenced objects, so none was added.
- Live Telegram, large real albums and a multi-hour workload remain unverified.

## Verification
54 unit tests pass, release self-contained publish succeeds. Native viewer smoke passes image/video open, play/pause, seek, volume, zoom/fit, details, fullscreen, bounded navigation, back and clicked-card selection. Independent review caught mutable-pager eviction and temp-file collisions; corrected with a failed-growth regression test and unique temp paths. Locked card/global files and media grid rules remain unchanged.
