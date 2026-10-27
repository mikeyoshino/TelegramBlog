# Saved-content search

Search uses the complete favorite snapshot loaded by `SqliteFavoritesStore.Load()` on account connection, not the current Telegram history page. No database migration or new Telegram search request is needed. Saved snapshots take precedence over transient history cards for this view.

Search covers saved titles/text/links, group and topic names, collection names and personal notes. Whitespace-separated terms all need to match, potentially in different fields; matching ignores case and normalizes Unicode to NFC. Collection and viewed-state filters continue to apply.

Search result cards retain their media preview and add a clickable title, source group/topic and an excerpt around a matching word. Matching terms are highlighted safely after escaping all post/user content. Long notes reveal the matching passage instead of only their beginning. Viewer and original-message actions are retained.

The browser unit tests first failed for missing search behavior; the UI test reproduced the missing group/topic matches before implementation. Tests cover a 60-item saved snapshot without history requests, queries across fields, literal punctuation/HTML, highlights, notes beyond the initial excerpt, filters, clear/no-results, viewer/source and reopen. The result screenshot `/tmp/teleblog-saved-search.png` was visually inspected.

Limits: this searches saved metadata/text, not text recognized from images or video speech. It does not add offline account startup or media downloads. Search currently scans the in-memory SQLite snapshot; unusually large libraries may warrant a dedicated indexed search later.

Validation: all 23 UI suites and all 9 JavaScript unit tests passed. The signed ios-arm64 build succeeded with zero warnings/errors; `git diff --check` passed.
