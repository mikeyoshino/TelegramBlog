# Telegram loading recovery

2026-10-27. User reported no Telegram data after update. Device health showed authenticated=true/internet=true with stage authenticated. Code inspection found ConnectedAsync awaiting desktop.UseConnectedServiceAsync → LoadGroupsAsync → complete forum unread enumeration. An incomplete forum snapshot could fail the entire group list, and slow scans delayed startup.

Changes:
- iOS attaches the authenticated service without fetching the desktop group rail. Desktop default behavior remains unchanged.
- Group RPC retrieves dialogs without forum unread scans and has a 25-second timeout; session resume is also bounded to 25 seconds.
- Separate groupUnread RPC performs optional forum counts, bounded to 15 seconds total and 4 seconds per group. Per-group errors/incomplete counts become unknown, never a fabricated zero or a failure of group discovery.
- UI renders group results immediately and merges optional counts only for the current account, group revision and connection generation. Browsing remains available while counts are pending or fail.

Verification:
- UnreadSnapshotTests + MigrationTests: 4 passed.
- unread-isolation-check: group/topic/photo navigation succeeds with a held unread RPC, failed count does not close viewer, later counts update normally.
- All 18 browser suites passed.
- Physical arm64 build: 0 warnings/errors; installed and launched on paired iPhone.
- Post-install device health: unread-partial, authenticated=true, internet=true at 2026-10-27T16:01:19Z. Reaching this stage requires the client to have received its group list and requested optional counts. Confirms live account/group loading proceeds despite incomplete unread data. No logout, session deletion or data reset.
