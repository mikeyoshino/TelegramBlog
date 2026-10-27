# TeleGramBlog

Windows desktop viewer built with .NET 10 MAUI. Connect your own Telegram account, browse joined groups and forum Topics, and view historical photos and links in a clean white/blue paginated grid.

Continuing on a Mac? See the [iOS development handoff](docs/IOS-HANDOFF.md) and
[Luma mobile UI reference](docs/design/luma-mobile.html). The current app is Windows-only;
the iOS implementation is pending.

## เปิดใช้งาน

1. เปิด `artifacts/TeleGramBlog-1.7.2-win-x64/TeleGramBlog.App.exe` โดยเก็บไฟล์ทั้งหมดในโฟลเดอร์ไว้ด้วยกัน ไม่ใช่ EXE เดี่ยว
2. ทดลองหน้าตาได้ทันทีด้วย **Explore the design preview** ข้อมูลในโหมดนี้เป็นตัวอย่าง ไม่ได้เชื่อม Telegram
3. กด **Connect Telegram** เพื่อแสดง QR จริง เปิด Telegram มือถือ → Settings → Devices → Link Desktop Device แล้วสแกนและยืนยัน QR จะต่ออายุอัตโนมัติ
4. ถ้า Telegram ขอรหัส 2FA ให้กรอกในโปรแกรม เลือก **Log in with phone number** ได้หากต้องการใช้เบอร์และรหัสยืนยันแทน อย่าส่งรหัสเหล่านี้ในแชต
5. เลือกกลุ่ม → Topic → เปิดรูปหรือกดลิงก์ เลือก 24/48 โพสต์ต่อหน้าได้

## ตั้งค่าโดยเจ้าของแอปครั้งเดียว

Telegram ยังต้องใช้ API ID/API hash ของ **แอป** เพื่อสร้าง QR จริง แต่ผู้ใช้ทั่วไปไม่ต้องกรอกค่านี้ในหน้าเข้าสู่ระบบแล้ว เจ้าของแอปลงทะเบียนที่ https://my.telegram.org/apps แล้วคัดลอก `telegram.example.json` เป็น `telegram.json` ข้าง EXE และใส่ค่าจริงในเครื่อง ห้ามใช้ bot token หรือคัดลอก credentials ของแอปอื่น

Alternatively set `TELEGRAM_API_ID` and `TELEGRAM_API_HASH`. Existing protected configuration from version 1.0 remains supported. The example deliberately cannot authenticate. A manually supplied JSON file is plaintext and ignored by Git. A distributed build should be configured by its application owner; users then only scan or sign in. If configuration is missing, the app explains that this copy needs application-owner setup; it does not blame the network or show a fake QR. No real credentials are included in this repository or published test artifact.

## Behavior

- Version 1.6 uses one connected desktop shell: My groups → forum overview → selected topic posts. Groups without topics open their posts directly. Group tiles use Telegram photo metadata when available and stable initial/color fallbacks otherwise; no invented statistics are displayed.
- Shell controls use Thai labels and explicitly scoped styles. The account menu contains sign-out. Topic sidebar can collapse without losing selection. Search and remembered page positions survive back navigation. Sorting is limited to the displayed loaded records; loaded counts are not Telegram totals.
- First loading, updating existing data, empty media, search with no matches, canceled loading, saved data and errors have separate states. Saved-history timestamps reflect successful history fetches; older cache without timestamps has no invented update time.
- MediaCardView, gallery/player views, global application styles, media grid breakpoints and 16px gaps are unchanged by this shell update. Baseline hashes and verification notes are in `docs/verification`.

- Reads groups the account belongs to, including ordinary groups and forum topics. Broadcast-only channels and private direct messages are excluded.
- Cards use a portrait 4:5 media area with AspectFit (no cropped faces), a compact date/link footer and clickable image. Captions appear in details; link-only cards retain text. Mixed cards have preview arrows and a horizontal thumbnail strip; selection carries into details. Only two extra thumbnails per visible card are prefetched, sequentially; remaining items load on selection.
- Albums are one card, including albums split across Telegram fetches. Click **View image**, **View video**, or **View album** (or the thumbnail) to open details. Mixed albums keep photos and videos in one post, in original media order. A horizontally scrollable thumbnail gallery below the viewer selects any item; video thumbnails and card badges show a play icon. Previous/Next media also remain available. Link-only posts have a placeholder.
- Videos stream on demand through the native Windows player. The decoder requests byte ranges, including the end of a file when its index is stored there. Seeking fetches the requested region without downloading all preceding content. Play/pause, seek and volume are available without autoplay.
- A 16 MiB in-memory LRU cache reuses recent 256 KiB blocks while the viewer is open. Closing/changing media cancels reads and clears the cache; no full video temporary file is required. Startup and seek latency depend on network, Telegram throttling, file indexing/keyframes and installed codecs. Unsupported files can be opened in Telegram. No transcoding or adaptive bitrate is provided.
- Pages are built on demand. Number buttons represent **loaded pages**; the UI does not invent a total for unfetched history. Very sparse histories can take longer; Cancel loading stops navigation work.
- **Search loaded posts** searches the already fetched topic index, not every historical Telegram message. Clear search to return to the page. Topic/group searches filter discovered lists.
- Sync checks fresh history and returns to page one. Stored pages are labeled cached until refreshed. Cached cards remain visible if fetching fails; this is not a full offline Telegram client, and initial account/group discovery still requires connectivity.
- One signed-in account at a time. Session and legacy saved config are Windows-user protected. Account-scoped metadata and thumbnails under `%LOCALAPPDATA%/TeleGramBlog/accounts` are not encrypted; thumbnail cache is bounded to approximately 256 MiB. An unfinished QR transport session is not treated as an authenticated login on restart.
- Disconnect attempts remote revocation, clears session and cached accounts, and reports remote or local cleanup failures separately. App config is retained. Telegram Settings → Devices can revoke an unreachable session.
- The viewer does not send/edit/delete Telegram messages. Group/topic changes cancel outstanding display work; stale responses cannot replace the selected topic. Ongoing media transport may finish before cancellation, but canceled results are not displayed.

