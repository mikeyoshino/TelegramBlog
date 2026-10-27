# Windows desktop / iOS parity

The default Windows app now uses the same local HybridWebView UI, MobileBackend, SQLite favorites/collections and search implementation as iOS. Sources/assets are linked at build time, not copied into an independent UI fork. Desktop pointer layouts add a left navigation rail, bounded centered dialogs and a wide viewer with a thumbnail/action sidebar. Mobile layout remains below the desktop media query.

## Included behavior

- QR/phone authentication, existing-session resume, group/topic navigation, latest-first ordering and positive known unread badges.
- Message cards, pagination, mixed-media thumbnails, context menus, photo fullscreen and keyboard navigation.
- Local SQLite favorites, one collection per post, notes, viewed state, collection creation, remembered recent category and complete saved-snapshot search with highlighted excerpts.
- Windows MediaPlayerElement uses the existing VideoRandomAccessStream/ChunkedMediaSource path for inline playback; fullscreen reuses the player. Closing/changing media disposes native playback, range streams and overlays.
- Windows export opens a Save As picker. Source links open externally through the existing validated launcher flow.

## Compatibility

Windows retains `%LOCALAPPDATA%/TeleGramBlog` and the existing DPAPI-protected `account.session`. Configuration supports existing protected `app.config`, `telegram.json` next to the executable, environment overrides, and owner-configured packaged JSON. The app ID remains `app.telegramblog.desktop`. WebView2 data uses a writable application-data folder. SQLite, notes and collections are local to each device/account; this change does not synchronize iPhone favorites to Windows.

The original XAML shell remains available through `--legacy-ui`; old capture/probe switches select that shell explicitly. Existing legacy UI Automation scripts are not evidence for the new HybridWebView shell. Default `--design-preview` uses the new shell with clearly labeled sample content; normal startup uses Telegram only.

## Build

On Windows with .NET 10 and MAUI Windows workload:

```powershell
dotnet build src/TeleGramBlog.App/TeleGramBlog.App.csproj -r win-x64
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1
```

The folder output is `artifacts/TeleBlog-2.0-win-x64`; it is not an installer or signed release. WebView2 Runtime is required.

On macOS, compile the actual shared/backend/native-Windows C# against Windows reference assemblies without invoking XAML tooling:

```sh
dotnet build tests/TeleGramBlog.Desktop.Compile/TeleGramBlog.Desktop.Compile.csproj
```

This produces a compile-check library, not a runnable desktop app. The full desktop build on this Mac fails because Windows App SDK `XamlCompiler.exe` cannot execute (exit 126). No Windows EXE is claimed verified or published here.

## Required Windows acceptance

Run the new default application with a real account: verify DPAPI session restoration, QR and phone/2FA login, unread refresh, inline video audio/seek, full-window/restore, resizing and Windows display scaling, Save As cancellation/export, restart persistence, offline saved-image access, logout cleanup and account switching. Browser tests and successful C# compilation do not verify WinUI visual stacking or native video decoding.

Bridge protocol reference: [Microsoft MAUI HybridWebView](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/hybridwebview?view=net-maui-10.0).

## Evidence from this session

- All 24 browser UI suites passed, including the Chromium desktop suite at 880/1280/1440/1920 widths. All 10 JavaScript unit tests passed, including the Windows WebView2 raw-message contract.
- Nine focused .NET tests passed (SQLite favorites/collections, unread snapshots and desktop/iOS migration).
- The Windows-reference compile-check project built with zero warnings/errors. The shared iOS app also built successfully.
- Independent read-only review found no confirmed correctness/security regression; native Windows acceptance remains outstanding.
- Preview screenshots: [groups](desktop-parity/groups.png), [viewer](desktop-parity/viewer.png). They show sample data, not a live Windows-account run.
