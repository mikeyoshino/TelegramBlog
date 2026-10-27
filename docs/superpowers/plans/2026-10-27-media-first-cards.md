# Approved media-first cards

User approved implementation after comparing A portrait and B square mockup. Implement recommended A: 4:5 image region, no cropped faces (AspectFit), minimal date/link footer, no generic title or view button; image itself opens details. Mixed albums retain badges and thumbnail strip with video marks and selection. Caption/links remain in details. Link-only posts keep readable text.

Use a reusable native MediaCardView with its own selection and cancelable thumbnail loading. MainPage supplies the account thumbnail loader and detail navigation, including selected index. Keep responsive grid and pagination. Dispose selection tasks on recycling/unload, guard late callbacks, and respect flood waits. Verify build, existing tests, native card open and mixed album selection. Publish separately as 1.5 and retain earlier releases.

Completed: 1.5 Release published; 40 tests pass. Native smoke confirms 4:5 bounds and card-selected photo 3 opens details at photo 3, plus video play/pause/seek and mixed gallery. Review fixes and WinUI sizing findings recorded in progress.md.