## Build and test

Prerequisites: Windows x64, .NET 10 SDK, `maui-windows` workload, Windows SDK. Native MAUI XAML; no web server.

```powershell
dotnet restore TeleGramBlog.slnx
dotnet test tests/TeleGramBlog.Tests/TeleGramBlog.Tests.csproj
dotnet build src/TeleGramBlog.App/TeleGramBlog.App.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1
```

Publish includes .NET and Windows App SDK dependencies in a folder. It is not an installer, Store package or signed release. Minimum target is Windows 10 build 17763. macOS/mobile are not initial targets.

## Layout

- `src/TeleGramBlog.Core`: album grouping, pagination, safe link parsing, navigation lifetime.
- `src/TeleGramBlog.Telegram`: WTelegramClient adapter, message mapping, protected session and local index.
- `src/TeleGramBlog.App`: native MAUI views and presentation state.
- `tests/TeleGramBlog.Tests`: deterministic paging, mapping, storage, cancellation and preview-navigation checks.
- `scripts/smoke-media.ps1`: launches the built app with `--design-preview` (skipping account initialization), opens the generated demo video through the same random-access adapter, and exercises native play/pause controls without Telegram credentials. The demo clip is a three-second H.264 test pattern generated locally with FFmpeg.
- `docs/design/desktop-grid-concept.png`: approved visual concept. Native implementation uses letter avatars initially.

## Live acceptance still required

Version 1.7.1 reduces history allocation churn, budgets remembered history to 8 topics / 20,000 raw messages, streams history JSON and media cache writes, and explicitly unhooks native image events and disposes decoder-stream cancellation sources. Current loaded history remains expandable; this is not a hard process-RAM ceiling. See `docs/verification/memory-1.7.1.md` for measured results and residual growth still needing investigation.

`--memory-probe --memory-cycles=40` runs isolated demo open/play/close cycles and records memory, handles and weak references in `memory-probe/samples.jsonl`. `--memory-photo-only` isolates image/navigation behavior. Forced garbage collection exists only in this explicit diagnostic; normal application paths never call `GC.Collect()`.

Version 1.7 replaces only the opened-post viewer with a dark, full-window media stage: 56 DIP toolbar, 96 DIP thumbnail strip (hidden for one item), bounded previous/next navigation, image fit/zoom/drag, full-screen toggle and a collapsible details panel. The original Topic cards and media grid are unchanged. Image selection resets zoom; leaving the viewer releases video and returns to the existing Topic page. Escape exits full screen first; arrow keys switch media except inside native video controls. Telegram originals are available in the secondary menu for connected accounts.

Run `scripts/smoke-viewer.ps1` against `artifacts/TeleGramBlog-1.7-win-x64/TeleGramBlog.App.exe` for native interaction checks. `--capture-viewer` is an isolated preview-only diagnostic that writes native UI screenshots and checks under `viewer-screenshots`. Portrait/landscape use existing bundled photos; the square fixture is the app icon, long-filmstrip fixtures repeat sample media, and loading/error captures are deliberate diagnostic states. These fixtures are never loaded into a real account. Native video uses a composition surface that may appear black in RenderTargetBitmap captures; playback is checked separately through native controls.

Shell verification: `scripts/smoke-shell.ps1` checks native navigation, search states, sidebar and account menu through UI Automation. `--capture-shell` runs an isolated, clearly labeled preview and writes real native UI screenshots under the executable's `screenshots` folder at 1280×720, 1440×900 and 1920×1080. Additional screenshots use explicitly labeled stress fixtures (80 long group/topic names); these fixtures never enter connected-account mode. Native controls expose keyboard focus, but actual OS Tab/Enter delivery could not be verified in the automated sandbox. Screenshots capture app content; the system title bar remains native.

Without real application credentials and an interactive user login, automated fixtures cannot establish live Telegram compatibility. Check: code delivery/retry, 2FA, restart/resume, archived and large group lists, forum General and other topics, album boundary, image originals, loss of access, flood waits, offline refresh, logout and Telegram Devices revocation. Do not interpret the preview or a successful build as a live-account test.

API references: [WTelegramClient](https://github.com/wiz0u/WTelegramClient), [Telegram QR login](https://core.telegram.org/api/qr-login), [Telegram authorization](https://core.telegram.org/api/auth), [forum topics](https://core.telegram.org/api/forum).
