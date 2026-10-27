# TeleGramBlog desktop viewer

## Intent and decisions

Personal desktop viewer for a user's joined Telegram groups. Connect the user's Telegram account, select a group, select a forum topic, and browse historical picture/link posts in a paginated grid. The user requested .NET MAUI and approved the white/blue visual direction in `docs/design/desktop-grid-concept.png`. This document proposes the functional details; visual approval is not approval of all details below.

Builder defaults: Windows x64 first, .NET 10 MAUI native XAML, English interface labels matching the concept, with Unicode/Thai message content. macOS packaging is out of initial scope. The deliverable is a runnable desktop application distribution; a single self-contained EXE is not promised.

## Approaches

- Native MAUI XAML: selected proposal within the user's requested technology; native controls, direct desktop integration, and no embedded web runtime for the interface.
- MAUI Blazor Hybrid: viable for CSS-based layouts but adds a web view and a second UI layer. No current requirement needs it.
- Hosted web viewer: does not meet the requested executable desktop experience.

## Screens and interaction

1. First run: white connection screen with QR login as primary flow (approved follow-up). Connect obtains and renews Telegram login tokens; mobile Devices scans and confirms. Phone/code login is an optional fallback; both support 2FA. Switching modes cancels pending prompts. Resume only authenticated sessions, not partially saved transport state. API configuration is supplied by the application owner outside the normal UI. Missing configuration gets a specific setup message. Do not solicit credentials in chat or log tokens/passwords.
2. Group picker: searchable joined groups, group image/name, and whether topics are available. Paginate Telegram dialog discovery until available groups are collected; do not silently show only the first batch. Private groups appear only when the account can access them.
3. Workspace: narrow group avatar rail, adjacent searchable topic sidebar, breadcrumb and topic heading, toolbar, content grid, footer pagination. A back action returns to all groups. Ordinary groups open their history directly. Forum groups list topics including General correctly.
4. Grid: consistent thumbnail aspect ratio, short caption, date, and explicit link action. Cards represent a message or a media album; an album is one card even across fetch boundaries. Posts without media or a link are omitted in this viewer. A link-only post has a restrained text/domain placeholder. No invented titles or link previews. Multiple links are listed in details.
5. Details: open image preview and full caption, allow choosing album media and opening extracted HTTP(S) links in the system browser. Support original Telegram post links where valid. Version 1.2 adds native Windows video playback after on-demand download, with play/pause/seek/volume, progress/cancel and temporary-file cleanup. Unsupported media retains an original-Telegram fallback.

White primary surface, pale blue rail/selection, azure actions, dark navy text, muted secondary text, fine borders and minimal shadows. Reference image is visual guidance, not literal sample content or data. Grid adapts from two to four columns as window size changes; topic sidebar can collapse on narrow windows. Keyboard focus, accessible action labels and readable contrast are required. No analytics panels or marketing content.

## Pagination and loading

Default 24 cards per page, optional 48. Newest first. Keep Telegram history cursors and an ordered local index per account/group/topic. Fetch additional history until a visible page is full or history is exhausted; sparse eligible posts may require several requests. Show cancellable loading and obey Telegram wait limits. Previous/next and numbered discovered pages are available. Do not invent total cards/pages or provide unsupported random jumps into unfetched history. Show a final total only when the relevant history is exhausted.

Changing page size recalculates pages from the same ordered index. Search initially filters fetched posts and explicitly says "Search loaded posts". Topic and group searches filter their loaded lists. Refresh checks for newer posts and deliberately resets to the first page, avoiding silent shifts while browsing. Prevent duplicates using account/chat/message IDs; album grouping is scoped to the chat. A topic/group switch cancels old work and stale responses cannot populate the new selection.

## Architecture and account data

MAUI presentation uses view models and an isolated Telegram service adapter. Choose a maintained .NET Telegram client library during the implementation plan after verifying current authentication, dialog, forum-topic and history support. Use the Telegram user/client API rather than Bot API or website login. No application backend is needed.

Telegram application API ID/hash are required for a real connection. Provide documented local developer configuration and a clear missing-configuration screen; do not embed real credentials in source control. Distribution credentials and Telegram application registration need resolution before public release. The end user signs into their own account.

Store account sessions protected with Windows user-bound encryption. Keep message metadata and bounded thumbnail cache under per-account app data, with credentials excluded from logs. Cached media/content remains sensitive local data; do not claim all cache files are encrypted. Logout revokes the session when possible, clears local session/cache and returns to connection screen; offline logout explains local clearing if remote revocation cannot complete. Never reuse another account's cached content. No messages are sent, edited or deleted by this viewer.

Local SQLite may store the index and fetch cursors. Thumbnail downloads have bounded concurrency, cancellation and cache eviction; full-resolution media is loaded on demand. Handle deleted/unavailable media, revoked access, expired sessions, offline operation and Telegram flood waits with actionable UI states. Cached content must be marked stale/offline when it cannot be refreshed.

## Delivery and verification

Repository is initially empty. Available environment: Windows, .NET SDK 10.0.402, maui-windows workload. Create application, domain/service boundaries and focused automated tests. Verify album/page boundaries, topic isolation, duplicate handling, cancellation, links and session lifecycle using deterministic fixtures. Build and publish Windows output; smoke-test startup and navigation where desktop automation is available. Live Telegram acceptance requires actual application credentials and an account login; report it as unverified until exercised. Mock/demo content, if provided for preview, must be explicitly labeled and cannot stand in for a connected account.

Acceptance: connection flow, joined group discovery, forum topics, paginated historical media/link cards, details/link navigation, protected session reuse and logout; professional UI consistent with the approved concept. No posting, account management beyond connection/logout, full video player, bulk export, background exhaustive indexing or hosted service in version one.

## Open implementation checks

Validate the chosen client library, supported login delivery methods, topic-specific history/General semantics, Windows publishing prerequisites, and cache deletion behavior. These are implementation checks rather than reasons to fabricate successful integration. Any credentials or deployment limitation must be reported concretely.
