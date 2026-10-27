# Seekable video implementation

User requested faster playback and jumps to selected times after discussing streaming. Continue within that authorization. Native Windows and WTelegram remain the existing stack.

Approaches considered: whole-file download (current, long startup); localhost HTTP range proxy (works but adds listener and URL lifecycle); native IRandomAccessStream (selected implementation choice, no listener, decoder controls byte requests).

- [x] Add tested offset-based media reader: aligned 256 KiB blocks, 16 MiB LRU cache, cancellation, EOF, retry after failures.
- [x] Authenticate byte-range reads through WTelegram download DC; refresh expired file references and handle DC migration.
- [x] Implement native random-access stream with independent cursors for decoder clones.
- [x] Update player and details page to use stream leases; cancel/dispose on leaving or replacing media. Preview uses same native streaming adapter.
- [x] Run unit tests, publish Windows release, native play/pause/seek smoke, and code review.
- [x] Deliver version 1.3 with existing user configuration copied privately.

No time-to-first-frame promise: container index placement, keyframes, codecs, network and Telegram throttling affect latency. No transcoding, adaptive bitrate or persistent media cache. Cache lifetime is the open viewer. API credentials and session never enter player URLs. Live Telegram validation requires user's authenticated session and remains distinct from generated sample validation.

References: https://core.telegram.org/api/files and https://learn.microsoft.com/en-us/uwp/api/windows.media.core.mediasource.createfromstream
