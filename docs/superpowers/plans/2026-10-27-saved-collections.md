# Saved Collections Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task-by-task.

**Goal:** One collection per saved message, notes, viewed state, search and source navigation.
**Architecture:** Add independent SQLite metadata tables; gated native RPC; saved-page controls and card menus. Existing media and account boundaries remain authoritative.
**Tech Stack:** .NET 10, SQLite-net, JavaScript, CSS, MAUI HybridWebView.
**Spec:** ../specs/2026-10-27-saved-collections-design.md (approved in conversation).

## Global Constraints
One nullable collection per message. Preserve saved images. Local account-specific state. Explicit viewed toggle. Delete collection unassigns posts.

## Review Focus
Duplicate names; deleted collection references; logout during mutation; refresh overwrites metadata; failed save retains editor input.

## Tasks
- [x] Add SQLite schema and APIs LoadOrganization/SaveCollection/DeleteCollection/SaveOrganization; verify persistence, reassignment, deletion, validation, account isolation in SqliteFavoritesTests.
- [x] Add organization RPC serialized by favoritesGate and guarded by authVersion; return authoritative snapshot.
- [x] Add saved cards, search, collection/status filters and editor; retain existing viewer and source actions; handle errors before closing editors.
- [x] Run focused SQLite and browser checks, JS syntax check and physical build; update connected iPhone without uninstall.

## Verification
SQLite favorites: 4/4 pass, browser regression: 12 suites pass, collections UI flow: pass (including failed save retention and remove/re-add metadata reset), model tests: 6/6 pass. Device build: 0 warnings/errors. Reviewer found stale metadata and replaced-editor races; both corrected.
Device update installed successfully at 18:47 on paired iPhone (bundle app.telegramblog.luma). Final collections browser check also verifies delayed collection creation cannot replace a newly opened note draft. Retained existing saved-grid density options.

## Follow-up: remember save destination
Implemented first-save category picker / inline creation, reuse last successful category in memory for 30 minutes since successful save, reset on app reload/account reset, reject removed destination. Favorites and chosen category commit in one SQLite transaction. Both individual and bulk-save actions route through the picker. Editor can re-save a post still open in the viewer after removing it from Saved.
Verification: SQLite 5/5; prior 12 browser regression suites passed. Updated collections check and new save-category check pass after fixing removed-viewer post lookup. Final physical build 0 warnings/errors. Static review found no additional defect.
