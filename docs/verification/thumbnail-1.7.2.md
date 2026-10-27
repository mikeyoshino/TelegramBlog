# Album thumbnail clipping fix — 1.7.2

Reproduced the user's white-card thumbnail strip being cut off when the first item in a Windows grid was a shorter single-media card. WinUI ItemsWrapGrid measured that first item and gave every container the same short height. At 1920×1080 the album's native content was 540.8 DIP high but its container was only 472.8 DIP high, clipping 68 DIP from the bottom.

Fix: set the native uniform slot height to fit the tallest card in the displayed page, including native container padding/margins. Recompute after data, width/span and card-height changes. Original cover 4:5 sizing, thumbnail sizes, column thresholds, gaps, gallery actions and dark viewer layout are preserved. Cards are still vertically aligned as WinUI previously aligned them when an album happened to be first.

Also corrected recycled cells: SetCardWidth now refreshes total card height when media count changes even if cover width/height stays the same. Previously single→album at unchanged width could retain a short HeightRequest.

Verification: isolated `--capture-cards` puts a single-media card before the album to reproduce the failure. Native geometry and screenshots at 1280×720 and 1920×1080, 125% scaling, show the complete strip. Runtime assertions check no native card exceeds its container and recycled single↔album cells grow/shrink by the 68 DIP strip reservation. 54 unit tests pass; self-contained release publish succeeds. No Telegram account accessed for verification.

Before: `artifacts/card-baseline/card-verification/`. Final evidence: `docs/verification/thumbnail-1.7.2/`.
