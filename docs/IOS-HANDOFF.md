# Luma iOS

The iOS target migrated from the desktop app lives in `src/TeleGramBlog.iOS`. The Windows application
remains in `src/TeleGramBlog.App`; its project and visual components are unchanged.
Its browsing logic is the existing desktop `MainViewModel` source linked into the
iOS project, backed by the same Core and Telegram assemblies.
See the [desktop migration mapping](verification/ios-desktop-migration.md).
The approved visual reference remains [luma-mobile.html](design/luma-mobile.html).

## Run on a Mac

Requirements: .NET 10 SDK, MAUI/iOS workload, compatible Xcode, and an installed
Simulator runtime. This implementation was built with .NET SDK 10.0.302, MAUI
10.0.20, Xcode 26.6 and launched on iPhone 17 Pro / iOS 26.4 Simulator.

Boot an iPhone in Simulator, then run from the repository root:

```sh
./scripts/run-ios.sh --design-preview
```

The flag enters the explicitly labeled sample gallery. Omit it for welcome/login.
Set `LUMA_SIMULATOR` to a specific Simulator UUID if multiple devices are booted.
The script targets Apple Silicon (`iossimulator-arm64`); Intel Macs need
`iossimulator-x64`. Build this project directly, not the Windows solution:

```sh
dotnet build src/TeleGramBlog.iOS/TeleGramBlog.iOS.csproj \
  -p:RuntimeIdentifier=iossimulator-arm64 -p:CodesignKey=-
```

Physical-device deployment requires your Apple development team, provisioning,
and a device build (`ios-arm64`). This repository does not include signing
material, an App Store release, or a signed distributable IPA.

## Telegram configuration

The application owner obtains their own API ID/hash from https://my.telegram.org/apps.
Copy `telegram.example.json` to `telegram.json` at the repository root and fill it
locally. The iOS project bundles that file only when it exists at build time.
Do not commit it. There are no account credentials in the source or preview.
Without configuration, the app remains usable in sample mode and explains the
missing setup when attempting login.

QR login with automatic renewal is also available (scan from another signed-in
Telegram device). Phone, verification code, 2FA password and email challenges are entered inside the
app. An existing authenticated session resumes at startup. Telegram sessions are
stored in iOS Keychain with `AfterFirstUnlockThisDeviceOnly`; shared Windows code
continues to use its default DPAPI store. Do not copy a Windows session to iOS.

## Implemented behavior

- Reference CSS, SVG icons and local preview imagery; no remote UI dependency.
- Real status bar, native safe areas, keyboard resizing, compact/landscape layouts.
- Square media grid, 2/3/4 columns, scoped search, date grouping and sorting.
- Joined groups and topics use the desktop workspace flow. Page size is 24/48;
  page/query memory, bounded remembered-history cache, refresh/cancellation and
  offline cached-page fallback come from the same desktop ViewModel. Search covers
  all fetched topic history; sorting covers displayed records. Counts are loaded
  counts, never invented total Telegram history. Live browsing selects one
  group/topic at a time; sample mode also demonstrates the all-groups view.
- Albums retain media order and mixed photo/video types. Originals open fitted
  to the viewer; arrows, swipes, zoom and filmstrip navigate the loaded media.
- Favorites persist account-scoped post metadata (up to 500) on this device.
  Preview favorites have their own storage and never appear in a connected account.
- Native share sheet exports originals; save destinations are selected in iOS.
  There is no direct Photos-library write, so the app does not request broad Photos
  access. Telegram source links open externally when supplied for that group.
- Live video uses AVPlayer with an `AVAssetResourceLoaderDelegate` reading the
  existing `ChunkedMediaSource` on demand. It retains the desktop 256 KiB blocks
  and 16 MiB cache; seeking requests the required range. Closing/backgrounding
  cancels reads and releases the player/cache. Sharing a video still needs a
  temporary original file, cleaned after the share operation.
- Logout revokes remotely when reachable and removes local account data. Incomplete
  local cleanup blocks automatic resume and offers a retry on the welcome screen.

## Implementation map

- `MainPage.cs`: safe-area host, allowlisted backend messages and lifecycle.
- `Services/MobileBackend.cs`: bridge adapter for shared desktop commands/state,
  authentication, favorites and iOS media.
- `src/TeleGramBlog.App/ViewModels/MainViewModel.cs`: canonical shared browsing
  implementation, linked directly into the iOS target.
- `Services/KeychainSessionStore.cs`: protected transport-session persistence.
- `Services/NativeMedia.cs` and `StreamingResourceLoader.cs`: AVPlayer and the
  desktop chunked-source adapter.
- `Resources/Raw/wwwroot`: local HTML/CSS/JS, reference assets, isolated preview.
- `src/TeleGramBlog.Telegram/ISessionStore.cs`: shared platform abstraction.

The app bundles a minimal iOS raw-message bridge for .NET 10 HybridWebView rather
than relying on a generated `_framework/hybridwebview.js` asset.
[Microsoft's bridge contract](https://learn.microsoft.com/dotnet/maui/user-interface/controls/hybridwebview?view=net-maui-10.0)
documents that integration.

## Verification

See [verification results](verification/luma-ios.md) and its native screenshot.

Logic tests need Node 20+:

```sh
node --test tests/luma-ios/*.test.mjs
```

For browser interaction checks, install development dependencies in
`tests/luma-ios` (`npm install`), then install WebKit (`npx playwright install webkit`).
Start the static UI server from the repository root:

```sh
python3 -m http.server 8765 --bind 127.0.0.1 \
  --directory src/TeleGramBlog.iOS/Resources/Raw/wwwroot
```

In another terminal:

```sh
node tests/luma-ios/ui-check.cjs
node tests/luma-ios/auth-check.cjs
node tests/luma-ios/cleanup-check.cjs
node tests/luma-ios/migration-check.cjs
node tests/luma-ios/qr-check.cjs
```

These checks use isolated simulated accounts; they do not log in to Telegram.

## Remaining acceptance and limitations

Real Telegram authentication, 2FA, Keychain persistence on a physical iPhone,
network failures and large live media require user/device testing. No live account
credentials were supplied or used during implementation. Native share/playback
integration compiles but has not been exercised with authenticated Telegram media.

Native streaming playback and seeking passed with the bundled sample through the
production range adapter; real Telegram transport and long-video stress remain
unverified. History and favorite metadata plus thumbnails use the app sandbox;
only the session uses Keychain. Preview sharing depends on Web Share support.

Run the existing desktop regression suite on Windows for the three cases that
rely on Windows DPAPI/file-lock semantics. A successful Simulator build and
browser preview do not establish live-account or App Store acceptance.
