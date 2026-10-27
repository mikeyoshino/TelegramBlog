# Desktop shell implementation

User provided detailed implementation authorization: shared desktop shell across groups, group overview and media posts; Thai system copy, dense accessible desktop controls, real group avatars, meaningful loading/empty/error/search states, scope-aware sorting/footer. No change to media cards, gallery, media grid columns/gaps/responsive rules, or global styles.

Chosen approach: retain native MAUI/XAML and existing ViewModel/service. One connected shell hosts three mutually exclusive content states. Explicit keyed styles apply only to new shell elements. Existing native titlebar remains. Group tiles use native Button overlays for keyboard/hover/focus. Existing preview is used only for labeled screenshots/tests, never production data.

- [x] Capture immutable card/style hashes and media grid baseline.
- [x] Add view state tests; group overview before topic, nonforum direct posts, consistent loading/search/error states, cache timestamps, scoped sorting and footer.
- [x] Use Telegram chat photo metadata for cached group avatars; stable fallback palette and actual topic icon colors.
- [x] Replace shell XAML, retain card template and grid declarations; wire navigation, sidebar, account menu, scoped styles and scroll restoration.
- [x] Capture actual rendered native UI screenshots via explicit preview capture mode if desktop capture unavailable. Test 1280x720,1440x900,1920x1080 and report actual scaling.
- [x] Verify state tests and native workflow, independent code review, immutable hashes, publish 1.6.

Do not fabricate group statistics or last-updated times. Old cache without timestamp displays saved-data status without a time. Sort is explicitly scoped to loaded page/search results, not unrequested Telegram history. No live account requests for screenshot verification.
