# iOS development handoff

## Current status

The working application is the Windows .NET 10 MAUI desktop app (version 1.7.2).
The iOS app has **not been implemented yet**. Files under the existing app's
`Platforms/iOS` directory are template scaffolding, not a working mobile target.
The desktop project currently targets Windows only and uses WinUI-specific code.

The approved mobile visual reference is [design/luma-mobile.html](design/luma-mobile.html).
Open it in Safari to inspect the interactive Luma prototype. Its sample media,
account, dates and counters are design fixtures, not live Telegram data.

## Start on your Mac

```sh
git clone https://github.com/mikeyoshino/TelegramBlog.git
cd TelegramBlog
open docs/design/luma-mobile.html
```

Install the .NET 10 SDK and a compatible Xcode/iOS MAUI workload for mobile
development. Configure signing when deploying to a physical iPhone. Restoring or
building the whole existing solution on macOS is not the mobile build workflow:
it includes the Windows-only desktop project.

The platform-neutral core can be built independently:

```sh
dotnet build src/TeleGramBlog.Core/TeleGramBlog.Core.csproj
```

The existing test suite includes Windows-specific storage/UI integration and has
been verified on Windows, not macOS. Do not treat it as an iOS acceptance suite.

## Agreed direction for implementation

- Create a separate MAUI iOS application, sharing Core and Telegram logic, while
  preserving the existing desktop app.
- Use HybridWebView / WKWebView to reproduce the HTML design closely: typography,
  white/blue palette, square media grid, floating tabs, sheets and media viewer.
- Use the real iOS status bar and safe areas; omit the prototype's decorative
  desktop device frame and fake status bar.
- Connect joined groups, topics and albums to real Telegram data. Retain search,
  filters and selection, but do not publish invented counts or a fake account.
- Replace the Windows-only DPAPI session store with an injectable abstraction and
  implement iOS Keychain storage. Never move a Windows session file into source.
- Implement iOS media playback and lifecycle cleanup. The existing native Windows
  video player and random-access adapter cannot be reused directly on iOS.
- Make every displayed control functional, including favorites, source links and
  save/share actions; request Photos access only if the implementation requires it.
- Verify on iPhone simulator/device: safe areas, keyboard, gestures, image/video
  loading, authentication, cancellations, stale responses and memory over time.

## Reusable code and constraints

- `src/TeleGramBlog.Core`: album grouping, paging, navigation cancellation and
  bounded history cache.
- `src/TeleGramBlog.Telegram`: authentication, group/topic discovery, media loading,
  history persistence and bounded chunked video source.
- `ProtectedSessionStore` and its callers currently depend on Windows protection.
- `MediaRef` does not currently supply duration or file size; do not invent them.
- Keep the mobile UI isolated from desktop card styles and grid behavior.
- Desktop memory findings and thumbnail fixes are documented under
  `docs/verification`; residual memory growth is not proven resolved.

## Local files deliberately excluded from Git

Build output, downloaded packages, research caches, Telegram API credentials,
session files and signing material remain local. `telegram.example.json` contains
placeholders. Obtain your own app API ID/hash and configure them locally. Never
commit a real `telegram.json`, session, token or signing certificate.

Desktop screenshots, verification reports, bundled preview fixtures and the full
mobile design reference are included so development can continue from this repo.
