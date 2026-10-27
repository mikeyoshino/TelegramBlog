# Media viewer 1.7 verification — 2026-10-27

## Delivered
Viewer-only dark MAUI/WinUI page with 56 DIP toolbar, expanding fit stage, 72 DIP thumbnails in a 96 DIP strip, 320 DIP collapsible details panel and narrow-window overlay. Image zoom (1–8×), fit and mouse-drag panning use a dedicated native ScrollViewer. Native video transport and existing Telegram chunked streaming are retained. Bounded previous/next selection, selection auto-scroll, fullscreen, Thai status/retry/cancel, secondary Telegram-original action, and source-card focus restoration are implemented. The Topic page/VM remains on the navigation stack.

## Evidence
- `dotnet test tests/TeleGramBlog.Tests --no-restore -v minimal`: 50 passed.
- Release self-contained win-x64 publish succeeded.
- `scripts/smoke-viewer.ps1`: native UI Automation passed cover ratio, video open, play, pause, seek, volume slider, back, image open, zoom/fit, details toggle, fullscreen toggle, bounded navigation, mixed album selection and clicked-card initial index.
- `--capture-viewer`: 22 native checks passed, including actual keyboard listener attachment after Loaded, viewport/filmstrip geometry, portrait/landscape/square rendering, narrow panel, singleton and zero-media states, a 24-item filmstrip auto-scrolling to its last item, deliberate image-decode failure/retry, and rapid photo/video/photo changes.
- Native renders at requested 1280×720 and 1920×1080, display scaling 125%. Native content excludes system borders/titlebar (1266×681 and 1906×1041 DIP). Normal app windows retain the OS titlebar.
- `MediaCardView.cs` and `App.xaml` SHA-256 match the 1.6 baseline. The media grid responsive method remains byte-for-byte equivalent after newline normalization; column thresholds and 16 DIP gaps are unchanged. No MainPage XAML or global styles were changed.
- Independent read-only reviewer found no important defects. Additional runtime checks caught and fixed keyboard attachment timing and a MAUI focused-stroke teardown exception; the final native navigation checks passed after these fixes.

## Screenshot fixtures and limits
Screenshots in `viewer-1.7/` are native renders of the existing, explicitly isolated demo. Portrait/landscape use bundled preview photos; square uses the app icon; the long strip repeats sample media. Loading/error screenshots deliberately exercise diagnostics and are not real network failures. No fabricated metadata enters connected-account mode.

Real Telegram account/network behavior was not exercised. Original-link opening needs an account. MediaRef does not currently expose duration/file size, so the viewer does not invent these labels. Native video composition may render black in RenderTargetBitmap screenshots; actual playback/seek/volume were verified separately through native controls.

OS focus remained on another process when the diagnostic viewer requested focus (observed focused PID 9612 vs viewer PID 30432). Physical Tab/Enter/arrow/Esc delivery, mouse-drag input and OS-level source-card focus therefore still need an interactive desktop acceptance pass. Keyboard listener attachment, native focusability, navigation/state preservation by retained page instance, and code paths are checked; this is not a claim that physical key delivery or mouse dragging was automated successfully.

## Runtime corrections
- Image content is hosted in a viewport-sized native Grid so Uniform images center correctly instead of aligning left.
- Attach the keyboard handler after Loaded, not only OnAppearing, because the native page does not yet exist during initial appearance.
- Use native focus visuals rather than MAUI Focused border setters, which tried to access a disconnected ButtonHandler during pop navigation.
- Verification stores the root navigation instance before popping the viewer; detached-page navigation cannot mount subsequent singleton/long-strip fixtures.
