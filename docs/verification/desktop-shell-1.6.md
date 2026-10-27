# TeleGramBlog 1.6 verification

## Scope

One native MAUI shell now connects groups, forum overview and topic posts. Group tiles, avatars/fallbacks, topic lists, header/account menu, toolbar, footer and page states were redesigned. Nonforum groups open posts directly. Shell UI copy is Thai; source names/content are preserved.

The media card, details gallery, player views and global App.xaml have identical SHA-256 hashes to the pre-change baseline (`shell-card-baseline.json`). The media grid's responsive sizing method is identical to `grid-baseline.txt`; its GridItemsLayout still uses 16px horizontal/vertical spacing and the same column rules. New styles are explicitly keyed and applied only to shell controls. The media viewport explicitly retains its original white background so transparent card surfaces do not inherit the new gray shell background.

## Evidence

- 47 unit/regression tests pass. Coverage includes first load vs empty/error/cancel, retaining old data during refresh, canceled refresh retaining searchable history, restoring remembered pages, page-size changes blocked while loading, forum overview vs nonforum direct navigation, query preservation, long lists, true counts and existing paging/media behavior.
- Self-contained Windows x64 Release publish succeeds.
- Native UI Automation: groups → overview → posts; search with no matches and clear; sidebar collapse/expand; return navigation; account menu. Controls expose keyboard focus and InvokePattern.
- Native media smoke: actual portrait cover geometry, open/play/pause/seek, back navigation, mixed photo/video gallery and card selection passed into details.
- Real native visual-tree captures at requested window sizes 1280×720, 1440×900 and 1920×1080. XamlRoot reports **125% display scaling**. See `screenshots/capture-report.txt` for actual content DIP and raster dimensions (window borders/title bar excluded from screenshots).
- Additional isolated stress captures use 80 explicitly labeled long group names and 80 long topic names. The last group and topic (80) are reachable, with fixed headers/footer. Separate captures cover empty groups, empty topics and search with no results.
- Native system title bar and window controls remain unchanged.

## Review fixes

Independent review identified and prompted fixes for canceled refresh losing its pager, page-size changes during load, canceled group/topic loads being reported as empty, and returning to a later page without restoring that page. Queries and scroll offsets are kept separately by group/topic. Native item-container adjustments are scoped to rail/sidebar/overview lists, not media cards or the media grid.

## Limits

Screenshots use the existing, explicitly labeled design preview, not a live Telegram account. Stress fixture names appear only under the explicit `--capture-shell` verification path. Live Telegram avatar/network/revocation behavior was not exercised in this session. The sandbox did not grant OS keyboard focus, so literal Tab/Enter keystroke delivery remains a manual acceptance check; native keyboard-focusability and accessible activation were verified. No unsupported keyboard or live-account success is claimed.

Run `scripts/smoke-shell.ps1`, `scripts/smoke-media.ps1`, and `TeleGramBlog.App.exe --capture-shell` to reproduce. The capture flag bypasses account initialization and never uses the deployed Telegram credentials.
