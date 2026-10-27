# Double-tap media expansion

- Double-click/tap a photo in the viewer to fill the viewer area, hiding the header and thumbnail/action panel. The image stays contained, without reloading or cropping. Double-tap again or use the collapse button to return.
- Double-tap a video poster to start and expand it. If playback is already loading, expansion is deferred until the same playback request finishes. Closing/changing media invalidates that request.
- The native AVPlayer view has its own two-tap recognizer because touches over native video do not reach the web view. It expands/collapses the existing controller and player; UIControl touches are excluded and single-tap playback controls remain available. The recognizer is removed/disposed with the player.
- The photo expand button uses the same fullscreen layout. Switching media or reopening resets the layout.

`double-tap-check.cjs` first failed before implementation, then passed photo expansion/exit, hidden chrome, video reuse, expansion during loading and reset on reopening. Native touch recognition itself requires physical-device observation; WebKit tests exercise the web gesture and RPC flow.

Validation: all 22 UI suites passed; signed ios-arm64 build succeeded with zero warnings/errors; `git diff --check` passed.
