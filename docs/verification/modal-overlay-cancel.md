# Overlay dismissal equals Cancel

All app sheet dialogs now dismiss on a complete backdrop tap through the same cancellation path as the close button and Escape. This covers confirm dialogs, collections/notes, settings/options, and login sheets. QR cancellation invalidates auth responses and sends authCancel; late responses cannot reopen or replace the UI.

The shared modal-dismiss helper checks pointer down and up against the visible content rectangle. Inside padding, form interactions, drags and cancelled gestures do not dismiss. Events are consumed so they cannot activate the underlying screen. Saved-card context menus use the same helper around their inner menu surface. The fullscreen media viewer has no outside backdrop area; its existing back/Escape behavior is retained, and a sheet above it dismisses without closing the viewer.

Verification: modal-overlay-check exercises options, unsaved note discard, collection/favorite deletion confirmations, logout confirmation, context menu, viewer detail sheet, QR cancellation and stale QR response. Asserts no write/delete/logout request from cancellation and no underlying viewer click-through. Read-only review found no additional defect.
