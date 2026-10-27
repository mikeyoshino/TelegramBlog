# Desktop media viewer implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace only the opened-post viewer with the user's specified dark, full-window desktop viewer.
**Architecture:** Retain MAUI navigation, Telegram media loading and native MediaPlayerElement. Use a dedicated Windows image zoom handler and page-local styles. Keep the original topic page instance alive during navigation.
**Tech Stack:** Existing .NET 10 MAUI / WinUI, WTelegram.
**Spec:** The user's detailed request in this turn is the selected design and authorization to implement; no additional design approval is needed.

## Constraints / decisions
- Toolbar 56 DIP, filmstrip 96 DIP, 72 DIP thumbnails, #101216 stage. Details closed initially, 320 DIP right panel; overlay below 1000 DIP.
- Native system titlebar retained; hide MAUI navigation bar on viewer only. No shared style, card or media grid edits.
- Native image ScrollViewer supplies fit, 1–8x zoom and mouse drag. Images reset on selection, stage resize preserves fit when at 1x.
- Keep native video transport (play, pause, seek, volume); no autoplay. Dispose old source on selection/exit.
- Only real available metadata. Current MediaRef has no duration/dimensions/filesize; do not fabricate them.

## Review focus
Stale load/decode callbacks after switching; video controls must own arrow keys; exit restores prior fullscreen presenter and source-card focus; singleton/zero media layout; narrow details overlay and long strip reachability.

## Tasks
- [x] Add tested viewer selection/load state: clamped initial index, bounded navigation, stale completion rejection, cancellation and retry.
- [x] Build scoped viewer page, native zoom/pan, filmstrip, details, loading/error states, buffering and keyboard/fullscreen lifecycle.
- [x] Wire topic context and source focus without changing topic state or card appearance.
- [x] Run unit/native interaction checks, capture real preview at 1280×720 and 1920×1080 at 125%, verify locked files, publish 1.7.
- [x] Independent final review, address important findings and document verified behavior/limits.

Execution record: new ViewerSession tests failed to compile before implementation, then suite passed 50/50. Native keyboard-attachment check reproduced a missing listener and subsequently passed after moving attachment to Loaded. Native pop navigation reproduced a MAUI focused-stroke teardown exception and passed after using system focus visuals. Final independent review found no important defects. No Git worktree/commits were possible because this workspace has no Git metadata. Physical key/mouse injection remains an explicit environment limitation in the verification report.

## Verification
`dotnet test tests/TeleGramBlog.Tests --no-restore -v minimal`; `dotnet publish src/TeleGramBlog.App -c Release -r win-x64 --self-contained true --no-restore -o artifacts/TeleGramBlog-1.7-win-x64 -v minimal`; native UIA and native render captures. This directory has no Git metadata, so retain plan and file-hash evidence instead of commits/worktrees.
