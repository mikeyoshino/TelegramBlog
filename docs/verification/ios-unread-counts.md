# Group unread message counts

2026-10-27: strengthened existing Telegram unread badges.

- Ordinary groups use Telegram unread_count, with read-through/top-message consistency check. No message-ID subtraction and no media/album counting.
- Forums aggregate unique full topic read states, including General. Refuse incomplete pages/reduced topic objects instead of displaying partial counts as complete; reject repeated pagination cursors.
- Missing dialog state is nullable, displayed as unknown rather than zero.
- Group list labels positive/zero counts and shows last successful check. Refresh pending/failure shows prior counts as unconfirmed. Existing 45-second foreground polling, tab-return refresh, and lifecycle refresh remain.
- Collection viewed status is independent; this change does not mark Telegram messages as read on opening the media viewer.

Verification: UnreadSnapshotTests (3 tests) cover count-vs-ID-gap, fully read stale aggregates, unique topics/overlapping pages, General, changed read positions, incomplete/short responses, and unknown defaults. Browser groups-check covers positive→zero, stale count preservation on failure, successful retry and narrow layout.

Sources: https://core.telegram.org/constructor/dialog and https://core.telegram.org/constructor/forumTopic. Per-topic counts are unread messages; forumTopic short flag does not carry valid read fields.

Limits: periodic snapshots rather than instant push updates; live totals for every group in the user's account have not been independently compared against the official Telegram app.

Review correction: also reject totals when the number of collected topics exceeds the final server topic count (topic deletion during pagination). Covered by UnreadSnapshotTests. Foreground recovery browser check passes. Device build verified with no warnings or errors.
