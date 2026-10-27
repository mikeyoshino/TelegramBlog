# Collection inputs on iPhone

The category editor inherited 14px text for its inputs, and both category controls used native HTML selects. The editor and saved-page filter now use in-app disclosure buttons with selectable rows and a selected checkmark. Choice changes preserve the note draft; Escape collapses the choices before dismissing the sheet. Outside taps collapse the list, and the existing modal cancellation behavior remains.

Editable controls use at least 16px text to avoid iOS small-input focus zoom. User zoom remains enabled. The sheet is fixed to the viewport, bounded to 88dvh, and horizontally constrained; its contents remain scrollable on short screens.

`collection-input-check.cjs` first failed against the native select. It now covers category selection and persisted assignment, draft preservation, Escape, saved filtering, computed input font sizes and a 320×360 viewport. The WebKit screenshot `/tmp/teleblog-collection-picker.png` was visually inspected. Browser tests do not reproduce the physical iPhone keyboard, so that interaction still requires on-device observation.

All 21 UI suites passed. The signed ios-arm64 build succeeded with zero warnings/errors, and `git diff --check` passed.
