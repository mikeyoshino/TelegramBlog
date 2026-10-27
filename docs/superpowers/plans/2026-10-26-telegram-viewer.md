# Telegram Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver the approved white/blue Windows desktop Telegram group/topic viewer.

**Architecture:** MAUI native presentation, independently testable pagination/domain core, WTelegramClient adapter and Windows-protected account session. No backend.

**Tech Stack:** .NET 10, MAUI XAML, WTelegramClient, xUnit, local JSON index/cache (SQLite is optional in the design).

**Spec:** ../specs/2026-10-26-telegram-viewer-design.md

## Global Constraints

- Windows x64 first; English UI and Unicode message content.
- User/client API; read-only conversations, no invented connected data.
- Default 24 cards, optional 48; only known page numbers, no fictitious totals.
- Account-scoped data and protected session; no credentials in logs or source.

## Review Focus

- Albums spanning fetch boundaries remain a single complete card (task 1).
- Sparse media histories and duplicate messages cannot loop or skip cards (task 1).
- General topic history must not show other topic content (task 2).
- Late responses after navigation/logout cannot replace active content (task 3).
- Failed network logout still clears local session; revoked remote sessions are handled (task 2/3).

### Task 1: Domain and paging

Files: `src/TeleGramBlog.Core/Models.cs`, `HistoryPager.cs`, `LinkExtractor.cs`; `tests/TeleGramBlog.Tests/PagingTests.cs`, `LinkTests.cs`; solution and project files.

Interfaces: `HistoryMessage`, `PostCard`, `HistoryBatch`, `IHistorySource.FetchAsync(int beforeId, CancellationToken)`, `HistoryPager.GetPageAsync(int page, int pageSize, CancellationToken)` returning `PostPage`.

- [x] Write failing tests: two batches split one album -> one card with both media; repeated IDs -> no duplicates; 24 eligible messages across sparse batches -> full page; exhausted source -> accurate end; unsafe links -> excluded; canceled fetch -> no partial index commit.
- [x] Run `dotnet test tests/TeleGramBlog.Tests` and confirm missing implementation failures.
- [x] Implement domain, safe links, buffered album boundaries, cursor progress guard and index snapshot persistence contracts.
- [x] Run tests and confirm all pass.

### Task 2: Telegram and local account infrastructure

Files: `src/TeleGramBlog.Telegram/TelegramService.cs`, `TelegramHistorySource.cs`, `MessageMapper.cs`, `ProtectedSessionStream.cs`, `LocalStore.cs`; `tests/TeleGramBlog.Tests/TelegramMappingTests.cs`, `StorageTests.cs`.

Interfaces: `TelegramService.LoginAsync(string input)`, `GetGroupsAsync(CancellationToken)`, `GetTopicsAsync(GroupInfo, CancellationToken)`, `CreateHistorySource(GroupInfo, TopicInfo?)`, `GetMediaAsync(MediaRef, bool fullSize, CancellationToken)`, `LogoutAsync()`; domain GroupInfo/TopicInfo/MediaRef.

- [x] Restore current stable WTelegramClient, inspect exact signatures and pin resolved version.
- [x] Write/run failing mapping and storage tests: topic isolation including General, UTF-16 entity URLs, supported thumbnails, protected session roundtrip and overwrite/truncate, separate account caches.
- [x] Implement stepped login including 2FA, session resumption, full dialog/topic paging, photo downloads, flood-wait errors, account cache and local logout cleanup.
- [x] Run test suite and compile adapter.

### Task 3: MAUI interface

Files: `src/TeleGramBlog.App/App.xaml`, `App.xaml.cs`, `MauiProgram.cs`, `MainPage.xaml`, `MainPage.xaml.cs`, `ViewModels/MainViewModel.cs`, `Views/ConnectView.xaml`, `Views/WorkspaceView.xaml`, `Views/PostDetailsPage.cs`, resources and Windows bootstrap.

Interfaces: MainViewModel exposes connection/group/topic/post collections, busy/error states, commands and navigation generation cancellation; consumes task 2 service and task 1 pager.

- [x] Exercise stale-navigation protection with an isolated navigation coordinator test before implementation.
- [x] Build login, searchable group picker, group rail/topics, responsive grid, page controls, offline/error/empty/loading states and details viewer matching concept.
- [x] Add configuration loading and labeled demo preview for reviewing UI without credentials; no automatic demo fallback.
- [x] Compile Windows app, inspect output and smoke-test startup if execution supported.

### Task 4: Delivery

Files: `README.md`, `scripts/publish.ps1`, `.gitignore`, `telegram.example.json`.

- [x] Document credentials/setup, publishing, paging/search limits and live acceptance steps.
- [x] Run full tests and Windows publish; inspect executable distribution.
- [x] Request fresh independent review, resolve important findings, repeat affected checks.
- [x] Report artifact path and verified/unverified boundaries.

## Execution rulings

- User already approved implementation; proceed inline without repeating approval prompts.
- Empty directory is not a Git repository; work in the designated workspace without creating a separate worktree or pretending commits exist.
- Core and cache tests cover behavioral risks; no tests for visual constants or reversible static styling.

