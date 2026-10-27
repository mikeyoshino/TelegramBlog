# Mixed album gallery

User request: keep all photos/videos belonging to a Telegram album in one viewer post, mark videos, and allow selecting media in a gallery underneath.

Implementation choices within the requested UI: preserve the existing album grouping by Telegram grouped_id, order media by message ID ascending within each album, use a horizontal thumbnail strip underneath the detail viewer, retain previous/next buttons, show a blue selected border, and use play glyphs on video thumbnails and mixed-media grid badges. Separate messages without an album ID remain separate posts; no time-based guessing of album membership.

Files: HistoryPager (album order), Models (media label), MainViewModel (mixed counts and sample album), PostDetailsPage (gallery and thumbnail lifetime), GalleryTests (mixed cross-batch regression), smoke-media.ps1 (native image/video/image selection).

Thumbnail requests are sequential, stop on flood/cancellation, and preserve selectable placeholders when unavailable. Switching media cancels the old selection and disposes its player stream. Closing the viewer cancels thumbnail loads too.

Verification: 40 unit tests pass, Release 1.4 publish succeeds. Native smoke passed play/pause/seek/back/image and mixed album photo 1 -> video 2 -> photo 3. First run exposed an awaited scroll before layout blocking initial media; scrolling is now independent of media loading and only requested for measured tiles. Live Telegram album content and networking are not asserted by sample tests.
