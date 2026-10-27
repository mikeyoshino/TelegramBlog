# TeleBlog branding and startup

Approved concept: docs/design/mockups/teleblog-approved-concept.png.
Production artwork: vector B + chat tail + bookmark in Resources/appicon.svg, Resources/splash.svg, and wwwroot/assets/teleblog-logo.svg. Header mark uses the same path.

Startup hides navigation and explanatory copy while restoring the session. A centered logo uses a CSS orbit and subtle scale animation; reduced-motion disables animation. No video, external fonts, or network artwork. Failed restore still exposes retry/reconnect, and successful restore returns normal navigation.

Display name becomes TeleBlog. Existing app ID, namespaces, preferences and Keychain identifiers remain stable to preserve the installed app's session and saved collections.

Browser startup-logo-check verifies centered placement, absent header/text/video, motion and reduced motion, retry after error and successful startup transition.

Verified startup-logo-check plus all 14 existing browser suites passed. Physical arm64 build: 0 warnings/errors; built CFBundleDisplayName is TeleBlog. Updated the paired iPhone successfully without uninstall at 22:37 on 2026-10-27.

## Native icon correction

User reported the Home Screen icon still showed the old four-square Luma mark. Inspection confirmed both generated resizetizer appicon60x60@3x.png and the optimized PNG extracted from the actual signed .app still contained that old mark, despite the changed SVG source. Prior browser-logo checks had not caught this native-asset defect.

Renamed native sources to Resources/teleblog_icon.svg and Resources/teleblog_splash.svg; updated MauiIcon/MauiSplashScreen and XSAppIconAssets together; incremented ApplicationVersion to 2; cleaned and rebuilt ios-arm64. Inspected regenerated @3x icon, native splash PNG and pngcrush-decoded PNG from the final signed .app: all show the approved TeleBlog B/chat/bookmark mark. Final CFBundleIconName=teleblog_icon, CFBundleVersion=2. Build passed with 0 warnings/errors. Bundle ID and application data remain unchanged.
