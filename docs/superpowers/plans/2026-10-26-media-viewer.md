# In-app media viewing

User requested clicking to view images and videos. Extend the existing detail page: images show full-size, videos use native Windows MediaPlayerElement with play/pause, seeking and volume. Mixed albums use previous/next media. Grid labels distinguish View image / View video / View album. Ruling: installed WinUI MediaTransportControls lacks the UWP full-window control properties; fullscreen is out of this version rather than adding a second player or window subsystem.

Native Windows player is selected because this project targets Windows already; adding a web view or cross-platform player package is unnecessary. Download video to an account-scoped temporary file on demand, with progress and cancel; begin playback after download, never auto-play. Dispose player before deleting file on navigation. Keep original Telegram link available for unavailable media/unsupported codecs. No credentials printed or copied into source.

- [x] Test safe temporary-media publication, cancellation cleanup and mixed-media mapping.
- [x] Add video retrieval and Windows-native player; wire detail page and grid labels.
- [x] Include a tiny generated demo video to test actual player controls without an account.
- [x] Run tests, publish version 1.2, preserve configured application credentials locally, UI smoke and review.

This extends the approved desktop design within requested behavior. No Git repository exists. Telegram download must still be accepted against a live account by the user.
