# Companion 0.5.1.178

GO SEE can inspect a linked FC member’s Friends List estate menu while optional FC IDs are missing. Explicit non-members and conflicting known FC IDs are excluded. Missing character content IDs can resolve through exact friend name and home world; known IDs remain authoritative.

Refreshes the friends list before fallback, tries remaining linked friends when an estate menu has no usable exact-address row, and reports the current lookup in Adventure. When a known linked estate is on another world, travels to that world before checking available estate and friend teleports. Ward routing remains the fallback.

Native estate selection still requires matching estate type, district, ward, plot, menu owner and available gil. This does not bypass game estate-access restrictions. Regression tests cover incomplete FC data, identity mismatch and the reported Shirogane W9 P19 address. In-game validation remains necessary.
