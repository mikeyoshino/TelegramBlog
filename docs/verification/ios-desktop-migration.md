# Desktop → iOS migration

The source of functional requirements is the existing desktop README, not the
HTML prototype. The prototype supplies the mobile visual presentation.

## Shared implementation

The iOS project compiles the **same source file**
`src/TeleGramBlog.App/ViewModels/MainViewModel.cs` via a linked Compile item.
`MobileBackend` adapts its state/commands to the Luma UI. It no longer maintains
an independent history pager or page/search/cache rules. Desktop UI constructors
and existing behavior remain unchanged; the shared VM exposes read-only state and
an authenticated-service attachment method for the mobile host.

| Desktop behavior | iOS migration |
| --- | --- |
| Group → forum overview → topic; ordinary group opens posts | Shared `SelectGroupAsync` / `SelectTopicAsync`, mobile topic sheet |
| 24/48 posts; loaded-page counts | Shared `PageSize`, `ChangePageAsync`, `KnownPages` |
| Search all fetched topic history, clear back to remembered page | Shared `PostQuery` and `HistoryPager.LoadedCards` |
| Remember page/query when returning to a topic | Shared dictionaries and bounded `HistoryPagerCache` (8 topics / 20,000 remembered messages) |
| Show cached history while updating; retain data on offline refresh | Shared cached-page display/fallback, pushed to UI before completion |
| Cancel/ignore stale navigation | Shared `NavigationScope` plus versioned mobile snapshots |
| Group photos with initial fallback | Existing `GetGroupAvatarAsync` and mobile initial placeholder |
| QR renewal, 2FA, phone login and account resume | Existing `QrLoginFlow` / `PhoneLoginFlow` / `TelegramService` |
| Protected local session | `ISessionStore`: Windows DPAPI / iOS Keychain |
| Mixed albums spanning fetch boundaries | Existing Core album/pagination logic |
| Full captions and original post links | Shared post model projected with full text/links into mobile viewer |
| Video byte ranges, seeking, 16 MiB cache | Existing `OpenVideoAsync` / `ChunkedMediaSource`; iOS `StreamingResourceLoader` → AVPlayer |
| Logout remote/local cleanup status | Existing `LogoutAsync`, with iOS retry marker for incomplete local cleanup |

The Luma square grid and floating tabs intentionally replace the desktop's 4:5
cards and sidebar. iOS safe areas, gestures, native sharing and AVPlayer replace
Windows-only presentation integrations. Favorites remain an account-scoped mobile
addition. Initial group/topic discovery still needs connectivity; this is not a
full offline Telegram client.

## Verification

- iOS simulator build: succeeded, zero warnings/errors.
- Shared .NET suite: 54 passed, 3 existing Windows-dependent failures on macOS,
  57 total. See [earlier platform failure details](luma-ios.md).
- `MigrationTests.MobileSnapshotUsesDesktopPageMemoryAndLoadedHistorySearch`:
  checks shared page restoration, retained query, and all-loaded-history search.
- WebKit migration test: topic selection/search, restored page, server-backed
  history search, page size, cached offline refresh, original links, and navigation
  canceling a queued search.
- WebKit QR test: QR update, 2FA prompt, transition to connected groups.
- Existing UI/auth/cleanup browser checks retained.
- Native iPhone 17 Pro / iOS 26.4 stream probe: AVPlayer reached ReadyToPlay,
  playback advanced, seek to 3 seconds succeeded. The production resource loader
  served bundled sample video through the same chunked source. Result:
  [streaming-probe.json](luma-ios/streaming-probe.json).

The 1,128,375-byte fixture fetched five 256 KiB-aligned blocks, with no playback
file download. This confirms decoder integration, not streaming latency or memory
behavior for large Telegram videos. Real Telegram QR/phone authentication, network
range fetching, physical-device Keychain/signing and prolonged playback remain
live acceptance checks; no account credentials were used.

The AVPlayer adapter follows Apple's
[resource-loading data request contract](https://developer.apple.com/documentation/avfoundation/avassetresourceloadingdatarequest).

Debug simulator stream check (never authenticates):

```sh
./scripts/run-ios.sh --verify-streaming
```

Its report is written to the app sandbox's `Library/Caches/streaming-probe.json`.
