# Companion 0.5.1.145

Housing lottery buyer detection reads the full confirmation text, including FC/private wording after line breaks. Both scrolling and ordinary Yes/No confirmation layouts are supported. Timers uses the same parser. Ambiguous text mentioning both types stays unknown.

Pending confirmation type is bound to the character and world, expires after five minutes and is consumed by the submission message. An unknown later observation no longer downgrades a known type for the same entry. Paissa availability is not treated as proof of a submitted bid type.

This does not retroactively guess the type of existing unknown bids. Opening Duty > Timers > Estate can enrich an existing entry when its buyer type is available there. No Journal deployment is required. Live confirmation capture still needs an in-game test.
