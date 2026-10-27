# Desktop iOS Parity Implementation Plan

**Goal:** Make the Windows desktop app use the current TeleBlog interface and functions, adapted to a desktop window.

**Architecture:** Link the proven iOS web UI, backend and SQLite store into Windows rather than fork their behavior. Isolate session/configuration and native media integration per platform. Preserve Windows account directory, DPAPI session, existing streaming adapter and explicit legacy diagnostics.

**Tech Stack:** .NET 10 MAUI HybridWebView, WebView2, WinUI MediaPlayerElement, shared JavaScript/CSS, SQLite.

- [x] Add Windows raw-message bridge and responsive desktop layout with mouse/keyboard parity; verify browser interactions at desktop sizes.
- [x] Separate platform configuration/root/session selection from the shared backend and link shared sources/assets into Windows.
- [x] Implement native Windows inline/full-window player using the existing random-access streaming adapter, plus user-selected file export.
- [x] Make shared shell the Windows default, retain explicit legacy diagnostics, update branding/publish guidance.
- [x] Run web UI/unit regressions, build iOS to catch shared regressions, attempt Windows compile and report host limitations accurately. Record native Windows acceptance checklist.

User authorized iOS parity; no new visual design approval is needed. This Mac cannot run the Windows executable. Browser layout/bridge tests and compile evidence must not be described as a native Windows playback test.
