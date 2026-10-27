# iOS session persistence — 2026-10-27

The native persistence probe reproduced `NoSuchAttribute`: the Keychain update attributes included `kSecClass` because they used `SecRecord(SecKind.GenericPassword)`. The class now belongs only to the query; update attributes use an empty `SecRecord`.

Startup resumes an existing Telegram session explicitly without falling back to fresh authentication. A failed resume keeps the stored session and displays a retry action. Failed manual login no longer clears the persisted session automatically. Explicit logout still clears local data.

Validation on iPhone 17 Pro simulator, iOS 26.4:

- Debug build: zero warnings or errors.
- Isolated synthetic Keychain item: create, overwrite, read passed.
- Terminate, reinstall the same app without uninstalling, launch again: persisted bytes matched; the synthetic item was removed afterward.
- Browser resume regression: failed startup followed by retry connects without invoking fresh login, QR, cancellation, or logout.
- Five browser UI suites passed; five JavaScript logic tests passed.
- .NET suite: 54 passed, 3 failed on macOS in existing Windows-specific DPAPI/file-lock tests (`SessionProtectedAtRestCanOverwriteAndClear`, `LockedSessionDoesNotPreventCacheCleanupAndReportsFailure`, `DisposalRetriesUntilDecoderReleasesItsHandle`).

The probe uses a separate Keychain service and never accesses the user's session. The simulator was returned to normal launch without probe or preview flags. Real-account QR login followed by restart still requires user verification; a session that previously failed to save cannot be recovered by this change.
