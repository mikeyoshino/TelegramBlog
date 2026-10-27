# Saved-message context menu and tab order

Tab order: Groups, Messages, Saved. Initial signed-in destination remains Groups.

Saved cards expose one menu through a 550ms pointer hold, desktop secondary click, Shift+F10/ContextMenu, or the visible more button. Movement beyond 10px, scrolling, pointer cancel, backgrounding and navigation cancel the hold/menu. Opening a menu swallows the generated click so the viewer does not open too. Pending asynchronous menus are invalidated on scroll or a new gesture.

Actions: open, change collection, edit note, toggle viewed, original Telegram message, remove from Saved. Removal uses a confirmation, existing local favorites transaction, and authoritative success response. No Telegram delete RPC. Failed removal retains the card and confirmation for retry. Successful removal updates the header count.

Verification: saved-context-check covers tap/hold distinction, scroll cancellation including delayed menu data, secondary-click and keyboard access, source routing, note preservation, viewed toggle, cancellation/error/success removal, other-post preservation, count update and small-screen menu bounds. Full existing browser suite plus new check passed before final race/count refinements; focused check rerun afterward. Physical build checked before installing without uninstall.

Final focused browser check passed including delayed-scroll cancellation. Build: 0 warnings/errors. Installed update successfully on paired iPhone at 22:47, 2026-10-27.
