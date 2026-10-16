# Companion 0.5.1.146

Estate > Housing Lottery Status now reads the displayed labels and bounded text fields rather than assuming fixed UI offsets. It recognizes Type of Entry: Free Company Representative from the reported screenshot, alongside the exact plot, ward, district and entry number. Reopening these details can enrich an existing unknown bid with its observed type and sync that correction to Journal.

Includes 145's full confirmation parsing. Does not infer bid type from Paissa or change entry numbers. No Journal update required. Validated with the actual screenshot text in parser regression tests and a Release build; live UI capture remains to be checked in game.
