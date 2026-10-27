# Message image context menu

The Messages tab reuses the saved-message menu for a 550ms hold on a cover or attachment thumbnail, secondary click, or Shift+F10. Both one- and two-column layouts support it. Moving more than 10px or scrolling cancels the hold; multi-selection disables it. Dismissing the overlay cancels without opening the image underneath.

Unsaved posts offer open, save to collection, and original message. The collection action opens the existing editor so users can choose/create a category and add a note. Saved posts additionally support category changes, notes, viewed status and confirmed removal from local saved items. Telegram messages are never deleted by this menu.

`tests/luma-ios/message-context-check.cjs` exercises library covers, attachment thumbnails, collection/note editors, both layouts, scroll cancellation and selection mode. It failed on the missing library hold before implementation. Existing saved-menu and overlay tests cover shared gesture and dismissal behavior.

First-use saved-post menus load collection metadata without rerendering the originating card, so loading cannot invalidate the pending menu. The regression test also reproduced that cancellation before the fix.

Validation: all 20 UI suites passed; signed ios-arm64 build succeeded with zero warnings/errors; bundled UI files match source; `git diff --check` passed.
