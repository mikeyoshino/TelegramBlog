# Content-focused mobile UI

Verified 2026-10-27.

- Groups show the group name/avatar and a numeric badge for a known, positive unread count. Unknown, zero and stale counts show no badge or explanatory caption.
- Removed group totals, ordering explanations, last-checked timestamps, topic-type labels and redundant page subtitles.
- Library, saved items, viewer, collection picker and profile omit duplicate status/instruction text. Loading uses animation with accessible labels; actionable failures, login instructions, confirmation consequences and personal notes remain.
- Refresh remains available as an accessible icon button. Pagination retains numbered buttons and the current-page accessibility state.

Validation: all 19 Playwright UI suites passed, including `quiet-ui-check.cjs`; signed ios-arm64 build completed with zero warnings/errors; `git diff --check` passed. Group screenshot inspected at 393px width.
